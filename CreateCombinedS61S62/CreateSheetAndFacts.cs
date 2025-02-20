namespace CreateCombinedS61S62;
using Mapster;
using Microsoft.VisualBasic.FileIO;
using Serilog;
using Shared.DataModels;
using Shared.ExcelHelperRoutines;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class CreateSheetAndFacts
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;





    public CreateSheetAndFacts(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
    }



    public int CreateX(int year)
    {
        var docs = _SqlFunctions.SelectDocInstances(_parameterData.FundId, _parameterData.ModuleCode, _parameterData.ApplicableYear, _parameterData.ApplicableQuarter);
        if (!docs.Any())
        {
            var message = $"No documents found for Fund:{_parameterData.FundId} Year:{_parameterData.ApplicableYear} Quarter:{_parameterData.ApplicableQuarter}";
            _logger.Error(message);
            return 0;
        }
        var doc = docs.FirstOrDefault();
        var sheet = _SqlFunctions.SelectTemplateSheetsByTableId(doc!.InstanceId, 100001).FirstOrDefault();
        if (sheet != null)
        {
            _SqlFunctions.DeleteTemplateSheet(sheet.TemplateSheetId);
        }
        var tableCode = "S.06.02.01.99";
        var newSheet = new TemplateSheetInstanceDataModel()
        {
            
            InstanceId = doc.InstanceId,
            TableID = 100001,
            TableCode = tableCode,
            SheetCode=tableCode,
            SheetCodeZet = tableCode,
            DateCreated = DateTime.Now,
        };
        var id=_SqlFunctions.CreateTemplateSheet(newSheet);

        var fact = new TemplateSheetFactDataModel()
        {
            InstanceId = doc.InstanceId,
            TemplateSheetId = id,
            DateTimeValue=DateTime.Now,
            TextValue = "abc",
            Row="RRRR"
        };
        var xx= _SqlFunctions.CreateTemplateSheetFact(fact);

        return id;
    }
    


}
