namespace ExcelWriter;
using Shared.HostParameters;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.SharedHost;
using Shared.DataModels;
using ExcelWriter.DataModels;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection.Metadata;
using Syncfusion.XlsIO.Implementation;
using ExcelWriter.Common;
using Shared.SQLFunctions;
using System.Text.RegularExpressions;

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

        var TableGroupsList = CreateTableGroupsForModule(_documentInstance.ModuleCode, _documentInstance.ModuleId);

        ///////////////////////
        var s6Zet = "";
        foreach (var tableGroup in TableGroupsList)
        {

            var distinctSheetCodeZets = tableGroup.TableCodes
                .SelectMany(tc => SelectSheetCodeZets(tc))
                .Distinct()
                .ToList();

            if (!distinctSheetCodeZets.Any())
            {
                distinctSheetCodeZets.Add("");
            }

            var specialTemplateLayout = SpecialTemplateList.FindSpecialTemplateLayout(tableGroup.TemplateCode);
            var line = 0;
            foreach (var sheetCodeZet in distinctSheetCodeZets)
            {
                line++;
                //use the specialTemplateLayout if is  found in the static list, otherwise create one using the tables in the table group
                var zetTemplateLayout = specialTemplateLayout is null
                    ? ToZetTemplateLayout(tableGroup, sheetCodeZet)
                    : ToZetTemplateBundleSpecial(specialTemplateLayout, sheetCodeZet, tableGroup.TemplateDescription);

                zetTemplateLayout.SheetName = distinctSheetCodeZets.Count > 1 ? $"{zetTemplateLayout.GroupTableCode}_{line:D2}" : $"{zetTemplateLayout.GroupTableCode}";
                zetTemplateLayout.TemplateDescription = BuildMergedTableDescription(zetTemplateLayout);
                var isRendered = RenderOneZetSheet(zetTemplateLayout);
                if (isRendered)
                {
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



        FixCombinedS6Form(s6Zet);
        


        var specialTemplateForSingleS61 = "S.06.02.01.01_Single";
        var templateDescription = "Information on Positions Held";
        CreateSheetFromLayout(s6Zet, specialTemplateForSingleS61, templateDescription);

        var specialTemplateForSingleS62 = "S.06.02.01.02_Single";
        CreateSheetFromLayout(s6Zet, specialTemplateForSingleS62, templateDescription);


        var indexSheet = RenderIndexList(indexList);

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


        string BuildMergedTableDescription(ZetTemplateLayout zetTemplateBundle)
        {
            using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
            var sqlZet = @" SELECT mem.MemberLabel  FROM mMember mem where MemberXBRLCode= @zetValue";
            var zetLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlZet, new { zetValue = zetTemplateBundle.SheetCodeZet });
            var templateDesciption = string.IsNullOrEmpty(zetLabel)
                ? $"{zetTemplateBundle.TemplateDescription.Trim()}"
                : $"{zetTemplateBundle.TemplateDescription.Trim()} -- {zetLabel}";
            return templateDesciption;
        }

        void CreateSheetFromLayout(string s6Zet, string specialTemplateLayoutCode, string templateDescription)
        {
            var specialLayout = SpecialTemplateList.FindSpecialTemplateLayout(specialTemplateLayoutCode);
            var specialLayoutZet = ToZetTemplateBundleSpecial(specialLayout, s6Zet, templateDescription);
            var isRenderedx = RenderOneZetSheet(specialLayoutZet);
            if (isRenderedx)
            {
                var indexItem = new IndexSheetListItem(specialLayoutZet.SheetName, specialLayoutZet.SheetName, specialLayoutZet.TemplateDescription);
                indexList.ListItems.Add(indexItem);
            }
        }
    }


    private ZetTemplateLayout ToZetTemplateLayout(TableGroup templateBundle, string sheetCodeZet)
    {

        //A template has many tables S.23.01.01=>  S.23.01.01.01, S.23.01.01.02         
        //A template bundle defines a layout ( a list of horizontal lines where each line contains many sheets)
        //populate the special template layout with the sheets of the specified sheetCodeZet
        
            var tableMatrix = templateBundle.TableCodes
                .Select(tableCode => new HorizontalLine(new List<SheetExtensiveInfo>() { CreateSheetExtensiveInfo(tableCode, sheetCodeZet) }))
                .ToList();

        

        var ztb = new ZetTemplateLayout()
        {
            SheetCodeZet = sheetCodeZet,
            GroupTableCode = templateBundle.TemplateCode,
            TemplateDescription = templateBundle.TemplateDescription,
            TableMatrix = tableMatrix
        };
        return ztb;

    }
    private ZetTemplateLayout ToZetTemplateBundleSpecial(SpecialTemplateLayout specialTemplateLayout, string sheetCodeZet, string templateDescription)
    {
        if(specialTemplateLayout is null)
        {
            Console.WriteLine("null temp");
            throw(new ArgumentNullException(nameof(specialTemplateLayout)));
        }

        //populate the special template layout with the seets of the specified sheetCodeZet
        //each horizontalLine contains a set of sheets to be rendered next to each other 
        //some sheets may be null
        var tableMatrix = specialTemplateLayout.TableCodesMatrix
            //.Select(line =>line.Where(tableCode => _SqlFunctions.SelectTempateSheetBySheetCodeZet(_documentId, sheetCodeZet) is not null))
            .Select(line => new HorizontalLine(line.Select(code => CreateSheetExtensiveInfo(code, sheetCodeZet)).ToList()))
            .ToList();

        var ztb = new ZetTemplateLayout()
        {
            SheetCodeZet = sheetCodeZet,
            SheetName = specialTemplateLayout.TemplateSheetName,
            GroupTableCode = specialTemplateLayout.TemplateCode,
            TemplateDescription = templateDescription,
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
    private SheetExtensiveInfo CreateSheetExtensiveInfo(string tableCode, string sheetCodeZet)
    {
        var dbSheet = _SqlFunctions.SelectTempateSheetBySheetCodeZet(_documentId, tableCode,sheetCodeZet);
        //var dbSheet = SelectDbSheetBySheetCodeZet(tableCode, sheetCodeZet);
        var worksheet = SourceWorkbook?.Worksheets[dbSheet?.SheetTabName?.Trim() ?? ""];
        var tableDesc = _SqlFunctions.SelectTable(tableCode)?.TableLabel ?? "";
        return new SheetExtensiveInfo { TableCode = tableCode, DbSheet = dbSheet, WorkSheet = worksheet, TableDescription = tableDesc };
    }
    
    private bool RenderOneZetSheet(ZetTemplateLayout zetTemplateLayout)
    {

        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var hasElements = zetTemplateLayout.TableMatrix.Any(row => row.HorizontalSheetInfo.Any(sh => sh.WorkSheet is not null));
        if (!hasElements)
        {
            return false;
        }

        var destSheet = DestWorkbook.Worksheets.Create(zetTemplateLayout.SheetName);

        destSheet.Zoom = 80;

        var countVerticals = 0;
        var offsetVERTICAL = 1;

        
        foreach (var horizontalLine in zetTemplateLayout.TableMatrix)
        {
            //var hasSheet = vertical.HorizontalSheetInfo.   
            var isHorizontalLineValid = horizontalLine.HorizontalSheetInfo.Any(sheetInfo => sheetInfo.DbSheet is not null);
            if(!isHorizontalLineValid)
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
                    //var xx = ztbSheet.DbSheet.Description;                    
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
                //copyRange.CopyTo(destRange, ExcelCopyRangeOptions.CopyValueAndSourceFormatting);


                //save the ranges of the dest
                SaveDestDataName(destSheet, offsetVERTICAL, OffesetHORIZONTAL, srcWorksheet);


                CreateLinkToHomePage(destSheet);
                FormatColumnsWidth(isOpenTable, srcWorksheet, destRange);



                maxTableHeight = Math.Max(maxTableHeight, sheetLastRow);

                OffesetHORIZONTAL += OffesetHORIZONTAL + sheetLastCol + 3;
            }
            offsetVERTICAL = offsetVERTICAL + maxTableHeight + 5;
        }


        return true;
        /////////////////////////////////////////////////////////////////////        

        static void FormatColumnsWidth(bool isOpenTable, IWorksheet worksheet, IRange destRange)
        {
            //destRange.ColumnWidth = isOpenTable? 30 : 20;
            destRange.WrapText = false;
            IRange rowLabelCell = destRange["A1"];
            var rowRgxN = new Regex(@"^R\d{4}");
            var colRgxN = new Regex(@"^C\d{4}");
            if (isOpenTable)
            {
                destRange.ColumnWidth = 20;
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
                    and TemplateOrTableCode like 'S.%'
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
        indexSheet.Move(0);
        indexSheet.SetColumnWidth(1, 30);
        indexSheet.Zoom = 80;
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

    private bool FixCombinedS6Form(string s6Zet)
    {

        var s6SpecialTemplateLayout = SpecialTemplateList.FindSpecialTemplateLayout("S.06.02.01");
        var s6ZetTemplateBundle = ToZetTemplateBundleSpecial(s6SpecialTemplateLayout, s6Zet, "special S6");

        var s61Code = "S.06.02.01.01";
        var s62Code = "S.06.02.01.02";

                

        var s61Line = s6ZetTemplateBundle.TableMatrix.FirstOrDefault(line => line.HorizontalSheetInfo.Any(htbl => htbl.TableCode =="S.06.02.01.01"));
        var s61Worksheet = s61Line.HorizontalSheetInfo.FirstOrDefault(tbl => tbl.TableCode =="S.06.02.01.01").WorkSheet;

        var s62Line = s6ZetTemplateBundle.TableMatrix
            .FirstOrDefault(line => line.HorizontalSheetInfo.Any(htbl => htbl.TableCode == "S.06.02.01.02"));
        var s62Worksheet = s62Line.HorizontalSheetInfo.FirstOrDefault(tbl => tbl.TableCode == "S.06.02.01.02").WorkSheet;

        var sCombined = DestWorkbook.Worksheets["S.06.02.01"];

        if (s61Worksheet is null || s62Worksheet is null)
        {
            return false;
        }

        //the range for the s61 and s62 data is just one row, and we need to expand to the end of the sheet
        var s61TabName = $"{s61Worksheet.Name}_data";
        var s61OriginalDataLine = DestWorkbook?.Names[$"{s61Worksheet.Name}_data"]?.RefersToRange;
        var s62OriginalDataLine = DestWorkbook?.Names[$"{s62Worksheet.Name}_data"]?.RefersToRange;


        var s61DataLine = s61OriginalDataLine?.Rows.First().Offset(1, 0);
        var s62DataLine = s62OriginalDataLine?.Rows.First().Offset(1, 0);

        if (s61DataLine is null || s62DataLine is null)
        {
            return false;
        }

        //expand the range of s61 and s62 to include all the rows until the last Row (UsedRange)
        var s61Data = sCombined.Range[s61DataLine.Row, s61DataLine.Column, s61Worksheet.UsedRange.LastRow, s61DataLine.LastColumn];
        var s62Data = sCombined.Range[s62DataLine.Row, s62DataLine.Column, s62Worksheet.UsedRange.LastRow, s62DataLine.LastColumn];
        var s62KeyColumn = s62Data.Columns[0];



        foreach (var s61row in s61Data.Rows)
        {
            var key = s61row.Columns[1].Value;
            var s62Row = FindRow(key);
            if (s62Row is null)
            {
                continue;
            }
            s62Row.CopyTo(sCombined.Range[s61row.Row, s62Row.LastColumn + 5]);
        }


        var sortedRange = sCombined.Range[s62Data.Row, s62Data.LastColumn + 5, s61Data.LastRow, s62Data.LastColumn + 5 + s62Data.Columns.Length - 1];
        sortedRange.MoveTo(s62Data);
        sCombined.UsedRange.ColumnWidth = 30;
        var xxstyle = _pensionStyles.DataSectionStyle.Borders[ExcelBordersIndex.EdgeLeft];
        var newS62Range = sCombined.Range[s62Data.Row, s62Data.Column, s61Data.LastRow, s62Data.LastColumn];
        newS62Range.CellStyle = _pensionStyles.DataSectionStyle;
        s61Data.CellStyle = _pensionStyles.DataSectionStyle;
        var xxss = newS62Range.Columns.First();
        //xxss.CellStyle.Borders[ExcelBordersIndex.EdgeLeft] = ExcelLineStyle.Thick;
        xxss.CellStyle.Borders[ExcelBordersIndex.EdgeLeft].LineStyle = ExcelLineStyle.Thick;



        return true;

        IRange? FindRow(string key)
        {
            foreach (IRange keyRange in s62KeyColumn.FindAll(key, ExcelFindType.Text))
            {
                var s62Row = s62Data[keyRange.Row, s62Data.Column, keyRange.Row, s62Data.LastColumn];
                return s62Row;
            }
            return null;
        }


    }

}
