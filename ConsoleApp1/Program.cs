// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Serilog.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System;
using System.ComponentModel.DataAnnotations;
using Shared.DataModels;
using Shared.SharedHost;

var mappings = new Dictionary<string, string> {
	//to map nested or long command line parameters with simpler names
	{"--pc", "ConnectionStrings:PrimaryDatabaseConnection" }
};

//using will dispose when not needed
using var hostFluent = CreateHostFluent(mappings, args);
//var service = hostFluent.Services.GetService<IGetParameters>();

using var scope = hostFluent.Services.CreateScope();
var services = scope.ServiceProvider;

try
{
	hostFluent.Services.GetService<IMyMainApp>()?.Run();
}
catch (Exception ex)
{
	Console.WriteLine(ex.ToString());
}
var dir = Directory.GetCurrentDirectory();

return;
static IHost CreateHostFluent(Dictionary<string, string>? mappings, string[] args)
{
	//CreateDefaultBuilder intiliazes and returns an instance of host builder (hover over) with  appsettings.environment.json, env varialbles, sercrets,logger
	var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
	var app = Host.CreateDefaultBuilder()
	.ConfigureAppConfiguration((context, config) =>
	{
		var prod = context.HostingEnvironment.IsProduction();
		if (context.HostingEnvironment.IsProduction())
		{
			//depends on the value of DOTNET_ENVIRONMENT
		}
		//the configurations below would be automatically set but I rpofer to have the appsettings as optional
		config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
		config.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);
		config.AddEnvironmentVariables();
		config.AddCommandLine(args, mappings);
	})
 	.ConfigureServices((context, services) =>
	{
		//**!! vr -GetSection will get the values of the section corrsoponding to eiopa-versions (IU270, IU280, etc)
		var vr = context.Configuration["eiopa-version"] ?? "";
		services.Configure<VersionData>(context.Configuration.GetSection(vr));
		//services.Configure<LoggerFiles>(context.Configuration.GetSection("LoggerFiles"));
		services.AddScoped<ISharedParameterHandler, SharedParameterHandler>();
		services.AddScoped<IMyMainApp, MyMainApp>();
	})
	.UseSerilog((hostingContext, loggerConfiguration) =>
	{
		loggerConfiguration
		.ReadFrom.Configuration(hostingContext.Configuration);
	})
	.Build();


	return app;

}


public class VersionDataxxx
{
	public string SystemConnectionString { get; set; }
	public string EiopaConnectionString { get; set; }
	public string ExcelTemplateFile { get; set; }
}

public class ParameterDataxxx
{
	public string environment { get; set; }
	public string SystemConnectionString { get; set; }
	public string EiopaConnectionString { get; set; }
	public string ExcelTemplateFile { get; set; }
	public string LoggerFile { get; set; }
	public string EiopaVersion { get; set; }
	public int UserId { get; set; }
	public int FundId { get; set; }
	public int CurrencyBatchId { get; set; }
	public int ApplicationYear { get; set; }
	public int ApplicationQuarter { get; set; }
	public string ModuleCode { get; set; }
	public string FileName { get; set; }

}


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

