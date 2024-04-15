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
using System.Collections;


public partial class FactsDecorator : IFactsDecorator
{
    //_testingTableId = 69; //"S.06.02.01.01"
    //public int TestingTableId { get; set; } = 433;
    //private int _testingTableId = 114;
    private int _testingTableId =0;

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
        //MissingFieldException S.6.0 tables
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

        
        if(filings.Count > 0)
        {
            ModuleTables = ModuleTables.Where(table => filings.Contains(table.XbrlFilingIndicatorCode)).ToList();
        }
        

        
        
        if (_testingTableId > 0)
        {
            ModuleTables = ModuleTables.Where(mt => mt.TableID == _testingTableId).ToList();
        }


        var moduleZetsxx = new List<string>();
        
        foreach (var table in ModuleTables)
        {            
            Console.WriteLine($"\nTemplate being Processed :{table.TableID} - {table.TableCode}");

            table.IsOpenTable = _SqlFunctions.IsOpenTable(table.TableID);

            //*********** Select the facts for a template and update their zetvalues, RowSignatures and currencyDimValue            
            var tableFactsCount = UpdateTableFactsWithCellValues(table);
            Console.WriteLine($"\n---facts updated:{tableFactsCount}");
            if (tableFactsCount == 0)
            {
                continue;
            }

            //*********** Create one  sheet per zet group            
            //fact.ZetValues is a string concatenating the Facts' zet dims
            //facts with the same zet values(concatenated as a string) should be assigned to the same sheet
            
            var zetValues = FindDistinctZetValues(table.TableID);

            //moduleZets.AddRange(zetValues);
            Console.WriteLine($"\n---Grouping table facts by Zet");

            List<SheetInfoType> sheetsInfo = CreateSheetForEachZet(table, zetValues);
            
            //*********** Assign facts to sheets and update fact row, col, etc
            foreach(var sheetInfo in sheetsInfo)
            {
                AssignFactsToSheet280(table.TableID, sheetInfo);
            }
                        
            //**********  if the table is open, update the rows
            foreach (var sheetinfo in sheetsInfo)
            {                                
                UpdateRowForOpenTables(sheetinfo.TemplateSheetId);
            }

            CreateYFactsForOpenTable280(sheetsInfo);

            
        }
        //***********  update foreing keys
        UpdateForeignKeysOfChildTablesNN();
                        
        Console.WriteLine($"\nFinished Processing documentId: {_documentId}");
        return 0;

    }

    private string ToShortVersion(string tableCode)
    {
        var rgxFiling = new Regex(@"^(\w{1,3}\.\d\d\.\d\d)");
        var matchFiling = rgxFiling.Match(tableCode);
        var res = matchFiling.Success ? matchFiling.Groups[0].Value : "xxxx";
        return res;
    }

    private List<TemplateSheetFact> SelectFactsForTableAndZet(int tableId,string zetValues)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelect = @"select * from TemplateSheetFact where InstanceId=@_documentId and TableID=@tableId and ZetValues = @ZetValues";
        var facts = connectionInsurance.Query<TemplateSheetFact>(sqlSelect, new { _documentId, tableId,zetValues }).ToList();
        return facts;
    }

    private List<string> FindDistinctZetValues(int tableId)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelect = @"select distinct ZetValues from TemplateSheetFact where InstanceId=@_documentId and TableID=@tableId";
        var zetValues = connectionInsurance.Query<string>(sqlSelect, new { _documentId, tableId }).ToList();
        return zetValues;
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

        var sqlFacts = @"update TemplateSheetFact set TemplateSheetId=null where InstanceId =@_documentId and TemplateSheetId is not null";
        var sqlSheets = @"delete  from TemplateSheetInstance where InstanceId = @_documentId";

        var xx = connectionInsurance.Execute(sqlFacts, new { _documentId });
        var sheets = connectionInsurance.Execute(sqlSheets, new { _documentId });
        return sheets;
    }

    private List<SheetInfoType> CreateSheetForEachZet(MTable? table, List<string> FactZetValuesList)
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

            var sheetName = $"{table.TableCode}__{zetCount:d2}";

            var sheet = _SqlFunctions.CreateTemplateSheet(_documentId, sheetCode, FactZetValue, sheetName,FactZetValue, table);
            Console.WriteLine($"Create SheetCode: {sheetCode} {sheetName}");
            sheetInfo.Add(new SheetInfoType(sheet.TableID, sheet.TemplateSheetId, sheetCode, FactZetValue, sheetName, sheet.YDimVal));
            zetCount++;
        }

        return sheetInfo;
    }


    private int CreateYFactsForOpenTable280(List<SheetInfoType> sheetsInfo)
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
            Console.Write("y");
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

    private int UpdateTableFactsWithCellValues(MTable table)
    {
        //****there are NO zet and Y dims on the table. They can be foun in Ordinates table
        //zet and y are included is in the cell signature

        var count = 0;
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
        //tableCells = tableCells.Where(tc => tc.CellID == 12801).ToList();
        tableCells = tableCells.Where(cell => !string.IsNullOrEmpty(cell.DatapointSignature)).ToList();
        foreach (var tableCell in tableCells)
        {
            var cellSignature = tableCell.DatapointSignature;
            
            var cellRowCol = DimUtils.ParseCellRowCol(tableCell.BusinessCode);                                   
            if (!cellRowCol.IsValid)
            {
                var cellMessage = $"Invalid MTableCell :cellId:{tableCell.CellID} , businessCode:{tableCell.BusinessCode}";
                _logger.Error(cellMessage);
                throw new Exception(cellMessage);
                //continue;
            }

            
            var cellFacts = SelectFactsForCellUsingDims(cellSignature);

            //Console.WriteLine($"Updating facts with zet values and tableId for table: {table.TableID} {tableCell.BusinessCode}   ");
            foreach (var cellFact in cellFacts)
            {
                
                Console.Write(".");                
                var rowSignature = BuildRowSignature(cellFact!.DataPointSignature, yDims);                
                var zetValues = BuildFactZetValues(cellFact.DataPointSignature,zDims);
                var currencyValues = BuildFactCurrencyDims(cellFact.DataPointSignature, currencyDims);

                cellFact.TableID = table.TableID;
                cellFact.RowSignature = rowSignature;
                cellFact.ZetValues = zetValues;
                cellFact.CurrencyDim = currencyValues;
                cellFact.Col = cellRowCol.Col;
                cellFact.Row = cellRowCol.Row;
                cellFact.CellID= tableCell.CellID;
                var xx = UpdateCellFact(cellFact);
                count++;
            }

            if (cellFacts.Any())
            {
                Console.Write("#");
            }
            //tableFacts.AddRange(cellFacts);
        }
        return count;
    }

    private int UpdateCellFact(TemplateSheetFact fact)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlUpdate = @"
            UPDATE TemplateSheetFact
            SET 
              TableID = @TableID,
              RowSignature = @rowSignature,
              ZetValues =@zetValues,
              CurrencyDim = @CurrencyDim,
              Col = @Col, 
              Row= @Row,
              [CellID]=@CellID
            WHERE 
              FactId= @FactId;
            "; 

        try
        {
            var res = connectionInsurance.Execute(sqlUpdate, fact);
            return res;
        }
        catch (Exception ex)
        {
            var message = $@"ERROR updating fact:{fact.FactId} -- {ex.Message} ";
            _logger.Error(message);
            return 0;
        }
        
        
    }
    private static string BuildRowSignature(string signature, IEnumerable<string> yDims)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return "";
        }
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


     

    private List<TemplateSheetFact> SelectFactsForCellUsingDims(string cellSignature)
    {
        //MET(s2md_met:ei2426)|s2c_dim:MP(*)|s2c_dim:NF(*)|s2c_dim:PX(*)|s2c_dim:SU(s2c_MC:x168)|s2c_dim:UI(*)|s2c_dim:VC(*?[481;1655;1])|s2c_dim:XA(*)
        //MET(s2md_met:ei2426)|s2c_dim:MP(ID:)|s2c_dim:NF(ID:SH)|s2c_dim:PX(ID:)|s2c_dim:SU(s2c_MC:x168)|s2c_dim:UI(ID:CAU/INST/XT72-PIRAEUS BANK S.A.-EUR-Shareholders' funds-SH-Neither unit-linked nor index-linked)|s2c_dim:XA(NB:13)
        //Select fact which has dims (via context) that are exact, wild, and NOT exists any other
        //for normal dims use s2c_dim:SU(s2c_MC:x168), for wild dims use just the DIM 
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        if (!cellSignature.Contains("(*") && !cellSignature.Contains("(*?"))
        {
            var simpleFacts = _SqlFunctions.SelectFactsBySignature(_documentId, cellSignature);
            return simpleFacts;
        }



        List<string> fullDims = cellSignature
            .Split("|").ToList();
        var dims = fullDims.Skip(1);
        


        //var rgx = new Regex(@"s2c_dim:\w\w\((.*?)\)", RegexOptions.Compiled);
        //var evaluator = new MatchEvaluator(MatchReplacer);

        List<TemplateSheetFact> factss=new();
        
        var rgxMet = new Regex(@"^MET\((.*?)\)");
        var xbrlCodeFull = fullDims.FirstOrDefault()??"";
        var xbrlCodeMatch = rgxMet.Match(xbrlCodeFull);
        var xbrlCode = xbrlCodeMatch.Success ? xbrlCodeMatch.Groups[1].Value : "";

        var onlyOptionalDims = dims
                        .Where(dim => !dim.Contains('*') && dim.Contains('?'))
                        .OrderBy(dim => dim);


        var mandatoryExactList = dims
                        .Where(dim => !dim.Contains('*') && !dim.Contains('?'))                        
                        .OrderBy(dim => dim);

        var mandatoryDimsList = dims
                        .Where(dim => !dim.Contains('?'))
                        .Select(dim => ToJustDim(dim))
                        .OrderBy(dim => dim);
               
        var allCellDimsList = dims                        
                        .Select(dim => ToJustDim(dim))                        
                        .OrderBy(dim => dim).ToList();
        


        var mandatoryExactDims = string.Join(",",mandatoryExactList.Select(dim => $"'{dim}'"));        
        var mandatoryDims = string.Join( ",", mandatoryDimsList.Select(dim => $"'{dim}'"));           
        
        var allCellDims = string.Join( ",",allCellDimsList.Select(dim => $"'{dim}'"));

        var exclusionFromDimsSQL = allCellDimsList.Any() ? $"AND NOT cl1.Dimension NOT IN ({allCellDims})" : "";
        var mandatoryDimsSQL = mandatoryDimsList.Any() ? $"AND cl1.Dimension IN ({mandatoryDims})" : "";        
        var mandatoryExactSQL = mandatoryExactList.Any() ? $"AND cl2.Signature IN ({mandatoryExactDims})" : "";

        //do not look for optinal, but check that there are no fact dims not specified in  celldims (Alldims)        
        var exactCount = mandatoryExactList.Count();
        var mandatoryCount = mandatoryDimsList.Count();

        if(onlyOptionalDims.Count() > 0)
        {
            var xxxx = 33;
        }

        var sqlSelectWithoutMandatory = @"
                SELECT fact.FactId
                  FROM TemplateSheetFact fact                
                  WHERE
                    fact.InstanceId = @documentId
                  AND fact.XBRLCode = @xbrlCode                    			  
        ";

        var sqlSelectWithOnlyMandatory = @$"
           
                  SELECT DISTINCT fact.FactId
                  FROM TemplateSheetFact fact
                  INNER JOIN ContextLine cl1 ON cl1.ContextId = fact.ContextNumberId
                  WHERE
                    fact.InstanceId = @documentId
                    AND fact.XBRLCode = @xbrlCode                    
                    {mandatoryDimsSQL}
                  GROUP BY FactId
                  HAVING COUNT(*) = {mandatoryCount}           
            
            ";


        var sqlSelectWithExact = @$"
           WITH f1 AS (
                  SELECT DISTINCT fact.FactId
                  FROM TemplateSheetFact fact
                  INNER JOIN ContextLine cl1 ON cl1.ContextId = fact.ContextNumberId
                  WHERE
                    fact.InstanceId = @documentId
                    AND fact.XBRLCode = @xbrlCode                    
                    {mandatoryDimsSQL}
                  GROUP BY FactId
                  HAVING COUNT(*) = {mandatoryCount}
                )
                SELECT f2.FactId
                FROM TemplateSheetFact f2
                INNER JOIN f1 ON f1.FactId = f2.FactId
                INNER JOIN ContextLine cl2 ON cl2.ContextId = f2.ContextNumberId
                WHERE 1=1
                  {mandatoryExactSQL}
                GROUP BY f2.FactId
                HAVING COUNT(*) = {exactCount};
            
            ";




        var sqlSelect = exactCount > 0 ? sqlSelectWithExact
                        :mandatoryCount > 0 ? sqlSelectWithOnlyMandatory                        
                        :sqlSelectWithoutMandatory;

        var facts1 = connectionInsurance.Query<int>(sqlSelect, new { documentId = _documentId, xbrlCode }).ToList();
        

        var fullFacts = facts1
            .Where(factId=>IsFactDimInCellDims(factId, allCellDimsList))
            .Select(factId => _SqlFunctions.SelectFact(factId))
            .Where(f=>f is not null)
            .ToList();

        if(facts1.Count!= fullFacts.Count)
        {
            var ss = 3;
        }

        var temp = fullFacts.Select(ff => ff.DataPointSignature).ToList();
        return fullFacts;
        

        //facts = connectionInsurance.Query<TemplateSheetFact>(sqlSelect, new { documentId = _documentId, xbrlCode }).ToList();
        var hierarchyList = dims
                        .Where(dim => dim.Contains('['))
                        .OrderBy(dim => dim).ToList();
        foreach ( var hierarchyDim in hierarchyList)
        {
            //no need to check. it is highly unlikely to have the same dim but different member
            //var cellDimRecord = CellDim.ParseHierarchy(hierarchyDim);
             //facts = facts.Where(fact => IsMemberInHierarchy(fact.FactId, cellDimRecord)).ToList();
        }
        
        

        string ToJustDim(string dimSignature)
        {
            //s2c_dim:BL(s2c_LB:x145)=> BL
            var rgxJustDim = new Regex(@"^s2c_dim:(\w\w)\(.*?\)");
            var matchJustDim= rgxJustDim.Match(dimSignature);
            return matchJustDim.Success? matchJustDim.Groups[1].Value : "";
        }
    }


    bool IsFactDimInCellDims(int factId, List<string> cellDims)
    {

        // factSignature: s2c_dim:RM(s2c_TI:x41)|s2c_dim:TA(s2c_AM:x57)
        //cellDim: s2c_dim:AF(*?[79;432;1])

        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);


        //s2c_dim:AF(*?[79;432;1])=> we need the dim "AF" to find the dim (context line) of the fact, and then hierarchy Id=79 to check if the context line  of the fact is in the hierarchy
        //var cellDimRecord = CellDim.ParseHierarchy(cellDim);
        
        
        var sqlFactDim = @"
		    select cl.Dimension from TemplateSheetFact fact
			join ContextLine cl on cl.ContextId=fact.ContextNumberId
			where FactId=@FactId            
        ";
        var factDims = connectionInsurance.Query<string>(sqlFactDim, new { factId });

        bool isAllContained = factDims.All(item => cellDims.Contains(item));

        return isAllContained;

    }

    bool IsMemberInHierarchy(int factId, CellDim cellDimRecord)
    {

        // factSignature: s2c_dim:RM(s2c_TI:x41)|s2c_dim:TA(s2c_AM:x57)
        //cellDim: s2c_dim:AF(*?[79;432;1])

        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);


        //s2c_dim:AF(*?[79;432;1])=> we need the dim "AF" to find the dim (context line) of the fact, and then hierarchy Id=79 to check if the context line  of the fact is in the hierarchy
        //var cellDimRecord = CellDim.ParseHierarchy(cellDim);
        if (!cellDimRecord.IsValid)
        {
            return true;
        }
               

        var sqlFactDim = @"
			SELECT cl.DomainAndValue
            FROM TemplateSheetFact fact 
            join ContextLine cl on cl.ContextId=fact.ContextNumberId				
             WHERE 
                fact.FactId= @FactId
                and cl.Dimension=@dim  
            
        ";
        var factDomainAndValue = connectionInsurance.QuerySingleOrDefault<string>(sqlFactDim, new { factId, cellDimRecord.Dim });
        if (factDomainAndValue is null )
        {            
            return cellDimRecord.IsOptional; //return valid if optional
        }
        
        var cellHierarchyId= cellDimRecord.HierarchyId;//79        
               
        
        //var factMemberCode= matchMember.Groups[1].Value; //x41
        var sqlSelectHiMembers = @"
            select hn.HierarchyNodeLabel, mem.MemberCode, mem.*
                from mHierarchyNode hn
                join mMember mem on mem.MemberID=hn.MemberID
                where  HierarchyID=@HierarchyID
				and MemberXBRLCode =@MemberXbrlCode

        ";
        
        var memberFound = connectionEiopa.QueryFirstOrDefault<string>(sqlSelectHiMembers, new { HierarchyID = cellHierarchyId, MemberXbrlCode = factDomainAndValue??"" });

        return memberFound is not null;
        
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

    private void AssignFactsToSheet280(int tableId, SheetInfoType sh)
    {


        var tableFacts = SelectFactsForTableAndZet(tableId, sh.SheetCodeZet);
        //***** Assign each fact to ist sheet depending on the zet 
        foreach (var tableFact in tableFacts)
        {
            Console.Write(";");
            //******* Assign the facts to the sheet
            //if the fact is alreate assigned to antoher shhet, create a clone fact
            var cnt = AssignFactToSheet(tableFact.FactId, sh.TemplateSheetId,tableFact.CellID,tableFact.Zet,  tableFact.Row, tableFact.Col, tableFact.RowSignature, tableFact.ZetValues, tableFact.CurrencyDim);
            if (cnt == 0)
            {
                //Console.WriteLine($"+ double FactId:{tableFact.FactId} Row:{tableFact.Row}-{tableFact.Col} ");
                Console.Write("&");
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
                var masterSheet= _SqlFunctions.SelectTemplateSheetBySheetCodeZet(_documentId, kyrTable.FK_TableCode,childSeet.SheetCodeZet); //master is S.06.02
                if(masterSheet is null)
                {
                    continue;
                }
                Console.WriteLine($"Update keys for {masterSheet.SheetTabName}");
                var masterFacts = _SqlFunctions.SelectFactsByCol(_documentId, kyrTable.FK_TableCode, childSeet.SheetCodeZet, kyrTable.FK_TableCol);
                //find the single fact in each row  to get the Foreign key value 'ISIN/CAN...'
                var childFactsWithForeignKey = _SqlFunctions.SelectFactsByCol(_documentId, kyrTable.TableCode, childSeet.SheetCodeZet, kyrTable.TableCol);
                foreach (var childRowKeyFact in childFactsWithForeignKey)
                {
                    //one fact in each row has the key also present in the master table
                    //find the fact in the master table                     
                    var masterFact = masterFacts.FirstOrDefault(fct => fct.TextValue.Trim().Equals(childRowKeyFact.TextValue.Trim()));
                    if (masterFact is null)
                    {
                        //todo fuck it cannot find masterFact
                        _logger.Error($"Could NOT find Master fact for :{childRowKeyFact.TextValue}");
                        continue;
                    }
                    
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
