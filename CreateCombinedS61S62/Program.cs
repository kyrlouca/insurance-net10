// See https://aka.ms/new-console-template for more information


using CreateCombinedS61S62;
using ErrorFileCreator.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Shared.SharedHost;
using System;
//using CreateCombinedS61S62



//var dir = Directory.GetCurrentDirectory();
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var missingParam = CheckParams(args);
if (!string.IsNullOrEmpty(missingParam))
{
    Console.WriteLine($"Invalid Params. Missing Parameter:{missingParam} See SAMPLE usage below");
    Console.WriteLine(@".\CreateCombinedS61S62.exe external-id=1,fund-id=89 eiopa-version=IU282 module-code=qrs, year=2024, quarter=4 ");
    throw new ArgumentException($"parameter missing:{missingParam}");
}

using var host = HostCreator.CreateHostExplicit(args);
using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;


///////////////////////////////////////
///Execute the MainApp
///////////////////////////////////////
Console.WriteLine($"{args[0]},{args[1]},{args[3]},{args[4]}");
try
{
    var loadMain = host.Services.GetService<ICreateCombinedRun>();
    loadMain?.Run();
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
}

return 0;

string? CheckParams(string[] args)
{
    
    var paramNames = new[] { "external-id","fund-id","eiopa-version","module-code", "year","quarter" };
    var missingParam = paramNames.FirstOrDefault(par => !args.Any(arg => arg.Contains(par)));
    return missingParam;
}
