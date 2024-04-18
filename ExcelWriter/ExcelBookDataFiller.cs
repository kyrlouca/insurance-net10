namespace ExcelWriter;
using Shared.HostParameters;
using Shared.ExcelHelperRoutines;
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

using Shared.SQLFunctions;
using Microsoft.IdentityModel.Tokens;
using Shared.SpecialRoutines;
using Microsoft.VisualBasic;

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
        //Open the source file as a workbook, fill the sheets and save this Workbook with another name.
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
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, originMessage);
            return false;
        }


        _pensionStyles = _customPensionStyler.GetStyles(Workbook);

        ///////////////////////////////////////////////////////////////////
        ///
        var dbClosedSheets = _SqlFunctions.SelectTemplateSheets(_documentId)
            .Where(sheet => !sheet.IsOpenTable);

        //var debugClosedTableCode = "S.06.02.01.01";
        var debugClosedTableCode = "";
        dbClosedSheets = string.IsNullOrWhiteSpace(debugClosedTableCode)
             ? dbClosedSheets
             : dbClosedSheets.Where(tb => tb.TableCode?.Trim() == debugClosedTableCode);


        foreach (var dbClosedSheet in dbClosedSheets)
        {


            Console.WriteLine($"Populate Closed:{dbClosedSheet.SheetCode}");
            //Closed:S.04.01.01.02__s2c_GA_x14__s2c_LB_x146
            FillClosedTable280(dbClosedSheet);
        }


        var dbOpenSheets = _SqlFunctions.SelectTemplateSheets(_documentId)
            .Where(sheet => sheet.IsOpenTable);

        //var debugOpenTableCode = "S.06.02.01.01";
        var debugOpenTableCode = "";
        dbOpenSheets = string.IsNullOrWhiteSpace(debugOpenTableCode)
             ? dbOpenSheets
             : dbOpenSheets.Where(tb => tb.TableCode.Trim() == debugOpenTableCode);

        foreach (var dbOpenSheet in dbOpenSheets)
        {
            Console.WriteLine($"open:{dbOpenSheet.SheetCode}");
            FillOpenTable280(dbOpenSheet);
        }


        //var savedFile = @"C:\Users\kyrlo\soft\dotnet\insurance-project\TestingXbrl270\makaOUT1.xlsx";
        (var isValidSave, var destSaveMessage) = HelperRoutines.SaveWorkbook(Workbook, destFileName);
        if (!isValidSave)
        {
            _logger.Error(destSaveMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, destSaveMessage);
            return false;
        }

        return true;
    }



    private static void ClearLinks(IRange range)
    {

        if (range.Hyperlinks.Count > 0)
            for (int i = range.Hyperlinks.Count; i >= 1; i--)
            {
                range.Hyperlinks.RemoveAt(i - 1);
            }
    }

    private bool FillClosedTable280(TemplateSheetInstance dbSheet)
    {
        //normally, facts with row,col are unique within a sheet. However, the design allows for multiple facts if they have different currency or country
        //for multi facts, we need to create additional columns and write the currency/country above the column

        var dataName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_data"];
        var dataRange = dataName.RefersToRange;

        var wholeRangeName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_whole"];
        var wholeRange = wholeRangeName.RefersToRange;

        ClearLinks(wholeRange);

        var topPageRange = wholeRange[1, 1, 2, 2];

        wholeRange["B3"].Clear(ExcelClearOptions.ClearAll);

        var zetDescription = SelectZetValues(dbSheet);
        var zetList= SelectZetValuesList(dbSheet);
        if(zetList.Count > 0)
        {
            var zetRange = wholeRange["A3"];
            zetRange = zetRange.Resize(zetRange.Rows.Count()-1 + zetList.Count, 1);
            zetRange.CellStyle.Color = Syncfusion.Drawing.Color.Red;
            //zetRange.CellStyle = _pensionStyles.ZetLabelStyle;
            var zetRow = zetRange.Row;
            var zetCol = zetRange.Column;
            foreach (var zet in zetList)
            {
                var currentDimVal = wholeRange[zetRow, zetCol];
                var currentDomVal = wholeRange[zetRow, zetCol + 1];
                currentDimVal.Text = zet.dimension;
                currentDomVal.Text = zet.domValue;
                zetRow++;
            }
        }
                            
        
        

        var columnRow = dataRange.Rows.First();
        var exactColumnRow = HelperRoutines.ExtendRangeRowColsDirectional(columnRow, 0, -1, HelperRoutines.HorizontalDirection.Left, HelperRoutines.VerticalDirection.Up);
        exactColumnRow.CellStyle = _pensionStyles.TopColumnNumbersStyle;

        var columnCells = dataRange.Rows.First().Cells.Skip(1);

        foreach (var dataRow in dataRange.Rows)
        {
            var rowLabelCell = dataRow.First();
            if (string.IsNullOrEmpty(rowLabelCell.Value))
            {
                continue;
            }
            var rowLabelCellObj = HelperRoutines.CreateRowColObject(rowLabelCell.AddressR1C1Local);
            foreach (var colCell in columnCells)
            {
                var factX = FindFactFromRowColCurrency(dbSheet, rowLabelCell.Value, colCell.Value, "");
                if (factX is null)
                {
                    continue;
                }
                var cell = dataRange[rowLabelCell.Row, colCell.Column];
                SaveCellValue(cell, factX);
            }
        }

        var titles = FindTopLabelsRange(wholeRange, dataRange);
        if (titles is not null)
        {
            titles.CellStyle.Font.Size = 12;
            titles.CellStyle.WrapText=true;
        }


        //table code
        var tableCodeRange = wholeRange[1, 1];
        tableCodeRange.CellStyle = _pensionStyles.TableCodeStyle;
        //tableCodeRange.                       
        dataRange.ColumnWidth = 30;
        dataRange.CellStyle = _pensionStyles.DataSectionStyle;
        FormatDataSectionForProtectedCells(dataRange);


        //style columns        
        var columnsRange = HelperRoutines.ExtendRangeRowColsDirectional(dataRange.Rows.First(), 0, -1, HelperRoutines.HorizontalDirection.Left, HelperRoutines.VerticalDirection.Up);
        columnsRange.CellStyle = _pensionStyles.TopColumnNumbersStyle;

        //style row numbers
        var rowsRange = HelperRoutines.ExtendRangeRowColsDirectional(dataRange.Columns.First(), -1, 0, HelperRoutines.HorizontalDirection.Left, HelperRoutines.VerticalDirection.Up);
        rowsRange.CellStyle = _pensionStyles.LeftRowNumbersSectionStyle;
        rowsRange.ColumnWidth = 10;

        //row descriptions
        var rowDescriptionRange = wholeRange.Worksheet[dataRange.Row + 1, 1, dataRange.LastRow, dataRange.Column - 1];
        rowDescriptionRange.CellStyle = _pensionStyles.LeftLabelStyle;




        rowDescriptionRange.ColumnWidth = 40;


        return false;
    }

    private bool FillOpenTable280(TemplateSheetInstance dbSheet)
    {

        var worksheet = (Workbook!.Worksheets[$"{dbSheet.SheetTabName.Trim()}"]) ?? throw new Exception($"null worksheet {dbSheet.SheetTabName}");
        var dataRangeName = $"{dbSheet.SheetTabName.Trim()}_data";
        var dataRangeNameObject = Workbook!.Names[dataRangeName];
        var dataRange = dataRangeNameObject.RefersToRange;



        var wholeRangeName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_whole"];
        var wholeRange = wholeRangeName.RefersToRange;

        ClearLinks(wholeRange);
        
        wholeRange["B3"].Clear(ExcelClearOptions.ClearAll);
        
        //wholeRange["A4"].Clear(true);
        //IDataValidation validation = wholeRange["A3"].DataValidation;
        //validation.AllowType = ExcelDataType.Any;
        //validation.ListOfValues = Array.Empty<string>();

        var zetDescription = SelectZetValues(dbSheet);
        var ZetRange = wholeRange["A3"];
        ZetRange.Text = zetDescription;
        ZetRange.CellStyle = _pensionStyles.ZetLabelStyle;
        

        var yOrdinatesForKeys = _SqlFunctions.SelectTableAxisOrdinateInfo(dbSheet.TableID)
              .Where(ord => ord.AxisOrientation == "Y" && ord.IsRowKey && ord.IsOpenAxis)
              .OrderByDescending(ykey => ykey.OrdinateID);


        //expand the data range to include the keys
        var dataRangeWithKeys = HelperRoutines.ExtendRangeRowColsDirectional(dataRange, 0, yOrdinatesForKeys.Count() - 1, HelperRoutines.HorizontalDirection.Left, HelperRoutines.VerticalDirection.None);
        Workbook.Names.Remove(dataRangeName);
        var dataNamedObject = Workbook.Names.Add(dataRangeName);
        dataNamedObject.RefersToRange = dataRangeWithKeys;
        dataRange = dataNamedObject.RefersToRange;

        var numberOfKeys = AssignYKeysToColumns(dbSheet, dataRange);


        var columnCells = dataRange.Rows.First().Cells;
        var rowLabels = SelectOpenRowLabels(dbSheet.TemplateSheetId);

        var rowIndex = dataRange.Row + 1;
        foreach (var rowLabel in rowLabels)
        {
            foreach (var colCell in columnCells)
            {
                var factX = FindFactFromRowColCurrency(dbSheet, rowLabel, colCell.Value, "");
                if (factX is null)
                {
                    continue;
                }
                var cell = dataRange[rowIndex, colCell.Column];
                SaveCellValue(cell, factX);
            }
            rowIndex++;
            Console.Write(".");
        }
        Console.WriteLine();

        //expand the Data range
        var expandedDataRows = worksheet.Range[dataRange.Row, dataRange.Column, worksheet.UsedRange.LastRow, dataRange.LastColumn];

        Workbook.Names.Remove(dataRangeName);
        var dataNamedObjectE = Workbook.Names.Add(dataRangeName);
        dataNamedObjectE.RefersToRange = expandedDataRows;
        dataRange = dataNamedObjectE.RefersToRange;

        var xx3 = 33;


        var titles = FindTopLabelsRange(wholeRange, dataRange);
        if (titles is not null)
        {
            titles.CellStyle = _pensionStyles.TopLabelsStyle;           

        }


        //style data
        if (dataRange is not null)
        {
            dataRange.CellStyle = _pensionStyles.DataSectionStyle;
            dataRange.ColumnWidth = 20;            

            dataRange.Borders[ExcelBordersIndex.EdgeLeft].LineStyle = ExcelLineStyle.Thin;
            dataRange.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thin;
            dataRange.Borders[ExcelBordersIndex.InsideVertical].LineStyle = ExcelLineStyle.Thin;
            dataRange.Borders[ExcelBordersIndex.InsideHorizontal].LineStyle = ExcelLineStyle.Thin;
        }


        ///////////////////////fill keys


        //style columns        
        //var columnsRange = HelperRoutines.ExtendRangeRowColsDirectional(dataRangeWithKeys.Rows.First(), 0, -1, HelperRoutines.HorizontalDirection.Left, HelperRoutines.VerticalDirection.Up);
        var columnLabels = dataRangeWithKeys.Rows.First();
        columnLabels.CellStyle = _pensionStyles.TopColumnNumbersStyle;




        return false;


        int AssignYKeysToColumns(TemplateSheetInstance dbSheet, IRange dataRange)
        {
            var yOrdinatesForKeys = _SqlFunctions.SelectTableAxisOrdinateInfo(dbSheet.TableID)
                  .Where(ord => ord.AxisOrientation == "Y" && ord.IsRowKey && ord.IsOpenAxis)
                  .OrderBy(ykey => ykey.OrdinateID);

            var offsetCol = 0;
            var firstCell = dataRange.Rows.First().First();
            foreach (var yKey in yOrdinatesForKeys)
            {
                var keyPos = firstCell.Offset(0, offsetCol);
                keyPos.Text = yKey.Col;
                //keyPos.CellStyle = _pensionStyles.TopColumnNumbersStyle;
                var keyLabel = firstCell.Offset(-1, offsetCol);
                keyLabel.Text = yKey.AxisLabel;
                //keyLabel.CellStyle = _pensionStyles.TopColumnNumbersStyle;
                offsetCol += 1;
            }
            return yOrdinatesForKeys.Count();
        }
    }

    string SelectZetValues(TemplateSheetInstance dbSheet)
    {

        var zDimsAll = dbSheet.ZDimVal
            .Split("|",StringSplitOptions.RemoveEmptyEntries)
            .Select(zdim => DimDom.GetParts(zdim))
            .Where(dim => dim is not null)
            .ToList();

        var blDimDom = zDimsAll.FirstOrDefault(dd => dd.Dim == "BL");
        var blDesc = "";
        if (blDimDom != null)
        {
            var blMember = _SqlFunctions.SelectMMember(blDimDom.DomAndValRaw);
            blDesc= $"{blDimDom.Dim.Trim()}-{blMember?.MemberLabel?.Trim()}**";
        }

        var restDimDoms = zDimsAll.Where(dd => dd.Dim != "BL");
        
        var stringDims = restDimDoms            
            .Select(dimDom =>
            {
                var member = _SqlFunctions.SelectMMember(dimDom.DomAndValRaw);
                if (member is null)
                {
                    return dimDom.DomAndValRaw;
                }
                return $"{dimDom?.Dim.Trim()}-{member?.MemberLabel?.Trim()}"; 
            })
            .Where(dim => dim is not null);
        
        var resDesc = string.Join("*", stringDims);
        var fullDesc = $"{blDesc}{resDesc}";

        var xx222 = 33;
        return fullDesc;
    }


    List<(string dimension, string domValue)> SelectZetValuesList(TemplateSheetInstance dbSheet)
    {

        var zDimsAll = dbSheet.ZDimVal
            .Split("|", StringSplitOptions.RemoveEmptyEntries)
            .Select(zdim => DimDom.GetParts(zdim))
            .Where(dim => dim is not null)
        .ToList();

        if (!zDimsAll.Any()) return new List<(string,string)>();

        var index = zDimsAll.FindIndex(item => item.Dim == "BL");
        if (index != -1)
        {
            var blItem= zDimsAll.ElementAt(index);
            zDimsAll.RemoveAt(index); 
            zDimsAll.Insert(0, blItem);
        }

        List<(string dim,string memberVal)> allVals = zDimsAll
            .Where(dm => !string.IsNullOrEmpty(dm.Dim))
            .Select(dimDom =>
            {
                var dimension = _SqlFunctions.SelectDimensionByCode(dimDom.Dim);
                var member = _SqlFunctions.SelectMMember(dimDom.DomAndValRaw);
                if (dimension is null) return ("","");
                var tx = member is null
                    ? (dimension?.DimensionLabel?.Trim()??"", dimDom ?.DomAndValRaw ?.Trim()??"")
                    : (dimension?.DimensionLabel?.Trim()??"",member?.MemberLabel?.Trim()??"");
                return tx;
            })            
            .ToList();


        return allVals;
    }



    private void FormatDataSectionForProtectedCells(IRange dataRange)
    {
        foreach (var cell in dataRange.Cells)
        {
            var diagonal = cell.CellStyle.Borders[ExcelBordersIndex.DiagonalUp].LineStyle;
            if (diagonal == ExcelLineStyle.Thin)
            {
                cell.CellStyle = _pensionStyles.DiagonalStyle;
                //cell.CellStyle.ColorIndex = ExcelKnownColors.Grey_50_percent;
                //cell.CellStyle.Borders[ExcelBordersIndex.DiagonalUp].LineStyle = ExcelLineStyle.None;
                //cell.CellStyle.Borders[ExcelBordersIndex.DiagonalDown].LineStyle = ExcelLineStyle.None;
            }

        }
    }

    private static IRange? FindTopLabelsRange(IRange wholeRange, IRange dataRange)
    {
        IRange aboveRange = null; ;
        var rowsTocheck = wholeRange[1, dataRange.Column, dataRange.Row - 1, dataRange.LastColumn];
        var xx = 33;




        foreach (var row in rowsTocheck.Rows.Reverse())
        {
            var cells = row.Cells.Select(cel => cel.Text).ToList();
            var hasValue = row.Cells.Any(cell => !string.IsNullOrEmpty(cell.Value));
            if (!hasValue)
            {
                aboveRange = row;
                break;
            }
        }
        if (aboveRange == null)
        {
            return null;
        }
        var titleRange = wholeRange[aboveRange.Row + 1, rowsTocheck.Column, rowsTocheck.LastRow, rowsTocheck.LastColumn];
        return titleRange;
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
