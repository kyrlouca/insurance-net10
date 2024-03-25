namespace ErrorFileCreator;

using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;


using Shared.SQLFunctions;
using Shared.ExcelHelperRoutines;
using Syncfusion.XlsIO;
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
    private IWorkbook? _destinationWorkbook;


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
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdgWH5fc3RdRWFfU0B0W0o=");
        
        using var excelEngine = new ExcelEngine();
        (_destinationWorkbook, var xMessage) = HelperRoutines.CreateExcelWorkbook(excelEngine);
        if (_destinationWorkbook is null)
        {
           var errorMessage = $"Cannot create excel Workbook syncfusion file";
            _logger.Error(xMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, errorMessage + "--" + xMessage);
            return 0;
        }
        Console.WriteLine("hello0 create file");

        return 0;

    }
}