namespace NewValidator;

using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.CommonRoutines;
using Shared.SQLFunctions;

public class NewValidatorMain : INewValidatorMain
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private IDocumentValidator _documentValidator;
    //private readonly IExcelBookWriter _excelBookWriter;
    //private readonly IExcelBookDataFiller _excelBookDataFiller;
    //private readonly IExcelBookMerger _templateMerger;


    public int id = 12;
    public NewValidatorMain(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions, IDocumentValidator documentValidator)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _documentValidator = documentValidator;
        
    //_excelBookDataFiller = excelBookDataFiller;
    //_templateMerger = templateMerger;
}
    public int Run()
    {
        Console.WriteLine($"started Excel Writer - DocumentId:{_parameterData.DocumentId}");

        var doc = _SqlFunctions.SelectDocInstance(_parameterData.DocumentId);

        if (doc is null)
        {
            var message = $"Cannot Find DocInstance for fund:{_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
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



        if (1 == 1)
        {
            var smessage = $"Validator started documentId:{_parameterData.DocumentId} ";
            _logger.Information(smessage);
            _SqlFunctions.CreateTransactionLog(MessageType.INFO, smessage);

            

            //var res = 1;
            var res = _documentValidator.ValidateDocument();
            if (res == 0)
            {
                var fmessage = $"\nValidator Finished";
                _logger.Information(fmessage);
                _SqlFunctions.CreateTransactionLog(MessageType.COMPLETE, fmessage);
            }
            return res;
        }

        return 0;

    }
}





