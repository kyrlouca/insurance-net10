namespace ExcelWriter;
using Shared.CommonRoutines;
using Shared.HostRoutines;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.SharedHost;
using Shared.DataModels;
using System.Reflection.Metadata;
using Syncfusion.XlsIO.Implementation;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.Implementation.Collections;
using System;


public class ExcelBookWriter : IExcelBookWriter
{
	private readonly IParameterHandler _parameterHandler;
	ParameterData _parameterData = new();
	private readonly ILogger _logger;
	private readonly ICommonRoutines _commonRoutines;
	private IWorkbook? _destinationWorkbook;
	private IWorkbook? _originWorkbook; //template workbook
	int _documentId = 0;
	string debugTableCode = "";

	public ExcelBookWriter(IParameterHandler parametersHandler, ILogger logger, ICommonRoutines commonRoutines)
	{
		_parameterHandler = parametersHandler;
		_logger = logger;
		_commonRoutines = commonRoutines;
	}



	public bool CreateExcelBook(int documentId)
	{
		_documentId = documentId;
		_parameterData = _parameterHandler.GetParameterData();
		Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdgWH5fc3RdRWFfU0B0W0o=");

		//TestDebug();
		//return true;

		using var excelEngine = new ExcelEngine();


		(_originWorkbook, var originMessage) = ExcelHelperSync.OpenExistingExcelWorkbook(_parameterData.ExcelTemplateFile);
		if (_originWorkbook is null)
		{
			_logger.Error(originMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, originMessage);
			return false;
		}

		_destinationWorkbook = ExcelHelperSync.CreateExcelWorkbook(excelEngine);
		var errorMessage = "Cannot create excel stream file";
		if (_destinationWorkbook is null)
		{
			_logger.Error(errorMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, errorMessage);
			return false;
		}
		//////////////////////////////////////////////////////////////////
		//Code here

		var sheets = SelectExcelSheets().OrderBy(sh=>sh.TableCode);
		
		if (1 == 1)
		{
			
			int START_ROW = 1;
			int START_COL = 1;
			foreach (var sheet in sheets)
			{
				Console.WriteLine("process" + sheet?.TableCode);

				var template = GetTableOrTemplate(sheet.TableCode);
				if (template is null)
					continue;

				//the template has only 4 parts (S.04.01.01 )
				var filingSheetCode = string.Join(".", sheet.TableCode.Split(".").ToList().GetRange(0, 4));  
				var originSheet = _originWorkbook.Worksheets[filingSheetCode];
				if (originSheet is null) continue;
				var destSheet = _destinationWorkbook.Worksheets.Create(sheet.SheetTabName);


				///////////DESCRIPTION LABEL
				var _TC = template.TC;
				var descRange = CopyRangeToPosition(START_ROW, START_COL, originSheet, destSheet, _TC);
				if(descRange == null)
				{
					Console.WriteLine(sheet.TableCode);
				}


				//////////DATA RANGE 
				//for open tables template.TD does not include the key columns, so the starting column should extend to the left
				//get the left column of the description column 

				var _TD = template.TD;
				var originDataRange = originSheet.Range[_TD];
				if (originDataRange == null)
				{
					Console.WriteLine(sheet.TableCode);
				}
				if (sheet.IsOpenTable)
				{
					var originDataOriginalRange = originSheet.Range[_TD];
					var dStartRow = originDataOriginalRange.Row;
					var dStartCol = descRange?.Column ?? 0; //for open tables, the upper left col extends to the left 
					var dEndRow = originDataOriginalRange.LastRow;
					var dEndCol = originDataOriginalRange.LastColumn;
					originDataRange = originSheet.Range[dStartRow, dStartCol, dEndRow, dEndCol];
				}

				//Row and Col position for Destination Data Range must be fixed for both open and closed table
				//for open tables it will align with the description title
				var dataRowPos = START_ROW + 15;
				var dataColPos = sheet.IsOpenTable
					? START_COL
					: START_COL + 2;//make room for left label and row number
				
				var dataRange = CopyRangeToPosition(dataRowPos, dataColPos, originSheet, destSheet, _TD); //data
				if (dataRange == null)
				{
					Console.WriteLine(sheet.TableCode);
				}
				if (dataRange != null) dataRange.ColumnWidth = 30;


				/////////////LEFT Labels 
				var _TL = template.TL;
				if (!sheet.IsOpenTable)
				{
					var leftLabelRange = CopyRangeToPosition(dataRowPos, dataColPos - 2, originSheet, destSheet, _TL); //labels on the left							
					if (leftLabelRange != null) leftLabelRange.ColumnWidth = 50;
					if (leftLabelRange == null)
					{
						Console.WriteLine(sheet.TableCode);
					}
				}

				////////////TOP LABELS
				//Top labels must be above the destination data range
				var _TT = template.TT;
				if (_TT is null)
					continue;
				var originalTopLabelRng = originSheet.Range[_TT];
				

				var topLabelsRange = CopyRangeToPosition(dataRowPos - (originalTopLabelRng.LastRow - originalTopLabelRng.Row), dataColPos, originSheet, destSheet, _TT);
				if (topLabelsRange == null)
				{
					Console.WriteLine(sheet.TableCode);
				}



			}

		}


		//////////////////////////////////////////////////////////////////
		var (isSaveValid, saveMessage) = ExcelHelperSync.SaveWorkbook(_destinationWorkbook, _parameterData.FileName);
		if (!isSaveValid)
		{
			_logger.Error(saveMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, saveMessage);
			return false;
		}


		return true;

		static IRange? CopyRangeToPosition(int UpperLeftRow, int UpperLeftCol,  IWorksheet? originSheet, IWorksheet destSheet, string rangeStr)
		{
			try
			{

				var cOriginRange = originSheet.Range[rangeStr];
				var cOffset = ExcelHelperSync.OffsetRange(cOriginRange, UpperLeftRow, UpperLeftCol);
				IRange destRange = destSheet.Range[cOffset.StartRow, cOffset.StartCol, cOffset.EndRow, cOffset.EndCol];
				cOriginRange.CopyTo(destRange, ExcelCopyRangeOptions.All);
				return destRange;
			}
			catch (Exception ex)
			{
				//just go on
				return null;
			}
		}
	}
	private List<TemplateSheetInstance> SelectExcelSheets()
	{

		using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
		var sqlSheets = @"
			SELECT *, (SELECT COUNT(*) FROM TemplateSheetFact fact WHERE fact.TemplateSheetId= sheet.TemplateSheetId) AS FactsCounter
			FROM TemplateSheetInstance sheet
			WHERE
			  sheet.InstanceId = @_documentID
			ORDER BY sheet.SheetTabName   			";
		var sheets = connectionLocal.Query<TemplateSheetInstance>(sqlSheets, new { _documentId })
			.Where(sheet => sheet.FactsCounter > 0);

		if (!string.IsNullOrEmpty(debugTableCode))
		{
			sheets = sheets.Where(sheet => sheet.TableCode.Trim() == debugTableCode).ToList();
			Console.WriteLine($"**** Debugging-- Create ONLY the sheet: {debugTableCode} ");
		}
		return sheets.ToList();

	}

	private MTemplateOrTable? GetTableOrTemplate(string tableCode)
	{
		using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
		var sqlTemplate = @"
				SELECT * 
				FROM mTemplateOrTable tt
				WHERE 
				  1=1
				  AND TemplateOrTableType ='AnnotatedTable' 
				  AND tt.TemplateOrTableCode = @tableCode
				";
		var template = connectionEiopa.QueryFirstOrDefault<MTemplateOrTable>(sqlTemplate, new { tableCode });
		return template;

	}
	private void TestDebug()
	{
		using (ExcelEngine excelEngine = new ExcelEngine())
		{
			IApplication application = excelEngine.Excel;
			application.DefaultVersion = ExcelVersion.Xlsx;
			IWorkbook workbook = application.Workbooks.Create(1);
			IWorksheet worksheet = workbook.Worksheets[0];
			IRange range = worksheet[1, 4];

			//Hiding the range ‘D1’
			worksheet.ShowRange(range, false);
			IRange firstRange = worksheet[1, 1, 3, 3];
			IRange secondRange = worksheet[5, 5, 7, 7];
			RangesCollection rangeCollection = new RangesCollection(application, worksheet);
			rangeCollection.Add(firstRange);
			rangeCollection.Add(secondRange);

			//Hiding a collection of ranges
			worksheet.ShowRange(rangeCollection, false);

			//Saving the workbook as stream
			var filename = "C:\\Users\\kyrlo\\soft\\dotnet\\insurance-project\\TestingXbrl270\\axa.xlsx";
			FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite);
			workbook.SaveAs(stream);
			stream.Dispose();
		}
	}
}
