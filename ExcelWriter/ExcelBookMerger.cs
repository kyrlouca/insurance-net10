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

public class ExcelBookMerger : ITemplateMerger
{

    private readonly IParameterHandler _parameterHandler;
    ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ICommonRoutines _commonRoutines;
    private readonly ICustomPensionStyles2 _customPensionStyles;
    PensionStyles _pensionStyles;
    private IWorkbook? Workbook;
    private IWorkbook? DestWorkbook;
    int _documentId = 0;

    //private IStyle? tableCodeStyle;
    //private IStyle? bodyStyle;
    //private IStyle? headerStyle;
    //private IStyle? dataSectionStyle;


    public ExcelBookMerger(IParameterHandler parametersHandler, ILogger logger, ICommonRoutines commonRoutines, ICustomPensionStyles2 customPensionStyles)
    {
        _parameterHandler = parametersHandler;
        _logger = logger;
        _commonRoutines = commonRoutines;
        _customPensionStyles = customPensionStyles;
    }
    public bool MergeTemplates(int documentId, string sourceFile, string destFile)
    {
        _documentId = documentId;
        _parameterData = _parameterHandler.GetParameterData();
        


        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdgWH5fc3RdRWFfU0B0W0o=");

        using var excelEngine = new ExcelEngine();
        IApplication application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        (Workbook, var originMessage) = HelperRoutines.OpenExistingExcelWorkbook(excelEngine, sourceFile);
        if (Workbook is null)
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




        var moduleTemplateBundles = CreateTemplateBundlesForModule(_parameterData.ModuleCode);
        //for each templateBundle, create one or more zetTempleateBundle (one per zet)
        var moduleZetTemplateBundles = moduleTemplateBundles
                .SelectMany(templateBundle => ToZetTemplateBundles(templateBundle))
                .ToList();


        foreach (var zetTemplate in moduleZetTemplateBundles)
        {
            var specialTemplateLayout = SpecialTemplateList.FindSpecialTemplateLayout(zetTemplate.GroupTableCode);
            ZetTemplateBundle zetBundle = specialTemplateLayout is null
                ? zetTemplate
                : ToZetTemplateBundleSpecial(specialTemplateLayout);
            RenderOneZetSheet(zetTemplate);
        }


        (var isValidSave, var destSaveMessage) = HelperRoutines.SaveWorkbook(DestWorkbook, destFile);
        if (!isValidSave)
        {
            _logger.Error(destSaveMessage);
            _commonRoutines.CreateTransactionLog(0, MessageType.ERROR, destSaveMessage);
            return false;
        }

        return true;
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
                    new HorizontalTableInfolList(new List<TableExtensiveInfo>() { CreateTableInfo(tableCode, zet) }))
                    .ToList();

            var ztb = new ZetTemplateBundle()
            {
                GroupTableCode = templateBundle.TemplateCode,
                TemplateDescription = templateBundle.TemplateDescription,
                TableMatrix = tableMatrix
            };
            zetTemplateBundlesList.Add(ztb);
        }
        return zetTemplateBundlesList;
    }
    private ZetTemplateBundle ToZetTemplateBundleSpecial(SpecialTemplateLayout specialTemplateLayout)
    {

        var tableMatrix = specialTemplateLayout.TableCodesMatrix.Select(line =>
            new HorizontalTableInfolList(line.Select(code => CreateTableInfo(code, "")).ToList())
        )
        .ToList();

        var ztb = new ZetTemplateBundle()
        {
            GroupTableCode = specialTemplateLayout.TemplateCode,
            TemplateDescription = specialTemplateLayout.TemplateSheetName,
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
        var dbSheet = SelectSheetByZet(zet, tableCode);

        var worksheet = Workbook?.Worksheets[dbSheet?.SheetTabName?.Trim() ?? ""];
        return new TableExtensiveInfo { TableCode = tableCode, DbSheet = dbSheet, WorkSheet = worksheet };
    }
    private TemplateSheetInstance? SelectSheetByZet(string zetValue, string tableCode)
    {

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
    private void RenderOneZetSheet(ZetTemplateBundle zetTemplateBundle)
    {

        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var hasElements = zetTemplateBundle.TableMatrix.Any(row => row.HorizontalTables.Any(sh => sh.WorkSheet is not null));
        if (!hasElements)
        {
            return;
        }
        string mergedTabName = BuildMergedTabName(zetTemplateBundle);

        var sqlZet = @" SELECT mem.MemberLabel  FROM mMember mem where MemberXBRLCode= @zetValue";
        var zetLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlZet, new { zetValue = zetTemplateBundle.Zet });
        var templateDesciption = string.IsNullOrEmpty(zetLabel)
            ? $"{zetTemplateBundle.TemplateDescription.Trim()}"
            : $"{zetTemplateBundle.TemplateDescription.Trim()}#{zetLabel}";


        var destSheet = DestWorkbook.Worksheets.Create(mergedTabName);
        destSheet.Zoom = 80;

        var verticalOffset = 1;
        foreach (var vertical in zetTemplateBundle.TableMatrix)
        {

            var tableHeight = 1;
            foreach (var sheet in vertical.HorizontalTables)
            {
                var isOpenTable = sheet.DbSheet?.IsOpenTable ?? false;
                var horizontalOffset = 1;
                var worksheet = sheet.WorkSheet;
                if (worksheet is null)
                {
                    //noramlly you write the description of the empty table
                    continue;
                }
                var sheetLastRow = worksheet.Rows.Last().LastRow;
                var sheetLastCol = worksheet.Columns.Last().LastColumn;

                var copyRange = worksheet.Range[1, 1, sheetLastRow, sheetLastCol];
                var destRange = destSheet.Range[verticalOffset, horizontalOffset, verticalOffset + sheetLastRow, verticalOffset + sheetLastCol];
                copyRange.CopyTo(destRange);
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
                tableHeight = Math.Max(tableHeight, sheetLastRow);

                horizontalOffset += sheetLastRow + 5;
            }
            verticalOffset = verticalOffset + tableHeight + 5;

        }

        static string BuildMergedTabName(ZetTemplateBundle zetTemplateBundle)
        {
            var mergedTabName = string.IsNullOrEmpty(zetTemplateBundle.Zet)
                ? zetTemplateBundle.GroupTableCode
                : zetTemplateBundle.GroupTableCode + "#" + zetTemplateBundle.Zet;
            mergedTabName = mergedTabName.Replace(":", "_");
            return mergedTabName;
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
                ORDER BY mod.ModuleCode                       ";
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



}
