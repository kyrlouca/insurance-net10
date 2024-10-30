using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyLoad;

using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.CommonRoutines;
using Shared.SQLFunctions;
using System.Reflection.Metadata;
using System.Reflection;
using Shared.DataModels;

public class CurrencyLoadMain : ICurrencyLoadMain
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private ICurrencyLoader _currencyLoader;

    public int id = 12;
    public CurrencyLoadMain(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions, ICurrencyLoader currencyLoader)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _currencyLoader = currencyLoader;

    }




    public int Run()
    {
        //module-code="qrs"


        Console.WriteLine($"started Loading Currencies from file:{_parameterData.FileName}");

        
        _currencyLoader.LoadExcelFile("a");

        return 0;

    }



}
