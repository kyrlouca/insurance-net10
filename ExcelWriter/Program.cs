// See https://aka.ms/new-console-template for more information
using ExcelWriter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Shared.SharedHost;
using ExcelWriter.Hosting;
using System.Reflection;


//test 28/08
//var dir = Directory.GetCurrentDirectory();
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var missingParam = CheckParams(args);
if (!string.IsNullOrEmpty(missingParam))
{
	//todo may need to change this
	var sample = @".\ExcelWriter.exe external-id=12  eiopa-version=IU282  document-id=295 file-name=""C:\Users\kyrlo\Soft\eforos-Insurance-docs\Testing\TestingS14\cnp-7.xlsx";    
    Console.WriteLine($"Invalid Params. Missing Parameter:{missingParam} See SAMPLE usage below");
    Console.WriteLine(sample);
    throw new ArgumentException($"parameter missing:{missingParam}");
}  

using var host = HostCreator.CreateHostExplicit(args);
using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;



// ─────────────────────────────────────────────
// Execute the Main Application
// ─────────────────────────────────────────────
try
{
    var mainApp = host.Services.GetService<IExcelWriterMainApp>();
    mainApp?.Run();
    
    var assembly = Assembly.GetExecutingAssembly();
    Console.WriteLine($"Assembly Name: {assembly.GetName().Name}");
    Console.WriteLine($"Version: {assembly.GetName().Version}");

}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unhandled exception in MainApp: {ex}");
}

return 0;

string? CheckParams(string[] args)
{
	var paramNames = new[] { "external-id","eiopa-version", "document-id", "file-name" };
	var missingParam = paramNames.FirstOrDefault(par => !args.Any(arg => arg.Contains(par)));
	return missingParam;
}
