// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel.DataAnnotations;


var mappings = new Dictionary<string, string> {
	{"--pc", "ConnectionStrings:PrimaryDatabaseConnection" }
};
	

IConfigurationRoot conf = new ConfigurationBuilder()	
	.AddJsonFile("appsettings.json")
	.AddEnvironmentVariables()
	.AddCommandLine(args,mappings)
	.Build()
	;
var var1 = conf["AllowedHosts"];
var var2 =conf["ConnectionStrings:PrimaryDatabaseConnection"] ;


var host = CreateHost();
NumberWorker worker = ActivatorUtilities.CreateInstance<NumberWorker>(host.Services, "abc");

worker.PrintNumber();




static IHost CreateHost()
{
	

	var app = Host.CreateDefaultBuilder()		
		.ConfigureAppConfiguration((context, services)=>services.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DEVELEOP")}.json", true,true))
		.ConfigureAppConfiguration(services => services.AddEnvironmentVariables())
		.ConfigureServices((context, services) =>
		{
			services.AddScoped<INumberRepository, NumberRepository>();
			services.AddScoped<INumberService, NumberService>();
			services.Configure<ConnectionStringsX>(context.Configuration.GetSection("ConnectionStrings"));			
		})
		.Build();
	return app;

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
	private readonly IOptions<ConnectionStringsX> _options;

	public NumberService(INumberRepository repo, IConfiguration configuration, IOptions<ConnectionStringsX> options)
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
		var settingWithOptions = _options.Value.PrimaryDatabaseConnection;

		string dec = $"{settingWithOptions}-{_repo.GetString()}";
		var settingWithNested = _configuration.GetSection("Logging:LogLevel:Default").Value;


		return dec;
	}
}


public class ConnectionStringsX
{
	public string DefaultConnection { get; set; }
	public string DatabaseConnection { get; set; }
	public string UserDatabaseConnection { get; set; }
	public string PrimaryDatabaseConnection { get; set; }
	public string SecondaryDatabaseConnection { get; set; }
}
