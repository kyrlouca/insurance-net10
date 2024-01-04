namespace ExcelReader.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Serilog;
using Shared;
using Shared.HostParameters;
using Shared.CommonRoutines;
using Shared.SharedHost;
using Validator;
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
				var vv = 33;
				//depends on the value of DOTNET_ENVIRONMENT
			}

			//Make sure that appsettings files property is copy if newer. 
            //the configurations below would be automatically set but I prefer to set the order manually
            //the appsettings.json is NOT optional and will be used if the apsettings.Develop.json is not found
            
            config.AddEnvironmentVariables();
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            config.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);
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
             services.AddScoped<IValidator, Validator>();
             services.AddScoped<IValMainApp, ValMainApp>();
             //services.AddScoped<ISignatureMaker,SignatureMaker>();
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