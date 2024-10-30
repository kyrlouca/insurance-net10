using Serilog;
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

namespace CurrencyLoad;

public class CurrencyLoader : ICurrencyLoader
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private IWorkbook? _errorWorkbook;
    private IWorkbook? _warningWorkbook;
    private readonly ICustomPensionStyler _customStyler;
    PensionStyles _pensionStyles;

    public int id = 12;
    public CurrencyLoader(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions, ICustomPensionStyler customPensionStyles)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _customStyler = customPensionStyles;

    }
    public int LoadExcelFile(string fileName)
    {
        return 1;
    }
}