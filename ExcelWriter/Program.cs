// See https://aka.ms/new-console-template for more information
using ExcelWriter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Shared.SharedHost;
using ExcelWriter.Hosting;

//var dir = Directory.GetCurrentDirectory();
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var missingParam = CheckParams(args);
if (!string.IsNullOrEmpty(missingParam))
{
	Console.WriteLine($"parameter missing:{missingParam}");
	throw new ArgumentException($"parameter missing:{missingParam}");
}

using var host = HostCreator.CreateHostExplicit(args);
using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;

///////////////////////////////////////
///Execute the MainApp
///////////////////////////////////////
try 
{	
	host.Services.GetService<IMainApp>()?.Run();
}
catch (Exception ex)
{
	Console.WriteLine(ex.ToString());
}

return 0;

string? CheckParams(string[] args)
{
	var paramNames = new[] { "eiopa-version", "fund-id", "module-code", "year", "quarter", "file-name" };
	var missingParam = paramNames.FirstOrDefault(par => !args.Any(arg => arg.Contains(par)));
	return missingParam;
}
