namespace ErrorFileCreator.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;
using Shared.ExcelHelperRoutines;
//using CurrencyLoad;

public class HostCreator
{


	public static IHost CreateHostExplicit( string[] args)
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
			//the configurations below would be automatically set but I prefer to have the appsettings as optional
			config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
			config.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);
			config.AddEnvironmentVariables();
			config.AddCommandLine(args);
		})
		 .ConfigureServices((context, services) =>
		 {
			 //context provides access to configuration which has all the  command line args,jsonfiles, env variables together
			 //**!! vr -GetSection will get the values of the section corrsoponding to eiopa-versions (IU270, IU280, etc)
			 //configure will get the json (typed as VersionData) corresponding to the eiopa version
			 //ParameterHandler can convert the configuration data to the object ParameterData
			 //pass ParameterHandler as parameter to any other service. You then execute  function to get parameterData
			 var vr = context.Configuration["eiopa-version"] ?? "";
			 services.Configure<VersionData>(context.Configuration.GetSection(vr));			 
			 services.AddScoped<ISqlFunctions, SqlFunctions>();
			 services.AddScoped<IParameterHandler, ParameterHandler>();
             services.AddScoped<ICustomPensionStyler, CustomPensionStyler>();
             //services.AddScoped<ICurrencyLoader, CurrencyLoader>();
             //services.AddScoped<ICurrencyLoadMain, CurrencyLoadMain>();    

         })
		.UseSerilog((hostingContext, loggerConfiguration) =>
		{
			loggerConfiguration
			.ReadFrom.Configuration(hostingContext.Configuration);
		})
		.Build();


		return app;

	}
}