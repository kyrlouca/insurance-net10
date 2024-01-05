namespace ExcelWriter;
using Shared.HostParameters;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.SharedHost;
using Shared.DataModels;
using System.Reflection.Metadata;
using Syncfusion.XlsIO.Implementation;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.Implementation.Collections;
using System;
using System.Drawing;
using Syncfusion.XlsIO.Parser.Biff_Records;
using static System.Net.Mime.MediaTypeNames;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using ExcelWriter.Common;
using Shared.SQLFunctions;

public class ExcelBookDataFiller : IExcelBookDataFiller
{

    private readonly IParameterHandler _parameterHandler;
    ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private IWorkbook? Workbook;
    //private IWorkbook? _originWorkbook; //template workbook
    int _documentId = 0;
    
    private readonly ICustomPensionStyler _customPensionStyler;
    PensionStyles _pensionStyles;

    public ExcelBookDataFiller(IParameterHandler parametersHandler, ILogger logger, ISqlFunctions sqlFunctions, ICustomPensionStyler customPensionStyles)
    {
        _parameterHandler = parametersHandler;
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _customPensionStyler = customPensionStyles;
    }

    public bool FillExcelBook(int documentId, string sourceFilename, string destFileName)
    {
        _documentId = documentId;
        _parameterData = _parameterHandler.GetParameterData();


        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdgWH5fc3RdRWFfU0B0W0o=");

        using var excelEngine = new ExcelEngine();
        IApplication application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        (Workbook, var originMessage) = HelperRoutines.OpenExistingExcelWorkbook(excelEngine, sourceFilename);
        if (Workbook is null)
        {
            _logger.Error(originMessage);
            _SqlFunctions.CreateTransactionLog( MessageType.ERROR, originMessage);
            return false;
        }


        _pensionStyles = _customPensionStyler.GetStyles(Workbook);

        ///////////////////////////////////////////////////////////////////
        ///
        var dbClosedSheets = _SqlFunctions.SelectTempateSheets(_documentId)
            .Where(sheet => !sheet.IsOpenTable);

        //var debugClosedTableCode = "S.06.02.01.01";
        var debugClosedTableCode = "";
        dbClosedSheets = string.IsNullOrWhiteSpace(debugClosedTableCode)
             ? dbClosedSheets
             : dbClosedSheets.Where(tb => tb.TableCode?.Trim()   == debugClosedTableCode);


        foreach (var dbClosedSheet in dbClosedSheets)
        {
            if (dbClosedSheet.TableCode == "ab")
            {
                var x = 2;
            }
            Console.WriteLine($"Populate Closed:{dbClosedSheet.SheetCode}");
            //Closed:S.04.01.01.02__s2c_GA_x14__s2c_LB_x146
            FillClosedTable(dbClosedSheet);
        }

        
        var dbOpenSheets = _SqlFunctions.SelectTempateSheets(_documentId)
            .Where(sheet => sheet.IsOpenTable);

        //var debugOpenTableCode = "S.06.02.01.01";
        var debugOpenTableCode = "";
        dbOpenSheets = string.IsNullOrWhiteSpace(debugOpenTableCode)
             ? dbOpenSheets
             : dbOpenSheets.Where(tb => tb.TableCode.Trim() == debugOpenTableCode);

        foreach (var dbOpenSheet in dbOpenSheets)
        {
            Console.WriteLine($"open:{dbOpenSheet.SheetCode}");
            FillOpenTable(dbOpenSheet);
        }


        //var savedFile = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\makaOUT1.xlsx";
        (var isValidSave, var destSaveMessage) = HelperRoutines.SaveWorkbook(Workbook, destFileName);
        if (!isValidSave)
        {
            _logger.Error(destSaveMessage);
            _SqlFunctions.CreateTransactionLog( MessageType.ERROR, destSaveMessage);
            return false;
        }

        return true;
    }

    private bool FillClosedTable(TemplateSheetInstance dbSheet)
    {
        //normally, facts with row,col are unique within a sheet. However, the design allows for multiple facts if they have different currency or country
        //for multi facts, we need to create additional columns and write the currency/country above the column

        var dataName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_data"];
        var dataRange = dataName.RefersToRange;

        var topColName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_top"];
        var topColumnRange = topColName.RefersToRange;

        var leftRowName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_left"];
        var leftRowRange = leftRowName.RefersToRange;

        var topLabelRange = topColumnRange.Offset(-1, 0);
        var currencyRange = topLabelRange.Offset(-2, 0);

        var CurrencyZetList = GetFactCurrencyZets().Order().ToList();
        var isMultiCurrency = (CurrencyZetList.Count > 0) && !string.IsNullOrWhiteSpace(CurrencyZetList.FirstOrDefault());
        if (isMultiCurrency)
        {
            var originalCols = topColumnRange.Cells.Select(cl => cl.Text).ToList();

            //Each column must be repeated for every currency
            //C0080=> C0080 for "GREECE", "ROMANIA", "CYPRUS"
            //therefore, the extra columns will be  columns.Count * zet.count-1 
            //populate the extra columns with cols, and zetvalues 
            var countCols = topLabelRange.Count();
            var addCols = countCols * (CurrencyZetList.Count - 1);
            topLabelRange = HelperRoutines.ExtendRangeRowCols(topLabelRange, 0, addCols);
            dataRange = HelperRoutines.ExtendRangeRowCols(dataRange, 0, addCols);
            topColumnRange = HelperRoutines.ExtendRangeRowCols(topColumnRange, 0, addCols);
            currencyRange = HelperRoutines.ExtendRangeRowCols(currencyRange, 0, addCols);

            dataRange.CellStyle = _pensionStyles.DataSectionStyle;
            topColumnRange.CellStyle = _pensionStyles.TopColumnNumbersStyle;

            var val = topColumnRange.Rows.First().Columns.First().Value;
            topColumnRange.Value = val;
            
            for (var curIdx = 0; curIdx < CurrencyZetList.Count; curIdx++)                
            {
                for (var colIdx = 0; colIdx < countCols; colIdx++)
                {
                    var newCol = topColumnRange.Column + (curIdx * countCols) + colIdx;
                    topColumnRange[topColumnRange.Row, newCol].Value = originalCols[colIdx];                    
                    currencyRange[currencyRange.Row, newCol].Value = CurrencyZetList[curIdx];
                }

            }
        }
            


        foreach (var dataRow in dataRange.Rows)
        {
            foreach (var cell in dataRow.Cells)
            {
                var dataCell = HelperRoutines.CreateRowColObject(cell.AddressR1C1Local);
                if(dataCell is null)
                {
                    continue;
                }
                var rowLabel = leftRowRange[dataCell.Row, leftRowRange.Column].Value;
                var colLabel = topColumnRange[topColumnRange.Row, dataCell.Col].Value;

                if (string.IsNullOrEmpty(rowLabel) || string.IsNullOrEmpty(colLabel))
                {
                    continue;
                }

                var currencyDim = isMultiCurrency ? currencyRange[currencyRange.Row, cell.Column].Value : "";
                var factX = FindFactFromRowColCurrency(dbSheet, rowLabel, colLabel, currencyDim);
                if (factX is null)
                {
                    continue;
                }
                SaveCellValue(cell, factX);
            }
        }

        if (isMultiCurrency)
        {
            foreach(var cell in currencyRange.Cells)
            {
                var mMember = _SqlFunctions.SelectMMember(cell.Value);
                var domainValue = mMember?.MemberLabel ?? "";
                cell.Value = domainValue;

            }
        }

        return false;

        List<string> GetFactCurrencyZets()
        {
            var sqlCurrency = @"
                SELECT fact.CurrencyDim
                FROM
                  TemplateSheetFact fact
                WHERE
                  fact.TemplateSheetId = @sheetId
                GROUP BY
                  fact.CurrencyDim;
                ";

            using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
            var currencyZets = connectionInsurance.Query<string>(sqlCurrency, new { sheetId = dbSheet.TemplateSheetId }).ToList() ?? new List<string>();
            return currencyZets;
        }
    }


    private bool FillOpenTable(TemplateSheetInstance dbSheet)
    {

        var dataName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_data"];
        var dataRange = dataName.RefersToRange;
        var workSheet = dataRange.Worksheet;

        var topName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_top"];
        var topRange = topName.RefersToRange;


        var rowLabels = SelectOpenRowLabels(dbSheet.TemplateSheetId);
        var rowIndex = dataRange.Row;
        foreach (var rowLabel in rowLabels)
        {
            foreach (var colCell in topRange)
            {
                var colObject = HelperRoutines.CreateRowColObject(colCell.AddressR1C1Local);
                var colIndex = colObject.Col;
                var cell = workSheet[rowIndex, colIndex];

                var fact = FindFactFromRowColCurrency(dbSheet, rowLabel, colCell.Text, "");
                if (fact is null)
                {
                    continue;
                }

                SaveCellValue(cell, fact);
            }
            rowIndex += 1;
        }
        return true;
    }


    private void SaveCellValue(IRange cell, TemplateSheetFact fact)
    {

        var DataTypeUse = fact.DataTypeUse;
        cell.HorizontalAlignment = ExcelHAlign.HAlignLeft;
        switch (DataTypeUse)
        {
            case "D": //date
                cell.DateTime = fact.DateTimeValue;
                cell.NumberFormat = "yyyy-mm-dd";
                break;
            case "B": //boolean
                cell.Boolean = fact.BooleanValue;
                break;
            case "N": //Numeric (Decimal) 
            case "M": //monetary
                cell.Number = (double)fact.NumericValue;
                cell.HorizontalAlignment = ExcelHAlign.HAlignRight;
                cell.NumberFormat = "#,###,##0.00";
                break;
            case "P": //Percent
                cell.Number = (double)fact.NumericValue;
                cell.HorizontalAlignment = ExcelHAlign.HAlignRight;
                cell.NumberFormat = "0.00%";
                break;
            case "S": //String
                cell.Text = fact.TextValue.Trim();
                break;
            case "E": // Enumeration/Code"					  
                var memDescription = XbrlCodeToValue(fact.TextValue);
                cell.Text = memDescription;
                break;
            case "I": //integer
                cell.Number = (int)Math.Floor(fact.NumericValue);
                cell.HorizontalAlignment = ExcelHAlign.HAlignRight;
                break;
            case "NULL"://fact is null                            
                break;
            default:
                cell.Text = "ERROR VALUE";
                break;
        }
    }

    private List<TemplateSheetFact> FindFactsFromRowCol(TemplateSheetInstance sheet, string row, string col)
    {
        //more than one fact with the same row,col but with different currency
        var sqlFact =
      @"
		SELECT *                  
		FROM dbo.TemplateSheetFact fact
		WHERE
		  fact.TemplateSheetId = @sheetId
		  AND fact.Row = @row
		  AND fact.Col = @col                                    
	";

        using var connectionLocalDb = new SqlConnection(_parameterData.SystemConnectionString);
        var facts = connectionLocalDb.Query<TemplateSheetFact>(sqlFact, new { sheetId = sheet.TemplateSheetId, row, col }).ToList();
        return facts;
    }

    private TemplateSheetFact? FindFactFromRowColCurrency(TemplateSheetInstance sheet, string row, string col, string currencyDomValue)
    {
        //more than one fact with the same row,col but with different currency
        //currency dom value: s2c_GA:GR           
        var sqlFactNoZet =
      @"
		SELECT *                  
		FROM dbo.TemplateSheetFact fact
		WHERE
		  fact.TemplateSheetId = @sheetId
		  AND fact.Row = @row
		  AND fact.Col = @col                                    
	";
        var sqlFactZet =
      @"
            SELECT *    
			FROM dbo.TemplateSheetFact fact
			WHERE
			  fact.TemplateSheetId = @sheetId
			  AND fact.Row = @row
			  AND fact.Col = @col
			  AND fact.CurrencyDim = @currencyDomValue                
     ";
        var sqlFact = string.IsNullOrEmpty(currencyDomValue) ? sqlFactNoZet : sqlFactZet;
        using var connectionLocalDb = new SqlConnection(_parameterData.SystemConnectionString);

        var fact = connectionLocalDb.QueryFirstOrDefault<TemplateSheetFact>(sqlFact, new { sheetId = sheet.TemplateSheetId, row, col, currencyDomValue });
        return fact;
    }

    private string XbrlCodeToValue(string xbrlValue)
    {
        using var connectionEiopaDb = new SqlConnection(_parameterData.EiopaConnectionString);

        var sqlMember = "select mem.MemberLabel from mMember mem where mem.MemberXBRLCode = @xbrlCode";
        var memDescription = connectionEiopaDb.QuerySingleOrDefault<string>(sqlMember, new { xbrlCode = xbrlValue }) ?? "";
        return memDescription;
    }


    private List<string> SelectOpenRowLabels(int templateSheetId)
    {
        using var connectionLocalDb = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlRows = @"select  distinct fact.Row from TemplateSheetFact fact  where  fact.TemplateSheetId= @sheetId order by fact.Row";
        var rowLabels = connectionLocalDb.Query<string>(sqlRows, new { sheetId = templateSheetId }).ToList();
        return rowLabels;
    }




}
