using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace Shared.SharedHost;



public class MyMainApp : IMyMainApp
{
	//do not pass serilog, pass a class with serilog
	ISharedParameterHandler _parameterHandler;

	Serilog.ILogger _logger;

	public int id = 12;
	public MyMainApp(ISharedParameterHandler getParameters, Serilog.ILogger logger)
	{
		_parameterHandler = getParameters;
		_logger = logger;
	}
	public string Run()
	{
		var xx = _parameterHandler.GetParameterData();
		_logger.Information("helloffv");
		_logger.Warning("warffnvv");
		_logger.Error("Erroffrvv");
		var yy = _parameterHandler.GetParameterData();
		var xy = yy.EiopaConnectionString;
		return xx.EiopaVersion;
	}
}
