namespace NewValidator;

using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.CommonRoutines;
using Shared.SQLFunctions;
using System.Reflection.Metadata;
using System.Reflection;
using Shared.DataModels;

public class NewValidatorMain : INewValidatorMain
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger; 
    private readonly ISqlFunctions _SqlFunctions;
    private IDocumentValidator _documentValidator;
    
    public NewValidatorMain(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions, IDocumentValidator documentValidator)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _documentValidator = documentValidator;
    }

    public int Run()
    {
        //module-code="qrs"

        Console.WriteLine($"started Validating Document - DocumentId:{_parameterData.DocumentId}");

        var doc = _SqlFunctions.SelectDocInstance(_parameterData.DocumentId);

        if (doc is null)
        {
            var message = $"Cannot Find DocInstance  Id:{_parameterData.DocumentId} for fund:{_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
            return 1;
        }

        if (doc.Status.Trim() == "P")
        {
            var message = $"Document currently being Processed by another User. Document Id:{doc.InstanceId}";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
            return 1;
        }

        if (doc.EiopaVersion.Trim() != _parameterData.EiopaVersion)
        {
            var message = $"Eiopa Version Submitted :{_parameterData.EiopaVersion} different than Document eiopa version: {_parameterData.EiopaVersion} ";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
            return 1;
        }        
        _documentValidator.ValidateDocument();
        return 0;
    }
}





