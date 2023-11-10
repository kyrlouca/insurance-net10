namespace ConsoleApp1;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.CommonRoutines;
using Shared.DataModels;
using Shared.HostRoutines;
using Shared.SharedHost;
using System.Xml.Linq;

public class MyMainApp : IMyMainApp
{
	//do not pass serilog, pass a class with serilog
	private readonly IParameterHandler _parameterHandler;
	ParameterData _parameterData = new();
	private readonly ILogger _logger;
	private readonly ICommonRoutines _commonRoutines;
	MModule _mModule = new();
	XDocument? _xmlDoc;

	public int id = 12;
	public MyMainApp(IParameterHandler getParameters, ILogger logger, ICommonRoutines commonRoutines)
	{
		_parameterHandler = getParameters;
		_logger = logger;
		_commonRoutines = commonRoutines;
	}
	public int Run()
	{
		_parameterData = _parameterHandler.GetParameterData();


		var (isValid, message) = IsValidDocument();
		if (!isValid)
		{
			_logger.Error(message);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);
			return 1;
		}

		_xmlDoc = ParseXmlFile();
		if(_xmlDoc == null)
		{			
			return 1;
		}
		return 0;
	}

	private (bool isValid, string message) IsValidDocument()
	{
		_mModule = _commonRoutines.GetModuleByCodeNew(_parameterData.ModuleCode);
		if (_mModule == null)
		{
			var message = $"Invalid Module code : {_parameterData.ModuleCode}";
			return (false, message);
		}
		if (!File.Exists(_parameterData.FileName))
		{
			var message = $"File not FOUND : {_parameterData.FileName}";
			return (false, message);
		}

		return (true, "");
	}

	private XDocument? ParseXmlFile()
	{
		XDocument xmlDoc;

		using (TextReader sr = File.OpenText(_parameterData.FileName))  //utf-8 stream

			try
			{
				xmlDoc = XDocument.Load(sr);
			}
			catch (Exception e)
			{
				var message = $"XBRL file not valid : {_parameterData.FileName}";				
				Log.Error(e.Message);
				_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);												
				return null;
			}
		return xmlDoc;
	}
}
