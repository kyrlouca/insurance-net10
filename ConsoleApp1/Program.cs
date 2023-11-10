// See https://aka.ms/new-console-template for more information
using ConsoleApp1;
using Microsoft.Extensions.DependencyInjection;
using Shared.SharedHost;

var mappings = new Dictionary<string, string> {
	//to map nested or long command line parameters with simpler names
	{"--pc", "ConnectionStrings:PrimaryDatabaseConnection" }
};

//using will dispose when not needed
using var hostFluent = HostCreator.CreateHostFluent(mappings, args);

using var scope = hostFluent.Services.CreateScope();
var services = scope.ServiceProvider;

///////////////////////////////////////
///Execute the Mainapp
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

