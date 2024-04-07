namespace XbrlReader;
using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;

public class ReaderMainApp : IReaderMainApp
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private readonly IFactsCreator _factsCreator;
    private readonly IFactsDecorator _factsDecorator;


    public int id = 12;
    public ReaderMainApp(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions, IFactsCreator factsCreator, IFactsDecorator factsDecorator)
    {
        _parameterHandler = getParameters;
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _factsCreator = factsCreator;
        _factsDecorator = factsDecorator;

    }
    public int Run()
    {
        _parameterData = _parameterHandler.GetParameterData();

        var _documentId = 59;        

        Console.WriteLine($"Xbrl Reading and Loading file:{_parameterData.FileName}");
        
        if (1 == 1)
        {
            var (isHandleSuccess, handleMessage) = _factsCreator.HandleExistingDocuments();
            if (!isHandleSuccess)
            {
                _logger.Information(handleMessage);
                _SqlFunctions.CreateTransactionLog(MessageType.COMPLETE, handleMessage);
                return 1;
            }
        }

        if (1 == 1)
        {
            _documentId = _factsCreator.CreateLooseFacts();
            if (_documentId == 0)
            {
                return 1;
            }
        }

        
        if (1 == 1)
        {
            var res = _factsDecorator.DecorateFactsAndAssignToSheets(_documentId);
            if (res != 0)
            {
                return res;
            }
        }

        _SqlFunctions.UpdateDocumentStatus(_documentId, "L");
        var message = $"Xbrl Document Loaded Successfully:DocumentId= {_documentId}";
        _logger.Information(message);
        _SqlFunctions.CreateTransactionLog(MessageType.COMPLETE, message);
        return 0;
    }

}
