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
using Shared.SharedHost;

using Shared.SpecialRoutines;
using Shared.HostParameters;
using Shared.DataModels;
using Mapster;
using System.Security.AccessControl;
using Syncfusion.XlsIO.Parser.Biff_Records;
using Shared.SQLFunctions;
using System.Reflection.Metadata;

public class FactsDecorator : IFactsDecorator
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
    public List<MTable> ModuleTablesFiled { get; private set; } = new List<MTable>();


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

        _document = _SqlFunctions.SelectDocInstance(documentId);
        if (_document is null)
        {

            var message = $"Cannot find DocInstance for: docId:{_documentId}, fundId:{_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
            Console.WriteLine(message);
            Log.Error(message);
            return 1;
        }
        _moduleCode = _document.ModuleCode.Trim();
        _moduleId = _document.ModuleId;

        ////////////////////////////////////////////////////////////////////
        Console.WriteLine($"\n Facts processing Started");

        ////////// Cleanup
        var deletedSheets = DeleteExistingSheets();
        

        //ModuleTablesFiled = GetFiledModuleTables();
        ModuleTablesFiled = _SqlFunctions.SelectTablesInModule280(_moduleId)
            .Where(tab => tab.TableCode.StartsWith("S"))
            .OrderBy(tab => tab.TableCode)
            .ToList();


        //_testingTableId = 173;
        if (_testingTableId > 0)
        {
            ModuleTablesFiled = ModuleTablesFiled.Where(table => table.TableID == _testingTableId).ToList();
        }


        //************************************************************************

        foreach (var table in ModuleTablesFiled)
        {
            if (table.TableCode == "S.06.02.01.01")
            {
                var y = "stop";
            }

            Console.WriteLine($"\nTemplate being Processed : {table.TableCode}");
            //*********** Select the facts for a template and 
            //var tableFacts = SelectFactsForTempateTable(table);
            var tableFacts = SelectFactsForTempateTable280(table);
            Console.WriteLine($"\n---facts:{tableFacts.Count}");


            //*********** Create one  sheet per zet group 
            //todo check if already exists !!
            //fact.ZetValues is a string concatenating the Facts' zet dims
            //facts with the same zet values(concatenated as a string) should be assigned to the same sheet (currency and country were excluded)
            List<string> distinctFactZetStrings = tableFacts
                    .GroupBy(fact => fact.ZetValues ?? "")
                    .Select(group => group.Key).ToList();

            List<SheetInfoType> sheetInfo = CreateSheetForEachZetGroup(table, distinctFactZetStrings);

            //*********** Assign facts to sheets
            AssignFactsToSheet280(tableFacts, sheetInfo);
            CreateYFactsForOpenTables280(sheetInfo);

            //***********  todo update foreing keys
            if (table.IsOpenTable)
            {
                //CreateYFactsForOpenTables(sheetInfo);
                UpdateForeignKeysOfChildTablesNN();
            }


        }

        Console.WriteLine($"\nFinished Processing documentId: {_documentId}");
        return 0;

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
        var sheetCount = 1;
        foreach (var FactZetValue in FactZetValuesList)
        {

            var sheetCode = string.IsNullOrEmpty(FactZetValue)
                    ? table.TableCode
                    : $"{table.TableCode}__{FactZetValue}";

            var sheetName = $"{table.TableCode}__{sheetCount:D2}";
            //var sheetName = sheetCode;
            table.IsOpenTable = table.YDimVal?.Contains('*') ?? false;
            //************************************************
            //Create a Sheet
            //To save time when testing I only create a sheet if it does not exist.If it does I will return the existing
            var sheet = SelectTemplateSheetBySheetCode(table.TableID, sheetCode);
            sheet ??= _SqlFunctions.CreateTemplateSheet(_documentId, sheetCode, FactZetValue, sheetName, table);


            Console.WriteLine($"Create SheetCode: {sheetCode} {sheetName}");
            sheetInfo.Add(new SheetInfoType(sheet.TableID, sheet.TemplateSheetId, sheetCode, FactZetValue, sheetName, sheet.YDimVal));

            sheetCount++;
        }

        return sheetInfo;
    }
    private TemplateSheetInstance? SelectTemplateSheetBySheetCode(int tableId, string sheetCode)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelect = @"select * from TemplateSheetInstance sheet where sheet.InstanceId = @_documentId and sheet.TableID = @tableId and SheetCode = @SheetCode";
        var sheet = connectionInsurance.QueryFirstOrDefault<TemplateSheetInstance>(sqlSelect, new { _documentId, tableId, sheetCode });
        return sheet;
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

            var sqlDistinct = @"select min(fact.Row)as minRow, max(fact.Row)as maxRow from TemplateSheetFact fact where fact.TemplateSheetId= @TemplateSheetId";
            var sheetRows = connectionInsurance.QueryFirstOrDefault<(string minRow, string maxRow)>(sqlDistinct, new { _documentId, sheetInfo.TemplateSheetId });

            var minRow = Convert.ToInt32(RegexUtils.GetRegexSingleMatch(new Regex(@"R(\d{4})"), sheetRows.minRow ?? "0"));
            var maxRow = Convert.ToInt32(RegexUtils.GetRegexSingleMatch(new Regex(@"R(\d{4})"), sheetRows.maxRow ?? "0"));
            if (minRow == 0 || maxRow == 0)
            {
                return 0;
            }
            foreach (var rowInt in Enumerable.Range(minRow, maxRow))
            {
                var xxx323 = minRow;
                CreateYFactsForRow280(sheetInfo, rowInt, yOrdinatesForKeys);
            }


            var xxx = 233;
            continue;

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
            var x = _SqlFunctions.CreateTemplateSheetFact(newFact);
            Console.Write("+");
        }
        return;
    }

    private MAPPING? SelectMapping(int tableId, string dimCode)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var dimx = DimDom.GetParts(dimCode).Dim;
        var dimCodeLike = $"s2c_dim:{dimx}%";
        var sqlDim = "select * from MAPPING map where map.TABLE_VERSION_ID= @tableId and map.DIM_CODE like @dimCodeLike";
        var dimMapping = connectionEiopa.QueryFirstOrDefault<MAPPING>(sqlDim, new { tableId, dimCodeLike });
        return dimMapping;
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
    private int AssignFactToSheet(int factId, int templateSheetId, string row, string col, string rowSignature, string zetValues, string currencyDim)
    {
        //zetvalues has all the zet and zet has the zet for currency or country which do NOT change page

        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlUpdateFact = @"
            UPDATE TemplateSheetFact
            SET 
              TemplateSheetId=@TemplateSheetId, Row= @row, Col=@col, RowSignature= @rowSignature,zetValues=@zetValues ,CurrencyDim=@CurrencyDim
            WHERE 
              FactId= @FactId AND TemplateSheetId IS NULL 
            ";
        var x = connectionInsurance.Execute(sqlUpdateFact, new { factId, templateSheetId, row, col, rowSignature, zetValues, currencyDim });
        return x;
    }

        private List<TemplateSheetFact> SelectFactsForTempateTable280(MTable table)
    {
        //****there are NO zet and Y dims on the table. Everything is on the cell

        var tableFacts = new List<TemplateSheetFact>();
        var tableCells = _SqlFunctions.SelectTableCells(table.TableID);
        if (table.TableID == 68)
        {
            var xxx = 3;
        }

        //*********************************************************************************
        //for each RowCol of this table, select the facts which have the exact dims (open or close) found from  field mappings, Y dims, Zet dims
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
                cellFact.Row = cellRowCol.IsOpen ? $"R{++count:d4}" : cellRowCol.Row;//open tables will be updated later based on their y dims
                cellFact.Col = cellRowCol.Col;

                //todo assign them
                //cellFact.ZetValues = zPageFactDimStr;
                //cellFact.CurrencyDim = zCurrencyFactDimStr;
                //cellFact.RowSignature = yRowSignature;
            }

            if (tableFacts.Count > 0)
            {
                Console.Write(".");
            }
            tableFacts.AddRange(cellFacts);
        }
        return tableFacts;
    }

        private List<TemplateSheetFact> SelectFactsFromCellSignature280(string cellSignature)
    {
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
        facts = FindFactsFromDims(cellDims);
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
            facts = FindFactsFromDims(dims);
            if (facts.Any())
            {
                break;
            }
        }
        return facts;
    }

    private List<TemplateSheetFact> FindFactsFromDims(List<string> dims)
    {
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
            var cnt = AssignFactToSheet(tableFact.FactId, sh.TemplateSheetId, tableFact.Row, tableFact.Col, tableFact.RowSignature, tableFact.ZetValues, tableFact.CurrencyDim);
            if (cnt == 0)
            {
                Console.WriteLine($"+ double FactId:{tableFact.FactId} Row:{tableFact.Row}-{tableFact.Col} ");
                tableFact.TemplateSheetId = sh.TemplateSheetId;                
                var x = _SqlFunctions.CreateTemplateSheetFact(tableFact);
            }
        }
    }

    private void UpdateForeignKeysOfChildTablesNN()
    {
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

        var sqlKyrTables = @"select * from mTableKyrKeys";
        var kyrTables = connectionEiopa.Query<MTableKyrKeys>(sqlKyrTables);//S.06.02.01.01       
        kyrTables = kyrTables.Where(kt => kt.TableCode.Trim() == "S.06.02.01.01");

        foreach (var kyrTable in kyrTables)
        {
            var sqlChild = @"select * from TemplateSheetInstance sheet where sheet.InstanceId= @docId and TableCode=@tableCode ";
            var childSheet = connectionLocal.QueryFirstOrDefault<TemplateSheetInstance>(sqlChild, new { docId = _documentId, tableCode = kyrTable.TableCode });
            if (childSheet is null) continue;

            var parentSheet = connectionLocal.Query<TemplateSheetInstance>(sqlChild, new { docId = _documentId, tableCode = kyrTable.FK_TableCode })
                    .Where(sh => sh.SheetCodeZet == childSheet.SheetCodeZet)
                    .FirstOrDefault();
            if (parentSheet is null) continue;

            var dimLike = $"%:{kyrTable.FK_TableDim.Trim()}%";
            var sqlMapping = @"select * from MAPPING where TABLE_VERSION_ID= @tableId  and DIM_CODE like @dimLike";

            var commonCol = connectionEiopa.QueryFirstOrDefault<MAPPING>(sqlMapping, new { parentSheet.TableID, dimLike });
            if (commonCol is null) continue;

            UpdateFactsWithMasterRowNN(childSheet.TemplateSheetId, parentSheet.TemplateSheetId, commonCol.DYN_TAB_COLUMN_NAME);


        }

    }
    private int UpdateFactsWithMasterRowNN(int childSheetId, int parentSheedId, string commonCol)
    {
        //update the RowForeign of the main table with the row of a related table.
        //For example, S.06.02.01.01 has links with S.06.02.01.02 on the "UI" dim. (SEVERAL rows of S.06.02.01.01 may correspond to a row of S.06.02.01.02 ** checked and true)       
        //  Therefore, each cell of the S.06.02.01 has a rowForeign which points to a cell of S.06.02.01.02        
        //  ---------------------------------------------------------------------------------------------        

        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);

        //the sql will update ALL the columns of the child table 
        var sqlUpdate = @"
                WITH jq AS (
                SELECT child.Row AS child_row, parent.TextValue AS key_value, parent.Row AS parent_row
                    FROM TemplateSheetFact child
                    LEFT OUTER JOIN TemplateSheetFact parent ON parent.TextValue=child.TextValue
                  WHERE child.InstanceId=@docId
                  AND child.TemplateSheetId=@child
                  AND child.Col=@commonCol
                  AND parent.InstanceId=@docId
                  AND parent.TemplateSheetId=@parent
                  AND parent.Col=@commonCol
                )
                UPDATE TemplateSheetFact
                SET RowForeign= jq.parent_row
                FROM TemplateSheetFact fact
                  JOIN jq ON fact.Row= jq.child_row
                WHERE InstanceId=@docId
                AND TemplateSheetId=@child
                -- ALL the cols in the UPDATE not just the commonCol
            ";
        var count = connectionLocal.Execute(sqlUpdate, new { docId = _documentId, child = childSheetId, parent = parentSheedId, commonCol });
        return count;

    }


    public static string MakeCellSignatureWild(string cellSignature)
    {
        //replace selections with sql wildcard s2c_dim:AX(*[8;1;0])=>s2c_dim:AX(%). 
        //replace optional dims with %
        //delete wildcard if at the end of line |%$


        //@"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)";
        //allow optional =>@"MET(s2md_met:mi87)|s2c_dim:AF(%)|s2c_dim:AX(%)|s2c_dim:BL(s2c_LB:x9)"
        //not allow optional=>@"MET(s2md_met:mi87)|s2c_dim:AX(%)|s2c_dim:BL(s2c_LB:x9)");


        var dimListBasic = cellSignature.Split("|").ToList();

        var rgx = new Regex(@"s2c_dim:\w\w\((.*?)\)", RegexOptions.Compiled);
        var evaluator = new MatchEvaluator(MatchReplacer);

        var dimList = dimListBasic
            .Select(dim => dim.Contains('?') ? dim.Replace(dim, "%") : dim)
            .Select(dim => dim.Contains('*') ? rgx.Replace(dim, evaluator) : dim);


        var wildSig = string.Join("|", dimList);

        var regExOptional = new Regex(@"\|%", RegexOptions.Compiled);
        wildSig = regExOptional.Replace(wildSig, "%");

        return wildSig;

        static string MatchReplacer(Match match)
        {
            if (!match.Success)
            {
                return match.Value;
            }
            var newVal = match.Value.Replace(match.Groups[1].Value, "%");
            return newVal;
        }
    }




}
