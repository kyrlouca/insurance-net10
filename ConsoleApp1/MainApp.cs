namespace XbrlReader;
using Serilog;
using Shared.CommonRoutines;
using Shared.HostRoutines;
using Shared.SharedHost;

public class MyMainApp : IMyMainApp
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private readonly IFactsProcessor _factsProcessor;
    private readonly IFactsCreator _factsCreator;
    private readonly IFactsMover _factsMover;


    public int id = 12;
    public MyMainApp(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions, IFactsCreator factsCreator, IFactsProcessor factsProcessor, IFactsMover factsMover)
    {
        _parameterHandler = getParameters;
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _factsProcessor = factsProcessor;
        _factsCreator = factsCreator;
        _factsMover = factsMover;

    }
    public int Run()
    {
        _parameterData = _parameterHandler.GetParameterData();

        var _documentId = 13032;
        var filingsSubmitted = new List<string>()
        {
            "S.06.02",
        };

        if (1 == 2)
        {
            (_documentId, filingsSubmitted) = _factsCreator.CreateLooseFacts();
            if (_documentId == 0)
            {
                return 1;
            }

        }

        if (1 == 1)
        {
            var res = _factsMover.DecorateFactsAndAssignToSheets(_documentId, filingsSubmitted);
            if (res != 0)
            {
                return res;
            }
            return 0;
        }

        

        var filingsUkDefence = new List<string>()
        {

            "S.01.01",
            "S.01.02",
            "S.02.01",
            "S.05.01",
            "S.06.02",
            "S.06.03",
            "S.17.01",
            "S.23.01",
            "S.28.01",
        };
     

        

        //_SqlFunctions.UpdateDocumentStatus(_documentId, "L");

        //var message = $"Xbrl Document Loaded Successfully:DocumentId= {_documentId}";
        //_logger.Information(message);
        //_SqlFunctions.CreateTransactionLog(0, MessageType.COMPLETE, message);
        return 0;
    }



}
