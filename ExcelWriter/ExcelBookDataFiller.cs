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

		(Workbook, var originMessage) = ExcelHelperSync.OpenExistingExcelWorkbook(filename);
		if (Workbook is null)
		{
			_logger.Error(originMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, originMessage);
			return false;
		}

		foreach (var worksheet in Workbook.Worksheets.OrderBy(sheet=>sheet.Name))
		{
			var sheet = SelectTempateSheetInstance(sheet.SheetTabName);
			if (sheet is null)
			var drDataName = Workbook.Names[$"{worksheet.Name.Trim()}_data"];
			var dataRange = drDataName.RefersToRange;
			foreach (var dataRow in dataRange.Rows)
			{
				
				if (dataRow.Text == "abc")
				{
					dataRow.Text = "cde";
				}
			}

		}

		return true;
	}

	private TemplateSheetInstance? SelectTempateSheetInstance(string sheetTabName)
	{

		using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
		var sqlSheet = @"
			SELECT * 
			FROM TemplateSheetInstance sheet
			WHERE
			  sheet.InstanceId=@_documentId
			  AND sheet.SheetTabName<> @SheetTabName
			";
		var sheet = connectionLocal.QueryFirstOrDefault<TemplateSheetInstance>(sqlSheet, new { _documentId, sheetTabName });

		return sheet;
		

	}

}
