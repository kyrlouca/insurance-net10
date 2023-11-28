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
		
	

	public int id = 12;
	public MyMainApp(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions, IFactsCreator factsCreator, IFactsProcessor factsProcessor)
	{
		_parameterHandler = getParameters;
		_logger = logger;
		_SqlFunctions = sqlFunctions;
		_factsProcessor = factsProcessor;
		_factsCreator = factsCreator;

	}
	public int Run()
	{
		_parameterData = _parameterHandler.GetParameterData();
		
		var (_documentId,filingsSubmitted)= _factsCreator.CreateLooseFacts();
		if(_documentId == 0)
		{
			return 1;
		}
		var res= _factsProcessor.DecorateFactsAndAssignToSheets(_documentId,filingsSubmitted);
		if(res != 0)
		{
			return res;
		}
		
		_SqlFunctions.UpdateDocumentStatus(_documentId,"L");

		var message = $"Xbrl Document Loaded Successfully:DocumentId= {_documentId}";
		_logger.Information(message);
		_SqlFunctions.CreateTransactionLog(0, MessageType.COMPLETE, message);
		return 0;
	}



}
