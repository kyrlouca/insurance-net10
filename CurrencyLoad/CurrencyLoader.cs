using Azure;
using Microsoft.IdentityModel.Tokens;
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
    private string _fileName;

    private record CurencyPairType(string Currency, double Rate);

    public int id = 12;
    public CurrencyLoader(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions, ICustomPensionStyler customPensionStyles)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _customStyler = customPensionStyles;
        _fileName = _parameterData.FileName;

    }

    public int LoadExcelFile(string fileName)
    {


        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdgWH5fc3RdRWFfU0B0W0o=");
        using var excelEngine = new ExcelEngine();


        _fileName = _parameterData.FileName;

        //(var workBook, var xMessage) = HelperRoutines.CreateExcelWorkbook(excelEngine);
        (var workBook, var xMessage) = HelperRoutines.OpenExistingExcelWorkbook(excelEngine, _fileName);
        if (workBook is null)
        {
            var errorMessage = $"Cannot Read excel Workbook syncfusion file";
            _logger.Error(xMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, errorMessage + "--" + xMessage);
            return 1;
        }


        var worksheet = workBook.Worksheets[0];
        var values = ReadCurrencyValues(worksheet);
        return 1;
    }

    private static List<CurencyPairType> ReadCurrencyValues(IWorksheet worksheet)
    {
        var clist = new List<CurencyPairType>();
        IRange? currencyLabelCell = null;
        for (var i = 0; i <= worksheet.UsedRange.LastRow; i++)
        {
            var row = worksheet.Rows[i];
            if (row.IsBlank)
            {
                continue;
            }
            currencyLabelCell = row.Cells.FirstOrDefault(static cell => cell.Text is not null && cell.Text.ToUpper() == "CURRENCY");
            if (currencyLabelCell is not null)
            {
                break;
            }
        }
        if (currencyLabelCell is null)
        {
            return clist;
        }

        //.Row , .Column and LastRow are one Based
        var startRow = currencyLabelCell.Row;//ignore the label so no need for -1
        var startCol = currencyLabelCell.Column - 1;
        
        for (var j = startRow; j < worksheet.UsedRange.LastRow; j++)
        {
            var row = worksheet.Rows[j];            
            var curr = row.Cells[startCol].Text;
            var val = row.Cells[startCol+ 1].Number;      //it will assign zero if not valid
            
            clist.Add(new CurencyPairType(curr, val));
        }
        clist = clist.Where(r => !string.IsNullOrWhiteSpace(r.Currency)  ).ToList();
        return clist;
    }


    private int StoreInDb(List<CurencyPairType> currencies)
    {
        foreach (var c in currencies) { 
        }
        return 0;        
    }


}