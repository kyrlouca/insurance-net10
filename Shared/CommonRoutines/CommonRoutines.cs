namespace Shared.CommonRoutines;
using Dapper;
using Microsoft.Data.SqlClient;
using Shared.DataModels;
using Shared.HostRoutines;
using Shared.SharedHost;
using System.Reflection;
using Serilog;


public class CommonRoutines : ICommonRoutines
{
	readonly ParameterData _parameterData;
	readonly IParameterHandler? _parameterHandler;
	readonly ILogger _logger;
	public CommonRoutines(IParameterHandler parameterHandler, ILogger logger)
	{
		_parameterHandler = parameterHandler;
		_parameterData = _parameterHandler?.GetParameterData() ?? new();
		_logger = logger;
	}

	public DocInstance GetDocInstance(int documentId)
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

	public  MModule? GetModuleByCodeNew( string moduleCode)
	{
		using var connectionPension = new SqlConnection(_parameterData.SystemConnectionString);
		using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

		//module code : {ari, qri, ara, ...}
		var sqlModule = "select ModuleCode, ModuleId, ModuleLabel from mModule mm where mm.ModuleCode = @ModuleCode";
		var module = connectionEiopa.QuerySingleOrDefault<MModule>(sqlModule, new { moduleCode = moduleCode.ToLower().Trim() });		
		return module;

	}


	public void CreateTransactionLog(int docInstanceId,MessageType messageType, string message)
	{		
		using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
		var tl = new LogTransactionModel ()
		{
			ExternalId = -1,
			PensionFundId = _parameterData.FundId,
			ModuleCode = _parameterData.ModuleCode,
			ApplicableYear = _parameterData.ApplicableYear,
			ApplicableQuarter = _parameterData.ApplicableQuarter,
			Message = message,
			UserId = _parameterData.UserId,
			ProgramCode = "EX",
			ProgramAction = ProgramAction.INS.ToString(),
			InstanceId = docInstanceId,
			MessageType = messageType.ToString()
			
		};
		var sqlInsert = @"
                INSERT INTO TransactionLog(ExternalId,PensionFundId, ModuleCode, ApplicableYear, ApplicableQuarter, Message, UserId, ProgramCode, ProgramAction,InstanceId,MessageType,FileName)
                VALUES(@externalId,@PensionFundId, @ModuleCode, @ApplicableYear, @ApplicableQuarter, @Message,  @UserId, @ProgramCode, @ProgramAction,@InstanceId,@MessageType,@FileName);
            ";
		var x = connectionInsurance.Execute(sqlInsert,tl );
	}

}
