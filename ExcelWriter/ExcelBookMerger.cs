namespace ExcelWriter;
using Dapper;
using Shared.ExcelHelperRoutines;
using ExcelWriter.ExcelDataModels;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.DataModels;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;
using Syncfusion.Compression;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.FormatParser.FormatTokens;
using Syncfusion.XlsIO.Implementation;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shared.SpecialRoutines;

public class ExcelBookMerger : IExcelBookMerger
{

    private readonly IParameterHandler _parameterHandler;
    ParameterData _parameterData = new();
    private readonly ILogger _logger;

    private readonly ISqlFunctions _SqlFunctions;
    private readonly ICustomPensionStyler _customPensionStyles;
    PensionStyles _pensionStyles;
    private IWorkbook? SourceWorkbook;
    private IWorkbook? DestWorkbook;
    int _documentId = 0;
    DocInstance _documentInstance;
    const int SPACE_BETWEEN_TABLES_HORIZONTAL = 0;
    const int SPACE_BETWEEN_TABLES_VERTICAL = 3;

    //private IStyle? tableCodeStyle;
    //private IStyle? bodyStyle;
    //private IStyle? headerStyle;
    //private IStyle? dataSectionStyle;


    public ExcelBookMerger(IParameterHandler parametersHandler, ILogger logger, ISqlFunctions sqlFunctions, ICustomPensionStyler customPensionStyles)
    {
        _parameterHandler = parametersHandler;
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _customPensionStyles = customPensionStyles;
    }
    public bool MergeTables(int documentId, string sourceFile, string destFile)
    {
        _documentId = documentId;
        _documentInstance = _SqlFunctions.SelectDocInstance(_documentId)!;
        if (_documentInstance is null)
        {
            var eMessage = "Document not fuound";
            _logger.Error(eMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, eMessage);
            return false;
        }


        _parameterData = _parameterHandler.GetParameterData();


        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdgWH5fc3RdRWFfU0B0W0o=");

        using var excelEngine = new ExcelEngine();
        IApplication application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        (SourceWorkbook, var originMessage) = HelperRoutines.OpenExistingExcelWorkbook(excelEngine, sourceFile);
        if (SourceWorkbook is null)
        {
            _logger.Error(originMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, originMessage);
            return false;
        }


        (DestWorkbook, var destMessage) = HelperRoutines.CreateExcelWorkbook(excelEngine);
        if (DestWorkbook is null)
        {
            _logger.Error(destMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, destMessage);
            return false;
        }

        _pensionStyles = _customPensionStyles.GetStyles(DestWorkbook);

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //Merge sheets for each Table group ( template Code with 3 parts) based on Zet.
        //Each TableGroup groups the sheets with the same zetCode
        //"S.01.01.02=>"S.01.01.02.01","S.01.01.02.02","S.01.01.02.03"        
        //A TableGroup contains the template code and a list of horizontal tableCodes lists like {S.19.01.01, {S.19.01.01.01,19.01.01.02,etc},{19.01.01.08}}
        //

        var indexList = new IndexSheetList("List", new List<IndexSheetListItem>());

        var tableGroupsList = CreateTableGroupsForModule(_documentInstance.ModuleCode, _documentInstance.ModuleId);

        tableGroupsList = tableGroupsList
            .Where(tg => !SpecialTemplateList.ExcludeTemplateGroups().Contains(tg.TemplateCode)).ToList();

        var specialGroups = SpecialTemplateList.SinglePageTableGroupsId()
            .Select(code => SpecialTemplateList.FindSpecialTemplateLayoutByCode(code))
            .Where(sp => sp is not null)
            .Select(sp => new TableGroup(sp!.TemplateCode, "", new List<string>()))
            .ToList();
         
        tableGroupsList.AddRange(specialGroups);




        ///////////////////////
        var s6Zet = "";
        //tableGroupsList = tableGroupsList.Where(gl => gl.TemplateCode == "S.19.01.01").ToList();
        foreach (var tableGroup in tableGroupsList)
        {

            var distinctBlZets = tableGroup.TableCodes
            .SelectMany(tc => SelectSheetCodeZets(tc))
            .Select(tc=>ExtractZetForBusinessLine(tc))
            .Distinct()
            .ToList();
            

            if (!distinctBlZets.Any())
            {
                distinctBlZets.Add("");
            }

            var specialTemplateLayout = SpecialTemplateList.FindSpecialTemplateLayoutByCode(tableGroup.TemplateCode);


            if (!specialTemplateLayout?.IsZetImportant ?? false)
            {
                //to avoid rendering twice the same mergedsheet for multiple zets (case S.28.01.01)
                distinctBlZets = new() { "" };
            }
            var line = 0;
            foreach (var sheetCodeZet in distinctBlZets)
            {
                line++;
                //use the specialTemplateLayout if is  found in the static list, otherwise create one using the tables in the table group
                var zetTemplateLayout = specialTemplateLayout is null
                    ? ToZetTemplateLayout(tableGroup, sheetCodeZet)
                    : ToZetTemplateUsingSpecialLayout(specialTemplateLayout, sheetCodeZet);


                //zetTemplateLayout.SheetName =  distinctSheetCodeZets.Count > 1 ? $"{zetTemplateLayout.GroupTableCode}_{line:D2}"
                //                                   :    specialTemplateLayout is not null ? specialTemplateLayout.TemplateSheetName                                                   
                //                                   : $"{zetTemplateLayout.GroupTableCode}";


                var specialSheetName = specialTemplateLayout is not null ? specialTemplateLayout.TemplateSheetName : zetTemplateLayout.GroupTableCode;                                
                if (distinctBlZets.Count > 1) {
                    specialSheetName = $"{specialSheetName}_{line:D2}";
                }

                zetTemplateLayout.SheetName = specialSheetName;                

                
                zetTemplateLayout.TemplateDescription = BuildMergedTableDescription(specialTemplateLayout is not null, zetTemplateLayout);
                (var isRendered, var sheet) = RenderOneZetSheet(zetTemplateLayout);
                if (isRendered)
                {
                    Console.WriteLine($"Merge:{ zetTemplateLayout.SheetName}");
                    var indexItem = new IndexSheetListItem(zetTemplateLayout.SheetName, zetTemplateLayout.SheetName, zetTemplateLayout.TemplateDescription);
                    indexList.ListItems.Add(indexItem);
                }
                if (tableGroup.TemplateCode == "S.06.02.01")
                {
                    s6Zet = sheetCodeZet;
                }
            }
        }
        ///////////////////////

        var sheet61 = CreateSheetFromLayout("", "S.06.02.01.01_Single");
        var sheet62 = CreateSheetFromLayout("", "S.06.02.01.02_Single");


        FixCombinedS6Form(s6Zet);


        var sortedItems = indexList.ListItems.OrderBy(li => li.templateCode).ToList();
        var sortedIndexList = indexList with { ListItems = sortedItems };

        var indexSheet = RenderIndexList(sortedIndexList);
        SortWorksheets(DestWorkbook, sortedIndexList);

        indexSheet.Activate();

        var ss = DestWorkbook.TabSheets.Count;

        (var isValidSave, var destSaveMessage) = HelperRoutines.SaveWorkbook(DestWorkbook, destFile);
        if (!isValidSave)
        {
            _logger.Error(destSaveMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, destSaveMessage);
            return false;
        }

        return true;


        string BuildMergedTableDescription(bool isSpecialTemplate, ZetTemplateLayout zetTemplateBundle)
        {
            //using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

            var dimDomZet = DimDom.GetParts( zetTemplateBundle.SheetCodeZet).DomAndValRaw;
            var label = _SqlFunctions.SelectMMember(dimDomZet)?.MemberLabel??"";

            //var sqlZet = @" SELECT mem.MemberLabel  FROM mMember mem where MemberXBRLCode= @zetValue";
            //var zetLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlZet, new { zetValue = zetTemplateBundle.SheetCodeZet });


            var templateDesciption = string.IsNullOrEmpty(label)
                ? $"{zetTemplateBundle.TemplateDescription.Trim()}"
                : $"{zetTemplateBundle.TemplateDescription.Trim()} -- {label}";
            return templateDesciption;
        }

        IWorksheet? CreateSheetFromLayout(string s6Zet, string specialTemplateLayoutCode)
        {
            var specialLayout = SpecialTemplateList.FindSpecialTemplateLayoutByCode(specialTemplateLayoutCode);
            var specialLayoutZet = ToZetTemplateUsingSpecialLayout(specialLayout, s6Zet);
            (var isRenderedx, var sheet) = RenderOneZetSheet(specialLayoutZet);
            if (isRenderedx)
            {
                var indexItem = new IndexSheetListItem(specialLayoutZet.SheetName, specialLayoutZet.SheetName, specialLayoutZet.TemplateDescription);
                indexList.ListItems.Add(indexItem);
                return sheet;
            }
            return null;
        }
        string ExtractZetForBusinessLine(string zetVal)
        {
            var xx = zetVal.Split("|", StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(val => val.StartsWith("s2c_dim:BL")) ??"";
            return xx;
        }
    }


    private ZetTemplateLayout ToZetTemplateLayout(TableGroup tableGroup, string sheetCodeZet)
    {

        //A template has many tables S.23.01.01=>  S.23.01.01.01, S.23.01.01.02         
        //A template bundle defines a layout ( a list of horizontal lines where each line contains many sheets)
        //populate the special template layout with the sheets of the specified sheetCodeZet

        var tableMatrix = tableGroup.TableCodes
            .Select(tableCode => new HorizontalLine(new List<SheetExtensiveInfo>() { CreateSheetExtensiveInfo(tableCode, sheetCodeZet, true) }))
            .ToList();


        var ztb = new ZetTemplateLayout()
        {
            SheetCodeZet = sheetCodeZet,
            GroupTableCode = tableGroup.TemplateCode,
            TemplateDescription = tableGroup.TemplateDescription,
            IsOnlyZet = true,
            TableMatrix = tableMatrix
        };
        return ztb;

    }
    private ZetTemplateLayout ToZetTemplateUsingSpecialLayout(SpecialTemplateLayout specialTemplateLayout, string sheetCodeZet)
    {

        //populate the special template layout with the se of the specified sheetCodeZet
        //each horizontalLine contains a set of sheets to be rendered next to each other 
        //some sheets may be null
        var tableMatrix = specialTemplateLayout.TableCodesMatrix
            .Select(line => new HorizontalLine(line.Select(code => CreateSheetExtensiveInfo(code, sheetCodeZet, specialTemplateLayout.IsZetImportant)).ToList()))
            .ToList();

        var ztb = new ZetTemplateLayout()
        {
            SheetCodeZet = sheetCodeZet,
            SheetName = specialTemplateLayout.TemplateSheetName,
            GroupTableCode = specialTemplateLayout.TemplateCode,
            TemplateDescription = specialTemplateLayout.TemplateSheetDescription,
            IsOnlyZet = specialTemplateLayout.IsZetImportant,
            TableMatrix = tableMatrix
        };
        return ztb;

    }


    private List<string> SelectSheetCodeZets(string templateCode)
    {
        //S.04.01.01 =>
        // Or S.04.01.01.01
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        //currency is can be CD,CR,OC but for s.19 is oc
        var templateCodeLike = $"{templateCode}%";
        var sqlZet = @"select  sheet.SheetCodeZet  from TemplateSheetInstance sheet where InstanceId=@_documentId and TableCode like @templateCodeLike";

        var distinctList = connectionInsurance.Query<string>(sqlZet, new { _documentId, templateCodeLike }).ToList();

        return distinctList;

    }
    private SheetExtensiveInfo CreateSheetExtensiveInfo(string tableCode, string sheetCodeZet, bool isZetImportant)
    {
        TemplateSheetInstance? dbSheet;
        if (string.IsNullOrWhiteSpace(sheetCodeZet) || !isZetImportant )
        {
            dbSheet= _SqlFunctions.SelectTemplateSheetByTableCodeAllZets(_documentId, tableCode).FirstOrDefault();
        }
        else 
        {
             dbSheet= _SqlFunctions.SelectTemplateSheetByTableCodeAllZets(_documentId, tableCode)
                                            .FirstOrDefault(sh => sh.ZDimVal.Contains(sheetCodeZet));
        }
        
            
        //var dbSheet = isZetImportant
        //    ? _SqlFunctions.SelectTemplateSheetBySheetCodeZet(_documentId, tableCode, sheetCodeZet)
        //    : _SqlFunctions.SelectTemplateSheetByTableCodeAllZets(_documentId, tableCode).FirstOrDefault();

        var worksheet = SourceWorkbook?.Worksheets[dbSheet?.SheetTabName?.Trim() ?? ""];
        var tableDesc = _SqlFunctions.SelectTable(tableCode)?.TableLabel ?? "";
        return new SheetExtensiveInfo { TableCode = tableCode, DbSheet = dbSheet, WorkSheet = worksheet, TableDescription = tableDesc };
    }

    private (bool, IWorksheet?) RenderOneZetSheet(ZetTemplateLayout zetTemplateLayout)
    {

        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var hasElements = zetTemplateLayout.TableMatrix.Any(row => row.HorizontalSheetInfo.Any(sh => sh.WorkSheet is not null));
        if (!hasElements)
        {
            return (false, null);
        }
        var destSheet = DestWorkbook.Worksheets.Create(zetTemplateLayout.SheetName);

        destSheet.Zoom = 90;
        var countVerticals = 0;
        var offsetVERTICAL = 1;


        foreach (var horizontalLine in zetTemplateLayout.TableMatrix)
        {
            //var hasSheet = vertical.HorizontalSheetInfo.   
            var isHorizontalLineValid = horizontalLine.HorizontalSheetInfo.Any(sheetInfo => sheetInfo.DbSheet is not null);
            if (!isHorizontalLineValid)
            {
                continue;
            }
            countVerticals++;

            var maxTableHeight = 0;
            var OffesetHORIZONTAL = 1;
            foreach (var ztbSheet in horizontalLine.HorizontalSheetInfo)
            {
                var isOpenTable = ztbSheet.DbSheet?.IsOpenTable ?? false;

                var srcWorksheet = ztbSheet.WorkSheet;

                if (srcWorksheet is null)
                {
                    //they do not want the empty anymr
                    continue;
                }
                if (srcWorksheet is null)
                {

                    //noramlly you write the description of the empty table
                    var emptyTableCode = destSheet[offsetVERTICAL, OffesetHORIZONTAL];
                    emptyTableCode.Text = $"{ztbSheet.TableCode} - empty table";
                    emptyTableCode.CellStyle = _pensionStyles.TableCodeStyle;                    
                    emptyTableCode.Offset(1, 0).Value = ztbSheet.TableDescription;
                    continue;
                }

                var sheetLastRow = srcWorksheet.Rows.Last().LastRow;
                var sheetLastCol = srcWorksheet.Columns.Last().LastColumn;

                var copyRange = srcWorksheet.Range[1, 1, sheetLastRow, sheetLastCol];
                var dCol = OffesetHORIZONTAL + sheetLastCol + 1;
                var dRow = offsetVERTICAL + sheetLastRow;
                var destRange = destSheet.Range[offsetVERTICAL, OffesetHORIZONTAL, offsetVERTICAL + sheetLastRow - 1, OffesetHORIZONTAL + sheetLastCol - 1];

                copyRange.CopyTo(destRange);

                //save the ranges of the dest
                SaveDestDataName(destSheet, offsetVERTICAL, OffesetHORIZONTAL, srcWorksheet);
                CreateLinkToHomePage(destSheet);
                FormatTableColumnsWidth(isOpenTable, srcWorksheet, destRange);
                maxTableHeight = Math.Max(maxTableHeight, sheetLastRow);
                OffesetHORIZONTAL += OffesetHORIZONTAL + sheetLastCol + SPACE_BETWEEN_TABLES_HORIZONTAL;
            }
            offsetVERTICAL = offsetVERTICAL + maxTableHeight + SPACE_BETWEEN_TABLES_VERTICAL;
        }


        return (true, destSheet);
        /////////////////////////////////////////////////////////////////////        

        static void FormatTableColumnsWidth(bool isOpenTable, IWorksheet worksheet, IRange destRange)
        {
            IRange rowLabelCell = destRange["A1"];
            var rowRgxN = new Regex(@"^R\d{4}");
            var colRgxN = new Regex(@"^C\d{4}");
            if (isOpenTable)
            {
                destRange.ColumnWidth = 20;
                destRange.WrapText = false;
                var cellx = destRange.Cells.FirstOrDefault(cell => Regex.IsMatch(cell.Value, @"C\d{4}"));
                if (cellx != null)
                {
                    var rowx = cellx.EntireRow.Offset(-1, 0);
                    rowx.WrapText = true;

                }
            }
            if (!isOpenTable)
            {
                destRange.ColumnWidth = 30;
                var rowCounter = 0;
                foreach (var row in destRange.Rows)
                {
                    var cells = row.Cells;
                    rowCounter++;
                    if (rowCounter > 10)
                    {
                        break;
                    }
                    try
                    {
                        rowLabelCell = row.Cells.First(cell => rowRgxN.IsMatch(cell.Value));
                        rowLabelCell.ColumnWidth = 10;

                        var secondColCell = rowLabelCell.Offset(-1, 2);
                        var firstDataCell = rowLabelCell.Offset(0, 1);
                        var type = ((WorksheetImpl)worksheet).GetCellType(firstDataCell.Row, firstDataCell.Column, false);
                        WorksheetImpl.TRangeValueType firstDatacellType = ((WorksheetImpl)worksheet).GetCellType(firstDataCell.Row, firstDataCell.Column, false);
                        var isColumn = colRgxN.IsMatch(secondColCell.Text ?? "");
                        if (!isColumn && firstDatacellType.ToString() == "String")
                        {
                            firstDataCell.ColumnWidth = 60;
                        }
                        break;
                    }
                    catch
                    {
                        var xxs = 3;
                    }

                }


            }
        }

        void CreateLinkToHomePage(IWorksheet destSheet)
        {
            var linkRange = destSheet["A1"];

            IHyperLink hyperlink = destSheet.HyperLinks.Add(linkRange);
            hyperlink.Type = ExcelHyperLinkType.Workbook;
            var address = $"List!A1";
            hyperlink.Address = address;
            linkRange.CellStyle = _pensionStyles.TableCodeStyle;
        }

        void SaveDestDataName(IWorksheet destSheet, int verticalOffset, int horizontalOffset, IWorksheet? worksheet)
        {
            if (SourceWorkbook is null)
            {
                return;
            }
            var srcDataName = SourceWorkbook.Names[$"{worksheet?.Name.Trim()}_data"];

            if (srcDataName != null)
            {
                var srcDataRange = srcDataName.RefersToRange;
                var obj = HelperRoutines.CreateRowColObject(srcDataRange.AddressR1C1Local);
                if (obj is null) { return; }

                var dataDestRange = destSheet[obj.Row + verticalOffset - 1, obj.Col + horizontalOffset - 1
                    , obj.LastRow + verticalOffset - 1, obj.LastCol + horizontalOffset - 1];

                var destName = DestWorkbook.Names.Add($"{worksheet?.Name.Trim()}_data");
                destName.RefersToRange = dataDestRange;
            }
        }
    }
    private List<TableGroup> CreateTableGroupsForModule(string moduleCode, int moduleId)
    {
        //templateCode="", tableCode=""
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var templateTableBundles = new List<TableGroup>();
        //find the grouptables
        var sqlTables = @"
                SELECT va.TemplateOrTableID, va.TemplateOrTableCode,va.TemplateOrTableLabel
                FROM mModuleBusinessTemplate mbt
                LEFT OUTER JOIN mTemplateOrTable va ON va.TemplateOrTableID = mbt.BusinessTemplateID
                LEFT OUTER JOIN mModule mod ON mbt.ModuleID = mod.ModuleID
                WHERE 1 = 1
                    and (TemplateOrTableCode like 'S.%'or TemplateOrTableCode like 'SR.%')
                    and mod.ModuleCode= @moduleCode                    
                ORDER BY va.TemplateOrTableCode                       ";
        var templateOrTables = connectionEiopa.Query<MTemplateOrTable>(sqlTables, new { moduleCode });


        foreach (var tot in templateOrTables)
        {
            var sqlTableCodes = @"
                       select  tab.TableCode
                from mTemplateOrTable child 
                join mTemplateOrTable par on par.TemplateOrTableID = child.ParentTemplateOrTableID
                join mTaxonomyTable taxo on taxo.AnnotatedTableID= child.TemplateOrTableID
                join mTable tab on tab.TableID=taxo.TableID
                where 1=1
                and par.TemplateOrTableID= @TemplateOrTableID
                order by par.TemplateOrTableCode;
                ";
            var tableCodes = connectionEiopa.Query<string>(sqlTableCodes, new { tot.TemplateOrTableID })?.ToList() ?? new List<string>();
            templateTableBundles.Add(new TableGroup(tot.TemplateOrTableCode, tot.TemplateOrTableLabel, tableCodes));


        }
        return templateTableBundles;

    }

    private IWorksheet RenderIndexList(IndexSheetList indexList)
    {

        var indexSheet = DestWorkbook.Worksheets.Create("List");
        indexSheet.SetColumnWidth(1, 30);
        indexSheet.Zoom = 90;
        var titleCell = indexSheet[1, 1];
        titleCell.Text = "List of Templates";
        titleCell.CellStyle = _pensionStyles.HeaderStyle;
        var row = 3;

        foreach (var indexItem in indexList.ListItems)
        {
            var tableCodeCell = indexSheet[row, 1];
            tableCodeCell.Text = indexItem.templateCode;

            var descriptionCell = indexSheet[row, 2];
            descriptionCell.Text = indexItem.Description;
            descriptionCell.CellStyle = _pensionStyles.Normal;

            IHyperLink hyperlink = indexSheet.HyperLinks.Add(tableCodeCell);
            hyperlink.Type = ExcelHyperLinkType.Workbook;
            var address = $"'{indexItem.sheetName}'!A1";
            hyperlink.Address = address;

            tableCodeCell.CellStyle = _pensionStyles.TableCodeStyle;
            row++;
        }

        return indexSheet;
    }

    private IWorksheet? FixCombinedS6Form(string s6Zet)
    {
        var combinedCode = "S.06.02.01_Combined";
        var s61Code = "S.06.02.01.01";
        var s62Code = "S.06.02.01.02";

        var sCombinedWorksheet = DestWorkbook.Worksheets[combinedCode];
        var s61Worksheet = DestWorkbook.Worksheets[s61Code];
        var s62Worksheet = DestWorkbook.Worksheets[s62Code];

        if (sCombinedWorksheet is null || s61Worksheet is null || s62Worksheet is null)
        {
            return null;
        }

        var S61DataRange = FindNamedRange(DestWorkbook, s61Code);
        var S62DataRange = FindNamedRange(DestWorkbook, s62Code);

        var last = S61DataRange.Rows.First().Columns.Last();
        var s62InCombined = sCombinedWorksheet.Range[last.Row, last.Column + 1, last.Row, last.Column + 20];
        var cellx = s62InCombined.Cells.FirstOrDefault(cell => Regex.IsMatch(cell.Value, @"C\d{4}"));

        foreach (var s61row in S61DataRange!.Rows.Skip(1))
        {
            var key = s61row.Columns[1].Value ?? "";
            var s62xRow = Find62Row(S62DataRange, key);
            if (s62xRow is null)
            {
                continue;
            }
            s62xRow.CopyTo(sCombinedWorksheet.Range[s61row.Row, cellx.Column]);
        }

        return sCombinedWorksheet;


        IRange? Find62Row(IRange range62, string key)
        {
            var rngCol0 = range62.Columns[0];
            var cellsInS62ColumnZeroWithKeyValue = range62.Columns[0].FindAll(key, ExcelFindType.Text);
            if(cellsInS62ColumnZeroWithKeyValue is null)
            {
                return null;
            }            

            foreach (IRange keyRange in cellsInS62ColumnZeroWithKeyValue)
            {
                var s62Row = range62[keyRange.Row, range62.Column, keyRange.Row, range62.LastColumn];
                return s62Row;
            }

            return null;
        }

        IRange? FindNamedRange(IWorkbook workbook, string startingText)
        {
            var names = workbook.Names;
            IRange range = null;
            foreach (var r in Enumerable.Range(1, names.Count))
            {
                var x = names[r];
                var name = x.Name ?? "";
                if (name.StartsWith(startingText))
                {
                    range = x.RefersToRange;
                    return range;
                }
            }
            return null;
        }
    }
    private void SortWorksheets(IWorkbook workbook, IndexSheetList indexList)
    {
        var list = indexList.ListItems;
        foreach (var li in indexList.ListItems)
        {
            var pos = list.IndexOf(li);
            var sheet = workbook.Worksheets[li.sheetName];
            sheet.Move(pos);
        }
        var listsheet = workbook.Worksheets["List"];
        listsheet.Move(0);

    }

}
