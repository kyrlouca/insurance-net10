namespace Shared.CommonRoutines;
using Dapper;
using Microsoft.Data.SqlClient;
using Shared.DataModels;
using Shared.HostRoutines;
using Shared.SharedHost;

public class CommonRoutines : ICommonRoutines
{
	readonly ParameterData _parameterData;
	readonly IParameterHandler? _parameterHandler;
	public CommonRoutines(IParameterHandler parameterHandler)
	{
		_parameterHandler = parameterHandler;
		_parameterData = _parameterHandler?.GetParameterData() ?? new();
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

}
