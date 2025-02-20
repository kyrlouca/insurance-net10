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
using Syncfusion.XlsIO.Interfaces;
using ForeignKeys;

public class ForeignKeyMain : IForeignKeyMain
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private IUpdateForeignKeys _updateForeignKeys;

    public int id = 12;
    public ForeignKeyMain(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions,IUpdateForeignKeys updateForeignKeys)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _updateForeignKeys = updateForeignKeys;        

    }




    public int Run()
    {
        //module-code="qrs"


        Console.WriteLine($"started Uupdating Keys for Year:{_parameterData.ApplicableYear}");

        _updateForeignKeys.UpdateForeignKeysForYear(_parameterData.ApplicableYear);
        //_currencyLoader.LoadExcelFile("a");

        return 0;

    }



}
