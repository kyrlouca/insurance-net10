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

        var _documentId = 81;
        var filingsSubmitted = new List<string>();
        var filingsSubmittedxx = new List<string>()
        {
    "S.01.01",
    "S.01.02",
    "S.02.01",
    "S.05.01",
    "S.06.02",
    "S.06.04",
    "S.09.01",
    "S.14.02",
    "S.17.01",
    "S.17.03",
    "S.18.01",
    "S.19.01",
    "S.20.01",
    "S.21.01",
    "S.21.02",
    "S.21.03",
    "S.23.01",
    "S.23.02",
    "S.23.03",
    "S.23.04",
    "S.25.01",
    "S.26.01",
    "S.26.02",
    "S.26.04",
    "S.26.05",
    "S.26.06",
    "S.27.01",
    "S.28.01",
    "S.29.01",
    "S.29.02",
    "S.29.03",
    "S.29.04",
    "S.30.01",
    "S.30.02",
    "S.30.03",
    "S.30.04",
    "S.31.01",

        };

        Console.WriteLine($"Xbrl Reading and Loading file:{_parameterData.FileName}");
        
        if (1 == 0)
        {
            var (isHandleSuccess, handleMessage) = _factsCreator.HandleExistingDocuments();
            if (!isHandleSuccess)
            {
                _logger.Information(handleMessage);
                _SqlFunctions.CreateTransactionLog(MessageType.COMPLETE, handleMessage);
                return 1;
            }
        }

        if (1 == 0)
        {
            (_documentId, filingsSubmitted) = _factsCreator.CreateLooseFacts();            
            if (_documentId == 0)
            {
                return 1;
            }
        }

        
        if (1 == 1)
        {
            var res = _factsDecorator.DecorateFactsAndAssignToSheets(_documentId,filingsSubmitted);
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
