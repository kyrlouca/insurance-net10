namespace XbrlReader;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.CommonRoutines;
using Shared.GeneralUtils;
using Shared.SpecialRoutines;
using Shared.DataModels;
using Shared.HostParameters;
using Shared.SharedHost;
using System;
using System.Globalization;

using System.Reflection.Metadata;
using System.Reflection;
using System.Xml.Linq;
using XbrlReader;
using System.Text;
using Syncfusion.XlsIO;
using Shared.SQLFunctions;
using Shared.Various;

public class FactsCreator : IFactsCreator
{

	private readonly IParameterHandler _parameterHandler;
	ParameterData _parameterData = new();
	private readonly ILogger _logger;
	private readonly ISqlFunctions _SqlFunctions;


	MModule _mModule = new();
	XDocument? _xmlDoc;
	private readonly DocInstance? _docInstance;
	private int _documentId = 0;
	private FundModel? _fund;

	public XElement? RootNode { get; private set; }
	readonly XNamespace xbrli = "http://www.xbrl.org/2003/instance";
	readonly XNamespace xbrldi = "http://xbrl.org/2006/xbrldi";
	readonly XNamespace xlink = "http://www.w3.org/1999/xlink";
	readonly XNamespace link = "http://www.xbrl.org/2003/linkbase";
	//readonly XNamespace typedDimNs = "http://eiopa.europa.eu/xbrl/s2c/dict/typ";
	readonly XNamespace findNs = "http://www.eurofiling.info/xbrl/ext/filing-indicators";
	readonly XNamespace xsiNs = "http://www.w3.org/2001/XMLSchema-instance";

	List<string> FilingsSubmitted = new();


	public FactsCreator(IParameterHandler parametersHandler, ILogger logger, ISqlFunctions commonRoutines)
	{
		_parameterHandler = parametersHandler;
		_logger = logger;
		_SqlFunctions = commonRoutines;
	}

    public (bool success, string message) HandleExistingDocuments()
    {
        _parameterData = _parameterHandler.GetParameterData();
        _mModule = _SqlFunctions.SelectModuleByCode(_parameterData.ModuleCode);
		
		var existingDocs = _SqlFunctions.SelectDocInstances(_parameterData.FundId, _parameterData.ModuleCode, _parameterData.ApplicableYear, _parameterData.ApplicableQuarter);
        var lockedDocument = existingDocs.FirstOrDefault(doc => doc.Status.Trim() == "P");
        //var lockedDocument = existingDocs.FirstOrDefault(doc => doc.Status.Trim() == "X");
        if (lockedDocument is not null)
        {
            var message = $"Cannot create Document. Another Document is currently being processed :{lockedDocument.InstanceId} ";
            return (false, message);
        }
        var sbmittedDocument = existingDocs.FirstOrDefault(doc => doc.IsSubmitted);
        if (sbmittedDocument is not null)
        {
            var message = $"Cannot create Document. It was already been submitted {sbmittedDocument.InstanceId} ";
            return (false, message);
        };

        //delete older versions (except from locked or submitted)
        existingDocs.Where(doc => doc.Status.Trim() != "P" && !doc.IsSubmitted)
            .ToList()
            .ForEach(doc => DeleteDocument(doc.InstanceId));

        return (true, "");
    }


    public (int, List<string>) CreateLooseFacts()    
	{


        var fund = _SqlFunctions.SelectFund(_parameterData.FundId);
        if (fund is null)
        {
            var messagex = $"fund {_parameterData.FundId} NOT found";
            _logger.Error(messagex);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, messagex);
            return (0,new List<string>());
        }
		_fund = fund;


        //Parse an xbrl file and create on object of the class which has the contexts, facts, etc
        //However, with the new design design, contexts and facts are saved in memory tables and NOT in data structures            		
        var message = "";
		var (parseValid, parseMessage, parsexmlDoc) = ParseXmlFile();		
		if (!parseValid)
		{
			_logger.Error(parseMessage);
			_SqlFunctions.CreateTransactionLog(MessageType.ERROR, parseMessage);
            return (0, new List<string>());
        }
        _xmlDoc = parsexmlDoc!;

        var RootNode = _xmlDoc.Root;		
		var reference = RootNode?.Element(link + "schemaRef")?.Attribute(xlink + "href")?.Value ?? "N?F";


		var moduleCodeXbrl = RegexUtils.GetRegexSingleMatch(@"http.*mod\/(\w*)", reference);		
		Console.WriteLine($"Opened Xblrl=>  Module: {moduleCodeXbrl} ");
		if (moduleCodeXbrl != _mModule.ModuleCode)
		{
			var moduleMessage = @$"The Module Code in the Xbrl file is ""{moduleCodeXbrl}"" instead of ""{_mModule.ModuleCode}""";
            _logger.Error(moduleMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, moduleMessage);            
			return (0, new List<string>());
		}

		var (isValidReferenceDate, referenceMessage) = IsValidReferenceDate();
		if (!isValidReferenceDate)
		{
			_logger.Error(referenceMessage);
			_SqlFunctions.CreateTransactionLog(MessageType.ERROR, referenceMessage);
            return (0, new List<string>());
        }

		var fundLei = GetXmlElementFromXbrl(_xmlDoc, "si1899");
		var fundFromDb = GetDbFundByLei(fundLei);
		if (fundFromDb == null || fundFromDb.FundId != _parameterData.FundId)
		{
			message = $"The license number is incorrect:{fundLei}";
			_logger.Error(message);
			_SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
			return (0, new List<string>());
		}

		///////////////////////////
		//*************************
		//Create the document to attache the facts
		//Then Start creating facts
		//*************************
		_documentId = CreateDocInstanceInDb();
		if (_documentId == 0)
		{
			message = $"Cannot Create DocInstance for: {_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
			Console.WriteLine(message);
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
            return (0, new List<string>());
		}


		AddValidFilingIndicators();
		Console.WriteLine("filing Indicators");

		Console.WriteLine("\nCreate Units");
		Dictionary<string, string> Units = new Dictionary<string, string>();
		AddUnits();

		Console.WriteLine("\nCreate Contexts");
		AddContexts();

		Console.WriteLine("\nCreate Facts");
		AddFacts();

        //DeleteContexts();
        return (_documentId, FilingsSubmitted);


        void AddValidFilingIndicators()
		{
			//filing indicators
			var filingsHeader = RootNode.Element(findNs + "fIndicators");
			var filingIndicators = filingsHeader?.Elements(findNs + "filingIndicator").Where(element=>element is not null).ToList();
			foreach (var fi in filingIndicators)
			{
				var isNotFiled = fi.Attribute(findNs + "filed")?.Value == "false";
				if (isNotFiled)
				{
					continue;
				}
				FilingsSubmitted.Add(fi.Value);
			}
		}
		void AddUnits()
		{
			//units
			var units = RootNode.Elements(xbrli + "unit");
			foreach (var unit in units)
			{
				var id = unit.Attribute("id").Value;
				var measure = unit.Element(xbrli + "measure").Value;
				Units.Add(id, measure);
			}
		}


		void AddContexts()
		{
			using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
            //Each contextElement has contextLines (typed and implicit)
            //I save context but do not  context-lines.
            // context has a signature wich concatenates  and all the context lines (explicit and typed)
            //typed context lines are converted same format as explicit (the is explicit/type info is lost)

            var contextElements = RootNode.Elements(xbrli + "context");
			var i = 0;
			foreach (var contextElement in contextElements)
			{

				//read the explicit and typed dimensions of each context 
				i += 1;
				var contextLines=new List<ContextLine>();
				var contextXbrlId = contextElement.Attribute("id").Value;
				//Console.WriteLine($"context:{contextXbrlId}");
				var scenario = contextElement.Element(xbrli + "scenario");

				//Explicit dims //<xbrldi:explicitMember dimension="s2c_dim:AG">s2c_VM:x17</xbrldi:explicitMember>                    
				var explicitDims = scenario?.Elements(xbrldi + "explicitMember") ?? new List<XElement>();
				foreach (var explicitDim in explicitDims)
				{
					//s2c_dim:VG(s2c_AM:x80) the result I want
					//<xbrldi:explicitMember dimension="s2c_dim:AG">s2c_VM:x17</xbrldi:explicitMember>
					var dimAndType = explicitDim.Attribute("dimension").Value; //s2c_dim:AG                    
					var domainAndMember = explicitDim.Value; //s2c_VM:x17
					
					var ctxSignature = $"{dimAndType}({domainAndMember})";
					var dimDom = DimDom.GetParts(ctxSignature);                    
                    var ctxLine = new ContextLine() {ContextId=0, Signature = dimDom.Signature, Dimension =dimDom.Dim,Domain=dimDom.Dom,DomainAndValue=dimDom.DomAndValRaw, DomainValue = dimDom.DomValue,IsExplicit =true};					
					contextLines.Add(ctxLine);					
				}


				var typedDims = scenario?.Elements(xbrldi + "typedMember") ?? new List<XElement>();
				foreach (var typedDim in typedDims)
				{
                    //<xbrldi:typedMember dimension="s2c_dim:FN"><s2c_typ:ID>1</s2c_typ:ID></xbrldi:typedMember>
                    //get the domNodeValue from  the typed element(ID) -- 1 in the case above

                    

                    var dimAndType = typedDim.Attribute("dimension").Value; //s2c_dim:AG 

					var domNode = typedDim.Elements()?.First(); //<s2c_typ:ID>1</s2c_typ:ID>                    
					var domain = domNode?.Name?.LocalName ?? ""; //ID
					var domainMember = domNode.Value; //1                     
					var domainAndMember = $"{domain}:{domainMember}";  //s2c_typ:ID					
					var ctxTypedSignature = $"{dimAndType}({domainAndMember})";

                    var isNil = domNode?.Attribute(xsiNs+"nil")?.Value ??""; //true 

                    var dimDom = DimDom.GetParts(ctxTypedSignature);
                    var ctxTypedLine = new ContextLine() { ContextId = 0, IsNil= isNil=="true",  Signature=dimDom.Signature, Dimension = dimDom.Dim, Domain = dimDom.Dom, DomainAndValue = dimDom.DomAndValRaw, DomainValue = dimDom.DomValue, IsExplicit = false };
                    contextLines.Add(ctxTypedLine);                                        
                }

				var ctLines = contextLines.Select(cl => cl.Signature).Order();
                var contexSignature = string.Join( "|",ctLines.ToList());
                
                
                var context = _SqlFunctions.CreateContext(new ContextModel() {InstanceId= _documentId, ContextId= 0,ContextXbrlId= contextXbrlId,Signature= contexSignature??"", TableId=0 });
                var contextId = context?.ContextId;
                if (context is null)
                {
                    continue;
                }

				foreach(var ctxLine in contextLines)
				{
					ctxLine.ContextId = context.ContextId;					
                    var x = _SqlFunctions.CreateContextLine(ctxLine);
                }

                

				//todo We do not need to update the signature since we are finding the facts using fact dims
				//however, we sould create fact dims directly


				Console.Write($"^");
			}
            
        }


		void DeleteContexts()
		{
			using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
			connectionInsurance.Execute("Delete from Context where InstanceId= @_documentId", new { _documentId });
		}

		void AddFacts()
		{
			using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
			using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

			//Read the facts
			//<s2md_met:ei1643 contextRef="c">s2c_CN:x12</s2md_met:ei1643>
			//<s2md_met:mi655 contextRef="AGx17_EXx13_FNID_POx60_RTx154_TAx2_VGx80" decimals="2" unitRef="u">327267425.67</s2md_met:mi655>

			//todo check if all facts are met 
			XNamespace metFactNs = "http://eiopa.europa.eu/xbrl/s2md/dict/met";

			//XbuFact.Contexts = Contexts; //it is a static property used by facts in all template Sheets
			//XbuFact.Units = Units; //it is a static property used by facts in all template Sheets
			var count = 0;
			var factElements = RootNode.Elements().Where(el => el.Name.Namespace == metFactNs).ToList();
			foreach (var fe in factElements)
			{
				// <s2md_met:mi503 contextRef="BLx119" decimals="2" unitRef="u">169866295.22</s2md_met:mi503>
				 var prefix = fe.GetPrefixOfNamespace(metFactNs); //maybe not needed as an attribute

				var unitRef2 = fe.Attribute("unitRef")?.Value;
				var decimals = 0;
				try
				{
					decimals = int.Parse(fe.Attribute("decimals")?.Value);
				}
				catch
				{
					decimals = 0;
				}

				var unitRef = fe.Attribute("unitRef")?.Value ?? "";
				var metric = fe.Name.LocalName.ToString(); //maybe not needed in Db                
				var xbrlCode = $"{prefix.Trim()}:{metric.Trim()}";

				var mMetric = FindFactMetricId(xbrlCode);  //"s2md_met:ei1633"                

				var dataTypeUse = mMetric is not null ? ConstantsAndUtils.SimpleDataTypes[mMetric.DataType] : "";
				//var dataTypeUse = CntConstants.SimpleDataTypes[mMetric.DataType]; //N, S,B,E..

				//var unitNN = XbuFact.Units.ContainsKey(unitRef) ? Units[unitRef] : unitRef;
				var contextXbrlId = fe.Attribute("contextRef")?.Value ?? "";

				//-----------------------                
				var context = _SqlFunctions.SelectContext(_documentId, contextXbrlId);
				if(context is null)
				{
					continue;
				}
				var factSignature=  $"MET({xbrlCode.Trim()})" ;
				if(!string.IsNullOrWhiteSpace(context?.Signature))
				{
                    factSignature = $"{factSignature}|{context?.Signature}";
				}
				
                
                var newFact = new TemplateSheetFact
				{
					InstanceId = _documentId,
					Row = "",
					Col = "",
					Zet = "",
					InternalCol = 0,
					InternalRow = 0,
					CellID = 0,
					CurrencyDim = "",
					//Metric = fe.Name.LocalName.ToString(),
					MetricID = mMetric?.MetricID ?? 0,
					//nsPrefix = prefix,
					XBRLCode = xbrlCode,
					ContextId = contextXbrlId,
					ContextNumberId=context?.ContextId??0,
					Unit = unitRef,
					Decimals = decimals,
					IsConversionError = false,
					IsEmpty = false,
					TextValue = fe.Value,
					NumericValue = 0,
					DateTimeValue = new DateTime(1999, 12, 31),
					BooleanValue = false,
					DataType = mMetric?.DataType ?? "",
					DataTypeUse = dataTypeUse,
					DataPointSignature = "",
					//DataPointSignatureFilled = "",
					RowSignature = "",
				};
                
                var contextLines = ParseContextLinesFromContext(_documentId, contextXbrlId);
				newFact.ContextId= contextXbrlId;				
                newFact.DataPointSignature = TemplateSheetFact.BuildFactSignature(xbrlCode, contextLines);
				newFact=AssignFactValuesFromText(newFact);
				
				if (factSignature != newFact.DataPointSignature)
				{
					//to test if my new method works
					throw new InvalidOperationException($"signature:{factSignature}");
				}
				
				var cFact = _SqlFunctions.CreateTemplateSheetFact(newFact,true);
				if(cFact ==0)
				{
					_logger.Error($"signature:{newFact.Signature} Fact cannot be created");
					continue;
				}

				

				Console.Write(".");

				count++;
				if (count % 1000 == 0)
				{
					Console.WriteLine($"facts Count:{count}");
				}


				//---------------------------

				List<string> ParseContextLinesFromContext(int documentId, string contextXbrlId)
				{
					//using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);                
					var sqlContext = @"select Signature from Context where ContextXbrlId= @ContextXbrlId and InstanceId =@documentId";
					var signature = connectionInsurance.QuerySingleOrDefault<string>(sqlContext, new { contextXbrlId, documentId });
					return signature?.Split("|", StringSplitOptions.RemoveEmptyEntries)?.ToList()??new();

				}


                static string BuildFactSignatureSimple(string xbrlCode, List<string> ctxLines)
                {
                    //A signature includes the metric and all the dimensions
                    //For explicit dimensions (used in open tables, where user can type the value) we do NOT take the value of the context item (add *)


                    var metXbrlCode = $"MET({xbrlCode})";

                    //var signatureList = contextLines?.Select(line => $"s2c_dim:{line.Dimension}({line.DomainAndValue})").ToList() ?? new List<string>();            
                    //signatureList.Sort();

                    //signatureList.Insert(0, metXbrlCode);
                    //var signature = string.Join("|", signatureList);

                    ctxLines.Sort();
                    ctxLines.Insert(0, metXbrlCode);

                    //var newSignature = StringRoutines.JoinStringCreate(ctxLines.ToList(), "|");
                    var newSignature = string.Join( "|",ctxLines.ToList());


                    return newSignature;
                }


            }

			MMetric FindFactMetricId(string xbrlCode)
			{
				//xbrl code is actually the metric of a fact
				using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

				var sqlMetric = @"
                SELECT met.MetricID, met.CorrespondingMemberID, met.DataType
                FROM dbo.mMetric met
                LEFT OUTER JOIN mMember mem ON mem.MemberID = met.CorrespondingMemberID
                WHERE mem.MemberXBRLCode = @xbrlCode
            ";
				var metric = connectionEiopa.QuerySingleOrDefault<MMetric>(sqlMetric, new { xbrlCode });
				return metric;
			}

		}


	}


    public TemplateSheetFact AssignFactValuesFromText(TemplateSheetFact fact)
    {
        fact.DateTimeValue = new DateTime(1999, 12, 31);
        if (string.IsNullOrWhiteSpace(fact.TextValue))
        {
            //if spaces it is NOT an error, it was left blank by the user
            fact.IsEmpty = true;
			fact.IsConversionError = false;
            return fact;
        }


        switch (fact.DataTypeUse)
        {
            case "S":
            case "E":
                //text value is already in textValue field
                break;
            case "B":
                fact.BooleanValue = fact.TextValue.Trim().ToUpper() == "TRUE";
                break;
            case "D":
                try
                {
                    fact.DateTimeValue = DateTime.Parse(fact.TextValue);
                }
                catch (System.Exception)
                {
                    fact.IsConversionError = true;
                }
                break;
            case "N": //numeric
            case "M": //money
            case "P": //this is a percentage
                try
                {
                    var nfi = new CultureInfo("en-US", false).NumberFormat;
                    fact.NumericValue = Convert.ToDouble(fact.TextValue, nfi);
                }
                catch (System.Exception)
                {
                    fact.IsConversionError = true;
                }
                break;
            case "I":
                try
                {
                    fact.NumericValue = Convert.ToInt32(fact.TextValue);
                }
                catch (System.Exception)
                {
                    fact.IsConversionError = true;
                }
                break;
            default:
                break;
        }
		return fact;
    }


    private (bool isParsed, string parseMessage, XDocument?) ParseXmlFile()
	{
        try
        {
            using TextReader sr = File.OpenText(_parameterData.FileName);
            XDocument xmlDoc = XDocument.Load(sr);
            return (true, "", xmlDoc);
        }
        catch (FileNotFoundException ex)
        {
			// Handle the case where the file does not exist
			var message1 = $"Cannot Find File {_parameterData.FileName}--{ex.Message}";
            return (false, message1, null);
        }
        catch (Exception ex)
        {
			// Handle other exceptions
			var message2 = $"An unexpected error occurred opening the file:{_parameterData.FileName} -- {ex.Message}";
            return (false, message2, null);
        }
                
		
	}

	static string GetXmlElementFromXbrl(XDocument xDoc, string xbrlCode)
	{
		//XNamespace ns = "http://CalculatorService/";
		//var html = xml.Descendants(ns + "html").ToList();

		//<s2md_met:si1899 contextRef="c0">LEI/2138006PEHZTJLNAPC69</s2md_met:si1899>  
		XNamespace metFactNs = "http://eiopa.europa.eu/xbrl/s2md/dict/met";
		var leiVal = xDoc.Root.Descendants(metFactNs + xbrlCode).FirstOrDefault()?.Value ?? "";
		return leiVal;
	}

	private FundModel? GetDbFundByLei(string lei)
	{
		using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);

		if (lei == null)
			return null;

		lei = lei.Replace(@"LEI/", "");//lei = "LEI/2138003JRMGVH8CGUR42"            
		var sqlFund = "select  fnd.FundId, fnd.FundName, fnd.IsActive, fnd.Lei , fnd.Wave from Fund fnd where fnd.Lei=@Lei";
		var fund = connectionLocal.QuerySingleOrDefault<FundModel>(sqlFund, new { lei });
		return fund;
	}



	private SubmissionReferenceDateModel? GetSubmissionReferenceDate(int category, int referenceYear, int quarter)
	{
		using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
		Console.WriteLine("systemString"+_parameterData.SystemConnectionString);
		var sqlSubDate = @"
                SELECT
                  srd.SubmissionReferenceDateId
                 ,srd.Category
                 ,srd.ReferenceYear
                 ,srd.ReferenceDate
                 ,srd.SubmissionDate
                 ,srd.Quarter
                FROM SubmissionReferenceDate srd
                WHERE srd.Category = @category
                AND srd.ReferenceYear = @referenceYear
                AND srd.Quarter = @quarter

                ";
		var sRecord = connectionInsurance.QueryFirstOrDefault<SubmissionReferenceDateModel>(sqlSubDate, new { referenceYear, category, quarter });

		return sRecord;


	}

	private (bool isValid, string message) IsValidReferenceDate()
	{
		var wave = _fund.Wave;
		var dbReferenceDate = GetSubmissionReferenceDate(wave, _parameterData.ApplicableYear, _parameterData.ApplicableQuarter);
		if (dbReferenceDate == null)
		{
			var message = $"Reference Date not defined in Database for:{_parameterData.ApplicableYear},{_parameterData.ApplicableQuarter},{_parameterData.CurrencyBatchId}";
			return (false, message);
		}

		var xbrlReferenceDateStr = GetXmlElementFromXbrl(_xmlDoc, "di1043");
		var isValidReferenceDate = DateTime.TryParseExact(xbrlReferenceDateStr, "yyyy-MM-dd", null, DateTimeStyles.None, out var xbrlReferenceDate);
		if (!isValidReferenceDate)
		{
			var message = $"Submission Date not valid:{xbrlReferenceDate}";
			return (false, message);
		}
		if (xbrlReferenceDate != dbReferenceDate?.ReferenceDate)
		{
			var message = $"Xbr Reference Date :{xbrlReferenceDate} different than Expected Reference Date : {dbReferenceDate?.ReferenceDate} ";
			return (false, message);
		}

		if (DateTime.Today > dbReferenceDate.SubmissionDate)
		{
			//commented out to allow submission of documents
			//var message = $"Document was submitted after deadline:{dbReferenceDate.SubmissionDate} ";
			//return (false, message);
		}

		return (true, "");
	}

	private int DeleteDocument(int documentId)
	{
		using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
		var sqlDeleteDoc = @"delete from DocInstance where InstanceId= @documentId";
		var rows = connectionInsurance.Execute(sqlDeleteDoc, new { documentId });

		var sqlErrorDocDelete = @"delete from ERROR_Document where ErrorDocumentId= @documentId";
		connectionInsurance.Execute(sqlErrorDocDelete, new { documentId });

        var sqlHangingFacts = "delete from TemplateSheetFact where InstanceId=@documentId";
        connectionInsurance.Execute(sqlHangingFacts, new { documentId });
        


        return rows;
	}

    private int CreateDocInstanceInDb()
	{
		using var connection = new SqlConnection(_parameterData.SystemConnectionString);
		using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

		var sqlInsertDoc = @"
               INSERT INTO DocInstance
                   (                                            
                    [PensionFundId]                   
                   ,[UserId]                   
                   ,[ModuleCode]           
                   ,[ApplicableYear]
                   ,[ApplicableQuarter]                   
                   ,[ModuleId]      
                   ,[FileName]
                   ,[CurrencyBatchId]
                   ,[Status]
                   ,[EiopaVersion]
                    )
                VALUES
                   (                                
                    @PensionFundId
                   ,@UserId
                   ,@ModuleCode                   
                   ,@ApplicableYear
                   ,@ApplicableQuarter                   
                   ,@ModuleId
                   ,@FileName
                   ,@CurrencyBatchId
                   ,@Status
                   ,@EiopaVersion
                    ); 
                SELECT CAST(SCOPE_IDENTITY() as int);
                ";




		var doc = new DocInstance()
		{
			PensionFundId = _parameterData.FundId,
			UserId = _parameterData.UserId,
			ModuleCode = _parameterData.ModuleCode,
			ApplicableYear = _parameterData.ApplicableYear,
			ApplicableQuarter = _parameterData.ApplicableQuarter,
			ModuleId = _mModule.ModuleID,
			FileName = _parameterData.FileName,
			CurrencyBatchId = _parameterData.CurrencyBatchId,
			Status = "P",
			EiopaVersion = _parameterData.EiopaVersion,
		};


		var result = connection.QuerySingleOrDefault<int>(sqlInsertDoc, doc);
		return result;
	}


}
