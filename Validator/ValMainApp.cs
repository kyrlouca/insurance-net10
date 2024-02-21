namespace Validator;


using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.DataModels;
using Validations;
using Shared.SQLFunctions;

public class ValMainApp : IValMainApp
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private IOldDocumentValidator _documentValidator;




    public ValMainApp(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions, IOldDocumentValidator documentValidator)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _documentValidator = documentValidator;        
    }
    public int Run()
    {

        var smessage = $"Validator started documentId:{_parameterData.DocumentId} ";
        _logger.Information(smessage);
        _SqlFunctions.CreateTransactionLog(MessageType.INFO, smessage);


        var docId = 5004;
        var result = false;


        var res = _documentValidator.ValidateDocument();
        if (res == 0)
        {
            var fmessage = $"\nValidator Finished";
            _logger.Information(fmessage);
            _SqlFunctions.CreateTransactionLog(MessageType.COMPLETE, fmessage);
        }
        return res;

    }
}


