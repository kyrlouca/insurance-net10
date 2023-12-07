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
using Shared.CommonRoutines;

using Shared.SpecialRoutines;
using Shared.HostRoutines;
using Shared.DataModels;
using Microsoft.VisualBasic.FileIO;
using System.Text.Json.Serialization.Metadata;
using System.Security.Cryptography;
using Syncfusion.XlsIO.FormatParser.FormatTokens;
using System.Drawing;
using System.Runtime.InteropServices;
using Mapster;

public class FactsMover : IFactsMover
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


    public FactsMover(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions)
    {
        //process all the tables (S.01.01.01.01, S.01.01.02.01, etc ) related to the filings (S.01.01)
        //for each cell in each table, create a sheet and associate the mathcing facts (or create new facts if a fact should be in two tables)            
        //for open tables, create  facts for the Y columns in each row based on rowContext

        _parameterHandler = getParameters;
        _logger = logger;
        _SqlFunctions = sqlFunctions;
    }

    record SheetInfoType(int TemplateSheetId, int tableId, string SheetCode, string SheetName, string YDims);
    public int DecorateFactsAndAssignToSheets(int documentId, List<string> filings)
    {
        _documentId = documentId;
        _filings = filings;
        _parameterData = _parameterHandler.GetParameterData();

        _document = _SqlFunctions.SelectDocInstance(documentId);
        _moduleCode = _document.ModuleCode.Trim();
        _moduleId = _document.ModuleId;

        ////////////////////////////////////////////////////////////////////
        Console.WriteLine($"\n Facts processing Started");

        ModuleTablesFiled = GetFiledModuleTables();

        //_testingTableId = 130;
        _testingTableId = 0;
        if (_testingTableId > 0)
        {
            ModuleTablesFiled = ModuleTablesFiled.Where(table => table.TableID == _testingTableId).ToList();
        }


        //************************************************************************
        foreach (var table in ModuleTablesFiled.OrderBy(tab => tab.TableID))
        {


            Console.WriteLine($"\nTemplate being Processed : {table.TableCode}");
            //*********** Select the facts for a template 
            var tableFacts = SelectFactsForTempateTable(table);
            //the fact.zet has a unique combination of fact zets
            var sheetZetCodes = tableFacts.GroupBy(fact => fact.Zet).Select(group => group.Key).ToList();

            //*********** Create one  sheet per zet group 
            List<SheetInfoType> sheetInfo = CreateSheetForEachZet(table, sheetZetCodes);
            
            
            AssignFactsToSheet(tableFacts, sheetInfo);

            //*********** update rows for Open tables 
            UpdateRowsForOpenTables(sheetInfo, tableFacts);

            //*********** Create Y facts  
            CreateYFactsForOpenTables(sheetInfo, tableFacts);

            if (table.IsOpenTable)
            {

            }


            Console.WriteLine($"\n---facts:{tableFacts.Count}");
        }


        //Console.WriteLine($"\ndocId: {_documentId} -- sheets: facts:{countFacts}");
        return 0;

    }


    private void AssignFactsToSheet(List<TemplateSheetFact> tableFacts, List<SheetInfoType> sheetInfo)
    {
        //assign the facts to each sheet
        foreach (var tableFact in tableFacts)
        {
            var sh = sheetInfo.FirstOrDefault(sd => sd.SheetCode == tableFact.Zet);
            if (sh is null)
            {
                continue;
            };
            //************************************************
            //******* Assign the facts to the sheet
            //if the fact is alreate assigned to antoher shhet, create a clone fact
            var cnt = AssignFactToSheet(tableFact.FactId, sh.TemplateSheetId, tableFact.Row, tableFact.Col, tableFact.RowSignature);
            if (cnt == 0)
            {
                Console.WriteLine($"+ FactId{tableFact.FactId}");
                tableFact.TemplateSheetId = sh.TemplateSheetId;
                var x = _SqlFunctions.CreateTemplateSheetFact(tableFact);
            }
        }
    }
    private List<SheetInfoType> CreateSheetForEachZet(MTable? table, List<string> sheetZetCodes)
    {

        var sheetInfo = new List<SheetInfoType>();

        //create sheets for each template due to zet (more than one)
        var sheetCount = 1;
        foreach (var sheetZetCode in sheetZetCodes)
        {

            var sheetCodess = string.IsNullOrEmpty(sheetZetCode)
                    ? table.TableCode
                    : $"{table.TableCode}__{sheetZetCode}";

            var sheetName = $"{table.TableCode}__{sheetCount:D2}";            
            table.IsOpenTable = table.YDimVal.Contains('*');
            //************************************************
            //Create a Sheet
            var sheet = _SqlFunctions.CreateTemplateSheet(_documentId, sheetZetCode, sheetName, table);
            Console.WriteLine($"Create SheetCode: {sheetZetCode} {sheetName}");
            sheetInfo.Add(new SheetInfoType(sheet.TableID, sheet.TemplateSheetId, sheetZetCode, sheetName, sheet.YDimVal));

            sheetCount++;
        }

        return sheetInfo;
    }

    private int CreateYFactsForOpenTables(List<SheetInfoType> sheetsInfo, List<TemplateSheetFact> facts)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var sqlFindMapping = @"
                            SELECT map.DIM_CODE                                           FROM MAPPING map
                            WHERE 
				            map.DYN_TAB_COLUMN_NAME not like 'PAGE%'
				            and map.ORIGIN = 'C'
                            AND map.IS_IN_TABLE = 1
                            AND map.TABLE_VERSION_ID = @tableId
				            and map.DIM_CODE like @dimCode
				
                    ";
        //var yDimMapping = connectionEiopa.QueryFirstOrDefault<MAPPING>(sqlFindMapping, new { tableId = sheet.TableID, dimCode = $"s2c_dim:{yTableDimDom.Dim}%" });
        //if (yDimMapping is null)
        //{
        //    continue;
        //}



        var sqlDistinct = @"select min(fact.Row)as minRow, max(fact.Row)as maxRow from TemplateSheetFact fact where fact.TemplateSheetId= @TemplateSheetId";




        foreach (var sheetInfo in sheetsInfo)
        {
            List<string> tableYDims = sheetInfo.YDims?
            .Split("|", StringSplitOptions.RemoveEmptyEntries)
            .ToList() ?? new List<string>();

            var yTableMappings = tableYDims
                        .Select(ydim => SelectMapping(sheetInfo.tableId, ydim))
                        .Where(mappping => mappping != null) ?? new List<MAPPING>();


            var sheetRows = connectionInsurance.QueryFirstOrDefault<(string minRow, string maxRow)>(sqlDistinct, new { _documentId, sheetInfo.TemplateSheetId });

            var minRow = Convert.ToInt32(RegexUtils.GetRegexSingleMatch(new Regex("R(/d{3})"), sheetRows.minRow ?? "0"));
            var maxRow = Convert.ToInt32(RegexUtils.GetRegexSingleMatch(new Regex("R(/d{3})"), sheetRows.maxRow ?? "0"));
            foreach (var rowInt in Enumerable.Range(minRow, maxRow))
            {
                CreateYFactsForRow(sheetInfo, rowInt, yTableMappings);
            }

        }

        return 0;

    }

    private void CreateYFactsForRow(SheetInfoType sheetInfo, int rowInt, IEnumerable<MAPPING> yTableMappings)
    {
        var row = $"R{rowInt:D4}";
        var rowFact = SelectRowFact(sheetInfo.TemplateSheetId, row);
        if (rowFact is null)
        {
            return;
        }
        foreach (var yMapping in yTableMappings)
        {
            var factDim = _SqlFunctions.SelectFactDims(rowFact.FactId).Where(fd => fd.Dim == yMapping.DIM_CODE);
            var newFact = rowFact.Adapt<TemplateSheetFact>();
            newFact.Col = yMapping.DIM_CODE;


        }

        //fore each dim get the Col from 
        return;
    }

    private MAPPING? SelectMapping(int tableId, string dimCode)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        //var yDimMapping = connectionEiopa.QueryFirstOrDefault<MAPPING>(sqlFindMapping, new { tableId = sheet.TableID, dimCode = $"s2c_dim:{yTableDimDom.Dim}%" });
        var sqlDim = "select * from MAPPING map where map.TABLE_VERSION_ID= @tableId and map.DIM_CODE like 's2c_dim:@dimCode%'";
        var dimMapping = connectionEiopa.QueryFirstOrDefault<MAPPING>(sqlDim, new { tableId, dimCode });
        return dimMapping;
    }

    private TemplateSheetFact? SelectRowFact(int templateSheetId, string row)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlDim = @"select * from TemplateSheetFact fact where 
                        fact.TemplateSheetId= @TemplateSheetId 
                        and fact.Row=@row 
                        and not (fact.TextValue is null Or trim(fact.TextValue)='')            
            ";
        var fact = connectionEiopa.QueryFirstOrDefault<TemplateSheetFact>(sqlDim, new { templateSheetId, row });
        return fact;
    }

    private int UpdateRowsForOpenTables(List<SheetInfoType> sheetsInfo, List<TemplateSheetFact> facts)
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
    private int AssignFactToSheet(int factId, int templateSheetId, string row, string col, string rowSignature)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlInsert = @"
            UPDATE TemplateSheetFact
            SET 
              TemplateSheetId=@TemplateSheetId, Row= @row, Col=@col, RowSignature= @rowSignature
            WHERE 
              FactId= @FactId AND TemplateSheetId IS NULL 
            ";
        var x = connectionInsurance.Execute(sqlInsert, new { factId, templateSheetId, row, col, rowSignature });
        return x;
    }

        private List<TemplateSheetFact> SelectFactsForTempateTable(MTable table)
    {
        //2. if there are no xbrl mappings (as in table 19.01.01.01) which whould have a rowCol then
        //+ table 19.01.01 has the MET xbrl codes in table ZDimVal and not in the mappings
        //+ table 19.01.01 has 'F' mappings with RowCol like every other table
        // --select the rowcols from the page mappings (distinct) and not from the Xbrl mappings (origin 'F' and dim_code starting with MET)
        // --find any other dims from the page mappings (is_InTable=1) and then add the zet mappings 
        // --find the facts
        var tableFacts = new List<TemplateSheetFact>();
        var allTableFieldMappings = _SqlFunctions.SelectMappings(table.TableID, MappingOrigin.Field)?.ToList() ?? new List<MAPPING>();

        //pagekeys create new sheets (not all zets do that)        

        var pageDims1 = _SqlFunctions.SelectMappings(table.TableID, MappingOrigin.Page)
            .Select(dim => DimDom.GetParts(dim.DIM_CODE).Dim)
            .ToList();

        var rowColDistinctMappings = allTableFieldMappings
            .Where(map => map.ORIGIN == "F")
            .GroupBy(map => map.DYN_TAB_COLUMN_NAME)
            .Select(map => map.First())
            .OrderBy(map => map.DYN_TAB_COLUMN_NAME)
            .ToList();


        IEnumerable<string> tableZetDims = table.ZDimVal?
            .Split("|", StringSplitOptions.RemoveEmptyEntries)
            ?? Enumerable.Empty<string>();

        List<string> tableYDims = table.YDimVal?
            .Split("|", StringSplitOptions.RemoveEmptyEntries)
            .ToList() ?? new List<string>();



        var zetDims = tableZetDims.Where(dim => !dim.StartsWith("MET"));
        var xbrlFull = tableZetDims.Where(dim => dim.StartsWith("MET")).FirstOrDefault() ?? "";
        var xbrlTable = DimUtils.ExtractXbrl(xbrlFull);



        foreach (var rowColDistinctMapping in rowColDistinctMappings)
        {
            var rowCol = rowColDistinctMapping.DYN_TAB_COLUMN_NAME.Trim();
            var rowColObject = DimUtils.CreateRowCol(rowCol);
            //get the 'f' minus the met

            var fieldMappings = _SqlFunctions.SelectRowColMappings(table.TableID, rowCol)
                .Where(map => map.ORIGIN == "F")
                .Select(map => map.DIM_CODE);


            var xbrlDim = fieldMappings?.FirstOrDefault(map => map.StartsWith("MET")) ?? "";
            var xbrl = string.IsNullOrEmpty(xbrlTable) ? DimUtils.ExtractXbrl(xbrlDim) : xbrlTable;
            fieldMappings = fieldMappings.Where(map => !map.Contains("MET"));

            var allCellMappings = fieldMappings.Concat(zetDims).Concat(tableYDims).ToList();

            //******************************************************************************
            //*** find the facts and update there col, row, and ysignature
            var rowColFacts = SelectFactsByDims(xbrl, rowColObject.Row, rowColObject.Col, allCellMappings, tableYDims, pageDims1);
            tableFacts.AddRange(rowColFacts);
            Console.Write($"row:{rowColObject?.Row}, {rowColObject?.Col}");
            if (tableFacts.Count() > 0)
            {
                Console.Write($"-row:{rowColObject?.Row}, {rowColObject?.Col}, count: {rowColFacts?.Count()} ");
            }
            Console.WriteLine("");

        }

        return tableFacts ?? new List<TemplateSheetFact>();
    }


    private List<TemplateSheetFact> SelectFactsByDims(string xbrlCode, string row, string col, List<string> allMappings, List<string> yMappings, List<string> pageDims)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);


        var allFactDimsStr = string.Join(",", allMappings.Select(map => $"'{DimDom.GetParts(map).Dim}'"));

        var openAllDims = allMappings.Where(dim => dim.Contains('*'));
        var openAllStr = string.Join(",", openAllDims.Select(dim => $"'{DimDom.GetParts(dim).Dim}'"));
        var openAllCount = openAllDims.Count();

        var openMandatoryDims = allMappings.Where(dim => dim.Contains('*') && !dim.Contains('?'));
        var openMandatoryString = string.Join(",", openMandatoryDims.Select(dim => $"'{DimDom.GetParts(dim).Dim}'"));
        var openMandatoryCount = openMandatoryDims.Count();

        var hasOpenOptionalDims = allMappings.Any(dim => dim.Contains('*') && dim.Contains('?'));


        var closedDims = allMappings.Where(dim => !dim.Contains('*'));
        var closedString = string.Join(",", closedDims.Select(dim => $"'{DimDom.GetParts(dim).Signature}'"));
        var closedCount = closedDims.Count();





        string sqlClosed = @$"
            AND EXISTS (                
                SELECT COUNT(*) AS cnt
                  FROM TemplateSheetFactDim fd
                WHERE 1=1
                AND fd.FactId=fact.FactId                
                AND fd.Signature IN ({closedString})
                GROUP BY fd.FactId
                HAVING COUNT(*)=@closedCount
              )
        ";

        var sqlOpenMandatory = $@"
            AND EXISTS (
                SELECT COUNT(*) AS cnt
                  FROM TemplateSheetFactDim fd
                WHERE 1=1
                AND fd.FactId=fact.FactId                
                AND fd.DIM IN ({openMandatoryString})
                GROUP BY fd.FactId
                HAVING COUNT(*)=@openMandatoryCount
                )
        ";

        var sqlNotExist = $@"
            AND NOT EXISTS (
                SELECT 1
                  FROM TemplateSheetFactDim fd
                WHERE 1=1
                    AND fd.FactId=fact.FactId                    
                    AND fd.DIM NOT IN ({allFactDimsStr})                                
                ) 
        ";

        var andRowSQL = string.IsNullOrWhiteSpace(row) ? "" : "AND fact.Row=@ROW  ";

        var basicSQL = @$"
            SELECT fact.*
            FROM TemplateSheetFact fact                 
            WHERE 1=1              
              AND fact.XBRLCode = @XBRLCode              
              AND fact.InstanceId=@_documentId
            ";

        var sqlSelect = $@"{basicSQL} {Environment.NewLine}";


        if (!string.IsNullOrEmpty(closedString))
        {
            sqlSelect = @$" {sqlSelect} {sqlClosed} {Environment.NewLine}";
        };

        if (!string.IsNullOrEmpty(openMandatoryString))
        {
            sqlSelect = @$" {sqlSelect} {sqlOpenMandatory} {Environment.NewLine}";
        };

        if (hasOpenOptionalDims)
        {
            sqlSelect = @$" {sqlSelect} {sqlNotExist}";
        };
        sqlSelect = $"{sqlSelect} order by fact.Row,fact.Col";

        var facts = connectionInsurance!.Query<TemplateSheetFact>(sqlSelect, new
        {
            _documentId,
            xbrlCode,
            closedCount,
            openMandatoryCount
        })?.ToList() ?? new List<TemplateSheetFact>();

        //todo update row and column, 
        //remove row col from select
        //what about row ??

        var yMappingsDims = yMappings.Select(map => DimDom.GetParts(map).Dim);
        foreach (var fact in facts)
        {

            var factdims = _SqlFunctions.SelectFactDims(fact.FactId);
            var pageFactDims = factdims
                .Where(dim => pageDims.Contains(dim.Dim))
                .Select(dim => DimDom.GetParts(dim.Signature).DomAndValRaw)
                .Select(dim => dim.Replace(":", "_"))
                .Order();


            var yFactDims = factdims
                .Where(dim => yMappingsDims.Contains(dim.Dim))
                .Select(dim => DimDom.GetParts(dim.Signature).Signature)
                .Order();

            var pageZet = string.Join("__", pageFactDims);
            var yRowSignature = string.Join("|", yFactDims);
            fact.Row = row;//open tables will be updated later based on their y dims
            fact.Col = col;
            fact.Zet = pageZet;
            fact.RowSignature = yRowSignature;
        }


        return facts;

    }

    static List<string> GetTableYDims(MTable table)
    {
        //these are the dims that will be added on MAPPINGS to make a mapping signature
        //var zDimsAll = table.ZDimVal?.Split("|")?.ToList() ?? new List<string>(); //apply to all mappings                        
        //var zDimsClosed = zDimsAll.Where(item => !item.Contains("(*")).ToList() ?? new List<string>();//s2c_dim:LG(*[GA_18;x0;0])=> 
        //var zDimsOpen = zDimsAll.Where(item => item.Contains("(*"))
        //    .Select(dim => Regex.Replace(dim, @"\(\*(.*?)\)", "(*)")).ToList();  //s2c_dim:LG(*[GA_18;x0;0])=>s2c_dim:LG(*)

        var yDimsAll = table.YDimVal?.Split("|")?.ToList() ?? new List<string>();// apply to all  in cells in the row            

        var yDimsClosed = yDimsAll.Where(item => !item.Contains("(*")).ToList() ?? new List<string>();
        var yDimsOpen = yDimsAll.Where(item => item.Contains("(*"))
            .Select(dim => Regex.Replace(dim, @"\(\*(.*?)\)", "(*)")).ToList();  //s2c_dim:LG(*[GA_18;x0;0])=>s2c_dim:LG(*)

        var tableDims = new List<string>();

        tableDims.AddRange(yDimsOpen);
        tableDims.AddRange(yDimsClosed);


        return tableDims;
    }

    TemplateSheetInstance CreateSheet(MTable table, string sheetCode)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var SqlInsertTemplateSheet = @"
                         INSERT INTO TemplateSheetInstance
                           (
                            [InstanceId]
                           ,[UserId]         
                           ,[TableId]
                           ,[DateCreated]
                           ,[SheetCode]                 
                           ,[TableCode]                 
                           ,[YDimVal]
                           ,[ZDimVal]
                           ,[status]
                           ,[Description]
                           ,[XbrlFilingIndicatorCode]
                            ,[IsOpenTable]

                            )
                        VALUES
                           (
                            @InstanceId
                           ,@UserId  
                           ,@TableId
                           ,@DateCreated
                           ,@SheetCode
                           ,@TableCode
                           ,@YDimVal
                           ,@ZDimVal
                           ,@status           
                           ,@Description
                           ,@XbrlFilingIndicatorCode
                            ,@IsOpenTable
                            );        
                            SELECT CAST(SCOPE_IDENTITY() as int);
                        ";
        Console.Write(',');

        var sheet = new TemplateSheetInstance()
        {
            InstanceId = _documentId,
            UserId = "KK",
            TableID = table.TableID,
            TableCode = table.TableCode,
            DateCreated = DateTime.Now,
            SheetCode = sheetCode,
            YDimVal = table.YDimVal,
            ZDimVal = table.ZDimVal,
            Status = "LD",
            Description = RegexUtils.TruncateString(table.TableLabel, 199),
            XbrlFilingIndicatorCode = table.XbrlFilingIndicatorCode,
            IsOpenTable = table.IsOpenTable,
            OpenRowCounter = 0
        };

        var sheetId = connectionInsurance.QuerySingle<int>(SqlInsertTemplateSheet, sheet);
        sheet.TemplateSheetId = sheetId;

        //ad the zet dims for each TemplateSheetInstance
        var dims = sheetCode.Split("__");
        foreach (var factDim in dims)
        {
            var zetParts = factDim.Split("#").ToList();
            if (zetParts.Count == 2)
            {
                var sqlZet = @"INSERT INTO SheetZetValue (Dim, Value, TemplateSheetId) VALUES (@dim, @value, @templateSheetId)";
                connectionInsurance.Execute(sqlZet, new { dim = zetParts[0], value = zetParts[1], templateSheetId = sheetId });
                Console.Write(',');
            }
        }

        return sheet;
    }


    private List<MTable> GetFiledModuleTables()
    {

        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var sqlTables = @"
              SELECT 	  
                    tab.TableID,
                    tab.TableCode,                    
                    tab.XbrlFilingIndicatorCode,
                    tab.XbrlTableCode,
                    tab.YDimVal,
                    tab.ZDimVal,
                    tab.TableLabel
                  FROM mModuleBusinessTemplate mbt
                  left outer join mTemplateOrTable  va on va.TemplateOrTableID =mbt.BusinessTemplateID
                  left outer join mTemplateOrTable bu on bu.ParentTemplateOrTableID = va.TemplateOrTableID
                  left outer join mTemplateOrTable anno on anno.ParentTemplateOrTableID = bu.TemplateOrTableID
                  left outer join mTaxonomyTable taxo on taxo.AnnotatedTableID=anno.TemplateOrTableID
                  left outer join mTable tab on tab.TableID = taxo.TableID
                  where mbt.ModuleID = @_moduleId";
        var moduleTables = connectionEiopa.Query<MTable>(sqlTables, new { _moduleId }).ToList();

        var validModuleTables = moduleTables.Where(mtable => _filings.Any(filing => mtable.TableCode.Contains(filing))).ToList();


        return validModuleTables;
    }


    public static string SimplifyCellSignature(string cellSignature, bool allowOptional)
    {
        //replace selections with sql wildcard s2c_dim:AX(*[8;1;0])=>s2c_dim:AX(%). 
        //if optional is not allowed remove terms which contain "?"            

        //@"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)";
        //allow optional =>@"MET(s2md_met:mi87)|s2c_dim:AF(%)|s2c_dim:AX(%)|s2c_dim:BL(s2c_LB:x9)"
        //not allow optional=>@"MET(s2md_met:mi87)|s2c_dim:AX(%)|s2c_dim:BL(s2c_LB:x9)");


        var dimListBasic = cellSignature.Split("|").ToList();
        if (!allowOptional)
        {
            dimListBasic = dimListBasic.Where(dim => !dim.Contains('?')).ToList();
        }


        var dimList = dimListBasic
        .Select(dim => dim.Replace("?", ""))
        .Select(dim => Regex.Replace(dim, @"\[.*\]", ""))
        .Select(dim => dim.Replace("*", "%")).ToList();
        var cleanSig = string.Join("|", dimList);

        return cleanSig;
    }



    ///************************

    public bool IsNewSignatureMatch(string cellSignature, string factSignature)
    {

        //check for valid hierarchy members[323;3;3] 
        var factDims = factSignature.Split("|");
        var cellDims = cellSignature.Split("|");

        //it does not have even an xbrl code
        if (!factDims.Any())
        {
            return false;
        }

        var factDimDoms = factDims.Select(fd => DimDom.GetParts(fd)).Skip(1).ToList();
        var cellDimDoms = cellDims.Select(cd => DimDom.GetParts(cd)).Skip(1).ToList();


        //List<DimDom> xx = cellDimDoms.Sort((DimDom a, DimDom b) => string.Compare(a.DomValue, b.DomValue)).ToList<DimDom>;
        cellDimDoms.Sort((DimDom a, DimDom b) => string.Compare(b.DomValue, a.DomValue));

        var countFactDimDoms = factDimDoms.Count();
        foreach (var cellDimDom in cellDimDoms)
        {


            var factDimDom = factDimDoms.FirstOrDefault(fd => fd.Dim == cellDimDom.Dim);
            //it is ok if cellDim is optional and fact does not have the dim.
            //But If the fact has the dim, check if value is in hierarchy (isNewDimMatch)              
            if (cellDimDom.IsOptional && factDimDom is null)
            {
                continue;
            }
            if (!cellDimDom.IsOptional && factDimDom is null)
            {
                return false;
            }

            if (!IsNewDimMatch(cellDimDom, factDimDom))
            {
                return false;
            }

            countFactDimDoms -= 1;
        }
        if (countFactDimDoms != factDimDoms.Count)
        {
            //throw (new Exception($"@@@count diferrent sig:{cellSignature}"));
        }
        //return factDimDoms.Count == 0;
        return countFactDimDoms == 0;

    }


    private bool IsNewDimMatch(DimDom cellDimDom, DimDom factDimDom)
    {
        //            
        // "*" allows for any value but brackets constrain the values to the hierechy members
        ////MET(s2md_met:mi686)|s2c_dim:AO(*?[16])|s2c_dim:EA(s2c_VM:x23)|s2c_dim:RT(s2c_RT:x97)|s2c_dim:VG(s2c_AM:x80)
        //MET(s2md_met:mi1157)|s2c_dim:BL(*[334;1512;0])|s2c_dim:CC(s2c_TB:x12)|s2c_dim:FC(*)|s2c_dim:RD(*)|s2c_dim:RE(*)
        //MET(s2md_met:mi289)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[332;1512;0])|s2c_dim:DY(s2c_TI:x1)|s2c_dim:OC(*?[237])|s2c_dim:RM(s2c_TI:x49)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)

        //*** should not happen but check anyway
        if (cellDimDom.Dim != factDimDom.Dim)
        {
            return false;
        }

        //***  Completely open, anything goes as dom value
        if (cellDimDom.DomAndValRaw == "*")
        {
            return true;
        }

        //If no * then check whole value
        if (!cellDimDom.IsWild)
        {
            return cellDimDom.DomAndValRaw == factDimDom.DomAndValRaw;
        }


        //check if the fact's dom value belongs in the hierarchy
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

        var hierarchyParts = RegexUtils.GetRegexSingleMatch(@"\[(.*)\]", cellDimDom.DomAndValRaw).Split(";");
        if (hierarchyParts.Length < 1)
        {
            return false;
        }

        var hierarchyId = hierarchyParts[0];
        var sqlSelectMem = @"select MemberID from mMember mem where mem.MemberXBRLCode=@MemberXBRLCode";
        var memberId = connectionEiopa.QueryFirstOrDefault<int>(sqlSelectMem, new { MemberXBRLCode = factDimDom.DomAndValRaw });
        if (memberId == 0)
        {
            return false;
        }

        var sqlSelectHiMembers = @"select nod.HierarchyID from mHierarchyNode nod where nod.HierarchyID= @HierarchyID and nod.MemberID = @MemberID";
        var hierarchyNode = connectionEiopa.QueryFirstOrDefault<int>(sqlSelectHiMembers, new { hierarchyId, memberId });
        return hierarchyNode > 0;


    }


    ///*******************


    private int CreateYFactsInDb(int sheetId)
    {
        //open tables: need to create y cells in EVERY row because they are NOT written as facts in xbrl files, but they are lines in the context 
        //for every row we need to create one cell for EACH Y dim column.(* may be more than one Y dim)            
        //RowContexts were created when preparing the facts



        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);


        var sqlSelectSheet = @"select TemplateSheetId, TableCode, SheetCode, TableID from TemplateSheetInstance where TemplateSheetId =@sheetId";
        var sheet = connectionInsurance.QuerySingleOrDefault<TemplateSheetInstance>(sqlSelectSheet, new { sheetId });
        if (sheet is null)
        {
            return 0;
        }

        //DISTINCT InternalRow AND OpenRowSignature because need only one fact per row with the unique OpenRowSignature
        var sqlFactSig = @"select distinct fact.InternalRow, OpenRowSignature,zet,zetValues from TemplateSheetFact fact  where TemplateSheetId= @sheetId";
        var factsWithRowSignature = connectionInsurance.Query<TemplateSheetFact>(sqlFactSig, new { sheetId }) ?? new List<TemplateSheetFact>();

        foreach (var factWithRowSignature in factsWithRowSignature)
        {

            var factYdims = factWithRowSignature?.OpenRowSignature?.Split("|", StringSplitOptions.RemoveEmptyEntries).ToList();
            if (factYdims is null)
            {
                return 0;
            }

            //***********************
            //var sqlTable = @"s2c_dim:BL(*[350;1512;0])|s2c_dim:LP(*)|s2c_dim:OD(*)|s2c_dim:RE(*)|s2c_dim:ST(*)";                
            var sqlTable = @"select YDimVal from  mTable tab where tab.TableID= @tableId";
            var yTableDimStr = connectionEiopa.QuerySingleOrDefault<string>(sqlTable, new { sheet.TableID });
            if (yTableDimStr is null)
            {
                return 0;
            }
            var tableYdims = yTableDimStr.Split("|", StringSplitOptions.RemoveEmptyEntries);

            foreach (var tableYdim in tableYdims)
            {
                var yTableDimDom = DimDom.GetParts(tableYdim);
                var sqlFindMapping = @"
                            SELECT
                              map.DYN_TAB_COLUMN_NAME
                             ,map.DIM_CODE
                             ,map.DOM_CODE
                             ,map.ORIGIN
                             ,map.DATA_TYPE
                             ,map.IS_PAGE_COLUMN_KEY
                             ,map.IS_IN_TABLE
                            FROM MAPPING map
                            WHERE 
				            map.DYN_TAB_COLUMN_NAME not like 'PAGE%'
				            and map.ORIGIN = 'C'
                            AND map.IS_IN_TABLE = 1
                            AND map.TABLE_VERSION_ID = @tableId
				            and map.DIM_CODE like @dimCode
				
                    ";
                var yDimMapping = connectionEiopa.QueryFirstOrDefault<MAPPING>(sqlFindMapping, new { tableId = sheet.TableID, dimCode = $"s2c_dim:{yTableDimDom.Dim}%" });
                if (yDimMapping is null)
                {
                    continue;
                }

                var factYdim = factYdims.FirstOrDefault(dim => DimDom.GetParts(dim).Dim == yTableDimDom.Dim);
                if (factYdim is null)
                {
                    continue;
                }

                var factDimDomValue = DimDom.GetParts(factYdim);
                var signatureFilled = $"YR|{DimDom.GetParts(factYdim).Dim}|{factWithRowSignature.OpenRowSignature}";
                var newFact = new TemplateSheetFact()
                {
                    InstanceId = _documentId,
                    TemplateSheetId = sheet.TemplateSheetId,
                    TableID = sheet.TableID,
                    Unit = "",
                    DataType = "",
                    DataTypeUse = tableYdim.Contains("[") ? "E" : "S",
                    XBRLCode = "RowKey",
                    DataPointSignatureFilled = signatureFilled,
                    OpenRowSignature = factWithRowSignature.OpenRowSignature,
                    CellID = 0,// cell does not exist for column cells
                    IsRowKey = true, //rowMapping.IS_PAGE_COLUMN_KEY,
                    Row = $"R{factWithRowSignature.InternalRow:D4}", // rowContext.RowNumber,
                    Col = yDimMapping.DYN_TAB_COLUMN_NAME,
                    Zet = factWithRowSignature.Zet,
                    InternalRow = factWithRowSignature.InternalRow,
                    InternalCol = 0,
                    TextValue = tableYdim.Contains("[") ? factDimDomValue.DomAndValRaw : factDimDomValue.DomValue,
                    FieldOrigin = "KYR",
                    CurrencyDim = "VV",
                    ZetValues = factWithRowSignature.ZetValues


                };
                newFact.ConvertTextValue();

                var SqlInsertTemplateSheetFact = @"
                        INSERT INTO [dbo].[TemplateSheetFact]
                           (
                            [InstanceId]
                           ,[TemplateSheetId]           
                           ,[TableID]
                           ,[Unit]
                           ,[DataType]
                           ,[DataTypeUse]
                           ,[XbrlCode]
                           ,[DataPointSignature]
                           ,[DataPointSignatureFilled]
                           ,[OpenRowSignature]
                           ,[CellID]
                           ,[IsRowKey]
                           ,[Row]
                           ,[Col]
                            ,Zet
                           ,[InternalRow]
                           ,[InternalCol]           
                           ,[TextValue] 
                           ,[NumericValue]
                           ,[DateTimeValue]
                           ,[BooleanValue]    
                           ,[IsConversionError]
                           ,[CurrencyDim]
                           ,ZetValues
                           ,[FieldOrigin]
                            )
                        VALUES
                           (            
                            @InstanceId
                           ,@TemplateSheetId           
                           ,@TableID                           
                           ,@unit
                           ,@DataType
                           ,@DataTypeUse
                           ,@XbrlCode
                           ,@DataPointSignature
                           ,@DataPointSignatureFilled
                           ,@OpenRowSignature
                           ,@CellID
                           ,@IsRowKey
                           ,@Row
                           ,@Col           
                           ,@Zet
                           ,@InternalRow
                           ,@InternalCol           
                           ,@TextValue        
                           ,@NumericValue
                           ,@DateTimeValue
                           ,@BooleanValue      
                           ,@IsConversionError
                           ,@CurrencyDim
                           ,@ZetValues
                           ,@FieldOrigin
                           )
                        ";

                var res = connectionInsurance.Execute(SqlInsertTemplateSheetFact, newFact);
                Console.Write(".");

            }

        }
        return 1;
    }

    private static List<string> ConstructFactFullZetList(string factSignature, string tabeZetSignature)
    {
        //find the dims of the fact that are contained in the table zet. table zet Dims can be explcit or wild (open)
        //normally zetSignatrue does not contain a Met but there is one case. Do not store it as dimension
        //zetSignature = @"MET(s2md_met:mi289)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[332;1512;0])|s2c_dim:OC(*?[237])|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";
        //factSignature = @"MET(s2md_met:mi289)|s2c_dim:AF(s2c_CA:x1)|s2c_dim:AX(s2c_AM:x4)|s2c_dim:BL(s2c_LB:x73)|s2c_dim:DY(s2c_TI:x1)|s2c_dim:OC(s2c_CU:EUR)|s2c_dim:RM(s2c_TI:x49)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)";

        var tabeZetList = tabeZetSignature?.Split("|")?.ToList() ?? new List<string>();
        if (tabeZetList.Count == 0)
        {
            return tabeZetList;
        }

        var zetOpenList = tabeZetList.Where(dim => dim.Contains("*")).ToList();
        var zetClosedList = tabeZetList.Where(dim => !dim.Contains("*")).ToList(); ;
        var factDims = factSignature?.Split("|")?.ToList() ?? new List<string>();

        var zetFinalList = new List<string>();

        foreach (var zetDim in zetOpenList)
        {

            var zetDimPart = RegexUtils.GetRegexSingleMatch(@"(s2c_dim.*?:\w\w)", zetDim);//s2c_dim:AF(*?[59]) => s2c_dim:AF
            var factDim = factDims.SingleOrDefault(dim => dim.Contains(zetDimPart));

            if (factDim is not null)
            {
                var fff = DimDom.GetParts(factDim);
                var factDimPart = RegexUtils.GetRegexSingleMatch(@"s2c_dim:(\w\w)", factDim);//"s2c_dim:AF(s2c_CA:x1)=> AF
                var factDomPart = RegexUtils.GetRegexSingleMatch(@"s2c_dim:\w\w\((.*?)\)", factDim); //"s2c_dim:AF(s2c_CA:x1)=> s2c_CA:x1                                        
                zetFinalList.Add($"{factDimPart}#{factDomPart}");
            }
        }

        foreach (var dim in zetClosedList)
        {
            var zetDimPart = RegexUtils.GetRegexSingleMatch(@"s2c_dim:(\w\w)", dim); //"s2c_dim:TA(s2c_AM:x57)=>TA
            var zetDomPart = RegexUtils.GetRegexSingleMatch(@"s2c_dim:\w\w\((s2c_.*?)\)", dim);// "s2c_dim:TA(s2c_AM:x57)=> AM:x57
            var xxx = DimDom.GetParts(dim);
            if (!string.IsNullOrEmpty(zetDimPart) && !string.IsNullOrEmpty(zetDomPart))
            {
                zetFinalList.Add($"{zetDimPart}#{zetDomPart}");
            }

        }

        zetFinalList.Sort();
        return zetFinalList;

    }


    //*******************************


    public void UpdateCellsForeignRow(int documentId)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var sqlSelectSheets = @"select sheet.TemplateSheetId,sheet.TableCode from TemplateSheetInstance sheet where sheet.IsOpenTable=1 and  sheet.InstanceId = @documentId";
        var sheets = connectionInsurance.Query<TemplateSheetInstance>(sqlSelectSheets, new { documentId })?.ToList() ?? new();
        foreach (var sheet in sheets)
        {

            Console.WriteLine($"Update Foreign Keys");
            UpdateSheetFactsWithMasterRow(sheet.TemplateSheetId);
        }

    }

    void UpdateSheetFactsWithMasterRow(int sheetId)
    {
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

        var sqlTable = @"select sheet.TableCode from TemplateSheetInstance sheet where sheet.TemplateSheetId= @sheetId";
        var table = connectionLocal.QueryFirstOrDefault<TemplateSheetInstance>(sqlTable, new { sheetId });

        var sqlKyr = "select kk.TableCode,kk.TableCodeKeyDim,kk.FK_TableCode, kk.FK_TableDim from mTableKyrKeys kk where kk.TableCode = @tableCode";
        var kyrRecord = connectionEiopa.QueryFirstOrDefault<MTableKyrKeys>(sqlKyr, new { table.TableCode });
        if (kyrRecord?.FK_TableCode is null) return;

        var sqlFacts = @"select fact.FactId, fact.InstanceId, fact.TextValue,  fact.Row, fact.RowForeign from TemplateSheetFact fact 
                where fact.TemplateSheetId= @sheetId 
                and (fact.FieldOrigin<>'KYR' or fact.FieldOrigin is null)
            ";
        var facts = connectionLocal.Query<TemplateSheetFact>(sqlFacts, new { sheetId });

        foreach (var fact in facts)
        {
            UpdateFactWithMasterRow(fact, kyrRecord);
        }
    }

    int UpdateFactWithMasterRow(TemplateSheetFact fact, MTableKyrKeys kyrRecord)
    {
        //update the RowForeign of the main table with the row of a related table.
        //For example, S.06.02.01.01 has links with S.06.02.01.02 on the "UI" dim. (SEVERAL rows of S.06.02.01.01 may correspond to a row of S.06.02.01.02 ** checked and true)       
        //  Therefore, each cell of the S.06.02.01 has a rowForeign which points to a cell of S.06.02.01.02
        //  ---------------------------------------------------------------------------------------------
        //Actually the main table may be related with more than one related tables.
        //For example, table S.30.02.01.01 is linked with S.30.02.01.03 with the RF dim and with S.30.02.01.04 with "CA" dim.
        //We would need a more complex design for this arrangment which was not asked.


        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);

        //select the dim based on the kyrkeys (the kyrKeys will provide the  master fact)
        var sqlFactDim = @"select fd.Dim,fd.Signature from TemplateSheetFactDim fd where fd.FactId= @factId and fd.Dim= @dim";
        var dim = connectionLocal.QuerySingleOrDefault<TemplateSheetFactDim>(sqlFactDim, new { fact.FactId, dim = kyrRecord.FK_TableDim });
        if (dim is null) return 0;

        //find the row of the "first" master fact using the fk dim
        var sqlMasterFact = @"
                SELECT TOP 1 fc.row, fc.col, fc.TextValue
                FROM TemplateSheetFact fc
                JOIN TemplateSheetInstance sheet ON sheet.TemplateSheetId = fc.TemplateSheetId
                JOIN TemplateSheetFactDim dm ON dm.FactId = fc.FactId
                WHERE sheet.InstanceId = @InstanceId AND sheet.TableCode = @TableCode AND dm.Signature = @Signature AND IsRowKey = 0
            ";
        var masterFact = connectionLocal.QueryFirstOrDefault<TemplateSheetFact>(sqlMasterFact, new { fact.InstanceId, tableCode = kyrRecord.FK_TableCode, dim.Signature });
        if (masterFact is null) return 0;

        var sqlUpdFact = @"update TemplateSheetFact set RowForeign= @FK_Row where FactId= @factId";
        _ = connectionLocal.Execute(sqlUpdFact, new { FK_Row = masterFact.Row, fact.FactId });

        return fact.FactId;

    }
    //*******************************

    public List<TemplateSheetFact> FindFactsFromSignatureNewxx(int documentId, string cellSignature)
    {
        //Select the facts that match the cell signature using two methods
        //if the fact signature has no selections, then use sql with direct signature matching
        //otherwise, use the xbrl and ONLY the dims without selections to find the facts matching
        //.... then conduct further filtering for each fact, checking the fact  dims agains the cell dims one by one
        ////var test= @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])||s2c_dim:FC(*)|s2c_dim:DI(s2c_DI:x5)|s2c_dim:OC(*?[237])";

        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var factList = new List<TemplateSheetFact>();


        var mandatoryWildSignature = SimplifyCellSignature(cellSignature, false);
        var dimsMandatoryAndXbrl = mandatoryWildSignature.Split("|").ToList();
        var dimsMandatory = dimsMandatoryAndXbrl.Skip(1).ToList();
        var xbrlMetric = dimsMandatoryAndXbrl.FirstOrDefault();
        var xbrlCode = string.IsNullOrEmpty(xbrlMetric) ? "" : RegexUtils.GetRegexSingleMatch(@"MET\((.*?)\)", xbrlMetric);
        if (string.IsNullOrEmpty(xbrlCode))
        {
            return factList;
        }

        var fuzzyRegex = new Regex(@"[\*\?\[]", RegexOptions.Compiled);
        var isfuzzySignature = fuzzyRegex.IsMatch(cellSignature);

        //************ signature is simple 
        //no optinal dims, no wildcar dims 
        //Select the facts directl using the signature without any wildcards
        if (!isfuzzySignature)
        {
            var sqlFullSignature = @"            
              SELECT  
                  fact.FactId
                 ,fact.TemplateSheetId
                 ,fact.Row
                 ,fact.Col
                 ,fact.Zet
                 ,fact.CellID
                 ,fact.FieldOrigin
                 ,fact.TableID
                 ,fact.DataPointSignature
                 ,fact.Unit
                 ,fact.Decimals
                 ,fact.NumericValue
                 ,fact.BooleanValue
                 ,fact.DateTimeValue
                 ,fact.TextValue
                 ,fact.DPS
                 ,fact.IsRowKey
                 ,fact.IsShaded
                 ,fact.XBRLCode
                 ,fact.DataType
                 ,fact.DataPointSignatureFilled                 
                 ,fact.InternalRow
                 ,fact.internalCol
                 ,fact.DataTypeUse
                 ,fact.IsEmpty
                 ,fact.IsConversionError
                 ,fact.ZetValues
                 ,fact.OpenRowSignature
                 ,fact.CurrencyDim                 
                 ,fact.contextId                 
                 ,fact.Signature
                 ,fact.RowSignature                 
                 ,fact.InstanceId                  

                FROM dbo.TemplateSheetFact fact
                WHERE fact.InstanceId = @documentId
                AND fact.XBRLCode = @xbrlCode
                AND fact.DataPointSignature = @sig;
             ";
            var factListSimple = connectionInsurance.Query<TemplateSheetFact>(sqlFullSignature, new { documentId, xbrlCode, sig = cellSignature }).ToList();
            return factListSimple;
        }



        var countOptionalDims = cellSignature.Split("|").Where(part => part.Contains('?')).Count();
        var sqlWildSelect = @"            
              SELECT  
                  fact.FactId
                 ,fact.TemplateSheetId
                 ,fact.Row
                 ,fact.Col
                 ,fact.Zet
                 ,fact.CellID
                 ,fact.FieldOrigin
                 ,fact.TableID
                 ,fact.DataPointSignature
                 ,fact.Unit
                 ,fact.Decimals
                 ,fact.NumericValue
                 ,fact.BooleanValue
                 ,fact.DateTimeValue
                 ,fact.TextValue
                 ,fact.DPS
                 ,fact.IsRowKey
                 ,fact.IsShaded
                 ,fact.XBRLCode
                 ,fact.DataType
                 ,fact.DataPointSignatureFilled                 
                 ,fact.InternalRow
                 ,fact.internalCol
                 ,fact.DataTypeUse
                 ,fact.IsEmpty
                 ,fact.IsConversionError
                 ,fact.ZetValues
                 ,fact.OpenRowSignature
                 ,fact.CurrencyDim                 
                 ,fact.contextId                 
                 ,fact.Signature
                 ,fact.RowSignature                 
                 ,fact.InstanceId                  

                FROM dbo.TemplateSheetFact fact
                WHERE fact.InstanceId = @documentId
                AND fact.XBRLCode = @xbrlCode
                AND fact.DataPointSignatureFilled like @sig;
             ";
        var wildFacts = new List<TemplateSheetFact>();
        if (countOptionalDims == 0)
        {
            //No OPTIONAL dims - but use wildcards
            wildFacts = connectionInsurance.Query<TemplateSheetFact>(sqlWildSelect, new { documentId, xbrlCode, sig = mandatoryWildSignature }).ToList();
        }
        else if (countOptionalDims == 1)
        {
            //there is one optional Dim. search without the optional and WITH the optinal dim
            wildFacts = connectionInsurance.Query<TemplateSheetFact>(sqlWildSelect, new { documentId, xbrlCode, sig = mandatoryWildSignature }).ToList();
            var optionalWildSignature = SimplifyCellSignature(cellSignature, true);
            var optionalWild = connectionInsurance.Query<TemplateSheetFact>(sqlWildSelect, new { documentId, xbrlCode, sig = optionalWildSignature }).ToList();
            wildFacts.AddRange(optionalWild);
        }
        else
        {
            //more than one optional, use the other method

            var sqlNewExample = @"
                 select fact.FactId,count(*)
                 from TemplateSheetFact fact 
                 join TemplateSheetFactDim dim on dim.FactId= fact.FactId
                 where 
                 fact.InstanceId=@documentId
                 and fact.XbrlCode= @xblrCode
                 and dim.Dim in ( 'BL','DI','LA','TZ','VG') 
                 group by fact.FactId
                 having count(*)=5
  
            ";

            var sqlNewPart1 = @"
                 select fact.FactId,count(*),fact.DataPointSignature
                 from TemplateSheetFact fact 
                 join TemplateSheetFactDim dim on dim.FactId= fact.FactId
                 where 
                 fact.InstanceId=@documentId
                 and fact.XbrlCode= @xbrlCode                   
            ";

            var mandatoryDimsInQuotes = dimsMandatory
                .Select(dm => DimDom.GetParts(dm).Dim)
                .Select(dm => $"'{dm}'");
            var sqldimPart2 = $" and dim in ({string.Join(",", mandatoryDimsInQuotes)})";
            var sqlByGrouping = sqlNewPart1 + sqldimPart2 + " Group by fact.factId,fact.DataPointSignature " + $" having count(*) ={dimsMandatory.Count} ";

            var possibleFacts = connectionInsurance.Query<TemplateSheetFact>(sqlByGrouping, new { documentId, xbrlCode })
                .Where(fact => fact?.FactId is not null)
                .ToList();
        }


        foreach (var wildFact in wildFacts)
        {
            //var sqlFact = "select fact.FactId, fact.DataPointSignature from TemplateSheetFact fact where fact.FactId= @factId";
            //var fact = connectionInsurance.QuerySingleOrDefault<TemplateSheetFact>(sqlFact, new { documentId, factId = possibleFact.FactId });
            var isMatch = IsNewSignatureMatch(cellSignature, wildFact?.DataPointSignature ?? "");
            if (isMatch)
            {
                factList.Add(wildFact);
            }
        }

        return factList;


    }


    public List<TemplateSheetFact> FindFactsFromSignatureWild(int documentId, string cellSignature)
    {
        //Select the facts that match the cell signature using two methods
        //if the fact signature has no selections, then use sql with direct signature matching
        //otherwise, use the xbrl and ONLY the dims without selections to find the facts matching
        //.... then conduct further filtering for each fact, checking the fact  dims agains the cell dims one by one
        ////var test= @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])||s2c_dim:FC(*)|s2c_dim:DI(s2c_DI:x5)|s2c_dim:OC(*?[237])";

        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var factList = new List<TemplateSheetFact>();


        var mandatoryWildSignature = SimplifyCellSignature(cellSignature, false);
        var dimsMandatoryAndXbrl = mandatoryWildSignature.Split("|").ToList();
        var dimsMandatory = dimsMandatoryAndXbrl.Skip(1).ToList();
        var xbrlMetric = dimsMandatoryAndXbrl.FirstOrDefault();
        var xbrlCode = string.IsNullOrEmpty(xbrlMetric) ? "" : RegexUtils.GetRegexSingleMatch(@"MET\((.*?)\)", xbrlMetric);
        if (string.IsNullOrEmpty(xbrlCode))
        {
            return factList;
        }

        //if there is no optional or selection then the  uses equality instead of like
        var fuzzyRegex = new Regex(@"[\*\?\[]", RegexOptions.Compiled);
        var isfuzzySignature = fuzzyRegex.IsMatch(cellSignature);

        //************ signature is simple 
        //no optinal dims, no wildcard dims 
        //Select the facts directly using the signature in sql expression without any wildcards
        if (!isfuzzySignature)
        {
            var sqlFullSignature = @"            
              SELECT  
                  fact.FactId
                 ,fact.TemplateSheetId
                 ,fact.Row
                 ,fact.Col
                 ,fact.Zet
                 ,fact.CellID
                 ,fact.FieldOrigin
                 ,fact.TableID
                 ,fact.DataPointSignature
                 ,fact.Unit
                 ,fact.Decimals
                 ,fact.NumericValue
                 ,fact.BooleanValue
                 ,fact.DateTimeValue
                 ,fact.TextValue
                 ,fact.DPS
                 ,fact.IsRowKey
                 ,fact.IsShaded
                 ,fact.XBRLCode
                 ,fact.DataType
                 ,fact.DataPointSignatureFilled                 
                 ,fact.InternalRow
                 ,fact.internalCol
                 ,fact.DataTypeUse
                 ,fact.IsEmpty
                 ,fact.IsConversionError
                 ,fact.ZetValues
                 ,fact.OpenRowSignature
                 ,fact.CurrencyDim                 
                 ,fact.contextId                 
                 ,fact.Signature
                 ,fact.RowSignature                 
                 ,fact.InstanceId                  

                FROM dbo.TemplateSheetFact fact
                WHERE fact.InstanceId = @documentId
                AND fact.XBRLCode = @xbrlCode
                AND fact.DataPointSignature = @sig;
             ";
            var factListSimple = connectionInsurance.Query<TemplateSheetFact>(sqlFullSignature, new { documentId, xbrlCode, sig = cellSignature }).ToList();

            //some facts may exist in many tables (we only need one)
            var distinctSimpleList = factListSimple.DistinctBy(fact => fact.DataPointSignature).ToList();

            return distinctSimpleList;
        }

        //Select the facts directl using the signature without any wildcards
        //replace optional dims with % and replace dims with value checking with sc2_dim/w/w:(%)
        var wildSignature = MakeCellSignatureWild(cellSignature);
        var sqlWildSelect = @"            
              SELECT  
                  fact.FactId
                 ,fact.TemplateSheetId
                 ,fact.Row
                 ,fact.Col
                 ,fact.Zet
                 ,fact.CellID
                 ,fact.FieldOrigin
                 ,fact.TableID
                 ,fact.DataPointSignature
                 ,fact.Unit
                 ,fact.Decimals
                 ,fact.NumericValue
                 ,fact.BooleanValue
                 ,fact.DateTimeValue
                 ,fact.TextValue
                 ,fact.DPS
                 ,fact.IsRowKey
                 ,fact.IsShaded
                 ,fact.XBRLCode
                 ,fact.DataType
                 ,fact.DataPointSignatureFilled                 
                 ,fact.InternalRow
                 ,fact.internalCol
                 ,fact.DataTypeUse
                 ,fact.IsEmpty
                 ,fact.IsConversionError
                 ,fact.ZetValues
                 ,fact.OpenRowSignature
                 ,fact.CurrencyDim                 
                 ,fact.contextId                 
                 ,fact.Signature
                 ,fact.RowSignature                 
                 ,fact.InstanceId                  

                FROM dbo.TemplateSheetFact fact
                WHERE fact.InstanceId = @documentId
                AND fact.XBRLCode = @xbrlCode
                AND fact.DataPointSignatureFilled like @sig ESCAPE '#';
             ";
        var wildFacts = connectionInsurance.Query<TemplateSheetFact>(sqlWildSelect, new { documentId, xbrlCode, sig = wildSignature }).ToList();
        foreach (var wildFact in wildFacts)
        {
            //var sqlFact = "select fact.FactId, fact.DataPointSignature from TemplateSheetFact fact where fact.FactId= @factId";
            //var fact = connectionInsurance.QuerySingleOrDefault<TemplateSheetFact>(sqlFact, new { documentId, factId = possibleFact.FactId });
            var isMatch = IsNewSignatureMatch(cellSignature, wildFact?.DataPointSignature ?? "");
            if (isMatch)
            {
                factList.Add(wildFact);
            }
        }

        //some facts may exist in many tables (we only need one)
        var distinctList = factList.DistinctBy(fact => fact.DataPointSignature).ToList();
        return distinctList;


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
