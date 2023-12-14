namespace ExcelWriter.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Serilog;
using Shared.HostRoutines;
using Shared.CommonRoutines;
using Shared.SharedHost;

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
			//the configurations below would be automatically set but I rpofer to have the appsettings as optional
			config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
			config.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);
			config.AddEnvironmentVariables();
			config.AddCommandLine(args);
		})
		 .ConfigureServices((context, services) =>
		 {
			 //**!! vr -GetSection will get the values of the section corrsoponding to eiopa-versions (IU270, IU280, etc)
			 var vr = context.Configuration["eiopa-version"] ?? "";
			 services.Configure<VersionData>(context.Configuration.GetSection(vr));			 
			 services.AddScoped<ISqlFunctions, SqlFunctions>();
			 services.AddScoped<IParameterHandler, ParameterHandler>();
			 services.AddScoped<IExcelBookWriter,ExcelBookCreator>();
			 services.AddScoped<IExcelBookDataFiller, ExcelBookDataFiller>();
			 services.AddScoped<IExcelBookMerger, ExcelBookMerger>();
			 services.AddScoped<IMainApp, MainApp>();
             services.AddScoped<ICustomPensionStyler, CustomPensionStyler>();
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