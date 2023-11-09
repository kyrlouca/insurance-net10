namespace ConsoleApp1;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.CommonRoutines;
using Shared.DataModels;
using Shared.HostRoutines;
using Shared.SharedHost;


public class MyMainApp : IMyMainApp
{
	//do not pass serilog, pass a class with serilog
	IParameterHandler _parameterHandler;
	ParameterData _parameterData =new();
	ILogger _logger;
	ICommonRoutines _commonRoutines;

	public int id = 12;
	public MyMainApp(IParameterHandler getParameters, ILogger logger , ICommonRoutines commonRoutines)
	{
		_parameterHandler = getParameters;		
		_logger = logger;
		_commonRoutines = commonRoutines;
	}
	public string Run()
	{
		_parameterData = _parameterHandler.GetParameterData();
		_logger.Information("helloffv");		
		_logger.Error("Erroffrvv");		
		var doc = GetDocument(9762);
		var xx = _commonRoutines.GetDocInstance(9762);
		return _parameterData.EiopaVersion;
	}

	private DocInstance GetDocument(int documentId)
	{
		var sqlGetDocument = @"
                    SELECT
                      doc.InstanceId
                     ,doc.PensionFundId
                     ,doc.ModuleId
                     ,doc.Status
                     ,doc.ModuleCode
                     ,doc.ApplicableYear
                     ,doc.ApplicableQuarter
                     ,doc.EntityCurrency
                     ,doc.UserId
                    FROM dbo.DocInstance doc
                    WHERE doc.InstanceId = @documentId
                    ";
		using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
		var doc = connectionInsurance.QuerySingleOrDefault<DocInstance>(sqlGetDocument, new { documentId });
		return doc;
	}

}
