// See https://aka.ms/new-console-template for more information
using XbrlReader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Shared.SharedHost;

//var dir = Directory.GetCurrentDirectory();
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var missingParam = CheckParams(args);
if (!string.IsNullOrEmpty(missingParam))
{
	Console.WriteLine($"parameter missing:{missingParam}");
	throw new ArgumentException($"parameter missing:{missingParam}");
}

using var hostFluent = HostCreator.CreateHostFluent(args);
using var scope = hostFluent.Services.CreateScope();
var services = scope.ServiceProvider;

///////////////////////////////////////
///Execute the MainApp
///////////////////////////////////////
try
{
	hostFluent.Services.GetService<IReaderMainApp>()?.Run();
}
catch (Exception ex)
{
	Console.WriteLine(ex.ToString());
	return 1;
}

return 0;

string? CheckParams(string[] args)
{
	var paramNames = new[] { "eiopa-version", "currency-batch-id", "user-id", "fund-id", "module-code", "year", "quarter", "file-name"};	
	var missingParam = paramNames.FirstOrDefault(par => !args.Any(arg=>arg.Contains(par)));
	return missingParam;
}
