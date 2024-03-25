namespace ErrorFileCreator;

using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;

using Shared.SQLFunctions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



public class ExcelFileCreator : IExcelFileCreator
{


    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private IErrorFileCreatorMain _errorFileHandlerMain;


    public int id = 12;
    public ExcelFileCreator(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions )
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;        

    }



    public int CreateExcelFile()
    {

        Console.WriteLine("hello0 create file");

        return 0;

    }
}