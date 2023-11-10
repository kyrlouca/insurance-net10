namespace ConsoleApp1;
using Dapper;
using EntityClasses;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.CommonRoutines;
using Shared.DataModels;
using Shared.HostRoutines;
using Shared.SharedHost;
using System;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection;
using System.Xml.Linq;

public class MyMainApp : IMyMainApp
{
	//do not pass serilog, pass a class with serilog
	private readonly IParameterHandler _parameterHandler;
	ParameterData _parameterData = new();
	private readonly ILogger _logger;
	private readonly ICommonRoutines _commonRoutines;
	MModule _mModule = new();
	XDocument? _xmlDoc;

	public int id = 12;
	public MyMainApp(IParameterHandler getParameters, ILogger logger, ICommonRoutines commonRoutines)
	{
		_parameterHandler = getParameters;
		_logger = logger;
		_commonRoutines = commonRoutines;
	}
	public int Run()
	{
		_parameterData = _parameterHandler.GetParameterData();

		var message = "";
		var (isValidMessage, paramsMessage) = IsValidParameters();
		if (!isValidMessage)
		{
			_logger.Error(paramsMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, paramsMessage);
			return 1;
		}

		var (isExistingValid, existingMessage) = HandleExistingDocuments();
		if (!isExistingValid)
		{
			_logger.Error(existingMessage );
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, existingMessage);
			return 1;
		}

		var (parseValid,parseMessage, parsexmlDoc) = ParseXmlFile();
		_xmlDoc = parsexmlDoc;
		if (!parseValid )
		{
			_logger.Error(parseMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, parseMessage);
			return 1;
		}

		var (isValidReferenceDate, referenceMessage) = IsValidReferenceDate();
		if (!isValidReferenceDate)
		{
			_logger.Error(referenceMessage);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, referenceMessage);
			return 1;
		}

		var fundLei = GetXmlElementFromXbrl(_xmlDoc, "si1899");
		var fundFromDb = GetDbFundByLei(fundLei);
		if (fundFromDb == null || fundFromDb.FundId != _parameterData.FundId)
		{
			message = $"The license number is incorrect:{fundLei}";
			_logger.Error(message);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);
			return 1;
		}


		


		message = $"Xbrl Document Loaded Successfully:DocumentId= jxx";
		_logger.Information(message);
		_commonRoutines.CreateTransactionLog(0, MessageType.COMPLETE, message);
		return 0;
	}

	private (bool isValid, string message) IsValidParameters()
	{
		_mModule = _commonRoutines.GetModuleByCodeNew(_parameterData.ModuleCode);
		if (_mModule == null)
		{
			var message = $"Invalid Module code : {_parameterData.ModuleCode}";
			return (false, message);
		}
		if (!File.Exists(_parameterData.FileName))
		{
			var message = $"File not FOUND : {_parameterData.FileName}";
			return (false, message);
		}
		var fund = GetDbFundById(_parameterData.FundId);
		if (fund == null)
		{
			var message = $"Fund Id not Found : {_parameterData.FundId}";
			return (false, message);
		}

		return (true, "");
	}

	private (bool isParsed,string parseMessage,XDocument?) ParseXmlFile()
	{
		XDocument xmlDoc;

		using (TextReader sr = File.OpenText(_parameterData.FileName))  //utf-8 stream

			try
			{
				xmlDoc = XDocument.Load(sr);

			}
			catch (Exception e)
			{
				Log.Error(e.Message);
				var message = $"XBRL file not valid : {_parameterData.FileName}";
				return (false, message, null);
			}
		return (true,"",xmlDoc);
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


	private FundModel? GetDbFundById(int fundId)
	{
		using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);

		var sqlFund = "Select * from fund fnd where fnd.FundId= @FundId";
		var fund = connectionLocal.QuerySingleOrDefault<FundModel>(sqlFund, new { fundId });
		return fund;
	}


	private SubmissionReferenceDateModel? GetSubmissionReferenceDate(int category, int referenceYear, int quarter)
	{
		using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

		var sqlSubDate = @"
                SELECT
                  srd.SubmissionReferenceDateId
                 ,srd.Category
                 ,srd.ReferenceYear
                 ,srd.ReferenceDate
                 ,srd.SubmissionDate
                 ,srd.Quarter
                FROM dbo.SubmissionReferenceDate srd
                WHERE srd.Category = @category
                AND srd.ReferenceYear = @referenceYear
                AND srd.Quarter = @quarter

                ";
		var sRecord = connectionInsurance.QueryFirstOrDefault<SubmissionReferenceDateModel>(sqlSubDate, new { referenceYear, category, quarter });

		return sRecord;


	}

	private (bool isValid, string message) IsValidReferenceDate()
	{
		var dbReferenceDate = GetSubmissionReferenceDate(_parameterData.CurrencyBatchId, _parameterData.ApplicationYear, _parameterData.ApplicationQuarter);
		if(dbReferenceDate == null)
		{
			var message = $"Reference Date not defined in Database for:{_parameterData.ApplicationYear},{_parameterData.ApplicationQuarter},{_parameterData.CurrencyBatchId}";
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

		if (DateTime.Today > dbReferenceDate.SubmissionDate   )
		{
			//commented out to allow submission of documents
			//var message = $"Document was submitted after deadline:{dbReferenceDate.SubmissionDate} ";
			//return (false, message);
		}

		return (true, "");
	}

	private List<DocInstance> GetExistingDocuments()
	{
		using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
		var sqlExists = @"
                    select doc.InstanceId, doc.Status, doc.IsSubmitted, EiopaVersion from DocInstance doc  where  
                    PensionFundId= @FundId and ModuleId=@moduleId
                    and ApplicableYear = @ApplicableYear and ApplicableQuarter = @ApplicableQuarter"
				;

		var docParams = new { _parameterData.FundId, ModuleId = _mModule.ModuleID, _parameterData.ApplicationYear, _parameterData.ApplicationQuarter };
		var docs = connectionInsurance.Query<DocInstance>(sqlExists, docParams).ToList();
		return docs;
	}


	private int DeleteDocument(int documentId)
	{
		using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
		var sqlDeleteDoc = @"delete from DocInstance where InstanceId= @documentId";
		var rows = connectionInsurance.Execute(sqlDeleteDoc, new { documentId });

		var sqlErrorDocDelete = @"delete from DocInstance where InstanceId= @documentId";
		connectionInsurance.Execute(sqlErrorDocDelete, new { documentId });

		return rows;
	}

	private (bool success, string message) HandleExistingDocuments()
	{

		var existingDocs = GetExistingDocuments();
		var lockedDocument = existingDocs.FirstOrDefault(doc => doc.Status.Trim() == "P");
		if (lockedDocument is not null)
		{
			var message = $"Cannot create Document. Another Document is currently being processed :{lockedDocument.InstanceId} ";			
			return (false,message);
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



}
