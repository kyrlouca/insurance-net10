namespace CreateCombinedS61S62;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;




using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.CommonRoutines;
using Shared.SQLFunctions;
using System.Reflection.Metadata;
using System.Reflection;
using Shared.DataModels;
using Syncfusion.XlsIO.Interfaces;


public class CreateCombinedS61S62 : ICreateCombinedS61S62
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private CreateSheetAndFacts _createSheetAndFacts;

    public int id = 12;
    public CreateCombinedS61S62(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions, CreateSheetAndFacts creatSheetAndFacts)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _createSheetAndFacts = creatSheetAndFacts;
    }

    public int Run()
    {
        //module-code="qrs"


        Console.WriteLine($"Started Creating Combined :fund:{_parameterData.FundId},Year:{_parameterData.ApplicableYear},quarter:{_parameterData.ApplicableQuarter}");
        _createSheetAndFacts.CreateX(_parameterData.ApplicableYear);        


        return 0;

    }



}
