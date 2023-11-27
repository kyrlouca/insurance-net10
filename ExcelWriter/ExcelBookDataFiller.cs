namespace ExcelWriter;
using Shared.CommonRoutines;
using Shared.HostRoutines;
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

public class ExcelBookDataFiller : IExcelBookDataFiller
{

    private readonly IParameterHandler _parameterHandler;
    ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ICommonRoutines _commonRoutines;
    private IWorkbook? Workbook;
    //private IWorkbook? _originWorkbook; //template workbook
    int _documentId = 0;
    string debugTableCode = "";

    public ExcelBookDataFiller(IParameterHandler parametersHandler, ILogger logger, ICommonRoutines commonRoutines)
    {
        _parameterHandler = parametersHandler;
        _logger = logger;
        _commonRoutines = commonRoutines;
    }

    public bool PopulateExcelBook(int documentId, string sourceFilename, string destFileName)
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
            _commonRoutines.CreateTransactionLog(0, MessageType.ERROR, originMessage);
            return false;
        }

        var dbClosedSheets = _commonRoutines.SelectTempateSheets(_documentId)
            .Where(sheet => !sheet.IsOpenTable);
        foreach (var dbClosedSheet in dbClosedSheets)
        {
            Console.WriteLine($"Closed:{dbClosedSheet.SheetCode}");
            PopulateClosedTable(dbClosedSheet);

        }

        var dbOpenSheets = _commonRoutines.SelectTempateSheets(_documentId)
            .Where(sheet => sheet.IsOpenTable);
        foreach (var dbOpenSheet in dbOpenSheets)
        {
            Console.WriteLine($"open:{dbOpenSheet.SheetCode}");
            PopulateOpenTable(dbOpenSheet);
        }


        //var savedFile = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\makaOUT1.xlsx";
        (var isValidSave, var destSaveMessage) = HelperRoutines.SaveWorkbook(Workbook, destFileName);
        if (!isValidSave)
        {
            _logger.Error(destSaveMessage);
            _commonRoutines.CreateTransactionLog(0, MessageType.ERROR, destSaveMessage);
            return false;
        }

        return true;
    }

    private bool PopulateClosedTable(TemplateSheetInstance dbSheet)
    {

        var dataName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_data"];
        var dataRange = dataName.RefersToRange;

        var topName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_top"];
        var topRange = topName.RefersToRange;

        var leftName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_left"];
        var leftRange = leftName.RefersToRange;

        var topLabelRange = topRange.Offset(-1, 0);

        var zetList = GetFactPivotZets();
        if (zetList.Count > 1)
        {
            topLabelRange = HelperRoutines.ExtendRangeRowCols(topLabelRange, 0, zetList.Count - 1);        
            dataRange=  HelperRoutines.ExtendRangeRowCols(dataRange,0,zetList.Count -1);
            topRange= HelperRoutines.ExtendRangeRowCols(topRange,0,zetList.Count - 1);

            var val= topRange.Rows.First().Columns.First().Value;
            topRange.Value = val;

            var zetIndex = 0;
            var topRows = topLabelRange.Rows.First().Cells;
            foreach (var cell in topRows)
            {
                var mMember = _commonRoutines.SelectDomainMember(zetList[zetIndex]);
                var domainValue = mMember?.MemberLabel;
                cell.Text = domainValue;
                zetIndex++;
            }
            
        }


        foreach (var dataRow in dataRange.Rows)
        {
            foreach (var cell in dataRow.Cells)
            {
                var dataCell = HelperRoutines.CreateRowColObject(cell.AddressR1C1Local);
                var rowLabel = leftRange[dataCell.Row, leftRange.Column].Value;
                var colLabel = topRange[topRange.Row, dataCell.Col].Value;

                if (string.IsNullOrEmpty(rowLabel) || string.IsNullOrEmpty(colLabel))
                {
                    continue;
                }
                var facts = FindFactsFromRowCol(dbSheet, rowLabel, colLabel);
                if (facts.Count == 0)
                {
                    continue;
                }

                if (facts.Count > 1)
                {
                    var x = 22;
                    continue;
                }


                var fact = facts.First(); //should'nt get more than one for open (no multicurrency facts)
                SaveCellValue(cell, fact);
            }
        }

        return false;

        List<string> GetFactPivotZets()
        {
            using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
            var sqlZetValues = @"select distinct fact.Zet from TemplateSheetFact fact where fact.TemplateSheetId = @sheetId order by fact.Zet";
            var pivotZets = connectionInsurance.Query<string>(sqlZetValues, new { sheetId = dbSheet.TemplateSheetId }).ToList() ?? new List<string>();
            return pivotZets;
        }
    }


    private bool PopulateOpenTable(TemplateSheetInstance dbSheet)
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

                var facts = FindFactsFromRowCol(dbSheet, rowLabel, colCell.Text);
                if (facts.Count == 0 || facts.Count > 1)
                {
                    continue;
                }
                var fact = facts.First(); //should'nt get more than one for open (no multicurrency facts)
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

    private TemplateSheetFact? FindFactFromRowColZet(TemplateSheetInstance sheet, string row, string col, string zet)
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
			  AND fact.Zet = @zet                
     ";
        using var connectionLocalDb = new SqlConnection(_parameterData.SystemConnectionString);
        var fact = connectionLocalDb.QueryFirstOrDefault<TemplateSheetFact>(sqlFact, new { sheetId = sheet.TemplateSheetId, row, col, zet });
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
