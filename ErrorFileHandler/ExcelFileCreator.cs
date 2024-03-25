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

        var errorSheet = _destinationWorkbook.Worksheets.Create("List");
        errorSheet.SetColumnWidth(1, 10);
        errorSheet.SetColumnWidth(2, 100);
        errorSheet.SetColumnWidth(3, 20);
        errorSheet.SetColumnWidth(4, 10);
        errorSheet.SetColumnWidth(5, 10);
        errorSheet.SetColumnWidth(6, 50);
        errorSheet.SetColumnWidth(8, 50);
        errorSheet.Zoom = 90;
        var titleCell = errorSheet[1, 1];
        titleCell.Text = "List of Templates";
        titleCell.CellStyle = _pensionStyles.HeaderStyle;

        var errors = _SqlFunctions.SelectErrorRules(112, ISqlFunctions.ErrorRuleTypes.Errors);


        var titleRow = 1;
        if (1 == 1)
        {
            var cell = errorSheet[titleRow, 1];
            cell.Text= "Rule Id";
            cell = errorSheet[titleRow, 2];
            cell.Text = "If Clause";
            cell = errorSheet[titleRow, 3];
            cell.Text = "Then Clause";
            cell = errorSheet[titleRow, 6];
            cell.Text = "Filter";
            cell = errorSheet[titleRow, 8];
            cell.Text = "RuleMessage";
        }

        var dataRow = 2;
        foreach (var error in errors)
        {
            var idCell = errorSheet[dataRow, 1];
            idCell.Number = error.RuleId;

            
            var ifCell = errorSheet[dataRow, 2];
            ifCell.Text = error.FormulaForIf;
            ifCell.CellStyle = _pensionStyles.Normal;

            var thenCell = errorSheet[dataRow,3 ];
            thenCell.Text = error.FormulaForThen;
            thenCell.CellStyle = _pensionStyles.Normal;

            var elseCell = errorSheet[dataRow, 4];
            elseCell.Text = error.FormulaForElse;
            elseCell.CellStyle = _pensionStyles.Normal;

            
            var filterCell = errorSheet[dataRow, 6];
            filterCell.Text = error.FormulaForFilter;
            filterCell.CellStyle = _pensionStyles.Normal;

            var ruleErrorMessage = errorSheet[dataRow, 8];
            ruleErrorMessage.Text=error.RuleMessage;
            ruleErrorMessage.CellStyle = _pensionStyles.Normal;

            dataRow++;
        }


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