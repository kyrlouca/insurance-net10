namespace ExcelWriter;
using Shared.CommonRoutines;
using Shared.HostRoutines;
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

public class ExcelBookMerger : ITemplateMerger
{

    private readonly IParameterHandler _parameterHandler;
    ParameterData _parameterData = new();
    private readonly ILogger _logger;

    private readonly ICommonRoutines _commonRoutines;
    private readonly ICustomPensionStyler _customPensionStyles;
    PensionStyles _pensionStyles;
    private IWorkbook? SourceWorkbook;
    private IWorkbook? DestWorkbook;
    int _documentId = 0;

    //private IStyle? tableCodeStyle;
    //private IStyle? bodyStyle;
    //private IStyle? headerStyle;
    //private IStyle? dataSectionStyle;


    public ExcelBookMerger(IParameterHandler parametersHandler, ILogger logger, ICommonRoutines commonRoutines, ICustomPensionStyler customPensionStyles)
    {
        _parameterHandler = parametersHandler;
        _logger = logger;
        _commonRoutines = commonRoutines;
        _customPensionStyles = customPensionStyles;
    }
    public bool MergeTables(int documentId, string sourceFile, string destFile)
    {
        _documentId = documentId;
        _parameterData = _parameterHandler.GetParameterData();



        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdgWH5fc3RdRWFfU0B0W0o=");

        using var excelEngine = new ExcelEngine();
        IApplication application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        (SourceWorkbook, var originMessage) = HelperRoutines.OpenExistingExcelWorkbook(excelEngine, sourceFile);
        if (SourceWorkbook is null)
        {
            _logger.Error(originMessage);
            _commonRoutines.CreateTransactionLog(0, MessageType.ERROR, originMessage);
            return false;
        }


        (DestWorkbook, var destMessage) = HelperRoutines.CreateExcelWorkbook(excelEngine);
        if (DestWorkbook is null)
        {
            _logger.Error(destMessage);
            _commonRoutines.CreateTransactionLog(0, MessageType.ERROR, destMessage);
            return false;
        }

        _pensionStyles = _customPensionStyles.GetStyles(DestWorkbook);

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //Merge sheets for each template Code (3 parts) based on dimension.(line of business BL and currency OC)
        //"S.01.01.02=>"S.01.01.02.01","S.01.01.02.02","S.01.01.02.03"        
        //A bundle contains the template code and a list of horizontal tableCodes lists like {S.19.01.01, {S.19.01.01.01,19.01.01.02,etc},{19.01.01.08}}
        //using TemplateBundle, the Merged sheet can render tables horizontally and vertically.

        var indexList = new IndexSheetList("List", new List<IndexSheetListItem>());

        var moduleTemplateBundles = CreateTemplateBundlesForModule(_parameterData.ModuleCode);
        //for each templateBundle, create one or more zetTempleateBundle (one per zet)
        var moduleZetTemplateBundles = moduleTemplateBundles
                .SelectMany(templateBundle => ToZetTemplateBundles(templateBundle))
                .ToList();

        foreach (var zetTemplate in moduleZetTemplateBundles)
        {            
            var specialTemplateLayout = SpecialTemplateList.FindSpecialTemplateLayout(zetTemplate.GroupTableCode);
            //if there is a specialTemplateLayout, create the ZetTemplates again
            ZetTemplateBundle zetTemplateToRender = specialTemplateLayout is null
                ? zetTemplate
                : ToZetTemplateBundleSpecial(specialTemplateLayout,zetTemplate.Zet,zetTemplate.TemplateDescription);
            
            zetTemplateToRender.SheetName = BuildMergedTabName(zetTemplateToRender);
            zetTemplateToRender.TemplateDescription=BuildMergedTableDescription(zetTemplateToRender);

            Log.Information($"Rendering Single Template:{zetTemplateToRender.GroupTableCode}");
            ///RENDER the Merged Sheet
            var isRendered = RenderOneZetSheet(zetTemplateToRender);
            if (isRendered)
            {
                var indexItem = new IndexSheetListItem(zetTemplateToRender.SheetName, zetTemplateToRender.SheetName, zetTemplateToRender.TemplateDescription);
                indexList.ListItems.Add(indexItem);
            }

        }

        var s6Bundle = moduleZetTemplateBundles.FirstOrDefault(zetTemplate => zetTemplate.GroupTableCode == "S.06.02.01");
        FixCombinedS6Form(s6Bundle);

        var indexSheet = RenderIndexList(indexList);
        
        indexSheet.Activate();

        (var isValidSave, var destSaveMessage) = HelperRoutines.SaveWorkbook(DestWorkbook, destFile);
        if (!isValidSave)
        {
            _logger.Error(destSaveMessage);
            _commonRoutines.CreateTransactionLog(0, MessageType.ERROR, destSaveMessage);
            return false;
        }

        return true;

        static string BuildMergedTabName(ZetTemplateBundle zetTemplateBundle)
        {
            var mergedTabName = string.IsNullOrEmpty(zetTemplateBundle.Zet)
                ? zetTemplateBundle.GroupTableCode
                : zetTemplateBundle.GroupTableCode + "__" + zetTemplateBundle.Zet;
            mergedTabName = mergedTabName.Replace(":", "_");
            return mergedTabName;
        }
        string BuildMergedTableDescription(ZetTemplateBundle zetTemplateBundle)
        {
            using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
            var sqlZet = @" SELECT mem.MemberLabel  FROM mMember mem where MemberXBRLCode= @zetValue";
            var zetLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlZet, new { zetValue = zetTemplateBundle.Zet });
            var templateDesciption = string.IsNullOrEmpty(zetLabel)
                ? $"{zetTemplateBundle.TemplateDescription.Trim()}"
                : $"{zetTemplateBundle.TemplateDescription.Trim()} -- {zetLabel}";
            return templateDesciption;            
        }

    }

    private List<ZetTemplateBundle> ToZetTemplateBundles(TemplateBundle templateBundle)
    {

        //A template has many tables S.23.01.01=>  S.23.01.01.01, S.23.01.01.02         
        //A template bundle groups the tables codes without considering a zet value
        // A ZetTemplate bundle contains the table info for a specific special Zet (for business line or currency 'BL','OC','CR'),
        // For the  templatedBundle return a list of ZetTemplatebundles (one for each zet)
        // If they have no special zet, just return one ZetTemplatebundle        


        var zetList = SelectSpecialZetList(templateBundle.TemplateCode);

        var zetTemplateBundlesList = new List<ZetTemplateBundle>();
        foreach (var zet in zetList)
        {
            //each table in a horizontal list of the matrix is rendered one next to each other
            // each horizontal list of tables in the matrix are rendered one under the other             
            //the matrix has one row for each tablecode and each row has just one table (basically all tables will be rendered vertically this way) 

            var tableMatrix = templateBundle.TableCodes.Select(tableCode =>
                    new HorizontalLine(new List<TableExtensiveInfo>() { CreateTableInfo(tableCode, zet) }))
                    .ToList();

            var ztb = new ZetTemplateBundle()
            {
                Zet=zet,
                GroupTableCode = templateBundle.TemplateCode,
                TemplateDescription = templateBundle.TemplateDescription,
                TableMatrix = tableMatrix
            };
            zetTemplateBundlesList.Add(ztb);
        }
        return zetTemplateBundlesList;
    }
    private ZetTemplateBundle ToZetTemplateBundleSpecial(SpecialTemplateLayout specialTemplateLayout,string zet,string templateDescription)
    {

        var tableMatrix = specialTemplateLayout.TableCodesMatrix.Select(line =>
            new HorizontalLine(line.Select(code => CreateTableInfo(code, zet)).ToList())
        )
        .ToList();

        var ztb = new ZetTemplateBundle()
        {
            Zet = zet,
            SheetName = specialTemplateLayout.TemplateSheetName,
            GroupTableCode = specialTemplateLayout.TemplateCode,
            TemplateDescription = templateDescription,
            TableMatrix = tableMatrix
        };
        return ztb;

    }
    private List<string> SelectSpecialZetList(string templateCode)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        //currency is can be CD,CR,OC but for s.19 is oc

        var sqlZet = @"
                    SELECT zet.value
                    FROM TemplateSheetInstance sheet
                    JOIN SheetZetValue zet ON zet.TemplateSheetId = sheet.TemplateSheetId
                    WHERE sheet.InstanceId = @_documentId
                        AND sheet.TableCode LIKE @templateCode
                        AND zet.Dim IN ('BL','OC','CR')
                    GROUP BY zet.Value
            ";
        var templateCodeLike = $"{templateCode}%";
        var zetBLList = connectionInsurance.Query<string>(sqlZet, new { _documentId, templateCode = templateCodeLike }).ToList();
        if (!zetBLList.Any())
        {
            zetBLList.Add("");
        }
        return zetBLList;

    }
    private TableExtensiveInfo CreateTableInfo(string tableCode, string zet)
    {
        var dbSheet = SelectDbSheetByZet(zet, tableCode);

        var worksheet = SourceWorkbook?.Worksheets[dbSheet?.SheetTabName?.Trim() ?? ""];
        return new TableExtensiveInfo { TableCode = tableCode, DbSheet = dbSheet, WorkSheet = worksheet };
    }
    private TemplateSheetInstance? SelectDbSheetByZet(string zetValue, string tableCode)
    {
        //if zet is null or empty do NOT use it in selection
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var sqlSheetWithoutZet = @"
                    SELECT sheet.TemplateSheetId, sheet.SheetCode, sheet.TableCode,sheet.SheetTabName
                    FROM TemplateSheetInstance sheet
                    WHERE sheet.InstanceId = @_documentId
                     AND sheet.TableCode= @tableCode                     
                ";

        var sqlSheetWithZet = @"
                    SELECT sheet.TemplateSheetId, sheet.SheetCode, sheet.TableCode,sheet.SheetTabName
                    FROM TemplateSheetInstance sheet
                    left outer join   SheetZetValue zet on zet.TemplateSheetId= sheet.TemplateSheetId
                    WHERE sheet.InstanceId = @_documentId
                        AND sheet.TableCode= @tableCode                     
                        and zet.Dim in ('BL','OC','CR')
                        and zet.Value = @zetValue
                ";

        var sqlSheets = string.IsNullOrEmpty(zetValue) ? sqlSheetWithoutZet : sqlSheetWithZet;
        var result = connectionInsurance.QueryFirstOrDefault<TemplateSheetInstance>(sqlSheets, new { _documentId, tableCode, zetValue });
        return result;
    }
    private bool RenderOneZetSheet(ZetTemplateBundle zetTemplateBundle)
    {

        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var hasElements = zetTemplateBundle.TableMatrix.Any(row => row.HorizontalTables.Any(sh => sh.WorkSheet is not null));
        if (!hasElements)
        {
            return false;
        }        

        var destSheet = DestWorkbook.Worksheets.Create(zetTemplateBundle.SheetName);

        destSheet.Zoom = 80;

        var offsetVERTICAL = 1;
        foreach (var vertical in zetTemplateBundle.TableMatrix)
        {
            //check if there is at least one sheet which is not null in tableMatrix 
            var hasTable = zetTemplateBundle.TableMatrix.Any(line => line.HorizontalTables.Any(ht => ht.WorkSheet is not null));

            var maxTableHeight = 0;
            var OffesetHORIZONTAL = 1;
            foreach (var ztbSheet in vertical.HorizontalTables)
            {
                var isOpenTable = ztbSheet.DbSheet?.IsOpenTable ?? false;

                var srcWorksheet = ztbSheet.WorkSheet;
                if (srcWorksheet is null)
                {
                    //noramlly you write the description of the empty table
                    var emptyTableCode = destSheet[offsetVERTICAL, OffesetHORIZONTAL];
                    emptyTableCode.Text = $"{ztbSheet.TableCode} - empty table";
                    emptyTableCode.CellStyle = _pensionStyles.TableCodeStyle;
                    continue;
                }


                var sheetLastRow = srcWorksheet.Rows.Last().LastRow;
                var sheetLastCol = srcWorksheet.Columns.Last().LastColumn;

                var copyRange = srcWorksheet.Range[1, 1, sheetLastRow, sheetLastCol];
                var dCol = OffesetHORIZONTAL + sheetLastCol + 1;
                var dRow = offsetVERTICAL + sheetLastRow;
                var destRange = destSheet.Range[offsetVERTICAL, OffesetHORIZONTAL, offsetVERTICAL + sheetLastRow -1, OffesetHORIZONTAL + sheetLastCol -1];

                copyRange.CopyTo(destRange);

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

        static void FormatColumnsWidth(bool isOpenTable, IWorksheet? worksheet, IRange destRange)
        {
            destRange.ColumnWidth = 30;
            if (!isOpenTable)
            {

                if ((destRange.LastColumn - destRange.Column) > 1)
                {
                    destRange.Columns[0].ColumnWidth = 50;
                    destRange.Columns[0].WrapText = false;
                    destRange.Columns[1].ColumnWidth = 10;
                }
                if (destRange.LastColumn - destRange.Column == 3)
                {
                    WorksheetImpl.TRangeValueType cellType = (worksheet as WorksheetImpl).GetCellType(destRange.LastRow - 1, 3, false);
                    if (cellType.ToString() != "Number")
                    {
                        destRange.Columns[2].ColumnWidth = 80;
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
    private List<TemplateBundle> CreateTemplateBundlesForModule(string moduleCode)
    {
        //templateCode="", tableCode=""
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var templateTableBundles = new List<TemplateBundle>();

        var sqlTables = @"
                SELECT va.TemplateOrTableCode,va.TemplateOrTableLabel
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
                SELECT  tab.TableCode
                FROM mTemplateOrTable va
                LEFT OUTER JOIN mTemplateOrTable bu ON bu.ParentTemplateOrTableID = va.TemplateOrTableID
                LEFT OUTER JOIN mTemplateOrTable anno ON anno.ParentTemplateOrTableID = bu.TemplateOrTableID
                LEFT OUTER JOIN mTaxonomyTable taxo ON taxo.AnnotatedTableID = anno.TemplateOrTableID
                LEFT OUTER JOIN mTable tab ON tab.TableID = taxo.TableID
                WHERE 1 = 1
                    AND va.TemplateOrTableCode = @templateOrTableCode
                ORDER BY tab.TableCode

                ";
            var tableCodes = connectionEiopa.Query<string>(sqlTableCodes, new { tot.TemplateOrTableCode })?.ToList() ?? new List<string>();
            templateTableBundles.Add(new TemplateBundle(tot.TemplateOrTableCode, tot.TemplateOrTableLabel, tableCodes));


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

    private bool FixCombinedS6Form(ZetTemplateBundle zetTemplateBundle)
    {

        var s61Code = "S.06.02.01.01";
        var s62Code = "S.06.02.01.02";


        var s61Line = zetTemplateBundle.TableMatrix
            .FirstOrDefault(line => line.HorizontalTables.Any(htbl => htbl.TableCode == "S.06.02.01.01"));
        var s61Worksheet = s61Line.HorizontalTables.FirstOrDefault(tbl => tbl.TableCode == "S.06.02.01.01").WorkSheet;

        var s62Line = zetTemplateBundle.TableMatrix
            .FirstOrDefault(line => line.HorizontalTables.Any(htbl => htbl.TableCode == "S.06.02.01.02"));
        var s62Worksheet = s62Line.HorizontalTables.FirstOrDefault(tbl => tbl.TableCode == "S.06.02.01.02").WorkSheet;

        var sCombined = DestWorkbook.Worksheets["S.06.02.01"];

        if (s61Worksheet is null || s62Worksheet is null)
        {
            return false;
        }

        //the range for the s61 and s62 data is just one row, and we need to expand to the end of the sheet
        var s61DataLine = DestWorkbook?.Names[$"{s61Code}_data"]?.RefersToRange;
        var s62DataLine = DestWorkbook?.Names[$"{s62Code}_data"]?.RefersToRange;

        var s61Data = sCombined.Range[s61DataLine.Row, s61DataLine.Column, s61Worksheet.UsedRange.LastRow, sCombined.UsedRange.LastColumn];
        var s62Data = sCombined.Range[s62DataLine.Row, s62DataLine.Column, s62Worksheet.UsedRange.LastRow, sCombined.UsedRange.LastColumn];
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
