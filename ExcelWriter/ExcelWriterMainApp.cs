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
	public ExcelWriterMainApp(IParameterHandler getParameters, ILogger logger, ICustomPensionStyles2 customPensionStyles, ICommonRoutines commonRoutines, IExcelBookWriter excelBookWriter, IExcelBookDataFiller excelBookDataFiller, ITemplateMerger templateMerger)
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

		var xx=_parameterData.FileName.Trim();
		var file= Path.GetFileName(xx);
		var dir=Path.GetDirectoryName(xx);
		if(dir is null)
		{
            var message = $"Cannot find Directory for path {xx} :FundId: {_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
            _logger.Error(message);
            _commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);
            return 1;
        }
		var EmptyFilename = Path.Combine(dir, $"{file}_empty.xlsx");
		var filledFilename = Path.Combine(dir, $"{file}_filled.xlsx");
		var mergedFilename = Path.Combine(dir, $"{file}_merged.xlsx");
        
		
        if (1 == 1)
		{
			_excelBookWriter.CreateExcelBook(doc.InstanceId,EmptyFilename);
			if (string.IsNullOrEmpty(EmptyFilename))
			{
				return 1;
			}			
			var y = _excelBookDataFiller.PopulateExcelBook(doc.InstanceId, EmptyFilename, filledFilename);
	}        
        var x = _templateMerger.MergeTemplates(doc.InstanceId, filledFilename,mergedFilename);


		return 0;

	}



}
