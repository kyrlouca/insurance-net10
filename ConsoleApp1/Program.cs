// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel.DataAnnotations;



var mappings = new Dictionary<string, string> {
	{"--pc", "ConnectionStrings:PrimaryDatabaseConnection" }
};

 
var hostFluent = CreateHostFluent(mappings, args);
var service = hostFluent.Services.GetService<IGetParameters>();
var xx = service?.BuildData();

var dir =Directory.GetCurrentDirectory();




return;
static IHost CreateHostFluent(Dictionary<string, string>? mappings, string[] args)
{
	
	
	var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");	
	var app = Host.CreateDefaultBuilder()
	.ConfigureAppConfiguration((context, config) =>
	{		
		config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
		config.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);
		config.AddEnvironmentVariables();
		config.AddCommandLine(args, mappings);
	})
 	.ConfigureServices((context, services) =>
	{				
		var vr = context.Configuration["eiopa-version"] ??"";
		services.Configure<VersionData>(context.Configuration.GetSection(vr));
		services.Configure<LoggerFiles>(context.Configuration.GetSection ("LoggerFiles"));
		services.AddScoped<IGetParameters,GetParameters>();
		services.AddScoped<ITest,Test>();
	})
	.Build();
		
	return app;

}


public class VersionData
{
	public string SystemConnectionString { get; set; }
	public string EiopaConnectionString { get; set; }
	public string ExcelTemplateFile { get; set; }	
}

public class ParameterData
{
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

public class LoggerFiles
{
	public string LoggerXbrlFile { get; set; }
	public string LoggerXbrlReaderFile { get; set; }
	public string LoggerValidatorFile { get; set; }
	public string LoggerExcelReaderFile { get; set; }
	public string LoggerExcelWriterFile { get; set; }
	public string LoggerAggregatorFile { get; set; }
}



public class Test : ITest
{
	IGetParameters _getParameters;
	
	public int id = 12;	
	public Test( IGetParameters getParameters)
	{
		_getParameters = getParameters;
		
	}
	public string Run()
	{
		var xx = _getParameters.BuildData();
		return xx.EiopaVersion;
	}
}


public class GetParameters : IGetParameters
{
	IConfiguration _configuration;
	IOptions<VersionData> _optionsVersionData;
	IOptions<LoggerFiles> _optionsLoggerFiles;


	public GetParameters(IConfiguration config, IOptions<VersionData> optionVersionData, IOptions<LoggerFiles>optionsLoggerFiles)
	{
		_configuration = config;
		_optionsVersionData = optionVersionData;
		_optionsLoggerFiles = optionsLoggerFiles;
	}
	public ParameterData BuildData()
	{
		//var x32 = x3.GetSection("LoggerFiles").Get<LoggerFiles>();
		var xx = _configuration["TestDev"] ?? "N/F";
		var y = _configuration.GetSection("LoggerFiles").Get<LoggerFiles>();
		var parameterData = new ParameterData()
		{
			UserId = int.TryParse(_configuration["userid"], out int userid) ? userid : 0,
			FundId = int.TryParse(_configuration["fundid"], out int fundId) ? fundId : 0,
			CurrencyBatchId = int.TryParse(_configuration["currency-batch-id"], out int currencyBatchId) ? currencyBatchId : 0,			
			EiopaVersion = _configuration["eiopa-version"] ?? "NF",
			ModuleCode = _configuration["module-code"] ?? "NF",
			ApplicationYear = int.TryParse(_configuration["year"], out int year) ? year : 0,
			ApplicationQuarter = int.TryParse(_configuration["quarter"], out int quarter) ? quarter : 0,			
			SystemConnectionString = _optionsVersionData.Value.SystemConnectionString,
			EiopaConnectionString = _optionsVersionData.Value.EiopaConnectionString,
			ExcelTemplateFile = _optionsVersionData.Value.ExcelTemplateFile,
			LoggerFile = _optionsLoggerFiles.Value.LoggerExcelReaderFile,
			FileName= _configuration["module-code"] ?? "NF",




		};
		return parameterData;
	}
}
