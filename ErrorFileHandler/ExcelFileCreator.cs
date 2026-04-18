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
using Shared.DataModels;
using static Shared.SQLFunctions.ISqlFunctions;

public class ExcelFileCreator : IExcelFileCreator
{


    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private IErrorFileCreatorMain _errorFileHandlerMain;
    private IWorkbook? _errorWorkbook;
    private IWorkbook? _warningWorkbook;
    private readonly ICustomPensionStyler _customStyler;
    PensionStyles _pensionStyles;

    public int id = 12;
    public ExcelFileCreator(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions , ICustomPensionStyler customPensionStyles)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _customStyler = customPensionStyles;

    }



    public int CreateExcelFile()
    {

        var errors = _SqlFunctions.SelectErrorRules(_parameterData.DocumentId, ISqlFunctions.ErrorRuleTypes.Errors);
        RenderBook(_parameterData.FileNameError,errors, ISqlFunctions.ErrorRuleTypes.Errors);

        var warnings = _SqlFunctions.SelectErrorRules(_parameterData.DocumentId, ISqlFunctions.ErrorRuleTypes.Warnings);
        RenderBook(_parameterData.FileNameWarning, warnings, ISqlFunctions.ErrorRuleTypes.Warnings);

        Console.WriteLine("files created");

        return 0;

    }

    
    private void  RenderBook(string workbookName,List<ERROR_Rule> errors, ISqlFunctions.ErrorRuleTypes errorRuleType)
    {
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXtfeHRdRmJfV0V2X0ZWYEo=");
        
        using var excelEngine = new ExcelEngine();

        (var workBook, var xMessage) = HelperRoutines.CreateExcelWorkbook(excelEngine);
        if (workBook is null)
        {
            var errorMessage = $"Cannot create excel Workbook syncfusion file";
            _logger.Error(xMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, errorMessage + "--" + xMessage);
            return ;
        }
        _pensionStyles = _customStyler.GetStyles(workBook);

        var errorSheet = workBook.Worksheets.Create("List");
        errorSheet.SetColumnWidth(1, 6);
        errorSheet.SetColumnWidth(2, 100);
        errorSheet.SetColumnWidth(3, 20);
        errorSheet.SetColumnWidth(4, 10);
        errorSheet.SetColumnWidth(5, 10);
        errorSheet.SetColumnWidth(6, 50);
        errorSheet.SetColumnWidth(7, 50);
        errorSheet.SetColumnWidth(8, 50);
        errorSheet.SetColumnWidth(9, 10);
        errorSheet.SetColumnWidth(10, 10);
        errorSheet.Zoom = 90;


        

        var headerStyle = HeaderStyle(workBook);
        var ruleIdStyle = RuleIdStyle(workBook,errorRuleType);

        if (1 == 1)
        {
            var headerRow = 1;
            errorSheet[headerRow, 1].Text = "Id";
            errorSheet[headerRow, 2].Text = "If Clause";
            errorSheet[headerRow, 3].Text = "Then Clause";
            errorSheet[headerRow, 4].Text = "Else Clause";
            errorSheet[headerRow, 6].Text = "Filter";
            errorSheet[headerRow, 7].Text = "Short label";
            errorSheet[headerRow, 8].Text = "Rule Message";
            errorSheet[headerRow, 9].Text = "Original Formula";
            errorSheet[headerRow, 10].Text = "";
            errorSheet.Rows.First().CellStyle = headerStyle;
        }


        var dataRow = 2;

        foreach (var error in errors)
        {
            errorSheet[dataRow, 1].Number = error.RuleId;
            errorSheet[dataRow, 1].CellStyle = ruleIdStyle;            
            //errorSheet[dataRow, 1].CellStyle.Font.Color = 

            errorSheet[dataRow, 2].Text = error.FormulaForIf;
            errorSheet[dataRow, 3].Text = error.FormulaForThen;
            errorSheet[dataRow, 4].Text = error.FormulaForElse;
            errorSheet[dataRow, 6].Text = error.FormulaForFilter;
            errorSheet[dataRow, 7].Text = error.ShortLabel;
            errorSheet[dataRow, 8].Text = error.RuleMessage;
            errorSheet[dataRow, 9].Text = error.TableBaseFormula;
            errorSheet[dataRow, 10].Text = " ";
            dataRow++;
        }
        errorSheet.Range["B2"].FreezePanes();

        var (isSaveValid, saveMessage) = HelperRoutines.SaveWorkbook(workBook, workbookName);
        if (!isSaveValid)
        {
            _logger.Error(saveMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, saveMessage);
            return ;
        }

    }
    private IStyle RuleIdStyle( IWorkbook workbook, ISqlFunctions.ErrorRuleTypes errorRuleType)
    {
        var styleName = "ruleIdStyleX";
        IStyle style;
        try
        {
            style = workbook.Styles[styleName];
        }
        catch (Exception ex)
        {
            style = workbook.Styles.Add(styleName);            
        }

        
        style.Font.Color = errorRuleType == ErrorRuleTypes.Errors ? ExcelKnownColors.Red : ExcelKnownColors.Green;
        style.Font.Underline = ExcelUnderline.None;
        style.Font.Size = 12;        
        style.Font.Bold = false;
        return style;
    }

    

    private IStyle HeaderStyle(IWorkbook workbook)
    {
        var styleName = "headerStyleX";
        IStyle style;        
        try
        {
            style = workbook.Styles[styleName];
        }
        catch (Exception ex)
        {
            style = workbook.Styles.Add(styleName);            
        }
        style.Font.Color = ExcelKnownColors.Black;
        style.Font.Underline = ExcelUnderline.None;
        style.Font.Size = 14;
        style.Font.Bold = true;
        return style;
    }
}