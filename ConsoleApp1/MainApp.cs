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
	//do not pass serilog, pass a class with serilog
	private readonly IParameterHandler _parameterHandler;
	ParameterData _parameterData = new();
	private readonly ILogger _logger;
	private readonly ICommonRoutines _commonRoutines;
	private readonly IFactsProcessor _factsProcessor;
	private readonly IFactsCreator _factsCreator;
	MModule _mModule = new();
	XDocument? _xmlDoc;
	private readonly DocInstance? _docInstance;
	private int _documentId = 0;

	public XElement RootNode { get; private set; }
	readonly XNamespace xbrli = "http://www.xbrl.org/2003/instance";
	readonly XNamespace xbrldi = "http://xbrl.org/2006/xbrldi";
	readonly XNamespace xlink = "http://www.w3.org/1999/xlink";
	readonly XNamespace link = "http://www.xbrl.org/2003/linkbase";
	//readonly XNamespace typedDimNs = "http://eiopa.europa.eu/xbrl/s2c/dict/typ";
	readonly XNamespace findNs = "http://www.eurofiling.info/xbrl/ext/filing-indicators";

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
		
		var documentId= _factsCreator.CreateLooseFacts();
		_factsProcessor.ProcessFactsAndAssignToSheets(FilingsSubmitted,_documentId);

		_commonRoutines.UpdateDocumentStatus(documentId,"L");
		var message = $"Xbrl Document Loaded Successfully:DocumentId= jxx";
		_logger.Information(message);
		_commonRoutines.CreateTransactionLog(0, MessageType.COMPLETE, message);
		return 0;
	}



}
