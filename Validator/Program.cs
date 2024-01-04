using ExcelReader.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Shared.SharedHost;
using Validator;


var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var missingParam = CheckParams(args);
if (!string.IsNullOrEmpty(missingParam))
{
    var sample = """.\Validator.exe external-id=12 user-id=1 fund-id=33 eiopa-version=PU270 module-code="qri" year=2022 quarter=1 file-name="C:\Users\kyrlo\soft\dotnet\pension-project\TestingHR\eac.xbr" """;
    Console.WriteLine($"Invalid Params. Missing Parameter:{missingParam} See SAMPLE usage below");
    Console.WriteLine(sample);
    throw new ArgumentException($"parameter missing:{missingParam}");
}

using var host = HostCreator.CreateHostExplicit(args);
using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;


///////////////////////////////////////
///Execute the XbrlWriterMainApp
///////////////////////////////////////
try
{
    var xx = host.Services.GetService<IValMainApp>();
    host.Services.GetService<IValMainApp>()?.Run();
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
}

return 0;

string? CheckParams(string[] args)
{
    var paramNames = new[] { "external-id", "eiopa-version", "user-id", "fund-id", "module-code", "year", "quarter" };
    var missingParam = paramNames.FirstOrDefault(par => !args.Any(arg => arg.Contains(par)));
    return missingParam;
}
