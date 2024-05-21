  // See https://aka.ms/new-console-template for more information
using NewValidator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Shared.SharedHost;
using NewValidator.Hosting;

//var dir = Directory.GetCurrentDirectory();
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var missingParam = CheckParams(args);
if (!string.IsNullOrEmpty(missingParam))
{
    //todo may need to change this
    var sample = @".\Validator.exe external-id=12 document-id=12 eiopa-version=IU280";
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
    host.Services.GetService<IValidatorMain>()?.Run();
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
