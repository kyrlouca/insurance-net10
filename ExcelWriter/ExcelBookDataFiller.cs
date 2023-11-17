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
using System.Drawing;
using Syncfusion.XlsIO.Parser.Biff_Records;
using static System.Net.Mime.MediaTypeNames;
using System.Text.RegularExpressions;
using System.Linq.Expressions;

public class ExcelBookDataFiller : IExcelBookDataFiller
{

	private readonly IParameterHandler _parameterHandler;
	ParameterData _parameterData = new();
	private readonly ILogger _logger;
	private readonly ICommonRoutines _commonRoutines;
	private IWorkbook? Workbook;	
	//private IWorkbook? _originWorkbook; //template workbook
	int _documentId = 0;
	string debugTableCode = "";

	public ExcelBookDataFiller(IParameterHandler parametersHandler, ILogger logger, ICommonRoutines commonRoutines)
	{
		_parameterHandler = parametersHandler;
		_logger = logger;
		_commonRoutines = commonRoutines;
	}

	public bool PopulateExcelBook(int documentId, string filename)
	{
		_documentId = documentId;
		_parameterData = _parameterHandler.GetParameterData();





		Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdgWH5fc3RdRWFfU0B0W0o=");

		using var excelEngine = new ExcelEngine();
		IApplication application = excelEngine.Excel;
		application.DefaultVersion = ExcelVersion.Xlsx;


		(Workbook, var originMessage) = ExcelHelperSync.OpenExistingExcelWorkbook(excelEngine, filename);
		if (Workbook is null)
		{
			_logger.Error(originMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, originMessage);
			return false;
		}
		
		

		var dbSheets = _commonRoutines.SelectTempateSheets(_documentId)
			.Where(sheet => !sheet.IsOpenTable);


		foreach (var dbSheet in dbSheets)
		{

			string cellVal = "init";
			if (dbSheet.SheetTabName is null)
			{
				continue;
			}
			var workSheet = Workbook.Worksheets[dbSheet.SheetTabName];

			var drDataName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_data"];
			var dataRange = drDataName.RefersToRange;

			var drTopName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_top"];
			var topRange = drTopName.RefersToRange;
			//var topRowCol = ExcelHelperSync.CreateRowColObject(topRange.AddressR1C1Local);
			
			

			var drLeftName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_left"];
			var leftRange = drLeftName.RefersToRange;
			//var leftRowCol= ExcelHelperSync.CreateRowColObject(leftRange.AddressR1C1Local);
			

			foreach (var dataRow in dataRange.Rows )
			{
				foreach (var cell in dataRow.Cells )
				{
					var dataCell = ExcelHelperSync.CreateRowColObject(cell.AddressR1C1Local);											
					var rowLabel = leftRange[dataCell.Row, leftRange.Column].Value;
					var colLabel = topRange[topRange.Row, dataCell.Col].Value;

					if (string.IsNullOrEmpty(rowLabel) || string.IsNullOrEmpty(colLabel))
					{
						continue;
					}
					var facts = FindFactsFromRowCol(dbSheet, rowLabel, colLabel);
					if (facts.Count == 0 || facts.Count>1)
					{
						continue;
					}

					var fact = facts.First(); //should'nt get more than one for open (no multicurrency facts)

					SaveCellValue(cell,fact);					

				}
			}

		}


		var savedFile = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\makaOUT1.xlsx";
		(var isValidSave, var destSaveMessage) = ExcelHelperSync.SaveWorkbook(Workbook, savedFile);
		if (!isValidSave)
		{
			_logger.Error(destSaveMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, destSaveMessage);
			return false;
		}

		return true;		
	}

	private void SaveCellValue(IRange cell,TemplateSheetFact fact)
	{

		var DataTypeUse = fact.DataTypeUse;
		switch (DataTypeUse)
		{
			case "D": //date
				cell.DateTime = fact.DateTimeValue;				
				break;
			case "B": //boolean
				cell.Boolean = fact.BooleanValue;				
				break;
			case "N": //Numeric (Decimal) 
			case "M": //monetary
				cell.Number =(double)fact.NumericValue;				
				break;
			case "P": //Percent
				cell.Number = (double)fact.NumericValue;				
				break;
			case "S": //String
				cell.Text = fact.TextValue.Trim();				
				break;
			case "E": // Enumeration/Code"					  
				var memDescription = XbrlCodeToValue(fact.TextValue);
				cell.Text = memDescription;				
				break;
			case "I": //integer
				cell.Number = (int)Math.Floor(fact.NumericValue);
				
				break;
			case "NULL"://fact is null                            
				break;
			default:
				cell.Text = "ERROR VALUE";
				break;
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

	private string XbrlCodeToValue(string xbrlValue) {
		using var connectionEiopaDb = new SqlConnection(_parameterData.EiopaConnectionString);

		var sqlMember = "select mem.MemberLabel from mMember mem where mem.MemberXBRLCode = @xbrlCode";
		var memDescription = connectionEiopaDb.QuerySingleOrDefault<string>(sqlMember, new { xbrlCode = xbrlValue})??"";
		return memDescription;
	}

}
