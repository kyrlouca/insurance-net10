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
    private readonly ICustomPensionStyler _customPensionStyles;
    PensionStyles _pensionStyles;

    public int id = 12;
    public ExcelFileCreator(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions , ICustomPensionStyler customPensionStyles)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _customPensionStyles = customPensionStyles;

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
        _pensionStyles = _customPensionStyles.GetStyles(_destinationWorkbook);

        var indexSheet = _destinationWorkbook.Worksheets.Create("List");
        indexSheet.SetColumnWidth(1, 30);
        indexSheet.Zoom = 90;
        var titleCell = indexSheet[1, 1];
        titleCell.Text = "List of Templates";
        titleCell.CellStyle = _pensionStyles.HeaderStyle;

        var (isSaveValid, saveMessage) = HelperRoutines.SaveWorkbook(_destinationWorkbook, _parameterData.FileNameError);
        if (!isSaveValid)
        {
            _logger.Error(saveMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, saveMessage);
            return 0;
        }

        Console.WriteLine("hello0 create file");

        return 0;

    }
}