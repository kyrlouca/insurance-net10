namespace ConsoleApp1;
using Dapper;
using Microsoft.Data.SqlClient;
using Shared.DataModels;
using Shared.HostRoutines;

internal class Updater : IUpdater
{
	ParameterData _parameterData;
	public Updater(ParameterData parameterData)
	{
		_parameterData = parameterData;
	}

	public DocInstance GetDocument(int documentId)
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
