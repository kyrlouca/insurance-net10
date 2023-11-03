// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
Console.WriteLine("Hello, World!");


var host = CreateHost();
NumberWorker worker = ActivatorUtilities.CreateInstance<NumberWorker>(host.Services);
worker.PrintNumber();




static IHost CreateHost() {
	var app = Host.CreateDefaultBuilder()
		.ConfigureServices((context, services) =>
		{
			services.AddScoped<INumberRepository, NumberRepository>();
			services.AddScoped<INumberService, NumberService>();
		})
		.Build();


	return app;
	
}


public class NumberWorker
{
	private readonly INumberService _service;

	public NumberWorker(INumberService service) => _service = service;

	public void PrintNumber()
	{
		var number = _service.GetPositiveNumber();
		Console.WriteLine($"My wonderful number is {number}");
	}
}

public interface INumberRepository
{
	int GetNumber();
}

public class NumberRepository : INumberRepository
{
	public int GetNumber()
	{
		return -42;
	}
}


public interface INumberService
{
	int GetPositiveNumber();
}

public class NumberService : INumberService
{
	private readonly INumberRepository _repo;

	public NumberService(INumberRepository repo) => _repo = repo;

	public int GetPositiveNumber()
	{
		int number = _repo.GetNumber();
		return Math.Abs(number);
	}
}
