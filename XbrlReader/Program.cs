// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Shared.SharedHost;
using Syncfusion.XlsIO.Implementation;
using XbrlReader;

//var dir = Directory.GetCurrentDirectory();
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var missingParam = CheckParams(args);
if (!string.IsNullOrEmpty(missingParam))
{

    var sample = """.\XbrlReader.exe external-id=12 currency-batch-id=1 user-id=1 fund-id=69 eiopa-version=IU282 module-code="ars" year=2024 quarter=0 file-name="C:\Users\kyrlo\Soft\eforos-Insurance-docs\Testing\TestingS14\CNP Asfalistiki Annual QRTs 2024.xbrl""";
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
///
try
{
    var app = services.GetService<IReaderMainApp>();
    if (app is not null)
    {
        var exitCode = await app.Run();   // 👈 await async Run
        return exitCode;
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
    return 1;
}

//try
//{

    

//    hostFluent.Services.GetService<IReaderMainApp>()?.Run();
//}
//catch (Exception ex)
//{
//	Console.WriteLine(ex.ToString());
//	return 1;
//}

return 0;

string? CheckParams(string[] args)
{
	var paramNames = new[] { "external-id", "eiopa-version", "currency-batch-id", "user-id", "fund-id", "module-code", "year", "quarter", "file-name"};	
	var missingParam = paramNames.FirstOrDefault(par => !args.Any(arg=>arg.Contains(par)));
	return missingParam;
}
//test