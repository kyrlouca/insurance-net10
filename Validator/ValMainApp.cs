namespace Validator;


using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.CommonRoutines;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.DataModels;


public class ValMainApp : IValMainApp
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private IValidator _validator;




    public ValMainApp(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions, IValidator eiopaXbrlDocument)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _validator = eiopaXbrlDocument;
        //_signatureMaker = signatureMaker;

    }
    public int Run()
    {

        var smessage = $"Xbrl Reader started  PensionFund:{_parameterData.FundId}  module:{_parameterData.ModuleCode} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter}  File:{_parameterData.FileName}-- ";
        _logger.Information(smessage);
        _SqlFunctions.CreateTransactionLog(1, MessageType.INFO, smessage);


        var docId = 5004;
        var result = false;


        var res = _validator.ValidateDocument();
        if (res == 0)
        {
            var fmessage = $"\nXbrl Document Created. File:{_parameterData.FileName}";
            _logger.Information(fmessage);
            _SqlFunctions.CreateTransactionLog(1, MessageType.COMPLETE, fmessage);
        }
        return res;

    }
}


