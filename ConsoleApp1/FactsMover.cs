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

    record SheetInfoType(int tableId, int TemplateSheetId, string SheetCode, string SheetCodeZet, string SheetName, string YDims);
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


            List<string> sheetZetCodes = tableFacts
                    .GroupBy(fact => fact.Zet ?? "")
                    .Select(group => group.Key).ToList();

            //*********** Create one  sheet per zet group 
            List<SheetInfoType> sheetInfo = CreateSheetForEachZet(table, sheetZetCodes);


            AssignFactsToSheet(tableFacts, sheetInfo);

            //*********** update rows for Open tables 
            UpdateRowsForOpenTables(sheetInfo, tableFacts);

            //*********** Create Y facts  
            CreateYFactsForOpenTables(sheetInfo);

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
            var sh = sheetInfo.FirstOrDefault(sd => sd.SheetCodeZet == tableFact.Zet);
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
                Console.WriteLine($"+ double FactId:{tableFact.FactId} Row:{tableFact.Row}-{tableFact.Col} ");
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

            var sheetCode = string.IsNullOrEmpty(sheetZetCode)
                    ? table.TableCode
                    : $"{table.TableCode}__{sheetZetCode}";

            //var sheetName = $"{table.TableCode}__{sheetCount:D2}";
            var sheetName = sheetCode;
            table.IsOpenTable = table.YDimVal.Contains('*');
            //************************************************
            //Create a Sheet
            var sheet = _SqlFunctions.CreateTemplateSheet(_documentId, sheetCode, sheetZetCode, sheetName, table);
            Console.WriteLine($"Create SheetCode: {sheetCode} {sheetName}");
            sheetInfo.Add(new SheetInfoType(sheet.TableID, sheet.TemplateSheetId, sheetCode, sheetZetCode, sheetName, sheet.YDimVal));

            sheetCount++;
        }

        return sheetInfo;
    }

    private int CreateYFactsForOpenTables(List<SheetInfoType> sheetsInfo)
    {
        //create facts for each y dim of a table (for each row)
        //for each row, use the first non-null fact as a clone
        //each ydim fact will get its value from the corresponding dim of the clone fact.
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        foreach (var sheetInfo in sheetsInfo)
        {
            List<string> tableYDims = sheetInfo.YDims?
            .Split("|", StringSplitOptions.RemoveEmptyEntries)
            .ToList() ?? new List<string>();

            foreach (var ydim in tableYDims)
            {
                var x = SelectMapping(sheetInfo.tableId, ydim);
            }

            var yTableMappings1 = tableYDims
                        .Select(ydim => SelectMapping(sheetInfo.tableId, ydim));




            var yTableMappings = tableYDims
                        .Select(ydim => SelectMapping(sheetInfo.tableId, ydim))
                        .Where(mappping => mappping != null) ?? new List<MAPPING>();


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
                CreateYFactsForRow(sheetInfo, rowInt, yTableMappings);
            }
        }
        return 1;
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
            var factDimsAll = _SqlFunctions.SelectFactDims(rowFact.FactId);
            var factDim = factDimsAll.FirstOrDefault(fd => fd.Dim == DimDom.GetParts(yMapping.DIM_CODE).Dim);
            if(factDim is null)
            {
                return;
            }
            var newFact = rowFact.Adapt<TemplateSheetFact>();
            newFact.Col = yMapping.DYN_TAB_COLUMN_NAME;
            //DimDom.GetParts(factDim?.DomValue??"").DomValue;
            newFact.TextValue = factDim.DomValue;
            var x= _SqlFunctions.CreateTemplateSheetFact(newFact);


        }

        //fore each dim get the Col from 
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

    private TemplateSheetFact? SelectRowFact(int templateSheetId, string row)
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
        var res = tableFacts ?? new List<TemplateSheetFact>();
        return res;
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


    ///************************

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



}
