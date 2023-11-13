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
		_logger = logger;
		_commonRoutines = commonRoutines;
		_excelBookWriter = excelBookWriter;

	}
	public int Run()
	{
		Console.WriteLine("started Excle");

		return 0;

	}



}
