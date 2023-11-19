namespace ExcelWriter;
using Serilog;
using Shared.CommonRoutines;
using Shared.HostRoutines;
using Shared.SharedHost;

public class ExcelWriterMainApp : IExcelWriterMainApp
{

	private readonly IParameterHandler _parameterHandler;
	private ParameterData _parameterData = new();
	private readonly ILogger _logger;
	private readonly ICommonRoutines _commonRoutines;
	private readonly IExcelBookWriter _excelBookWriter;
	private readonly IExcelBookDataFiller _excelBookDataFiller;
	private readonly ITemplateMerger _templateMerger;


	public int id = 12;
	public ExcelWriterMainApp(IParameterHandler getParameters, ILogger logger, ICommonRoutines commonRoutines, IExcelBookWriter excelBookWriter, IExcelBookDataFiller excelBookDataFiller, ITemplateMerger templateMerger)
	{
		_parameterHandler = getParameters;
		_parameterData = getParameters.GetParameterData();
		_logger = logger;
		_commonRoutines = commonRoutines;
		_excelBookWriter = excelBookWriter;
		_excelBookDataFiller = excelBookDataFiller;
		_templateMerger = templateMerger;
	}
	public int Run()
	{
		Console.WriteLine("started Excle");

		var doc = _commonRoutines.SelectDocInstance(_parameterData.FundId, _parameterData.ModuleCode, _parameterData.ApplicableYear, _parameterData.ApplicableQuarter);

		if (doc is null)
		{
			var message = $"Cannot Find DocInstance for fund:{_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
			_logger.Error(message);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);
			return 1;
		}

		if (doc.Status == "P")
		{
			var message = $"Document currently being Processed by another User. Document Id:{doc.InstanceId}";
			_logger.Error(message);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);
			return 1;
		}

		if (doc.EiopaVersion.Trim() != _parameterData.EiopaVersion)
		{
			var message = $"Eiopa Version Submitted :{_parameterData.EiopaVersion} different than Document eiopa version: {_parameterData.EiopaVersion} ";
			_logger.Error(message);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);
			return 1;
		}

        var sourceFilename = "C:\\Users\\kyrlo\\soft\\dotnet\\insurance-project\\TestingXbrl270\\maka.xlsx";
        var filledFilename = "C:\\Users\\kyrlo\\soft\\dotnet\\insurance-project\\TestingXbrl270\\makaFilled.xlsx";

        if (1 == 2)
		{
			//Create the empty excel file
			
			sourceFilename = _excelBookWriter.CreateExcelBook(doc.InstanceId);
			if (string.IsNullOrEmpty(sourceFilename))
			{
				return 1;
			}
			
			var y = _excelBookDataFiller.PopulateExcelBook(doc.InstanceId, sourceFilename, filledFilename);
		}
        var mergedFilename = "C:\\Users\\kyrlo\\soft\\dotnet\\insurance-project\\TestingXbrl270\\makaMerge.xlsx";
        var x = _templateMerger.MergeTemplates(doc.InstanceId, filledFilename,mergedFilename);


		return 0;

	}



}
