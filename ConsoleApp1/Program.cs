// See https://aka.ms/new-console-template for more information
using ConsoleApp1;
using Microsoft.Extensions.DependencyInjection;
using Shared.SharedHost;


//"using" will dispose host when not needed
using var hostFluent = HostCreator.CreateHostFluent( args);

using var scope = hostFluent.Services.CreateScope();
var services = scope.ServiceProvider;

///////////////////////////////////////
///Execute the Mainapp
///////////////////////////////////////
try
{
	hostFluent.Services.GetService<IMyMainApp>()?.Run();
}
catch (Exception ex)
{
	Console.WriteLine(ex.ToString());
}
var dir = Directory.GetCurrentDirectory();

return;

