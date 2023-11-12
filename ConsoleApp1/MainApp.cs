namespace XbrlReader;
using Dapper;
using EntityClasses;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.CommonRoutines;
using Shared.GeneralUtils;
using Shared.SpecialRoutines;
using Shared.DataModels;
using Shared.HostRoutines;
using Shared.SharedHost;
using System;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection;
using System.Xml.Linq;
using XbrlReader;

public class MyMainApp : IMyMainApp
{

	private readonly IParameterHandler _parameterHandler;
	private ParameterData _parameterData = new();
	private readonly ILogger _logger;
	private readonly ICommonRoutines _commonRoutines;
	private readonly IFactsProcessor _factsProcessor;
	private readonly IFactsCreator _factsCreator;
	int _documentId = 0;

	
	List<string> FilingsSubmitted = new();

	public int id = 12;
	public MyMainApp(IParameterHandler getParameters, ILogger logger, ICommonRoutines commonRoutines, IFactsCreator factsCreator, IFactsProcessor factsProcessor)
	{
		_parameterHandler = getParameters;
		_logger = logger;
		_commonRoutines = commonRoutines;
		_factsProcessor = factsProcessor;
		_factsCreator = factsCreator;

	}
	public int Run()
	{
		_parameterData = _parameterHandler.GetParameterData();
		
		_documentId= _factsCreator.CreateLooseFacts();
		if(_documentId == 0)
		{
			return 1;
		}
		var res= _factsProcessor.ProcessFactsAndAssignToSheets(FilingsSubmitted,_documentId);
		if(res != 0)
		{
			return res;
		}
		
		_commonRoutines.UpdateDocumentStatus(_documentId,"L");
		var message = $"Xbrl Document Loaded Successfully:DocumentId= {_documentId}";
		_logger.Information(message);
		_commonRoutines.CreateTransactionLog(0, MessageType.COMPLETE, message);
		return 0;
	}



}
