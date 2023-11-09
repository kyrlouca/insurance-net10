// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Serilog.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System;
using System.ComponentModel.DataAnnotations;
using Shared.DataModels;
using Shared.SharedHost;

var mappings = new Dictionary<string, string> {
	//to map nested or long command line parameters with simpler names
	{"--pc", "ConnectionStrings:PrimaryDatabaseConnection" }
};

//using will dispose when not needed
using var hostFluent = HostCreator.CreateHostFluent(mappings, args);

using var scope = hostFluent.Services.CreateScope();
var services = scope.ServiceProvider;

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

