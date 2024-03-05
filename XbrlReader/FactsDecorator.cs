namespace XbrlReader;
using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;
using System.ComponentModel;
using Shared.GeneralUtils;
using Shared.Various;
using Shared.SharedHost;

using Shared.SpecialRoutines;
using Shared.HostParameters;
using Shared.DataModels;
using Mapster;
using System.Security.AccessControl;
using Syncfusion.XlsIO.Parser.Biff_Records;
using Shared.SQLFunctions;
using System.Reflection.Metadata;
using System.Text;


public partial class FactsDecorator : IFactsDecorator
{
    //public int TestingTableId { get; set; } = 54;
    private int _testingTableId = 0;

    private readonly IParameterHandler _parameterHandler;
    ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private int _documentId = 0;
    private List<string> _filings = new();
    private DocInstance _document = new();



    public string DefaultCurrency { get; set; } = "EUR";
    public int _moduleId = 0;
    public string _moduleCode = "";
    public List<MTable> ModuleTables { get; private set; } = new List<MTable>();


    public FactsDecorator(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions)
    {
        //process all the tables (S.01.01.01.01, S.01.01.02.01, etc ) related to the filings (S.01.01)
        //for each cell in each table, create a sheet and associate the mathcing facts (or create new facts if a fact should be in two tables)            
        //for open tables, create  facts for the Y columns in each row based on rowContext

        _parameterHandler = getParameters;
        _logger = logger;
        _SqlFunctions = sqlFunctions;
    }

    record SheetInfoType(int TableId, int TemplateSheetId, string SheetCode, string SheetCodeZet, string SheetName, string YDims);
    public int DecorateFactsAndAssignToSheets(int documentId, List<string> filings)
    {
        _documentId = documentId;
        _filings = filings;
        _parameterData = _parameterHandler.GetParameterData();

        var document = _SqlFunctions.SelectDocInstance(documentId);
        if (document is null)
        {
            var message = $"Cannot find DocInstance for: docId:{_documentId}, fundId:{_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
            Console.WriteLine(message);
            Log.Error(message);
            return 1;
        }
        _document= document;

        _moduleCode = _document.ModuleCode.Trim();
        _moduleId = _document.ModuleId;

        ////////////////////////////////////////////////////////////////////
        Console.WriteLine($"\n Facts processing Started");

        ////////// Cleanup
        var deletedSheets = DeleteExistingSheets();


        //ModuleTablesFiled = GetFiledModuleTables();
        ModuleTables = _SqlFunctions.SelectTablesInModule280(_moduleId)
            .Where(tab => tab.TableCode.StartsWith("S"))
            .OrderBy(tab => tab.TableCode)
            .ToList();


        //_testingTableId = 68; //"S.06.02.01.01"
        //_testingTableId = 69;
        if (_testingTableId > 0)
        {
            ModuleTables = ModuleTables.Where(table => table.TableID == _testingTableId).ToList();
        }
        //ModuleTables = ModuleTables.Where(table => new int[]{68,69 }.Contains( table.TableID) ).ToList();

        var moduleZets = new List<string>();  
        foreach (var table in ModuleTables)
        {            
            Console.WriteLine($"\nTemplate being Processed : {table.TableCode}");

            table.IsOpenTable = _SqlFunctions.IsOpenTable(table.TableID);

            //*********** Select the facts for a template and update their zetvalues, RowSignatures and currencyDimValue            
            var tableFacts = SelectFactsForTempateTable280(table);
            Console.WriteLine($"\n---facts:{tableFacts.Count}");


            //*********** Create one  sheet per zet group
            //** alternatively we could update each fact with zet and then do the grouping in sql
            //fact.ZetValues is a string concatenating the Facts' zet dims
            //facts with the same zet values(concatenated as a string) should be assigned to the same sheet
            List<string> distinctTableZetStrings = tableFacts
                    .GroupBy(fact => fact.ZetValues ?? "")
                    .Select(group => group.Key).ToList();
            
            moduleZets.AddRange(distinctTableZetStrings);
            Console.WriteLine($"\n---Grouping table facts by Zet");

            List<SheetInfoType> sheetsInfo = CreateSheetForEachZetGroup(table, distinctTableZetStrings);
            
            //*********** Assign facts to sheets and update fact row, col, etc
            AssignFactsToSheet280(tableFacts, sheetsInfo);
            
            //**********  if the table is open, update the rows
            foreach (var sheetinfo in sheetsInfo)
            {                                
                UpdateRowForOpenTables(sheetinfo.TemplateSheetId);
            }

            CreateYFactsForOpenTables280(sheetsInfo);

            
        }
        //***********  update foreing keys
        foreach(var openTable in ModuleTables.Where(tbl=>tbl.IsOpenTable))
        {
            UpdateForeignKeysOfChildTablesNN();
        }
        
        //update tableSheetNames
        UpdateTableSheetNames(moduleZets);
        Console.WriteLine($"\nFinished Processing documentId: {_documentId}");
        return 0;

    }

    private void UpdateTableSheetNames(List<string> ModuleZets)
    {
        //since excel tabsheet names cannot exceed 30 characters, map each table zet to a unique number
        var uniqueModuleZets = ModuleZets.Distinct().ToList();
        var sheets = _SqlFunctions.SelectTemplateSheets(_documentId);
        foreach(var sheet in sheets)
        {            
            var idx = uniqueModuleZets.IndexOf(sheet.SheetCodeZet);
            var sheetName = $"{sheet.TableCode}_{idx:d3}";
            _SqlFunctions.UpdateTemplateSheetName(sheet.TemplateSheetId,sheetName);
        }

    }    
    private List<string> UpdateRowForOpenTables(int sheetId)
    {
        //only open tables have row signatures
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);        

        var sqlSelect = "select distinct(fact.RowSignature) from TemplateSheetFact fact where fact.InstanceId=@_documentId and fact.TemplateSheetId = @sheetId";
        var rowSignatures = connectionInsurance.Query<string>(sqlSelect, new { _documentId, sheetId })
            .Where(sig=>!string.IsNullOrEmpty(sig)); 


        var sqlUpdate = "update TemplateSheetFact set row= @row where InstanceId=@_documentId and TemplateSheetId=@sheetId and RowSignature=@rowSignature";
        var rowInt = 0;
        foreach (var rowSignature in rowSignatures)
        {
            rowInt++;
            var row = $"R{rowInt:D4}";
            var rowFacts = connectionInsurance.Execute(sqlUpdate, new { _documentId, sheetId, rowSignature, row });
            var xx = 33;
        }

        return new List<string>();

    }

    private int DeleteExistingSheets()
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var sqlFacts = @"update TemplateSheetFact set TemplateSheetId=null where InstanceId =@_documentId";
        var sqlSheets = @"delete  from TemplateSheetInstance where InstanceId = @_documentId";

        var xx = connectionInsurance.Execute(sqlFacts, new { _documentId });
        var sheets = connectionInsurance.Execute(sqlSheets, new { _documentId });
        return sheets;
    }

    private List<SheetInfoType> CreateSheetForEachZetGroup(MTable? table, List<string> FactZetValuesList)
    {
        //all the facts assigned to the sheet will have the same Zet values (except for county/currency)
        //concatenate the zets and assign it to SheetZetCode

        if (table == null)
        {
            return new List<SheetInfoType>();
        }
        var sheetInfo = new List<SheetInfoType>();

        //create sheets for each template due to page zets (more than one)
        //the tabsheetname will be used to group sheets togther in merge
        var zetCount = 1;
        foreach (var FactZetValue in FactZetValuesList)
        {
            
            var sheetCode = string.IsNullOrEmpty(FactZetValue)
                    ? table.TableCode
                    : $"{table.TableCode}__{FactZetValue}";

            var sheetName = $"update later";
            
            var sheet = _SqlFunctions.CreateTemplateSheet(_documentId, sheetCode, FactZetValue, sheetName,FactZetValue, table);
            Console.WriteLine($"Create SheetCode: {sheetCode} {sheetName}");
            sheetInfo.Add(new SheetInfoType(sheet.TableID, sheet.TemplateSheetId, sheetCode, FactZetValue, sheetName, sheet.YDimVal));
            zetCount++;
        }

        return sheetInfo;
    }


    private int CreateYFactsForOpenTables280(List<SheetInfoType> sheetsInfo)
    {
        //create facts for each y dim of a table (for each row)
        //for each row, use the first non-null fact as a clone
        //each ydim fact will get its value from the corresponding dim of the clone fact.
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        sheetsInfo.OrderBy(si => si.SheetCode);
        foreach (var sheetInfo in sheetsInfo)
        {

            var yOrdinatesForKeys = _SqlFunctions.SelectTableAxisOrdinateInfo(sheetInfo.TableId)
                .Where(ord => ord.AxisOrientation == "Y" && ord.IsRowKey && ord.IsOpenAxis);

            if (!yOrdinatesForKeys.Any())
            {
                continue;
            }

            //var sqlDistinct = @"select COALESCE(MIN(fact.Row), 'R0') as minRow, COALESCE(Max(fact.Row), 'R0') as maxRow from TemplateSheetFact fact where fact.TemplateSheetId= @TemplateSheetId";
            var sqlDistinct = @"select MIN(fact.Row) as minRow, MAX(fact.Row)  as maxRow from TemplateSheetFact fact where fact.TemplateSheetId= @TemplateSheetId";
            var sheetRows = connectionInsurance.QueryFirstOrDefault<(string minRow, string maxRow)>(sqlDistinct, new { _documentId, sheetInfo.TemplateSheetId });

            var minRow = string.IsNullOrEmpty(sheetRows.minRow) ? 0 : int.Parse(Regex.Match(sheetRows.minRow, @"\d+").Value);
            var maxRow = string.IsNullOrEmpty(sheetRows.maxRow) ? 0 : int.Parse(Regex.Match(sheetRows.maxRow, @"\d+").Value);

            foreach (var rowInt in Enumerable.Range(minRow, maxRow))
            {                
                CreateYFactsForRow280(sheetInfo, rowInt, yOrdinatesForKeys);
            }                        
        }
        return 1;
    }

    private void CreateYFactsForRow280(SheetInfoType sheetInfo, int rowInt, IEnumerable<TableAxisOrdinateInfoModel> yOrdinateKeys)
    {
        var row = $"R{rowInt:D4}";
        var rowFact = SelectRowFirstFact(sheetInfo.TemplateSheetId, row);
        if (rowFact is null)
        {
            return;
        }
        var context = rowFact.ContextNumberId;
        var contextLines = _SqlFunctions.SelectContextLines(rowFact.ContextNumberId);

        foreach (var ordinateKey in yOrdinateKeys)
        {
            var mp = DimDom.GetParts(ordinateKey.Signature);
            var ctxLine = contextLines.FirstOrDefault(cl => cl.Dimension == mp.Dim);
            if (ctxLine is null)
            {
                continue;
            }
            var newFact = rowFact.Adapt<TemplateSheetFact>();
            newFact.Col = ordinateKey.Col;

            var textValue = ctxLine.IsNil ? ""
                : ctxLine.IsExplicit ? ctxLine.Signature
                : ctxLine.DomainValue;

            newFact.TextValue = textValue;
            newFact.DataTypeUse = "S";
            newFact.FieldOrigin = "K";
            newFact.CellID = 0;
            var x = _SqlFunctions.CreateTemplateSheetFact(newFact);
            Console.Write("+");
        }
        return;
    }

    private TemplateSheetFact? SelectRowFirstFact(int templateSheetId, string row)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlDim = @"select * from TemplateSheetFact fact where 
                        fact.TemplateSheetId= @TemplateSheetId 
                        and fact.Row=@row 
                        and not (fact.TextValue is null Or fact.TextValue='')            
            ";
        var fact = connectionInsurance.QueryFirstOrDefault<TemplateSheetFact>(sqlDim, new { templateSheetId, row });
        return fact;
    }

    private int UpdateRowsForOpenTables(List<SheetInfoType> sheetsInfo)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlDistinct = @"
            SELECT DISTINCT fact.RowSignature
                FROM TemplateSheetFact fact
                WHERE
                  fact.InstanceId= @_documentId AND fact.TemplateSheetId= @templateSheetId
                GROUP BY fact.RowSignature;
            ";

        foreach (var sheetInfo in sheetsInfo)
        {
            var distinctRowSignatures = connectionInsurance.Query<string>(sqlDistinct, new { _documentId, sheetInfo.TemplateSheetId });
            var counter = 1;
            foreach (var distinctRowSignature in distinctRowSignatures)
            {
                var row = $"R{counter:D4}";
                var sqlRow = @"update TemplateSheetFact set Row= @row where TemplateSheetId= @TemplateSheetId AND RowSignature = @RowSignature;";
                var xx = connectionInsurance.Execute(sqlRow, new { templateSheetId = sheetInfo.TemplateSheetId, rowSignature = distinctRowSignature, row });
                counter++;
            }

        }

        return 0;

    }

    private List<TemplateSheetFact> SelectFactsForTempateTable280(MTable table)
    {
        //****there are NO zet and Y dims on the table. They can be foun in Ordinates table
        //zet and y are included is in the cell signature

        var tableFacts = new List<TemplateSheetFact>();
        var tableCells = _SqlFunctions.SelectTableCells(table.TableID);        

        var yDims = _SqlFunctions.SelectTableAxisOrdinateInfo(table.TableID)
            .Where(ord => ord.AxisOrientation == "Y" && ord.IsRowKey && ord.IsOpenAxis)
            .Select(dd => DimDom.GetParts(dd.Signature).Dim).ToList();

        var zDims = _SqlFunctions.SelectTableAxisOrdinateInfo(table.TableID)
            .Where(ord => ord.AxisOrientation == "Z")
            .Select(dd => DimDom.GetParts(dd.Signature).Dim).ToList();

        var currenciesAndCountryDims = new List<string>() { "OC", "CU" };
        var currencyDims = zDims.Where(zd => currenciesAndCountryDims.Contains(zd)).ToList();
        var xxx = 2;    

        //*********************************************************************************
        //for each RowCol of this table, select the facts which have the exact dims (open or close) 
        //a rowcol position may contain more than one fact (currency and country)
        //also update the zetValues of each fact
        foreach (var tableCell in tableCells)
        {
            var cellSignature = tableCell.DatapointSignature;

            if (cellSignature is null)
            {
                continue;
            }

            var cellRowCol = DimUtils.ParseCellRowCol(tableCell.BusinessCode);
            if (!cellRowCol.IsValid)
            {
                continue;
            }

            var cellFacts = SelectFactsFromCellSignature280(cellSignature);
            var count = 0;
            foreach (var cellFact in cellFacts)
            {
                
                Console.Write(".");                
                var rowSignature = BuildRowSignature(cellFact.DataPointSignature, yDims);                
                var zetValues = BuildFactZetValues(cellFact.DataPointSignature,zDims);
                var currencyValues = BuildFactCurrencyDims(cellFact.DataPointSignature, currencyDims);

                cellFact.RowSignature = rowSignature;
                cellFact.ZetValues = zetValues;
                cellFact.CurrencyDim = currencyValues;
                cellFact.Col = cellRowCol.Col;
                cellFact.Row = cellRowCol.Row;
                cellFact.CellID= tableCell.CellID;
                               
                
            }

            if (cellFacts.Any())
            {
                Console.Write("#");
            }
            tableFacts.AddRange(cellFacts);
        }
        return tableFacts;
    }

    private static string BuildRowSignature(string signature, IEnumerable<string> yDims)
    {
        //build the row signature using only the ydims
        var dims = signature.Split("|",StringSplitOptions.RemoveEmptyEntries)
            .Where(dim => yDims.Contains(GetDimValue(dim)))
            .Order()
            .ToList();
        
        if (!dims.Any())
        {
            return "";
        }

        IReadOnlyList<string> readOnlyList = dims.AsReadOnly();
        //var ySignature = string.Join("|", dims); DO NOT use this is very slow
        var ySignature = StringRoutines.JoinStringCreate(readOnlyList, "|");
        return ySignature;

        static string GetDimValue(string input)
        {
            var rgx = new Regex(@"^s2c_dim:(\w\w)\(");
            Match match = rgx.Match(input);
            return match.Success ? match.Groups[1].Value : "";
        }
    }


    private static string BuildFactZetValues(string signature, IEnumerable<string> zDims)
    {
        //build the row signature using only the ydims
        var dims = signature.Split("|", StringSplitOptions.RemoveEmptyEntries)
            .Where(dim => zDims.Contains(GetDimValue(dim)))
            .Order()
            .ToList();

        if (!dims.Any())
        {
            return "";
        }

        IReadOnlyList<string> readOnlyList = dims.AsReadOnly();
        //var ySignature = string.Join("|", dims); DO NOT use this is very slow
        var zValues = StringRoutines.JoinStringCreate(readOnlyList, "|");
        return zValues;

        static string GetDimValue(string input)
        {
            var rgx = new Regex(@"^s2c_dim:(\w\w)\(");
            Match match = rgx.Match(input);
            return match.Success ? match.Groups[1].Value : "";
        }
    }


    private static string BuildFactCurrencyDims(string signature, IEnumerable<string> zDims)
    {
        //build the row signature using only the ydims
        var dims = signature.Split("|", StringSplitOptions.RemoveEmptyEntries)
            .Where(dim => zDims.Contains(GetDimValue(dim)))
            .Order()
            .ToList();

        if (!dims.Any())
        {
            return "";
        }

        IReadOnlyList<string> readOnlyList = dims.AsReadOnly();
        //var ySignature = string.Join("|", dims); DO NOT use this is very slow
        var zValues = StringRoutines.JoinStringCreate(readOnlyList, "|");
        return zValues;

        static string GetDimValue(string input)
        {
            var rgx = new Regex(@"^s2c_dim:(\w\w)\(");
            Match match = rgx.Match(input);
            return match.Success ? match.Groups[1].Value : "";
        }
    }


    private List<TemplateSheetFact> SelectFactsFromCellSignature280(string cellSignature)
    {
        //match any facts with cell signature 
        //try without optional dims, optional dims one by one, all optional dims at once (max 2 optional dims)
        var facts = new List<TemplateSheetFact>();
        //MET(s2md_met:ei2426)|s2c_dim:MP(*)|s2c_dim:NF(*)|s2c_dim:PX(*)|s2c_dim:SU(s2c_MC:x168)|s2c_dim:UI(*)|s2c_dim:VC(*?[481;1655;1])|s2c_dim:XA(*)
        //MET(s2md_met:ei2426)|s2c_dim:MP(ID:)|s2c_dim:NF(ID:SH)|s2c_dim:PX(ID:)|s2c_dim:SU(s2c_MC:x168)|s2c_dim:UI(ID:CAU/INST/XT72-PIRAEUS BANK S.A.-EUR-Shareholders' funds-SH-Neither unit-linked nor index-linked)|s2c_dim:XA(NB:13)

        //if no wild chars match the exact fact signature        
        if (!cellSignature.Contains("(*") && !cellSignature.Contains("(*?"))
        {
            facts = _SqlFunctions.SelectFactsBySignature(_documentId, cellSignature);
            return facts;
        }

        //serach for facts with optional dims
        //there are maximum TWO optional dims(checked), so start with all maximum
        //start with No optional dims and try one by one
        List<string> cellDims = cellSignature
            .Split("|").ToList();

        //check all dims (mandatory and optional)
        facts = FindFactsByLikeSignature(cellDims);
        if (facts.Any())
        {
            return facts;
        }

        //check mandatory dims and add one by one the optional dims
        //start with no optional and therefore always add "" as a dim
        var mandatoryDims = cellDims.Where(cd => !cd.Contains('?')).ToList();
        var optionalDims = cellDims.Where(cd => cd.Contains('?')).ToList();
        optionalDims.Add("");

        foreach (var optionalDim in optionalDims)
        {
            var dims = new List<string>(mandatoryDims)
            {
                optionalDim
            };
            facts = FindFactsByLikeSignature(dims);
            if (facts.Any())
            {
                break;
            }
        }
        return facts;
    }

    private List<TemplateSheetFact> FindFactsByLikeSignature(List<string> dims)
    {
        //select the facts using signature 
        //Replace * or ? with % 

        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var rgx = new Regex(@"s2c_dim:\w\w\((.*?)\)", RegexOptions.Compiled);
        var evaluator = new MatchEvaluator(MatchReplacer);

        List<TemplateSheetFact> facts;
        var dimsToCheck = dims
                        .Where(dim => !string.IsNullOrEmpty(dim))
                        .Select(dim => dim.Contains('*') ? rgx.Replace(dim, evaluator) : dim)
                        .Select(dim => dim.Contains('?') ? rgx.Replace(dim, evaluator) : dim)
                        .OrderBy(dim => dim).ToList();
        var sig = string.Join("|", dimsToCheck);

        var sqlSelectContext = @"select * from TemplateSheetFact fact where InstanceId=@DocumentId and fact.DataPointSignature like @signature;	";
        facts = connectionInsurance.Query<TemplateSheetFact>(sqlSelectContext, new { documentId = _documentId, signature = sig }).ToList();
        return facts;
    }
    static string MatchReplacer(Match match)
    {
        if (!match.Success)
        {
            return match.Value;
        }
        var newVal = match.Value.Replace(match.Groups[1].Value, "%");
        return newVal;
    }

    private void AssignFactsToSheet280(List<TemplateSheetFact> tableFacts, List<SheetInfoType> sheetInfo)
    {

        
        //***** Assign each fact to ist sheet depending on the zet 
        foreach (var tableFact in tableFacts)
        {
            var sh = sheetInfo.FirstOrDefault(shi => shi.SheetCodeZet == (tableFact.ZetValues ?? ""));
            if (sh is null)
            {
                throw new Exception($"No sheet was found for fact {tableFact.FactId}, Zvalues :{tableFact.ZetValues}");
                continue;
            };

            //******* Assign the facts to the sheet
            //if the fact is alreate assigned to antoher shhet, create a clone fact
            var cnt = AssignFactToSheet(tableFact.FactId, sh.TemplateSheetId,tableFact.CellID,tableFact.Zet,  tableFact.Row, tableFact.Col, tableFact.RowSignature, tableFact.ZetValues, tableFact.CurrencyDim);
            if (cnt == 0)
            {
                Console.WriteLine($"+ double FactId:{tableFact.FactId} Row:{tableFact.Row}-{tableFact.Col} ");
                tableFact.TemplateSheetId = sh.TemplateSheetId;
                var x = _SqlFunctions.CreateTemplateSheetFact(tableFact);
            }
        }
    }

    private int AssignFactToSheet(int factId, int templateSheetId, int cellId, string zet, string row, string col, string rowSignature, string zetValues, string currencyDim)
    {
        //zetvalues has all the zet and zet has the zet for currency or country which do NOT change page

        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlUpdateFact = @"
            UPDATE TemplateSheetFact
            SET 
              TemplateSheetId=@TemplateSheetId, cellId=@cellId, Zet=@zet, Row= @row, Col=@col, RowSignature= @rowSignature,zetValues=@zetValues ,CurrencyDim=@CurrencyDim
            WHERE 
              FactId= @FactId AND TemplateSheetId IS NULL 
            ";
        var x = connectionInsurance.Execute(sqlUpdateFact, new { factId, templateSheetId,cellId, zet, row, col, rowSignature, zetValues, currencyDim });
        return x;
    }


    private void UpdateForeignKeysOfChildTablesNN()
    {
        //we need to update sheets with the SAME zet.
        //the ZET is on the sheet but it can also be on the fact

        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

        var sqlKyrTables = @"select * from mTableKyrKeys tk where tk.FK_TableCode is not null";
        var kyrTables = connectionEiopa.Query<MTableKyrKeys>(sqlKyrTables);//S.06.02.01.01       
        //kyrTables = kyrTables.Where(kt => kt.TableCode.Trim() == "S.06.02.01.01");

        foreach (var kyrTable in kyrTables)
        {            
            var childSheets = _SqlFunctions.SelectTemplateSheetByTableCodeAllZets(_documentId, kyrTable.TableCode);
            foreach (var childSeet in childSheets)
            {
                var masterSheet= _SqlFunctions.SelectTemplateSheetBySheetCodeZet(_documentId, kyrTable.TableCode,childSeet.SheetCodeZet);
                if(masterSheet is null)
                {
                    continue;
                }
                var masterFacts = _SqlFunctions.SelectFactsByCol(_documentId, kyrTable.FK_TableCode, childSeet.SheetCodeZet, kyrTable.FK_TableCol);
                //find the single fact in each row  to get the Foreign key value 'ISIN/CAN...'
                var childFactsWithForeignKey = _SqlFunctions.SelectFactsByCol(_documentId, kyrTable.TableCode, childSeet.SheetCodeZet, kyrTable.FK_TableCol);
                foreach (var childRowKeyFact in childFactsWithForeignKey)
                {
                    //one fact in each row has the key also present in the master table
                    //find the fact in the master table 
                    
                    var masterFact = masterFacts.FirstOrDefault(fct => fct.TextValue.Trim().Equals(childRowKeyFact.TextValue.Trim()));

                    var sqlUpdCHildFacts = @"
                                update TemplateSheetFact set RowForeign = @RowForeign 
                                where InstanceId= @documentId and TemplateSheetId=@TemplateSheetId and Row=@Row;
                                ";
                    
                   connectionLocal.Execute(sqlUpdCHildFacts, new { documentId = _documentId, TemplateSheetId = childSeet.TemplateSheetId, Row= childRowKeyFact.Row, RowForeign =masterFact.Row});
                }
            }
            
            
            



        }

    }
    

}
