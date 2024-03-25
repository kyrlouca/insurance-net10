namespace Shared.SharedHost;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shared.HostParameters;




public class ParameterHandler : IParameterHandler
{
    IConfiguration _configuration;
    IOptions<VersionData> _optionsVersionData;
    //IOptions<LoggerFiles> _optionsLoggerFiles;
    ParameterData _parameterData;


    public ParameterHandler(IConfiguration config, IOptions<VersionData> optionVersionData)
    {
        _configuration = config;
        _optionsVersionData = optionVersionData;
        //_optionsLoggerFiles = optionsLoggerFiles;

        if (optionVersionData.Value.SystemConnectionString is null)
        {
            throw new ArgumentException("** Appsettings.SystemConnectionString in appsettings is null");
        }
        if (optionVersionData.Value.EiopaConnectionString is null)
        {
            throw new ArgumentException("** Appsettings.EiopaConnectionString in appsettings is null");
        }



    }
    public ParameterData GetParameterData()
    {
        //get params from env, appsettings, commandline and build a parameterDataObject 
        if (_parameterData is not null)
        {
            return _parameterData;
        }
        var xx = _configuration["TestDev"] ?? "N/F";

        var parameterData = new ParameterData()
        {
            DocumentId = int.TryParse(_configuration["document-id"], out int documentId) ? documentId : 0,
            ExternalId = int.TryParse(_configuration["external-id"], out int externalid) ? externalid : 0,
            UserId = int.TryParse(_configuration["user-id"], out int userid) ? userid : 0,
            FundId = int.TryParse(_configuration["fund-id"], out int fundId) ? fundId : 0,
            CurrencyBatchId = int.TryParse(_configuration["currency-batch-id"], out int currencyBatchId) ? currencyBatchId : 0,
            EiopaVersion = _configuration["eiopa-version"] ?? "NF",
            ModuleCode = _configuration["module-code"] ?? "NF",
            ApplicableYear = int.TryParse(_configuration["year"], out int year) ? year : 0,
            ApplicableQuarter = int.TryParse(_configuration["quarter"], out int quarter) ? quarter : 0,
            //_optionsVersionData contains values for correspoinding EIOPA version. It was implemented in configureServices
            SystemConnectionString = _optionsVersionData.Value.SystemConnectionString,
            EiopaConnectionString = _optionsVersionData.Value.EiopaConnectionString,
            ExcelTemplateFile = _optionsVersionData.Value.ExcelTemplateFile,
            //LoggerFile = _optionsLoggerFiles.Value.LoggerExcelReaderFile,
            FileName = _configuration["file-name"] ?? "NF",
            FileNameError = _configuration["file-name-error"] ?? "NF",
            FileNameWarning = _configuration["file-name-warning"] ?? "NF",
            IsDevelop = _configuration["DOTNET_ENVIRONMENT"]?.Contains("Develop") ?? false

    };
    _parameterData = parameterData;
		return parameterData;
	}
}

