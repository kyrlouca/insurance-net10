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

    var sample = """.\XbrlReader.exe external-id=12 currency-batch-id=1 user-id=1 fund-id=33 eiopa-version=IU270 module-code="qra" year=2022 quarter=1 file-name="C:\Users\kyrlo\soft\dotnet\pension-project\TestingHR\eac.xbr" """;
    Console.WriteLine($"Invalid Params. Missing Parameter:{missingParam} See SAMPLE usage below");
    Console.WriteLine(sample);
    throw new ArgumentException($"parameter missing:{missingParam}");
	//aa
    
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
	var paramNames = new[] { "external-id", "eiopa-version", "currency-batch-id", "user-id", "fund-id", "module-code", "year", "quarter", "file-name"};	
	var missingParam = paramNames.FirstOrDefault(par => !args.Any(arg=>arg.Contains(par)));
	return missingParam;
}
