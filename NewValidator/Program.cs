// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using NewValidator;
using NewValidator.Hosting;
using Shared.SharedHost;
using System.Reflection;

//var dir = Directory.GetCurrentDirectory();
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var missingParam = CheckParams(args);
if (!string.IsNullOrEmpty(missingParam))
{
    //todo may need to change this
    var sample = @".\Validator.exe external-id=12 document-id=12 eiopa-version=IU282HOT";
    Console.WriteLine($"Invalid Params. Missing Parameter:{missingParam} See SAMPLE usage below");
    Console.WriteLine(sample);
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
    var mainApp = host.Services.GetService<IValidatorMain>();
    if( mainApp != null)
    {
        
        var assembly = Assembly.GetExecutingAssembly();
        Console.WriteLine($"Assembly Name: {assembly.GetName().Name}");
        Console.WriteLine($"Version: {assembly.GetName().Version}");
        var exitCode =  mainApp.Run();
        return exitCode; 
    }

    //var assembly = Assembly.GetExecutingAssembly();
    //Console.WriteLine($"Assembly Name: {assembly.GetName().Name}");
    //Console.WriteLine($"Version: {assembly.GetName().Version}");
    //var mainApp = host.Services.GetService<IValidatorMain>()
    //    ?? throw new InvalidOperationException("IValidatorMain service not found");
   
    //var exitCode = mainApp.Run();
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
}

return 0;

string? CheckParams(string[] args)
{
    var paramNames = new[] { "external-id", "eiopa-version", "document-id" };
    var missingParam = paramNames.FirstOrDefault(par => !args.Any(arg => arg.Contains(par)));
    return missingParam;
}
