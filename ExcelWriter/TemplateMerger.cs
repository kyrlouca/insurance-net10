namespace ExcelWriter;
using Shared.CommonRoutines;
using Shared.HostRoutines;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.SharedHost;
using Shared.DataModels;
using ExcelWriter.DataModels;
using System.Reflection.Metadata;
using Syncfusion.XlsIO.Implementation;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.Implementation.Collections;
using System;
using System.Drawing;
using Syncfusion.XlsIO.Parser.Biff_Records;
using static System.Net.Mime.MediaTypeNames;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Reflection;

public class TemplateMerger
{

	private readonly IParameterHandler _parameterHandler;
	ParameterData _parameterData = new();
	private readonly ILogger _logger;
	private readonly ICommonRoutines _commonRoutines;
	private IWorkbook? Workbook;
	//private IWorkbook? _originWorkbook; //template workbook
	int _documentId = 0;
	string debugTableCode = "";

	public TemplateMerger(IParameterHandler parametersHandler, ILogger logger, ICommonRoutines commonRoutines)
	{
		_parameterHandler = parametersHandler;
		_logger = logger;
		_commonRoutines = commonRoutines;
	}

	public bool MergeTemplates(int documentId, string filename)
	{
		_documentId = documentId;
		_parameterData = _parameterHandler.GetParameterData();


		Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdgWH5fc3RdRWFfU0B0W0o=");

		using var excelEngine = new ExcelEngine();
		IApplication application = excelEngine.Excel;
		application.DefaultVersion = ExcelVersion.Xlsx;

		(Workbook, var originMessage) = ExcelWriterHelper.OpenExistingExcelWorkbook(excelEngine, filename);
		if (Workbook is null)
		{
			_logger.Error(originMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, originMessage);
			return false;
		}

		var dbClosedSheets = _commonRoutines.SelectTempateSheets(_documentId)
			.Where(sheet => !sheet.IsOpenTable);
		foreach (var dbClosedSheet in dbClosedSheets)
		{
            Console.WriteLine($"Closed:{dbClosedSheet.SheetCode}");
        
		
		}



		//Merge sheets for each templeate Code (3 digit code) based on dimension .(line of business BL and currency OC)
		//If there is a TemplateBundel, the Merged sheet can merge horizontally and vertically.
		//A bundle contains the template code and a list of horizontal tableCodes lists like {S.19.01.01, {S.19.01.01.01,19.01.01.02,etc},{19.01.01.08}}
		var templates = CreateTemplateTableBundlesForModule( _parameterData.ModuleCode);
		//templates = templates.Where(bundle => (bundle.TemplateCode == "S.05.02.01" || bundle.TemplateCode == "S.19.01.01")).ToList();

		foreach (var template in templates)
		{
			MergeOneTemplate(template);
		}


		var savedFile = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\makaMerger.xlsx";
		(var isValidSave, var destSaveMessage) = ExcelWriterHelper.SaveWorkbook(Workbook, savedFile);
		if (!isValidSave)
		{
			_logger.Error(destSaveMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, destSaveMessage);
			return false;
		}

		return true;
	}




	private void MergeOneTemplate(TemplateBundle templateTableBundle)
	{
		//One template may have many Zet dimensions(for business line or currency)
		using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
		using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
		//currency is can be CD,CR,OC but for s.19 is oc

		var sqlZet = @"
                    SELECT zet.value
                    FROM TemplateSheetInstance sheet
                    JOIN SheetZetValue zet ON zet.TemplateSheetId = sheet.TemplateSheetId
                    WHERE sheet.InstanceId = @documentId
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
		foreach (var zetBlValue in zetBLList)
		{
			var mergedRecord = MergeOneZetTemplate(templateTableBundle, zetBlValue);
			if (!mergedRecord.IsValid)
			{
				//null when there are no tables OR when there is just one
				continue;
			}

			/// ****Fix
			//MakeBlancCells(mergedRecord.TabSheet);

			//***fix
			//ExcelHelperFunctions.CreateHyperLink(mergedRecord.TabSheet, WorkbookStyles);
			var sheetsToRemove = mergedRecord.ChildrenSheetInstances.Select(sheet => sheet.SheetTabName.Trim()).ToList();
			//IndexSheetList.RemoveSheets(sheetsToRemove);
			//IndexSheetList.AddSheetRecord(new IndexSheetListItem(mergedRecord.TabSheet.SheetName, mergedRecord.SheetDescription));

		}
	}



	private List<TemplateSheetFact> FindFactsFromRowCol(TemplateSheetInstance sheet, string row, string col)
	{
		//more than one fact with the same row,col but with different currency
		var sqlFact =
	  @"
		SELECT *                  
		FROM dbo.TemplateSheetFact fact
		WHERE
		  fact.TemplateSheetId = @sheetId
		  AND fact.Row = @row
		  AND fact.Col = @col                                    
	";

		using var connectionLocalDb = new SqlConnection(_parameterData.SystemConnectionString);
		var facts = connectionLocalDb.Query<TemplateSheetFact>(sqlFact, new { sheetId = sheet.TemplateSheetId, row, col }).ToList();
		return facts;
	}

	private TemplateSheetFact? FindFactFromRowColZet(TemplateSheetInstance sheet, string row, string col, string zet)
	{
		//more than one fact with the same row,col but with different currency
		var sqlFact =
	  @"
            SELECT *    
			FROM dbo.TemplateSheetFact fact
			WHERE
			  fact.TemplateSheetId = @sheetId
			  AND fact.Row = @row
			  AND fact.Col = @col
			  AND fact.Zet = @zet                
     ";
		using var connectionLocalDb = new SqlConnection(_parameterData.SystemConnectionString);
		var fact = connectionLocalDb.QueryFirstOrDefault<TemplateSheetFact>(sqlFact, new { sheetId = sheet.TemplateSheetId, row, col, zet });
		return fact;
	}

	private string XbrlCodeToValue(string xbrlValue)
	{
		using var connectionEiopaDb = new SqlConnection(_parameterData.EiopaConnectionString);

		var sqlMember = "select mem.MemberLabel from mMember mem where mem.MemberXBRLCode = @xbrlCode";
		var memDescription = connectionEiopaDb.QuerySingleOrDefault<string>(sqlMember, new { xbrlCode = xbrlValue }) ?? "";
		return memDescription;
	}




	private MergedSheetRecord MergeOneZetTemplate(TemplateBundle templateBundle, string zetBLValue)
	{


		using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
		using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

		var mergedTabName = string.IsNullOrEmpty(zetBLValue)
			? templateBundle.TemplateCode
			: templateBundle.TemplateCode + " # " + zetBLValue;
		mergedTabName = mergedTabName.Replace(":", "_");

		var sqlZet = @" SELECT mem.MemberLabel  FROM mMember mem where MemberXBRLCode= @zetValue";
		var zetLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlZet, new { zetValue = zetBLValue });
		var templateDesciption = string.IsNullOrEmpty(zetLabel)
			? $"{templateBundle.TemplateDescription.Trim()}"
			: $"{templateBundle.TemplateDescription.Trim()} # {zetLabel}";

		// each tableCode may have several dbSheets because of Zets other than business line and currency            
		List<List<TemplateSheetInstance>> dbSheets = new();
		var tableCodes = templateBundle.TableCodes;

		//A specialTemplate has horizontal tables in the same sheet.
		var specialTemplate = SpecialTemplate.Records.FirstOrDefault(special => special.TemplateCode == templateBundle.TemplateCode);

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
			//Need to merge also
			dbSheets = tableCodes.Select(tableCode => getOrCreateDbSheet( _documentId, tableCode, zetBLValue)).ToList();
		}

		var dbRealSheets = dbSheets.SelectMany(sheet => sheet).Where(sheet => sheet.TableID != -1).ToList();
		var countReal = dbRealSheets.Count;
		if (countReal == 0)
		{
			//If All the sheets in this template where created artificially OR just one table 
			return new MergedSheetRecord(null, mergedTabName, dbRealSheets, false);
		}
		else if (countReal == 1)
		{
			//If just one sheet,  do not merge but copy the same sheet as merged
			var realSheet = GetSheetFromBook(dbRealSheets[0]);

			//************************			
			IWorksheet newSheet = Workbook.Worksheets.Create(mergedTabName);
			var allSheets = dbSheets.SelectMany(dbSheet => dbSheet).ToList();
			return new MergedSheetRecord(newSheet, templateDesciption, allSheets, true);
		}

		//iSheets is a list of lists. Each inner list has the sheets which lay horizontally
		var iSheets = dbSheets.Select(tableCodeSheets => tableCodeSheets.Select(dbSheet => GetSheetFromBook(dbSheet)).ToList()).ToList();


		///**************************************************************************
		///**** Create the Merged Sheet 
		var mergedSheet = CreateMergedSheet(iSheets, mergedTabName);


		//this was grayed out
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
				var newSheet = Workbook.Worksheets.AddCopy(1);
				
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

		List<TemplateSheetInstance> getOrCreateDbSheet( int documentId, string? tableCode, string zetValue)
		{
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


	private IWorksheet CreateMergedSheet(List<List<IWorksheet>> sheetsToMerge, string destSheetName)
	{

		//Each iteration of the outer List will add VERTICALLY a list of HORIZONTAL sheets			

		//var destSheet = destSheetIdx == -1 ? DestExcelBook.CreateSheet(destSheetName) : DestExcelBook.GetSheetAt(destSheetIdx);
		var destSheet = Workbook.Worksheets[destSheetName] is null ? Workbook.Worksheets.Create(destSheetName) : Workbook.Worksheets[destSheetName];


		var rowGap = 4;


		//write horizontally a list of sheets
		var rowOffset = 0;
		foreach (var sheetList in sheetsToMerge)
		{
			AppendHorizontalSheets(sheetList, destSheet, rowOffset, 1);
			//**** fixed
			//rowOffset = destSheet.LastRowNum + rowGap;
		}

		//set columns width            
		//var firstRow = destSheet.Rows[ GetRow(0) ?? destSheet.CreateRow(0);


		for (int i = 0; i < 70; i++)
		{
			//var cell = firstRow?.GetCell(i) ?? firstRow?.CreateCell(i);
		}
		
		//**Fucking a
		if (1 == 1)
		{
			destSheet.SetColumnWidth(0, 12000);
			destSheet.SetColumnWidth(1, 2000);
			//for (var j = 2; j < firstRow?.Cells.Count; j++)
			//{
			//	destSheet.SetColumnWidth(j, 5000);
			//}
		}
		//*************
		return destSheet;
	}


	private List<TemplateBundle> CreateTemplateTableBundlesForModule( string moduleCode)
	{
		using var connectionEiopa = new SqlConnection();
		using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

		var templateTableBundles = new List<TemplateBundle>();

		var sqlTables = @"
                SELECT va.TemplateOrTableCode,va.TemplateOrTableLabel
                FROM mModuleBusinessTemplate mbt
                LEFT OUTER JOIN mTemplateOrTable va ON va.TemplateOrTableID = mbt.BusinessTemplateID
                LEFT OUTER JOIN mModule mod ON mbt.ModuleID = mod.ModuleID
                WHERE 1 = 1
                    and TemplateOrTableCode like 'S.%'
                    AND mod.ModuleID = @moduleId
                ORDER BY mod.ModuleID
                ";
		//todo make it empty list if null
		var templates = connectionEiopa.Query<MTemplateOrTable>(sqlTables, new { moduleCode });



		foreach (var template in templates)
		{
			var sqlTableCodes = @"
                SELECT  tab.TableCode
                FROM mTemplateOrTable va
                LEFT OUTER JOIN mTemplateOrTable bu ON bu.ParentTemplateOrTableID = va.TemplateOrTableID
                LEFT OUTER JOIN mTemplateOrTable anno ON anno.ParentTemplateOrTableID = bu.TemplateOrTableID
                LEFT OUTER JOIN mTaxonomyTable taxo ON taxo.AnnotatedTableID = anno.TemplateOrTableID
                LEFT OUTER JOIN mTable tab ON tab.TableID = taxo.TableID
                WHERE 1 = 1
                    AND va.TemplateOrTableCode = @templateCode
                ORDER BY tab.TableCode

                ";
			var tableCodes = connectionEiopa.Query<string>(sqlTableCodes, new { templateCode = template.TemplateOrTableCode })?.ToList() ?? new List<string>();
			templateTableBundles.Add(new TemplateBundle(template.TemplateOrTableCode, template.TemplateOrTableLabel, tableCodes));

			//var sheets= connectionInsurance.Query<TemplateSheetInstance>(sqlSheets, new { documentId,bCode }).ToList()?? new List<TemplateSheetInstance>();
			//TemplateCodes.Add(new BusinessTableBundle(tableCode, sheets));                



		}
		return templateTableBundles;

	}

	private  IWorksheet AppendHorizontalSheets(List<IWorksheet> sheetsToMerge, IWorksheet  destSheet, int rowOffset, int colGap)
	{
		//add each sheet in the list  HORIZONTALLY one after the other
		//var colGap = 2;
		var totalColOffset = 0;
		foreach (var childSheet in sheetsToMerge)
		{
			if (childSheet is null)
			{
				continue;
			}
			
			//*** fix
			//ExcelHelperFunctions.CopyManyRowsSameBook(childSheet, destSheet, childSheet.FirstRowNum, childSheet.LastRowNum, true, rowOffset, totalColOffset);
			//var childColOffset = ExcelHelperFunctions.GetMaxNumberOfColumns(childSheet, childSheet.FirstRowNum, childSheet.LastRowNum);
			//totalColOffset += childColOffset + colGap;

		}
		return destSheet;
	}
}
