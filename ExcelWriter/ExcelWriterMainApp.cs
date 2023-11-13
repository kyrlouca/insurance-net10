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




	public int id = 12;
	public ExcelWriterMainApp(IParameterHandler getParameters, ILogger logger, ICommonRoutines commonRoutines, IExcelBookWriter excelBookWriter)
	{
		_parameterHandler = getParameters;
		_parameterData = getParameters.GetParameterData();
		_logger = logger;
		_commonRoutines = commonRoutines;
		_excelBookWriter = excelBookWriter;

	}
	public int Run()
	{
		Console.WriteLine("started Excle");
		
		var doc = _commonRoutines.GetDocInstance(_parameterData.FundId, _parameterData.ModuleCode, _parameterData.ApplicableYear, _parameterData.ApplicableQuarter);

		if (doc is null)
		{
			var message = $"Cannot Find DocInstance for fund:{_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
			_logger.Error(message);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);
			return 1;					
		}

		if (doc.Status == "P")
		{
			var message = $"Cannot create Excel because of Invalid Document Status:{doc.Status}";
			_logger.Error(message);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);
			return 1;
		}

		if (doc.EiopaVersion.Trim()!= _parameterData.EiopaVersion)
		{
			var message = $"Eipa Version Submitted :{_parameterData.EiopaVersion} different than Document eiopa version: {_parameterData.EiopaVersion} ";
			_logger.Error(message);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);
			return 1;
		}

		
		return 0;

	}



}
