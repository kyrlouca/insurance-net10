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

public class ExcelBookMerger : ITemplateMerger
{

    private readonly IParameterHandler _parameterHandler;
    ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ICommonRoutines _commonRoutines;
    private IWorkbook? Workbook;
    private IWorkbook? DestWorkbook;
    int _documentId = 0;
    string debugTableCode = "";

    public ExcelBookMerger(IParameterHandler parametersHandler, ILogger logger, ICommonRoutines commonRoutines)
    {
        _parameterHandler = parametersHandler;
        _logger = logger;
        _commonRoutines = commonRoutines;
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
            var specialBundle = SpecialTemplateList.FindSpecialTemplateLayout(zetTemplate.GroupTableCode);
            ZetTemplateBundle zetBundle = specialBundle is null ? zetTemplate : ToZetTemplateBundle(specialBundle, zetTemplate);
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

    private List<ZetTemplateBundle> ToZetTemplateBundles(TemplateBundle templateTableBundle)
    {
        //A template has many tables S.23.01.01=>  S.23.01.01.01, S.23.01.01.02 
        //each table may have many Zet dimensions(for business line or currency)
        // Merge sheets under the same template code if they have the same zet.
        // A Zet template bundle groups the tables for the same Zet zet.Dim IN('BL','OC','CR')
        //If they have no zet, they will also be merged

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
        var templateCode = $"{templateTableBundle.TemplateCode}%";
        var zetBLList = connectionInsurance.Query<string>(sqlZet, new { _documentId, templateCode }).ToList();


        if (!zetBLList.Any())
        {
            zetBLList.Add("");
        }

        //
        var zetTemplateBundlesList = new List<ZetTemplateBundle>();
        foreach (var zet in zetBLList)
        {
            var tableSpecialInfoList = templateTableBundle.TableCodes.Select(tableCode => CreateTableInfo(tableCode, zet)).ToList();

            //the matrix has one row for each tablecode and each row has just one table info 
            //this way, the render layout is vertical with one table under the other
            var tableMatrix = templateTableBundle.TableCodes.Select(tableCode => 
                    new HorizontalTableInfolList( new List<TableExtensiveInfo>() { CreateTableInfo(tableCode, zet) } ) )
                    .ToList();

            var ztb = new ZetTemplateBundle()
            {
                GroupTableCode = templateTableBundle.TemplateCode,
                TemplateDescription = templateTableBundle.TemplateDescription,
                TableInfosMatrix = tableMatrix
            };
            zetTemplateBundlesList.Add(ztb);
        }
        return zetTemplateBundlesList;
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

        var hasElements = zetTemplateBundle.TableInfosMatrix.Any(row => row.HorizontalTables.Any(sh => sh.WorkSheet is not null));
        if (!hasElements)
        {
            return;
        }

        var mergedTabName = string.IsNullOrEmpty(zetTemplateBundle.Zet)
            ? zetTemplateBundle.GroupTableCode
            : zetTemplateBundle.GroupTableCode + "#" + zetTemplateBundle.Zet;
        mergedTabName = mergedTabName.Replace(":", "_");

        var sqlZet = @" SELECT mem.MemberLabel  FROM mMember mem where MemberXBRLCode= @zetValue";
        var zetLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlZet, new { zetValue = zetTemplateBundle.Zet });
        var templateDesciption = string.IsNullOrEmpty(zetLabel)
            ? $"{zetTemplateBundle.TemplateDescription.Trim()}"
            : $"{zetTemplateBundle.TemplateDescription.Trim()}#{zetLabel}";


        var destSheet = DestWorkbook.Worksheets.Create(mergedTabName);


        var verticalOffset = 1;
        foreach (var vertical in zetTemplateBundle.TableInfosMatrix)
        {

            foreach (var sheet in vertical.HorizontalTables)
            {
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
                var destRange = destSheet.Range[verticalOffset, horizontalOffset];
                copyRange.CopyTo(destRange);
                horizontalOffset += 25;
            }
            verticalOffset = verticalOffset + 30;

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

    private static ZetTemplateBundle ToZetTemplateBundle(SpecialTemplateLayout special, ZetTemplateBundle zetBundle)
    {
        var sheetMatrix = new List<List<TableExtensiveInfo>>();

        foreach (var horizontalLine in special.TableCodesMatrix ?? new string[][] { })
        {
            horizontalLine
                .Select(tableCode => FindMatchingTableInfo(tableCode))
                .Where(shw => shw is not null)
                .ToList();
            var horizontalNL = new List<TableExtensiveInfo>();
            sheetMatrix.Add(horizontalNL);
        }

        var ztb = new ZetTemplateBundle()
        {
            //var workPairs= special.TableCodes(debugTableCode=>CreateWorksheetPair())
            GroupTableCode = special.TemplateCode,
            TemplateDescription = special.TemplateSheetName
        };
        return ztb;

        TableExtensiveInfo? FindMatchingTableInfo(string tableCode)
        {
            var horizonalList = zetBundle.TableInfosMatrix.FirstOrDefault(hl => hl.HorizontalTables.Any(hor => hor.TableCode == tableCode));

            var tableInfo = horizonalList.HorizontalTables.FirstOrDefault(tt => tt.TableCode == tableCode);

            return tableInfo;

        }


    }


}
