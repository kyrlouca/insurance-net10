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

IHost hostConfig = CreateHostFromConfig(args, mappings);

var yy = hostConfig.Services.GetService<IConfiguration>();
////////////////////



var hostFluent = CreateHostFluent(mappings, args);
NumberWorker worker = ActivatorUtilities.CreateInstance<NumberWorker>(hostFluent.Services, "abc");
var x3 = hostFluent.Services.GetService<IConfiguration>();
var x31 = x3["TestDev"];
var x311 = x3.GetValue<string>("TestDev");
var x32 = x3.GetSection("LoggerFiles");


Console.WriteLine($"testDev:{x31}, testDev:{x311}" );
worker.PrintNumber();



static IHost CreateHostFluent(Dictionary<string, string>? mappings, string[] args)
{
	//Create default builder will read automatically from json, env, command

	var apps = Host.CreateDefaultBuilder().Build();
	var s1 = apps.Services.GetService<IConfiguration>();	
	var sl1b = s1.GetSection("LoggerFiles").Get<LoggerFiles>();
	
	
	
	var sl1 = s1.GetSection("LoggerFiles");	
	var sl2 = sl1.GetValue<string>("LoggerXbrlFile");
	var sl3 = s1["TestDev"];
	var sl33 = s1.GetValue<string>("TestDev");

	
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
		var vr = context.Configuration["version"]??"";
		services.Configure<VersionData>(context.Configuration.GetSection(vr));
		services.Configure<LoggerFiles>(context.Configuration.GetSection ("LogerFiles"));		
	})
	.Build();

	
	
	
	var x2 = app.Services.GetService<LoggerFiles>();
	var xx = app.Services.GetService<IConfiguration>();
	

	//this works but is not binded to class
	var log2 = xx.GetSection("LoggerFiles").Get<LoggerFiles>();
	var logSectin = xx.GetSection("LoggerFiles");
	var l1 = logSectin.GetValue<string>("LoggerXbrlFile");

	Console.WriteLine($"tt:{xx["TestDev"]} loggerXbrl:{l1}");


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

	var var1 = conf["AllowedHosts"];
	var var2 = conf["ConnectionStrings:PrimaryDatabaseConnection"];
	var var3 = conf["TestDev"];
	var var4 = conf.Get<LoggerFiles>();


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
	private readonly string _version;

	public NumberWorker(INumberService service, string version)
	{
		_service = service;
		_version = version;
	}

	public void PrintNumber()
	{
		var number = _service.GetPositiveNumber();
		Console.WriteLine($"My wonderful number is {number}");

		var str = _service.GetDecoratedString();
		Console.WriteLine($"dec: {_version}-{str}");
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


public class LoggerFiles
{
	public string LoggerXbrlFile { get; set; }
	public string LoggerXbrlReaderFile { get; set; }
	public string LoggerValidatorFile { get; set; }
	public string LoggerExcelReaderFile { get; set; }
	public string LoggerExcelWriterFile { get; set; }
	public string LoggerAggregatorFile { get; set; }
}
