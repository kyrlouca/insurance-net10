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

public class ExcelBookWriter : IExcelBookWriter
{
	private readonly IParameterHandler _parameterHandler;
	ParameterData _parameterData = new();
	private readonly ILogger _logger;
	private readonly ICommonRoutines _commonRoutines;
	private IWorkbook? _workbook;
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
		using var excelEngine = new ExcelEngine();

		
		_workbook = ExcelHelperSync.CreateExcelWorkbook(excelEngine);

		//var isValid = true;
		var errorMessage = "Cannot create excel stream file";
		if (_workbook is null)
		{
			_logger.Error(errorMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, errorMessage);
			return false;
		}
		//////////////////////////////////////////////////////////////////
		//Code here
		var sheets = SelectExcelSheets();
		//IWorksheet? newWorksheet;
		foreach (var sheet in sheets)
		{
			//var sqlCount = @"select COUNT(*) as cnt from TemplateSheetFact fact where fact.TemplateSheetId = @TemplateSheetId";
			_workbook.Worksheets.Create(sheet.SheetTabName);			
		}


		//////////////////////////////////////////////////////////////////
		var (isSaveValid, saveMessage) = ExcelHelperSync.SaveWorkbook(_workbook, _parameterData.FileName);
		if (!isSaveValid)
		{
			_logger.Error(saveMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, saveMessage);
			return false;
		}


		return true;

	}
	private List<TemplateSheetInstance> SelectExcelSheets()
	{

		using var connectionEiopa = new SqlConnection(_parameterData.SystemConnectionString);
		var sqlSheets = @"
			SELECT *, (SELECT COUNT(*) FROM TemplateSheetFact fact WHERE fact.TemplateSheetId= sheet.TemplateSheetId) AS FactsCounter
			FROM TemplateSheetInstance sheet
			WHERE
			  sheet.InstanceId = @_documentID
			ORDER BY sheet.SheetTabName   			";
		var sheets = connectionEiopa.Query<TemplateSheetInstance>(sqlSheets, new { _documentId })
			.Where(sheet=>sheet.FactsCounter>0);		

		if (!string.IsNullOrEmpty(debugTableCode))
		{
			sheets = sheets.Where(sheet => sheet.TableCode.Trim() == debugTableCode).ToList();
			Console.WriteLine($"**** Debugging-- Create ONLY the sheet: {debugTableCode} ");
		}
		return sheets.ToList();

	}
}
