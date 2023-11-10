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


		var (isValid, message) = IsValidDocument();
		if (!isValid)
		{
			_logger.Error(message);
			_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);
			return 1;
		}
		_xmlDoc = ParseXmlFile();
		if(_xmlDoc == null)
		{			
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

		return 0;
	}

	private (bool isValid, string message) IsValidDocument()
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

	private XDocument? ParseXmlFile()
	{
		XDocument xmlDoc;

		using (TextReader sr = File.OpenText(_parameterData.FileName))  //utf-8 stream

			try
			{
				xmlDoc = XDocument.Load(sr);
			}
			catch (Exception e)
			{
				var message = $"XBRL file not valid : {_parameterData.FileName}";				
				Log.Error(e.Message);
				_commonRoutines.CreateTransactionLog(0, MessageType.ERROR, message);												
				return null;
			}
		return xmlDoc;
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

	private  FundModel? GetDbFundByLei(string lei)
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
		var fund = connectionLocal.QuerySingleOrDefault<FundModel>(sqlFund, new {fundId });
		return fund;
	}


	private SubmissionReferenceDateModel? GetSubmissionReferenceDate( int category, int referenceYear, int quarter)
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


}
