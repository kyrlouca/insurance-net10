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
			var (topRangeRow, topRangeCol) = GetRowCol(topRange.AddressR1C1);
			

			var drLeftName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_left"];
			var leftRange = drLeftName.RefersToRange;
			var (leftRangeRow, leftRangeCol) = GetRowCol(leftRange.AddressR1C1);

			foreach (var dataRow in dataRange.Rows)
			{
				foreach (var cell in dataRow.Cells)
				{
					//application.RangeIndexerMode = ExcelRangeIndexerMode.Relative;

					var (row, col) = GetRowCol(cell.AddressR1C1Local);
					var rowLabel = leftRange[row, leftRangeCol].Value;
					var colLabel = leftRange[topRangeRow, col].Value;


					if (string.IsNullOrEmpty(rowLabel) || string.IsNullOrEmpty(colLabel))
					{
						continue;
					}
					var facts = FindFactsFromRowCol(dbSheet, rowLabel, colLabel);
					if (facts.Count == 0)
					{
						continue;
					}

					var fact = facts.First(); //should'nt get more than one for open (no multicurrency facts)
					cell.Text = fact.TextValue;

				}
			}

		}

		return true;

		(int row, int col) GetRowCol(string addressR1C1)
		{
			var rg = new Regex("R(\\d*)C(\\d*)");
			//var rg = new Regex("R(\\d*)C(\\d*)");
			var match = rg.Match(addressR1C1);
			if (!match.Success) return (0, 0);
			return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
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


}
