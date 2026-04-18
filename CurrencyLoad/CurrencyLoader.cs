using Azure;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.DataModels;
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
using Mapster;
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
    
    private record CurencyPairType(string Currency, double ExchangeRate);

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
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXtfeHRdRmJfV0V2X0ZWYEo=");
        using var excelEngine = new ExcelEngine();                
        
        (var workBook, var xMessage) = HelperRoutines.OpenExistingExcelWorkbook(excelEngine, _parameterData.FileName);
        if (workBook is null)
        {
            var errorMessage = $"Cannot Read exce file snc";
            _logger.Error(xMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, errorMessage + "--" + xMessage);
            return 1;
        }

        var worksheet = workBook.Worksheets[0];
        var values = ReadExchangeRatesFromExcelFile(worksheet);       
        
        //delete currency batch (all exchange rates will be cascade deleted) and create a new one
        var currencyBatch = _SqlFunctions.SelectCurrencyBatch(_parameterData.ApplicableYear, _parameterData.ApplicableQuarter, _parameterData.Wave);
        _SqlFunctions.DeleteCurrencyBatch(currencyBatch?.CurrencyBatchId??-1);
        var cb = new CurrencyBatch()
        {
            Year = _parameterData.ApplicableYear,
            Quarter = _parameterData.ApplicableQuarter,
            Wave = _parameterData.Wave,
            Status="S",
            DateCreated=DateTime.Now
        };
        var currencyBatchNew =_SqlFunctions.CreateCurrencyBatch(cb);
        var cbn = SaveExchangeRatesInDb(currencyBatchNew, values);
        return 0;
    }

    private static List<CurencyPairType> ReadExchangeRatesFromExcelFile(IWorksheet worksheet)
    {
        var clist = new List<CurencyPairType>();
        IRange? currencyLabelCell = null;

        currencyLabelCell = FindCurrencyLabelCell(worksheet);
        if (currencyLabelCell is null)
        {
            return clist;
        }

        //.Row , .Column and LastRow are one Based
        var startRow = currencyLabelCell.Row + 1;
        var startCol = currencyLabelCell.Column;

        for (var j = startRow; j <= worksheet.UsedRange.LastRow; j++)
        {
            var curr = worksheet[j, startCol].Text;
            var val = worksheet[j, startCol + 1].Number;      //it will assign NaN if cell has not a valid number
            clist.Add(new CurencyPairType(curr, val));
        }
        clist = clist.Where(r => !string.IsNullOrWhiteSpace(r.Currency) && !double.IsNaN(r.ExchangeRate)).ToList();
        return clist;

        static IRange? FindCurrencyLabelCell(IWorksheet worksheet)
        {            
            IRange? currencyLabelCell = null;
            for (var i = 1; i <= worksheet.UsedRange.LastRow; i++)
            {
                var row = worksheet[i, 1, i, 50];
                //var row = worksheet.Rows[i];
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

            return currencyLabelCell;
        }
    }

    private int SaveExchangeRatesInDb(int currencyBatchId, List<CurencyPairType> currencies)
    {

        foreach (var c in currencies) { 
            var exchangeRate= c.Adapt<CurrencyExchangeRate>();
            exchangeRate.CurrencyBatchId = currencyBatchId;
            _SqlFunctions.CreateExchangeRate(exchangeRate);
        }
        return 0;        
    }

}