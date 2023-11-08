// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel.DataAnnotations;


////////////////////
var mappings = new Dictionary<string, string> {
	{"--pc", "ConnectionStrings:PrimaryDatabaseConnection" }
};

//IHost hostConfig = CreateHostFromConfig(args, mappings);
//var yy = hostConfig.Services.GetService<IConfiguration>();
////////////////////

var hostFluent = CreateHostFluent(mappings, args);
var service = hostFluent.Services.GetService<IGetParams>();
var xx = service?.BuildData();


var x3 = hostFluent.Services.GetService<IConfiguration>();
var x31 = x3["TestDev"];
var x311 = x3.GetValue<string>("TestDev");

Console.WriteLine($"testDev:{x31}, testDev:{x311}");

var dir =Directory.GetCurrentDirectory();


var x323 = 32;

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
		services.AddScoped<INumberRepository, NumberRepository>();
		services.AddScoped<INumberService, NumberService>();		
		var vr = context.Configuration["eiopa-version"] ??"";
		services.Configure<VersionData>(context.Configuration.GetSection(vr));
		services.Configure<LoggerFiles>(context.Configuration.GetSection ("LoggerFiles"));
		services.AddScoped<IGetParams,GetParams>();
		services.AddScoped<ITest,Test>();
	})
	.Build();
		
	return app;

}

static IHost CreateHostFromConfig(string[] args, Dictionary<string, string> mappings)
{
	var abc = $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json";
	IConfigurationRoot conf = new ConfigurationBuilder()
		.AddJsonFile("appsettings.json")
		.AddJsonFile($"{abc}", optional: true, reloadOnChange: true)
		.AddEnvironmentVariables()
		.AddCommandLine(args, mappings)
		.Build()
		;
	var hostF = new HostBuilder()
		.ConfigureServices((hostContext, services) =>
		{
			// Add your services here
			services.AddScoped<INumberRepository, NumberRepository>();
		})
		.ConfigureAppConfiguration((hostContext, configBuilder) =>
		{
			configBuilder.AddConfiguration(conf); // Use the prebuilt IConfigurationRoot
			configBuilder.AddEnvironmentVariables();
		})
		.Build();
	return hostF;
}

public class NumberWorker
{
	private readonly INumberService _service;
	private readonly string _eiopaVersion;

	public NumberWorker(INumberService service, string eiopaVersion)
	{
		_service = service;
		_eiopaVersion = eiopaVersion;
	}

	public void PrintNumber()
	{
		var number = _service.GetPositiveNumber();
		Console.WriteLine($"My wonderful number is {number}");

		var str = _service.GetDecoratedString();
		Console.WriteLine($"dec: {_eiopaVersion}-{str}");
	}
}

public interface INumberRepository
{
	int GetNumber();
	string GetString();
}

public class NumberRepository : INumberRepository
{
	public int GetNumber()
	{
		return -42;
	}
	public string GetString()
	{
		return "abc";
	}
}


public interface INumberService
{
	int GetPositiveNumber();
	string GetDecoratedString();
}

public class NumberService : INumberService
{
	private readonly INumberRepository _repo;
	private readonly IConfiguration _configuration;
	private readonly IOptions<VersionData> _options;

	public NumberService(INumberRepository repo, IConfiguration configuration, IOptions<VersionData> options)
	{
		_repo = repo;
		_configuration = configuration;
		_options = options;
	}

	public int GetPositiveNumber()
	{
		int number = _repo.GetNumber();
		return Math.Abs(number);
	}

	public string GetDecoratedString()
	{
		var settingWithOptions = _options.Value.EiopaConnectionString;

		string dec = $"{settingWithOptions}-{_repo.GetString()}";
		var settingWithNested = _configuration.GetSection("Logging:LogLevel:Default").Value;

		//varj xx = (IConfigurationRoot) _configuration.pro

		return dec;
	}
}


public class ConnectionStringsXOld
{
	public string DefaultConnection { get; set; }
	public string DatabaseConnection { get; set; }
	public string UserDatabaseConnection { get; set; }
	public string PrimaryDatabaseConnection { get; set; }
	public string SecondaryDatabaseConnection { get; set; }
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
	public string EiopaVersion { get; set; }
	public string LoggerFile { get; set; }
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
	IConfiguration _configuration;
	
	public int id = 12;
	public string mak = "abc";
	public Test(IConfiguration configuration, GetParams getParams)
	{
		_configuration = configuration;
		
	}
	public string Run()
	{

		return "SS";
	}
}


public class GetParams : IGetParams
{
	IConfiguration _configuration;
	IOptions<VersionData> _optionsVersionData;
	IOptions<LoggerFiles> _optionsLoggerFiles;


	public GetParams(IConfiguration config, IOptions<VersionData> optionVersionData, IOptions<LoggerFiles>optionsLoggerFiles)
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
		var paramData = new ParameterData()
		{
			SystemConnectionString = _optionsVersionData.Value.SystemConnectionString,
			EiopaConnectionString = _optionsVersionData.Value.EiopaConnectionString,
			ExcelTemplateFile = _optionsVersionData.Value.ExcelTemplateFile,
			LoggerFile = _optionsLoggerFiles.Value.LoggerExcelReaderFile,
			EiopaVersion = _configuration["eiopa-version"] ?? "NF",
		};
		return paramData;
	}
}
