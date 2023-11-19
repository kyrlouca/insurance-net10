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
        //If there is a TemplateBundel, the Merged sheet can merge horizontally and vertically.

        //A module (qrs) has tamplate codes S.05.02.01. 
        //"S.01.01.02=>"S.01.01.02.01","S.01.01.02.02","S.01.01.02.03"        
        var templateBundles = CreateTemplateBundlesForModule(_parameterData.ModuleCode);
        var zetTemplateBundles = new List<List<ZetTemplateBundle>>();

        foreach (var template in templateBundles)
        {
            Console.WriteLine($"template:{template.TemplateCode}");
            //Each module template code has one or more tableCodes "S.01.01.02=>"S.01.01.02.01","S.01.01.02.02","S.01.01.02.03"        
            // Each table code may correspone to one or many sheet instances, each with a diffrent zet
            var xxx = CreateZetTemplateBundles(template);
            zetTemplateBundles.Add(xxx);
            //MergeOneTemplatePerZet(template);
        }

        foreach(var zetTemplate in zetTemplateBundles)
        {
            foreach(var templateBundle in zetTemplate)
            {
                CreateOneZetSheetNew(templateBundle);
            }
            
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


    private List<ZetTemplateBundle> CreateZetTemplateBundles(TemplateBundle templateTableBundle)
    {
        //One template may have many Zet dimensions(for business line or currency)
        // Merge sheets under the same template code if they have the same zet.
        //for example S.23.01.01.01, S.23.01.01.02 are under the same template S.23.01.01 with the same BL, OC dim value should be merged 
        //If they have no zet, they will also be merged
        // zet.Dim IN('BL','OC','CR')
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

        var zetTemplateBundlesList = new List<ZetTemplateBundle>();
        foreach (var zet in zetBLList)
        {
            var workPairs = templateTableBundle.TableCodes.Select(tableCode => CreateWorksheetPair(tableCode, zet)).ToList();

            var z1 = new ZetTemplateBundle()
            {
                GroupTableCode = templateTableBundle.TemplateCode,
                TemplateDescription = templateTableBundle.TemplateDescription,
                //one record for this list since there are not horizontal tables
                SheetsAndWorksheets = new List<List<SheetDbAndWorksheet>>() { workPairs },
            };
            zetTemplateBundlesList.Add(z1);
        }
        return zetTemplateBundlesList;
    }
    private SheetDbAndWorksheet CreateWorksheetPair(string tableCode, string zet)
    {
        var dbSheet = SelectSheetByZet(zet, tableCode);

        var worksheet = Workbook?.Worksheets[dbSheet?.SheetTabName?.Trim() ?? ""];
        return new SheetDbAndWorksheet { TableCode = tableCode, DbSheet = dbSheet, WorkSheet = worksheet };
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

    private void CreateOneZetSheetNew(ZetTemplateBundle zetTemplateBundle)
    {

        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var xxx = zetTemplateBundle.SheetsAndWorksheets.Any(ll=>ll.Any());

        var mergedTabName = string.IsNullOrEmpty(zetTemplateBundle.Zet)
            ? zetTemplateBundle.GroupTableCode
            : zetTemplateBundle.GroupTableCode + "#" + zetTemplateBundle.Zet;
        mergedTabName = mergedTabName.Replace(":", "_");

        var sqlZet = @" SELECT mem.MemberLabel  FROM mMember mem where MemberXBRLCode= @zetValue";
        var zetLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlZet, new { zetValue = zetTemplateBundle.Zet });
        var templateDesciption = string.IsNullOrEmpty(zetLabel)
            ? $"{zetTemplateBundle.TemplateDescription.Trim()}"
            : $"{zetTemplateBundle.TemplateDescription.Trim()}#{zetLabel}";


        var destSheet= DestWorkbook.Worksheets.Create(mergedTabName);
        
        var horizontalOffset = 1;
        var verticalOffset = 1;
        foreach (var ss in zetTemplateBundle.SheetsAndWorksheets)
        {

            foreach (var sheet in ss)
            {
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

    private void MergeOneTemplatePerZet(TemplateBundle templateTableBundle)
    {
        //One template may have many Zet dimensions(for business line or currency)
        // Merge sheets under the same template code if they have the same zet.
        //for example S.23.01.01.01, S.23.01.01.02 are under the same template S.23.01.01 with the same BL, OC dim value should be merged 
        //If they have no zet, they will also be merged
        // zet.Dim IN('BL','OC','CR')
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
        //one sheet per Zetvalue
        foreach (var zetBlValue in zetBLList)
        {
            var mergedRecord = MergeSheetsWithSameZet(templateTableBundle, zetBlValue);
            if (!mergedRecord.IsValid)
            {
                //null when there are no tables OR when there is just one
                continue;
            }


            //***fix
            //ExcelHelperFunctions.CreateHyperLink(mergedRecord.TabSheet, WorkbookStyles);
            var sheetsToRemove = mergedRecord.ChildrenSheetInstances.Select(sheet => sheet.SheetTabName.Trim()).ToList();
            //IndexSheetList.RemoveSheets(sheetsToRemove);
            //IndexSheetList.AddSheetRecord(new IndexSheetListItem(mergedRecord.TabSheet.SheetName, mergedRecord.SheetDescription));

        }
    }




    private MergedSheetRecord MergeSheetsWithSameZet(TemplateBundle templateBundle, string zetBLValue)
    {


        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var mergedTabName = string.IsNullOrEmpty(zetBLValue)
            ? templateBundle.TemplateCode
            : templateBundle.TemplateCode + "#" + zetBLValue;
        mergedTabName = mergedTabName.Replace(":", "_");

        var sqlZet = @" SELECT mem.MemberLabel  FROM mMember mem where MemberXBRLCode= @zetValue";
        var zetLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlZet, new { zetValue = zetBLValue });
        var templateDesciption = string.IsNullOrEmpty(zetLabel)
            ? $"{templateBundle.TemplateDescription.Trim()}"
            : $"{templateBundle.TemplateDescription.Trim()}#{zetLabel}";

        // each tableCode may have several dbSheets because of Zets other than business line and currency            
        List<List<TemplateSheetInstance>> dbSheets = new();
        var tableCodes = templateBundle.TableCodes;
        List<List<string>> tableCodesGraph = new();


        //A specialTemplate has horizontal tables in the same sheet.
        var specialTemplate = SpecialTemplate.Records.FirstOrDefault(special => special.TemplateCode == templateBundle.TemplateCode);

        //check for horizontal 
        if (specialTemplate is not null)
        {
            //the specialTemplate example here two horizontal lists with 3 tables each in this case
            // "S.05.02.01.01", "S.05.02.01.02", "S.05.02.01.03" , 
            // "S.05.02.01.04", "S.05.02.01.05", "S.05.02.01.06"                 
            foreach (var horizontalDbList in specialTemplate.TableCodes)
            {
                var horizontalDbTables = horizontalDbList.Select(tableCode => getOrCreateDbSheet(_documentId, tableCode, zetBLValue).FirstOrDefault()).ToList();
                dbSheets.Add(horizontalDbTables);
            }
        }
        else
        {
            //We can have more than one sheet for the same Business line, Currency , if the table has dimensions
            //******* THIS IS FUCKING change
            dbSheets = tableCodes.Select(tableCode => getOrCreateDbSheet(_documentId, tableCode, zetBLValue)).ToList();
        }

        var dbRealSheets = dbSheets.SelectMany(sheet => sheet).Where(sheet => sheet.TableID != -1).ToList();
        var countReal = dbRealSheets.Count;
        if (countReal == 0)
        {
            //If All the sheets in this template where created artificially 
            return new MergedSheetRecord(null, mergedTabName, dbRealSheets, false);
        }
        else if (countReal == 105)
        {
            //If just one sheet,  do not merge but copy the same sheet as merged
            var realSheet = GetSheetFromBook(dbRealSheets[0]);
            if (realSheet is null)
            {
                Console.WriteLine("xxx");
            }

            var newSheet = DestWorkbook.Worksheets.AddCopy(realSheet);
            //var newSheet = realSheet.CopySheet(mergedTabName);
            var allSheets = dbSheets.SelectMany(dbSheet => dbSheet).ToList();
            Console.WriteLine($"Createing Just one:{realSheet.Name}");
            return new MergedSheetRecord(newSheet, templateDesciption, allSheets, true);
        }

        //iSheets is a list of lists. Each inner list has the sheets which lay horizontally
        var iSheets = dbSheets.Select(tableCodeSheets => tableCodeSheets.Select(dbSheet => GetSheetFromBook(dbSheet)).ToList()).ToList();
        //var 

        ///**************************************************************************
        ///**** Create the Merged Sheet 

        var mergedSheet = CreateMergedSheet(iSheets, mergedTabName);

        //ExcelHelperFunctions.CreateHyperLink(mergedSheet, WorkbookStyles);

        var dbSheetFlatList = dbSheets.SelectMany(sheet => sheet).AsList();

        return new MergedSheetRecord(mergedSheet, templateDesciption, dbSheetFlatList, true);


        IWorksheet GetSheetFromBook(TemplateSheetInstance dbSheet)
        {

            var sheetTabName = dbSheet.SheetTabName.Trim();
            if (dbSheet.TableID == -1)
            {

                var sqlTbl = @"SELECT TemplateOrTableLabel FROM mTemplateOrTable tt where tt.TemplateOrTableCode= @tableCode and tt.TemplateOrTableType='BusinessTable'";
                var tableDescription = connectionEiopa.QueryFirstOrDefault<string>(sqlTbl, new { tableCode = dbSheet.TableCode }) ?? "";

                //********************* fix
                //var newSheet = DestExcelBook.CreateSheet(sheetTabName);
                var newSheet = DestWorkbook.Worksheets.Create(sheetTabName);

                //***************** fux
                //newSheet.CreateRow(0).CreateCell(0).SetCellValue($"{dbSheet.TableCode} - Empty Table");
                //newSheet.CreateRow(1).CreateCell(0).SetCellValue(templateBundle.TemplateDescription);
                //newSheet.CreateRow(2).CreateCell(0).SetCellValue(tableDescription);

                //ExcelHelperFunctions.CreateHyperLink(newSheet, WorkbookStyles);
                return newSheet;
            }

            var sheet = Workbook.Worksheets[dbSheet.SheetTabName.Trim()];

            return sheet;
        }

        List<TemplateSheetInstance> getOrCreateDbSheet(int documentId, string? tableCode, string zetValue)
        {
            //todo **need to rename this 
            //return a list of TemplateSheetInstance for the zetValue parameter
            using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
            using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

            var sqlSheetWithoutZet = @"
                    SELECT sheet.TemplateSheetId, sheet.SheetCode, sheet.TableCode,sheet.SheetTabName
                    FROM TemplateSheetInstance sheet
                    WHERE sheet.InstanceId = @documentId
                     AND sheet.TableCode= @tableCode                     
                ";

            var sqlSheetWithZet = @"
                    SELECT sheet.TemplateSheetId, sheet.SheetCode, sheet.TableCode,sheet.SheetTabName
                    FROM TemplateSheetInstance sheet
                    left outer join   SheetZetValue zet on zet.TemplateSheetId= sheet.TemplateSheetId
                    WHERE sheet.InstanceId = @documentId
                        AND sheet.TableCode= @tableCode                     
                        and zet.Dim in ('BL','OC','CR')
                        and zet.Value = @zetValue
                ";


            var sqlSheets = string.IsNullOrEmpty(zetValue) ? sqlSheetWithoutZet : sqlSheetWithZet;
            var result = connectionInsurance.Query<TemplateSheetInstance>(sqlSheets, new { documentId, tableCode, zetValue }).ToList();

            //if there are not sheets for this tablecode create an empty one which will be written as empty table
            if (result.Count == 0)
            {
                var new_sheetName = "new_" + tableCode.Trim() + "_" + zetValue.Trim();
                new_sheetName = new_sheetName.Replace(":", "_");
                var sheetInstance = new TemplateSheetInstance()
                {
                    TableCode = tableCode,
                    SheetTabName = new_sheetName,
                    TableID = -1
                };
                var newList = new List<TemplateSheetInstance>() { sheetInstance };
                return newList;
            }

            return result;
        }

    }




    private IWorksheet? CreateMergedSheet(List<List<IWorksheet>> sheetsToMerge, string destSheetName)
    {

        //Each iteration of the outer List will add VERTICALLY a list of HORIZONTAL sheets			

        //var destSheet = destSheetIdx == -1 ? DestExcelBook.CreateSheet(destSheetName) : DestExcelBook.GetSheetAt(destSheetIdx);

        if (DestWorkbook is null)
        {
            return null;
        }
        ///CREATE the empty Dest sheet
        var destSheet = DestWorkbook.Worksheets.Create(destSheetName);

        // horizontally a list of sheets(a horizontal may only have one)
        var rowOffset = 1;
        foreach (var sheetList in sheetsToMerge)
        {
            AppendHorizontalSheets(destSheet, sheetList, rowOffset, 1);
            rowOffset += 40;
        }

        return null;
    }


    private IWorksheet AppendHorizontalSheets(IWorksheet destSheet, List<IWorksheet> sheetsToMerge, int rowOffset, int colGap)
    {
        //add each sheet in the list  HORIZONTALLY one after the other
        if (sheetsToMerge.Count == 0)
        {
            return null;
        }
        var verticalOffset = rowOffset;
        var horizontalOffset = 1;
        foreach (var childSheet in sheetsToMerge)
        {

            if (childSheet is null)
            {
                continue;
            }
            var sheetLastRow = childSheet.Rows.Last().LastRow;
            var sheetLastCol = childSheet.Columns.Last().LastColumn;

            var copyRange = childSheet.Range[1, 1, sheetLastRow, sheetLastCol];
            var destRange = destSheet.Range[rowOffset, horizontalOffset];
            copyRange.CopyTo(destRange);

            verticalOffset = verticalOffset + 30;

        }
        return destSheet;
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
