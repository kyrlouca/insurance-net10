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
                   
        _testingTableId = 59;
        if (_testingTableId > 0)
        {
            ModuleTablesFiled = ModuleTablesFiled.Where(table => table.TableID == _testingTableId).ToList();
        }
        var count = 0;

        //************************************************************************
        foreach (var table in ModuleTablesFiled.OrderBy(tab => tab.TableID))
        {
        
            Console.WriteLine($"\nTable Processed : {table.TableCode}");
            var tableFacts = SelectTableFacts(table);
            var sheetNames = tableFacts.GroupBy(fact => fact.Zet);
            //UpdateSheetTabNames(table.TableCode); //make the tabnames simler to read

            Console.WriteLine($"\n---facts:{tableFacts.Count}");
        }

        //Create the Y facts fore open tables
        //var sqlSelectSheets = @"select sheet.TemplateSheetId from TemplateSheetInstance sheet where sheet.InstanceId = @documentId";
        //var sheets = connectionInsurance.Query<TemplateSheetInstance>(sqlSelectSheets, new { _documentId })?.ToList() ?? new();
        //foreach (var sheet in sheets)
        //{
        //    var yFactsCounter = CreateYFactsInDb(sheet.TemplateSheetId);
        //    Console.WriteLine($"\n---Yfacts:{yFactsCounter}");
        //}


        
        

        //****Update the foreign Keys of the cells in open tables
        //UpdateCellsForeignRow(_documentId);


        //Console.WriteLine($"\ndocId: {_documentId} -- sheets: facts:{countFacts}");
        return 0;

    }
    
    private int AssignFactsToTablesNew()
    {
        //iterate each table. For each table, read its cells, find the matching facts, and assign them rowcols            
        var count = 0;        
        _testingTableId = 1;
        if (_testingTableId > 0)
        {
            ModuleTablesFiled = ModuleTablesFiled.Where(table => table.TableID == 66).ToList();
        }
        
        foreach (var table in ModuleTablesFiled.OrderBy(tab => tab.TableID))
        {
            //************************************************************************
            Console.WriteLine($"\nTable start : {table.TableCode}");            
            var tableFacts = SelectTableFacts(table);            

            //UpdateSheetTabNames(table.TableCode); //make the tabnames simler to read

            
            Console.WriteLine($"\n---facts:{tableFacts.Count}");
        }

        //Create the Y facts fore open tables
        //var sqlSelectSheets = @"select sheet.TemplateSheetId from TemplateSheetInstance sheet where sheet.InstanceId = @documentId";
        //var sheets = connectionInsurance.Query<TemplateSheetInstance>(sqlSelectSheets, new { _documentId })?.ToList() ?? new();
        //foreach (var sheet in sheets)
        //{
        //    var yFactsCounter = CreateYFactsInDb(sheet.TemplateSheetId);
        //    Console.WriteLine($"\n---Yfacts:{yFactsCounter}");
        //}

        return count;
    }

    private List<TemplateSheetFact> SelectTableFacts(MTable table)
    {
        //use the xbrl mappings of the table to find the matching facts
        List<TemplateSheetFact> tableFacts = new();
        var tableMappings = _SqlFunctions.SelectTableMappings(table.TableID,MappingOrigin.All);
        var xbrlMappings = tableMappings.Where(map => map.ORIGIN == "F" && map.DIM_CODE.StartsWith("MET"));
        var pageZet = tableMappings.FirstOrDefault(map => map.ORIGIN == "C" && map.DYN_TAB_COLUMN_NAME.StartsWith("PAGE"))?.DIM_CODE??"";

        foreach (var xbrlMapping in xbrlMappings)
        {
            Console.WriteLine(xbrlMapping.DYN_TAB_COLUMN_NAME);

            //find zet and y dims
            var zetDims = table.ZDimVal ?? "";
            var yDims = table.YDimVal ?? "";
            var mathcingFacts = FindMatchingFacts(xbrlMapping,zetDims,pageZet );
            tableFacts.AddRange(mathcingFacts);
            var y = 3;
        }
        return tableFacts ;
    }
    private List<TemplateSheetFact> FindMatchingFacts(MAPPING xbrlMapping, string zetString,string pageZet)
    {
        //find the facts which have the xbrl code and the  right mappings of the table (the xblr and the tableID are found in the xbrl mapping)
        //the right mappings are all the origin='F' plus the Zet mappings of the table

        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var tableId = (int)xbrlMapping.TABLE_VERSION_ID;
        var rowCol = xbrlMapping.DYN_TAB_COLUMN_NAME;
        //tableId = 59;
        //rowCol = "R0020C0080";
        //var allDimsStr = "'s2c_dim:BL(s2c_LB:x59)', 's2c_dim:DI(s2c_DI:x5)', 's2c_dim:IZ(s2c_RT:x1)', 's2c_dim:TB(s2c_LB:x28)', 's2c_dim:VG(s2c_AM:x84)'";

        

        var zetClosedDimCodes =  zetString
            .Split("|",StringSplitOptions.RemoveEmptyEntries)
            .Where(dim => !dim.Contains('*'))
            .Select(dim => DimDom.GetParts(dim).Signature).ToList();


        //need to use this to get the fact. but it it can also be a page zet
        var zetOpenDimCodes = zetString
            .Split("|", StringSplitOptions.RemoveEmptyEntries)
            .Where(dim => dim.Contains('*'))
            .Select(dim => DimDom.GetParts(dim).Signature).ToList();

        var fieldMappings = _SqlFunctions.SelectRowColMappings(tableId, rowCol)
            .Where(map => !map.DIM_CODE.StartsWith("MET"));


        var fieldClosedDimCodes = fieldMappings.Where(map => !map.DIM_CODE.Contains("*")).Select(map=>map.DIM_CODE).ToList();
        var fieldOpenDimCodes = fieldMappings.Where(map => map.DIM_CODE.Contains("*")).Select(map=>map.DIM_CODE).ToList();




        var openDimCodes = new List<string>()
            .Concat(zetOpenDimCodes)
            .Concat(fieldOpenDimCodes)
            .Select(dim => $"'{dim}'");

        var closedDimCodes = new List<string>()
            .Concat(zetClosedDimCodes)
            .Concat(fieldClosedDimCodes)
            .Select(dim => $"'{dim}'");

        var dimsStringSQL = string.Join(",",allDims );
        var rowcolRec = NewUtils.CreateRowColRecord(xbrlMapping.DYN_TAB_COLUMN_NAME);
        var andRowSQL = rowcolRec.HasOnlyCol ? "" : "AND fact.Row=@ROW  ";


        var sqlSelectWithoutDims = @$"
            SELECT *
            FROM TemplateSheetFact fact
                 JOIN DocInstance doc ON doc.InstanceId= fact.InstanceId
            WHERE
              1=1
              AND fact.XBRLCode = @XBRLCode
              {andRowSQL}
              AND fact.Col=@COL
              AND fact.InstanceId=@_documentId              
            ";

        var sqlSelectWithDims = @$"
            SELECT fact.*
            FROM TemplateSheetFact fact
                 JOIN DocInstance doc ON doc.InstanceId= fact.InstanceId
            WHERE
              1=1
              AND fact.XBRLCode = @XBRLCode
              {andRowSQL}
              AND fact.Col=@COL
              AND fact.InstanceId=@_documentId
              AND EXISTS (
                SELECT COUNT(*) AS cnt
                  FROM TemplateSheetFactDim fd
                WHERE 1=1
                AND fd.FactId=fact.FactId
                AND fd.IsExplicit=1
                AND fd.Signature IN ({dimsStringSQL})
                GROUP BY fd.FactId
                HAVING COUNT(*)=@dimsCount
                ); 
           ";

        
        
        var dimsCount = allDims.Count();
        var sqlSelect = dimsCount == 0 ? sqlSelectWithoutDims : sqlSelectWithDims;
        
        var xbrlCode = RegexUtils.GetRegexSingleMatch(new Regex("MET\\((.*)\\)"), xbrlMapping.DIM_CODE);//xbrlCode = "s2md_met:mi503";
        

        Console.Write($"Find Fact:xbrl{xbrlCode},row:{rowcolRec.Row},col{rowcolRec.Col}");
        var facts = connectionInsurance.Query<TemplateSheetFact>(sqlSelect, new { _documentId, xbrlCode, row = rowcolRec.Row, col = rowcolRec.Col, dimsCount })?.ToList();
        if (facts.Count > 0)
        {
            var xx = 3;
        }
        return facts ?? new List<TemplateSheetFact>();

        
    }

    private string toProperXbrl(string xbrlWithParenthesis)
    {
        return xbrlWithParenthesis.Replace("(", "").Replace("(", "");
    }

    private int AssignFactsToTableDb(MTable table)
    {
        //Iterate through all the cells of the table and find the facts for each cell           
        // For each cell of the table
        // -- find the corresponding  xbuFacts  (one cell may correspond to many facts -- one for each  Z or even for fact zet such as currency)
        // -- find the corresponding MAPPING which has the rowcol
        // -- create the TemplateSheetFacts in the DB            
        // create one sheet for each new Zet found in the table's facts.


        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        DeleteMappingSignature(_documentId);

        var factCounter = 0;

        var isOpenTable = CreateMappingsSignaturesDb(table);
        table.IsOpenTable = isOpenTable;  //i dont' like this but I didn't like to add an attribute on an eiopa table
        var pivotZet = GetTablePivotZet(table);  //the dim for multifact cells                                            


        var sqlCells = @"SELECT
                      cell.CellID
                     ,cell.TableID
                     ,cell.IsRowKey
                     ,cell.IsShaded
                     ,cell.BusinessCode
                     ,cell.DatapointSignature
                     ,cell.DPS
                     ,cell.NoOpenDPS
                     ,IsShaded
                    FROM dbo.mTableCell cell
                    WHERE cell.TableID = @TableId
                    and cell.isShaded=0
                    ";
        var cells = connectionEiopa.Query<MTableCell>(sqlCells, new { table.TableID });
        Console.Write($"cells:{cells.Count()}-");

        foreach (var cell in cells)
        {
            if (string.IsNullOrEmpty(cell.DatapointSignature))
            {
                continue;
            }

            //****************************************************************************************
            //find the mapping which provides the row/col  corresponding to the cell. Fact is not involved                
            //****************************************************************************************
            //Console.Write($"m");
            var cellMappingNew = FindMappingRowColForTheCell(table, cell);


            if (string.IsNullOrWhiteSpace(cellMappingNew.DYN_TAB_COLUMN_NAME))
            {
                var message = $"Cannot find mappings for Cell: {cell.CellID}";
                Log.Error(message);

                continue;
            }


            //*****************************************************************
            //** One Or More Facts for each cell- because of sheet zet or fact zet such as currency, country, etc
            //** find the facts matching the the Cell's signature. 
            //** the fact signature has REAL values (From its context) and the cells may have wildcard values                        
            //*****************************************************************
            Console.Write($"!");

            //var factsList = FindMatchingFactsRegex(ConfigObject, DocumentId, cell.DatapointSignature);

            var factListNew = FindFactsFromSignatureWild(_documentId, cell.DatapointSignature);

            Console.Write($"$");

            if (factListNew.Count == 0)
            {
                continue;
            }

            foreach (var foundFact in factListNew)
            {
                var fact = foundFact;
                //update the fact open Zet dims (rowcol,currency, country,etc)
                Console.Write($".");
                if (fact.TemplateSheetId > 0)
                {
                    //the fact belongs ALSO in another sheet. Create a new ONE  //@ here is my problem: if we had two previous facts with the same signature we will create two more. but i need to check the zet
                    //new Fact has its own factId, SheetId, AND tableId
                    fact.TableID = table.TableID;
                    fact.CellID = cell.CellID;
                    if (table.IsOpenTable)
                    {
                        fact.Row = "";
                        fact.InternalRow = 0;
                    }
                    fact.FieldOrigin = "KYR";
                    //do NOT use fields  templateSheetId and factId, and tableId  is new 
                    var sqlInsertAnotherFact = @"
                            INSERT INTO dbo.TemplateSheetFact (  Row, Col, Zet, CellID, FieldOrigin, TableID, DataPointSignature, Unit, Decimals, NumericValue, BooleanValue, DateTimeValue, TextValue, DPS, IsRowKey, IsShaded, XBRLCode, DataType, DataPointSignatureFilled,  InternalRow, internalCol, DataTypeUse, IsEmpty, IsConversionError, ZetValues, OpenRowSignature, CurrencyDim,  metricId, contextId,  RowSignature,  InstanceId)
                            VALUES (  @Row, @Col, @Zet, @CellID, @FieldOrigin, @TableID, @DataPointSignature, @Unit, @Decimals, @NumericValue, @BooleanValue, @DateTimeValue, @TextValue, @DPS, @IsRowKey, @IsShaded, @XBRLCode, @DataType, @DataPointSignatureFilled,  @InternalRow, @internalCol, @DataTypeUse, @IsEmpty, @IsConversionError, @ZetValues, @OpenRowSignature, @CurrencyDim,  @metricId,  @contextId,  @RowSignature, @InstanceId);
                            SELECT CAST(SCOPE_IDENTITY() as int);
                        ";

                    var factId = connectionInsurance.QueryFirst<int>(sqlInsertAnotherFact, fact);
                    fact.FactId = factId;

                    CreateFactDimsDb(fact.FactId, fact.DataPointSignature);


                }
                var sheetFact = UpdateFactWithCellValuesInDb(table, cell, cellMappingNew, fact, pivotZet);
                factCounter += 1;
            }

        }
        return factCounter;
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


    private bool CreateMappingsSignaturesDb(MTable table)
    {

        //create a list with unique rowCol (dyn_tab_column_name) for the table
        //for each rowCol concatenate its dims
        //---- use the function STRING_AGG which concatenates values from different rows -- makes life much easier
        // Take the Y dims from the table ydimsVal
        // However, we cannot take the zet from the table zdimsVal since some are missing
        // we get the Z from the mappings
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);


        var yMappings = GetTableYDims(table);
        if (yMappings.Count > 0)
        {
        }


        var sqlFieldMappings = @"
                SELECT 
                    DYN_TAB_COLUMN_NAME, 
                    STRING_AGG(cast(DIM_CODE as nvarchar(1000)), '|') as Dims
                FROM MAPPING map
                where map.TABLE_VERSION_ID=@tableId
                and map.DYN_TAB_COLUMN_NAME not like 'PAGE%'
                GROUP BY 
                    DYN_TAB_COLUMN_NAME
                ";
        var fieldMappings = connectionEiopa.Query<(string rowCol, string dims)>(sqlFieldMappings, new { tableId = table.TableID })?.ToList() ?? new List<(string rowCol, string dims)>();
        var isOpenTable = fieldMappings?.Any(item => item.rowCol.StartsWith("C")) ?? false;

        //dims outside the table as Zet 
        var sqlOutOfTabl = @"
                        select map.DIM_CODE from MAPPING map where 
                        map.ORIGIN='C'
                        and map.IS_IN_TABLE=0
                        and map.TABLE_VERSION_ID =@tableId
                        ";
        var zetTableDims = connectionEiopa.Query<string>(sqlOutOfTabl, new { table.TableID })?.ToList() ?? new List<string>();

        //column dims inside the table
        var sqlAllInsideTable = @"
                select map.DIM_CODE from MAPPING map where 
                map.ORIGIN='C'
                and map.IS_IN_TABLE=1
                and map.IS_PAGE_COLUMN_KEY=1
                and map.TABLE_VERSION_ID = @tableId
                ";
        var allInsideTableDims = connectionEiopa.Query<string>(sqlAllInsideTable, new { table.TableID })?.ToList() ?? new List<string>();


        foreach (var (rowCol, dims) in fieldMappings)
        {

            var fieldDims = dims.Split("|", StringSplitOptions.RemoveEmptyEntries)?.ToList() ?? new List<string>();
            ///////////////////////

            var fullMappings = new List<string>();
            fullMappings.AddRange(fieldDims);
            fullMappings.AddRange(zetTableDims);
            fullMappings.AddRange(allInsideTableDims);
            if (isOpenTable)
            {
                fullMappings.AddRange(yMappings);
            }
            fullMappings.Sort();
            var fulldims = string.Join("|", fullMappings);


            var sqlInsertMappingSig = @"
                        INSERT INTO dbo.MappingSignatures (InstanceId, tableId, Signature, RowCol ,isOpenTable) VALUES (@InstanceId, @tableId, @Signature, @RowCol, @isOpenTable)
                        SELECT CAST(SCOPE_IDENTITY() as int);                            
                    ";

            var mappingSignature = new MappingSignatures(_documentId, table.TableID, fulldims, rowCol, isOpenTable);
            var mapp = connectionInsurance.QueryFirstOrDefault<int>(sqlInsertMappingSig, mappingSignature);
        }

        return isOpenTable;
    }

    TemplateSheetFact UpdateFactWithCellValuesInDb(MTable table, MTableCell cell, MAPPING cellMapping, TemplateSheetFact realFact, string tablePivotZet)
    {
        //update the fact with row,col, cellId and assign it to a sheet (or create a new one if does not exist)
        //we create a SHEET when the fact cannot find another fact with similar Zet values
        //fact zet values are the zet dims present in the fact signature
        //they tell us when the same facts belong in different sheets either bucause of
        //-- different open values which means multiple sheets of the same tableI
        //-- or different closed valued (completely different table)

        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        realFact.CellID = cell.CellID;
        realFact.TableID = table.TableID;

        var factRowCol = cellMapping.DYN_TAB_COLUMN_NAME;
        realFact.Col = RegexUtils.GetRegexSingleMatch(@"(C\d*)", factRowCol);
        realFact.Row = RegexUtils.GetRegexSingleMatch(@"(R\d*)", factRowCol);
        realFact.IsRowKey = (cellMapping.IS_PAGE_COLUMN_KEY == 1);

        //the fact.RowSignature was already builted when reading the element from xbrl 
        //but   OpenRowSignature cannot be builted when reading the xbrl file because  we need the table and the cell             
        realFact.OpenRowSignature = GetWildDims(realFact.DataPointSignatureFilled, table.YDimVal);

        //factZetvalues are all the z dimensions of the fact, which are also part of the sheetcode name            
        var factZetValuesList = ConstructFactFullZetList(realFact.DataPointSignatureFilled, table.ZDimVal);
        var factZetValuesStr = string.Join("__", factZetValuesList);
        realFact.ZetValues = factZetValuesStr;

        //fact.zet is the zet dimension of the fact, in case a cell corresponds to more than one fact in the same sheet (currency, etc)
        var factPivotZet = GetFactPivotZet(realFact.DataPointSignatureFilled, tablePivotZet);
        var pivotZetDimDom = DimDom.GetParts(factPivotZet);
        //realFact.Zet = string.IsNullOrEmpty(factPivotZet) ? "" : $"{pivotZetDimDom.Dim}#{pivotZetDimDom.DomAndValFull }";
        realFact.Zet = factPivotZet ?? "";
        realFact.CurrencyDim = pivotZetDimDom.Dim == "OC" ? pivotZetDimDom.Dom : DefaultCurrency;

        //assing the fact to a new or an existing sheet based on its zetValues
        var sheet = GetOrCreateSheet(table, realFact);
        realFact.TemplateSheetId = sheet.TemplateSheetId;

        var sqlUpdFact = @"
                UPDATE dbo.TemplateSheetFact
                SET 
                    TemplateSheetId = @TemplateSheetId
                    ,Row = @Row
                    ,InternalRow=@InternalRow
                    ,Col = @Col
                    ,Zet = @Zet
                    ,CellID = @CellID
                    ,TableID = @TableID
                    ,IsRowKey = @IsRowKey                    
                    ,ZetValues=@ZetValues
                    ,CurrencyDim = @CurrencyDim                    
                    ,OpenRowSignature = @OpenRowSignature                   
                WHERE FactId = @factId

            ";

        if (string.IsNullOrEmpty(realFact.Row))
        {

            //Only open tables have a column but NO row. 
            //Assign a row number if this is the first fact with that rowSignature (does not include xbrlcode) 

            var openRowNumber = GetOrUpdateOpenRowNumber(sheet, realFact);
            realFact.InternalRow = openRowNumber;
            realFact.Row = $"R{openRowNumber:D4}";
        }

        connectionInsurance.Execute(sqlUpdFact, realFact);
        return realFact;
    }

    private static string GetFactPivotZet(string factSignature, string zetDim)
    {
        if (string.IsNullOrEmpty(zetDim))
        {
            return "";
        }
        var tableZetDim = DimDom.GetParts(zetDim).Dim;
        var factDims = factSignature.Split("|")?.Where(dim => DimDom.GetParts(dim).Dim.Contains(tableZetDim))?.ToList();
        if (factDims.Count == 0)
        {
            return "";
        }
        return factDims.First();

    }

    private TemplateSheetInstance GetOrCreateSheet(MTable table, TemplateSheetFact fact)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var sheetCode = string.IsNullOrEmpty(fact.ZetValues)
                ? table.TableCode
                : $"{table.TableCode}__{fact.ZetValues}";

        //find an existing  sheet based on sheetCode = tableCode and ZetValues 
        var sqlSelSheet = @"select TemplateSheetId, SheetCode,TableCode, OpenRowCounter from TemplateSheetInstance sheet where sheet.InstanceId=@documentId and  SheetCode =@sheetCode";
        var sheet = connectionInsurance.QuerySingleOrDefault<TemplateSheetInstance>(sqlSelSheet, new { _documentId, sheetCode });

        if (sheet is null)
        {
            sheet = CreateSheet(table, sheetCode);
        }
        //var sheetId = sheet.TemplateSheetId;
        return sheet;
    }

    private int GetOrUpdateOpenRowNumber(TemplateSheetInstance sheet, TemplateSheetFact fact)
    {
        //update the rowNumber for open rows for the sheet.
        // --Use the rowNumber of another fact which has a similar openRowSignature
        // --Or use the sheet.openRowCounter to get a new row number
        //add the tableCode in front of the openRowSignature to have different rowSignatures for each Z sheet
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sheetId = sheet.TemplateSheetId;
        var sqlSameRowFact = @" SELECT fact.FactId, fact.Row, fact.InternalRow from TemplateSheetFact fact where fact.TemplateSheetId = @sheetId and fact.OpenRowSignature=@OpenRowSignature ";
        var sameRowFact = connectionInsurance.QueryFirstOrDefault<TemplateSheetFact>(sqlSameRowFact, new { sheetId, fact.OpenRowSignature });

        var openRowNumber = sameRowFact?.InternalRow ?? 0;
        if (sameRowFact is null)
        {
            openRowNumber = sheet.OpenRowCounter + 1; ;
            var sqlUpdateCounter = @"update TemplateSheetInstance set OpenRowCounter = @openRowNumber where TemplateSheetId = @sheetId";
            connectionInsurance.Execute(sqlUpdateCounter, new { sheetId, openRowNumber });
        }
        return openRowNumber;
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



    private MAPPING FindMappingRowColForTheCell(MTable table, MTableCell cell)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        // given the Cell,  Find the rowcol using the MAPPINGS table (plus the y,z on the mTable)
        // A list of MAPPINGS with the same row/col will correspond to the the noOpenDPS siganture of the cell            
        // -- return only the first mapping (it is enough to give you the row/col)

        //var isOpenTable = table.IsOpenTable;
        //var cellSignatureFull = isOpenTable ? cell.DPS : cell.NoOpenDPS;


        var cellSignatureFull = cell.DPS;
        var cellSignature = Regex.Replace(cellSignatureFull, @"\(\*(.*?)\)", "(*)");  ////s2c_dim:LG(*[GA_18;x0;0])=>s2c_dim:LG(*)


        var selectRowSql = @"
                SELECT
                  ms.Signature
                 ,ms.RowCol
                FROM dbo.MappingSignatures ms
                WHERE ms.InstanceId = @DocumentId
                AND ms.TableId = @TableId
                AND ms.Signature = @Signature
                ";
        var mappingSig = connectionInsurance.QuerySingleOrDefault<MappingSignatures>(selectRowSql, new { _documentId, table.TableID, signature = cellSignature });

        if (mappingSig is null)
        {
            return new MAPPING();
        }

        var sqlMapping = @"
                SELECT map.DYN_TAB_COLUMN_NAME, map.DIM_CODE, map.DOM_CODE, map.ORIGIN, map.DATA_TYPE, map.IS_PAGE_COLUMN_KEY, map.IS_IN_TABLE
                FROM MAPPING map
                WHERE 
                    map.TABLE_VERSION_ID =@tableId	                        
                    AND DYN_TAB_COLUMN_NAME = @rowCol;    
                ";
        var mapping = connectionEiopa.QueryFirstOrDefault<MAPPING>(sqlMapping, new { tableId = cell.TableID, rowCol = mappingSig.RowCol }) ?? new MAPPING();

        return mapping;
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


    private static string CleanCellSignatureWithoutOptionalxx(string cellSignature)
    {
        //var test= @"MET(s2md_met:mi87)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(s2c_LB:x9)|s2c_dim:DI(s2c_DI:x5)|s2c_dim:OC(*?[237])|s2c_dim:RB(*[332;1512;0])|s2c_dim:RM(s2c_TI:x44)|s2c_dim:TB(s2c_LB:x28)|s2c_dim:VG(s2c_AM:x80)";
        //var test2 = @"MET(s2md_met:mi1104)|s2c_dim:BL(*[334;1512;0])|s2c_dim:CC(s2c_TB:x12)|s2c_dim:FC(*)|s2c_dim:RD(*)|s2c_dim:RE(*)";

        //cellSignature = test2;
        var dimList = cellSignature.Split("|")
        .Where(dim => !dim.Contains('?'))
        .Select(dim => Regex.Replace(dim, @"\[.*\]", ""))
        .Select(dim => dim.Replace("*", "%")).ToList();
        var cleanSig = string.Join("|", dimList);

        var xbrlMetric = dimList.First();
        //var xbrlCode = GeneralUtils.GetRegexSingleMatch(@"MET\((.*?)\)", xbrlMetric);
        return cleanSig;
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



    private bool IsFactDimMatchingCellExpensive(string cellDim, string factDim)
    {
        //            
        //*  "*" allows for any value but brackets constrain the values to the hierechy members
        ////MET(s2md_met:mi686)|s2c_dim:AO(*?[16])|s2c_dim:EA(s2c_VM:x23)|s2c_dim:RT(s2c_RT:x97)|s2c_dim:VG(s2c_AM:x80)
        //MET(s2md_met:mi1157)|s2c_dim:BL(*[334;1512;0])|s2c_dim:CC(s2c_TB:x12)|s2c_dim:FC(*)|s2c_dim:RD(*)|s2c_dim:RE(*)
        //MET(s2md_met:mi289)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[332;1512;0])|s2c_dim:DY(s2c_TI:x1)|s2c_dim:OC(*?[237])|s2c_dim:RM(s2c_TI:x49)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)

        var isExact = !cellDim.Contains('*');
        if (isExact)
        {
            return factDim == cellDim;

        }
        else
        {
            var factDimDom = DimDom.GetParts(factDim);
            var cellDimDom = DimDom.GetParts(cellDim);

            if (factDimDom.Dim != cellDimDom.Dim)
            {
                return false;
            }

            //***  Dim is the same , so check if  open
            if (cellDimDom.DomAndValRaw == "*")
            {
                return true;
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


    }

    static string GetWildDims(string factSignature, string zDimVal)
    {
        //find the dims of a fact which correspond to the wild zet dims of the table
        //the factOpenZetdims  will be used to group facts with the same tablecode but in different sheets
        //multiple sheets with the same table code

        //table 124
        //table ZDimVal : MET(s2md_met:mi289)|s2c_dim:AF(*?[59])|s2c_dim:AX(*[8;1;0])|s2c_dim:BL(*[332;1512;0])|s2c_dim:OC(*?[237])|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)
        //DataPointSignatureFilled:  MET(s2md_met:mi289)|s2c_dim:AX(s2c_AM:x4)|s2c_dim:BL(s2c_LB:x34)|s2c_dim:DY(s2c_TI:x1)|s2c_dim:RM(s2c_TI:x49)|s2c_dim:TA(s2c_AM:x57)|s2c_dim:VG(s2c_AM:x80)
        // result : "s2c_dim:AX(s2c_AM:x4)|s2c_dim:BL(s2c_LB:x34)"

        var factAllDims = factSignature?.Split("|")?.ToList() ?? new List<string>();
        //--only use the table wild zet dims. Non-wild zet dims will be the same for all facts in the same table 
        //select only the Dim part (AX, BL, DY, ...)
        var tableZetOpenDims = zDimVal
        ?.Split("|")
        ?.Where(dim => dim.Contains("*"))
        ?.Select(dim => DimDom.GetParts(dim).Dim)?.ToList() ?? new List<string>();

        var factZetDims = factAllDims?.Where(dim => tableZetOpenDims.Exists(tblDim => dim.Contains(tblDim))).ToList() ?? new List<string>();

        var zetStr = string.Join("|", factZetDims);

        return zetStr;

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


    private string GetTablePivotZet(MTable table)
    {
        //find the in table zet dim which is not in the table zet dims but exists in the mappings as Is_in_table=1
        //for example in table 40, we have multiple facts in the same cell
        //the differenciating dim is not present in table zet dims. (which means is in the same sheet)
        //Assume we can only have one differenciating dim
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlInsideDims = @"
                select map.DIM_CODE from MAPPING map where 
                map.ORIGIN='C'
                and map.IS_IN_TABLE=1
                and map.IS_PAGE_COLUMN_KEY=1
                and map.TABLE_VERSION_ID = @tableId
                ";
        var inTableDims = connectionEiopa.Query<string>(sqlInsideDims, new { table.TableID })?.ToList();
        if (inTableDims.Count == 0)
        {
            return "";
        }

        var zdims = table.ZDimVal?.Split("|")
            ?.Select(dim => DimDom.GetParts(dim).Dim)
            ?.ToList() ?? new List<string>();

        var filterDims = inTableDims
            ?.Where(inTabDim => !zdims.Contains(DimDom.GetParts(inTabDim).Dim))
            ?.ToList() ?? new List<string>();
        if (inTableDims.Count != filterDims.Count)
        {
            var message = $"filterDimd different table:{table.TableCode}";
            //Console.WriteLine(message);
            //Log.Error(message);
        }

        if (filterDims.Count > 0)
        {
            return filterDims.First();
        }
        return "";

    }

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


    private void UpdateSheetTabNames(string tableCode)
    {
        //excel tab sheet names cannot exceed 30 chars
        //sheetCodes exceet the limit because we add zetDims to the tablecode (create one sheet for each OPEN zet dim)
        //CAN we have more than ONE open ZET ?? I think no
        //therefore, build sheetTabName which is the first 25 chars of the sheetcode + serial (resets for every tableCode)
        //if there is no open Z do not add the z in name

        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

        var count = 0;
        var sqlSelSheets = @"select TemplateSheetId, SheetCode, TableCode from  TemplateSheetInstance where InstanceId= @documentId and TableCode = @tableCode";
        var sheets = connectionInsurance.Query<TemplateSheetInstance>(sqlSelSheets, new { _documentId, tableCode }) ?? new List<TemplateSheetInstance>(); ;
        foreach (var sheet in sheets)
        {

            var sqlTableCode = @"select ZDimVal from mTable where mTable.TableCode = @tableCode";
            var table = connectionEiopa.QueryFirstOrDefault<MTable>(sqlTableCode, new { tableCode });

            var isOpenZet = table.ZDimVal?.Contains("*") ?? false;

            var sheetTabName = isOpenZet
                ? $"{tableCode.Trim()}___{count++:D2} "
                : tableCode.Trim(); //if no open z then just use the tablecode as the sheettab name

            var sqlUpdSheet = @"update TemplateSheetInstance set SheetTabName= @SheetTabName where TemplateSheetId = @TemplateSheetId";
            connectionInsurance.Execute(sqlUpdSheet, new { SheetTabName = sheetTabName, sheet.TemplateSheetId });
        }


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


    private void DeleteMappingSignature(int instanceId)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlDeleteSig = @"delete  FROM MappingSignatures  WHERE InstanceId = @InstanceId";
        connectionInsurance.Execute(sqlDeleteSig, new { instanceId });

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


    int CreateFactDimsDb(int factId, string signature)
    {

        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var dims = signature.Split("|").ToList();
        if (dims.Count > 0)
        {
            dims.RemoveAt(0);
        }

        var count = 0;
        foreach (var dim in dims)
        {
            count++;
            var dimDom = DimDom.GetParts(dim);
            var factDim = new TemplateSheetFactDim()
            {
                FactId = factId,
                Dim = dimDom.Dim,
                Dom = dimDom.Dom,
                DomValue = dimDom.DomValue,
                Signature = dimDom.Signature,
                IsExplicit = true
            };
            var sqlInsDim = @"
                    INSERT INTO dbo.TemplateSheetFactDim (FactId, Dim, Dom, DomValue, Signature, IsExplicit)
                    VALUES(@FactId, @Dim, @Dom, @DomValue, @Signature, @IsExplicit)";

            connectionInsurance.Execute(sqlInsDim, factDim);
        }

        return count;
    }


}
