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
using ExcelWriter.ExcelDataModels;
using System.ComponentModel;
using Syncfusion.XlsIO.Parser.Biff_Records.ObjRecords;

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


        if (_parameterData.IsDevelop)
        {

            //var debugClosedTableCode = "";
            var debugClosedTableCode = "S.04.02.01.02";
            if (!string.IsNullOrEmpty(debugClosedTableCode))
            {
                Console.Write($"In Develop and filtering Closed: {debugClosedTableCode}");
            }

            dbClosedSheets = string.IsNullOrWhiteSpace(debugClosedTableCode)
             ? dbClosedSheets
             : dbClosedSheets.Where(tb => tb.TableCode?.Trim() == debugClosedTableCode);

        }


        foreach (var dbClosedSheet in dbClosedSheets)
        {

            Console.WriteLine($"Populate Closed:{dbClosedSheet.SheetCode}");
            //Closed:S.04.01.01.02__s2c_GA_x14__s2c_LB_x146
            FillClosedTable280(dbClosedSheet);
        }


        var dbOpenSheets = _SqlFunctions.SelectTemplateSheets(_documentId)
            .Where(sheet => sheet.IsOpenTable);

        if (_parameterData.IsDevelop)
        {
            var debugOpenTableCode = "xS.14.01.01.01";
            //var debugOpenTableCode = "";
            if (!string.IsNullOrEmpty(debugOpenTableCode))
            {
                Console.Write($"In Develop and filtering Open: {debugOpenTableCode}");
            }

            dbOpenSheets = string.IsNullOrWhiteSpace(debugOpenTableCode)
                 ? dbOpenSheets
                 : dbOpenSheets.Where(tb => tb.TableCode.Trim() == debugOpenTableCode);
        }

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

    private enum DimensionType { Currency, Country, None }
    private bool FillClosedTable280(TemplateSheetInstance dbSheet)
    {
        //normally, facts with row,col are unique within a sheet. However, the design allows for multiple facts if they have different currency or country
        //for multi facts, we need to create additional columns and write the currency/country above the column
        var workSheet = Workbook!.Worksheets[dbSheet.SheetTabName.Trim()];

        var dataName = Workbook!.Names[$"{dbSheet.SheetTabName.Trim()}_data"];
        var dataRange = dataName.RefersToRange;

        var wholeRangeName = Workbook.Names[$"{dbSheet.SheetTabName.Trim()}_whole"];
        var wholeRange = wholeRangeName.RefersToRange;

        ClearLinks(wholeRange);


        var columnRow = dataRange.Rows.First();
        var exactColumnRow = HelperRoutines.ExtendRangeRowColsDirectional(columnRow, 0, -1, HelperRoutines.HorizontalDirection.Left, HelperRoutines.VerticalDirection.Up);
        exactColumnRow.CellStyle = _pensionStyles.TopColumnNumbersStyle;

        var columnCells = dataRange.Rows.First().Cells.Skip(1);
        var columnLabels=columnCells.Select(cc=>cc.Value).ToList();
        var extendedColumnsRow = HelperRoutines.ExtendRangeRowColsDirectional(dataRange.Rows.First(), 0, 10, HelperRoutines.HorizontalDirection.Right, HelperRoutines.VerticalDirection.None);

        ///CURRENCY/COUNTRY LABELS
        var multiTemplate = MultiDimensionTemplatesNew.Templates.FirstOrDefault(tmp => tmp.TemplateCode == dbSheet.TableCode);
        var isMultiTemplate = multiTemplate is not null;
    
        var zetMembers = GetSheetDistinctValuesNew(dbSheet.TemplateSheetId, multiTemplate!.TemplateCode, multiTemplate.Dimension, multiTemplate.Domain);
        var zetMembersCount = zetMembers.Count;
        
        if (isMultiTemplate)
        {
            //var sortedCurencyCountryList = SpecialOrderBy(currenciesOrCountriesXbrlCodes, "x0").ToList();                        
            //datarange includes the column numbers
            for (var i = 0; i < columnLabels.Count(); i++)
            {
                var columnLabelStr = columnLabels[i];
                //columns have been inserted but still columnLabelCell will point to the first label found 
                var columnLabelCell = extendedColumnsRow.FirstOrDefault(cc => cc.Value == columnLabelStr);
                for (var j = 0; j < zetMembersCount; j++)
                {
                    if (j > 0)
                    {
                        workSheet.InsertColumn(columnLabelCell.Column-1 );
                    }
                    var colZetLabel = wholeRange[dataRange.Row - 3, columnLabelCell.Column + j];
                    colZetLabel.Text = zetMembers[j].MemberLabel;
                    colZetLabel.CellStyle = _pensionStyles.TopLabelsStyle;
                    colZetLabel.ColumnWidth = 30;

                    var colLabel= wholeRange[dataRange.Row, colZetLabel.Column + j];
                    colLabel.Text = columnLabelStr;
                    colLabel.CellStyle = _pensionStyles.TopColumnNumbersStyle;
                }
            }
        };

        var currencyCount = zetMembers.Count == 0 ? 1 : zetMembers.Count;//to loop even for non-currencies
        foreach (var dataRow in dataRange.Rows)
        {
            //rowLabelCell the cell which has the row : R0110
            //then will go through all the columns for this row

            var rowLabelCell = dataRow.First();
            if (string.IsNullOrEmpty(rowLabelCell.Value))
            {
                continue;
            }


            foreach (var colCell in columnCells)
            {
                for (var i = 0; i < currencyCount; i++)
                {
                    var rowLabelCellObj = HelperRoutines.CreateRowColObject(rowLabelCell.AddressR1C1Local);

                    //s2c_dim:LR(s2c_GA:CY)
                    //var xbrlCode = dimensionType == DimensionType.None ? "" : currenciesOrCountriesXbrlCodes[i];
                    //var xbrlCode = isMultiTemplate ?  currenciesOrCountriesXbrlCodes[i]:"";

                    var cell = dataRange[rowLabelCell.Row, colCell.Column + i];
                    if (isMultiTemplate)
                    {
                        cell.CellStyle.Borders.LineStyle = ExcelLineStyle.Thin;
                        cell.CellStyle.Borders[ExcelBordersIndex.DiagonalUp].LineStyle = ExcelLineStyle.None;
                        cell.CellStyle.Borders[ExcelBordersIndex.DiagonalDown].LineStyle = ExcelLineStyle.None;
                    }
                    var factX = FindFactFromRowColCurrency(dbSheet, rowLabelCell.Value, colCell.Value, zetMembers[i].MemberXBRLCode, isMultiTemplate);

                    FormatCellValue(cell, factX);

                }
            }
        }


        //clear topRange
        var lastTopEmptyRow = FindTopLastEmptyRow(wholeRange, dataRange);
        if (lastTopEmptyRow > 0)
        {
            var topClearRange = wholeRange[1, 1, lastTopEmptyRow, dataRange.LastColumn + 4];
            if (topClearRange is not null)
            {
                topClearRange.Clear(ExcelClearOptions.ClearAll);
            }
        }


        //***********Table code
        var tableCode = wholeRange["A1"];
        tableCode.Text = dbSheet.TableCode;
        tableCode.CellStyle = _pensionStyles.TableCodeStyle;

        //template code        
        var tbl = _SqlFunctions.SelectTable(dbSheet.TableCode);
        var tblLabel = wholeRange["A2"];
        tblLabel.Text = tbl?.TableLabel;
        tblLabel.CellStyle = _pensionStyles.HeaderStyle;


        //************ set the zets        
        FillZetValuesAtTheTop(dbSheet, wholeRange);


        //format the top row title
        var titles = FindTopLabelsRange(wholeRange, dataRange, false);
        if (titles is not null)
        {
            titles.CellStyle.Font.Size = 12;
            titles.CellStyle.WrapText = true;
        }


        //expand the Data range
        if (currencyCount > 1)
        {
            var expandedDataRows = dataRange[dataRange.Row, dataRange.Column, dataRange.LastRow, dataRange.LastColumn + currencyCount - 1];
            var dataRangeName = dataName.Name;
            Workbook.Names.Remove(dataRangeName);
            var dataNamedObjectE = Workbook.Names.Add(dataRangeName);
            dataNamedObjectE.RefersToRange = expandedDataRows;
            dataRange = dataNamedObjectE.RefersToRange;


        };


        //data Range.                       
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

    private int FillZetValuesAtTheTop(TemplateSheetInstance dbSheet, IRange wholeRange)
    {

        var zetList = SelectZetValuesList(dbSheet);
        if (zetList.Count > 0)
        {
            var zetRange = wholeRange[3, 1, 3 + zetList.Count - 1, 2];
            zetRange.CellStyle = _pensionStyles.ZetLabelStyle;
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
        return zetList.Count;
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
                var factX = FindFactFromRowColCurrency(dbSheet, rowLabel, colCell.Value, "", false);
                if (factX is null)
                {
                    continue;
                }
                var cell = dataRange[rowIndex, colCell.Column];
                FormatCellValue(cell, factX);
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


        //clear topRange
        var lastTopEmptyRow = FindTopLastEmptyRow(wholeRange, dataRange);

        if (lastTopEmptyRow > 0)
        {
            var topClearRange = wholeRange[1, 1, lastTopEmptyRow, dataRange.LastColumn];
            if (topClearRange is not null)
            {
                topClearRange.Clear(ExcelClearOptions.ClearAll);
            }
        }


        //************ set the zets        
        var zetLines = FillZetValuesAtTheTop(dbSheet, wholeRange);


        // Table Code
        var tableCode = wholeRange["A1"];
        tableCode.Text = dbSheet.TableCode;
        tableCode.CellStyle = _pensionStyles.TableCodeStyle;

        //template code        
        var tbl = _SqlFunctions.SelectTable(dbSheet.TableCode);
        var tblLabel = wholeRange["A2"];
        tblLabel.Text = tbl?.TableLabel;
        tblLabel.CellStyle = _pensionStyles.HeaderStyle;

        //style titles above datarange
        var titles = FindTopLabelsRange(wholeRange, dataRange, true);
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


    List<(string dimension, string domValue)> SelectZetValuesList(TemplateSheetInstance dbSheet)
    {

        var zDimsAll = dbSheet.ZDimVal
            .Split("|", StringSplitOptions.RemoveEmptyEntries)
            .Select(zdim => DimDom.GetParts(zdim))
            .Where(dim => dim is not null)
        .ToList();

        if (!zDimsAll.Any()) return new List<(string, string)>();

        var index = zDimsAll.FindIndex(item => item.Dim == "BL");
        if (index != -1)
        {
            var blItem = zDimsAll.ElementAt(index);
            zDimsAll.RemoveAt(index);
            zDimsAll.Insert(0, blItem);
        }

        List<(string dim, string memberVal)> allVals = zDimsAll
            .Where(dm => !string.IsNullOrEmpty(dm.Dim))
            .Select(dimDom =>
            {
                var dimension = _SqlFunctions.SelectDimensionByCode(dimDom.Dim);
                var member = _SqlFunctions.SelectMMember(dimDom.DomAndValRaw);
                if (dimension is null) return ("", "");
                var tx = member is null
                    ? (dimension?.DimensionLabel?.Trim() ?? "", dimDom?.DomAndValRaw?.Trim() ?? "")
                    : (dimension?.DimensionLabel?.Trim() ?? "", member?.MemberLabel?.Trim() ?? "");
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
            }

        }
    }



    private static int FindTopLastEmptyRow(IRange wholeRange, IRange dataRange)
    {

        //find the row above the labels which is empty
        var rowsTocheck = wholeRange[1, dataRange.Column, dataRange.Row - 1, dataRange.LastColumn];

        foreach (var row in rowsTocheck.Rows.Reverse())
        {
            var cells = row.Cells.Select(cel => cel.Text).ToList();
            var hasValue = row.Cells.Any(cell => !string.IsNullOrEmpty(cell.Value));
            if (!hasValue)
            {
                return row.Row;
            }
        }
        return 0;
    }

    private static IRange? FindTopLabelsRange(IRange wholeRange, IRange dataRange, bool isOpenTable)
    {

        //find the range for the labels starting from the data until you find an empty line
        //if no empty line, then return the row above the datarange
        IRange aboveRange = null; ;
        var rowsTocheck = wholeRange[1, dataRange.Column, dataRange.Row - 1, dataRange.LastColumn];

        foreach (var row in rowsTocheck.Rows.Reverse())
        {
            //skip 2 to avoid zet values if open table           
            var cellsTocheck = isOpenTable
                ? row.Cells.Skip(2)
                : row.Cells;
            var hasValue = cellsTocheck.Any(cell => !string.IsNullOrEmpty(cell.Value));
            if (!hasValue)
            {
                aboveRange = row;
                break;
            }
        }
        if (aboveRange == null)
        {
            //fuck
            //var fftitleRange = wholeRange[dataRange.Row - 1, rowsTocheck.Column, rowsTocheck.LastRow, rowsTocheck.LastColumn];
            var fftitleRange = wholeRange[dataRange.Row, rowsTocheck.Column, rowsTocheck.LastRow, rowsTocheck.LastColumn];
            return fftitleRange;
            return null;
        }
        var titleRange = wholeRange[aboveRange.Row + 1, rowsTocheck.Column, rowsTocheck.LastRow, rowsTocheck.LastColumn];
        return titleRange;
    }


    private void FormatCellValue(IRange cell, TemplateSheetFact? fact)
    {
        cell.CellStyle = _pensionStyles.DataSectionStyle;
        if (fact == null)
        {
            return;
        }
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
                var rgx = new Regex(@"s2c_dim:\w\w\((.+)\)");
                var match = rgx.Match(fact.TextValue);
                //some operators place enum values in keys like this s2c_dim:BL(s2c_LB:x136)
                var cleanVal = match.Success ? match.Groups[1].Value : fact.TextValue;
                var memDescription = XbrlCodeToValue(cleanVal);
                cell.Text = string.IsNullOrEmpty(memDescription) ? fact.TextValue : memDescription;
                break;
            case "I": //integer
                cell.Number = (int)Math.Floor(fact.NumericValue);
                cell.HorizontalAlignment = ExcelHAlign.HAlignRight;
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


    
    //private TemplateSheetFact? FindFactFromRowColCurrency(TemplateSheetInstance sheet, string row, string col, string domMemberValue, DimensionType dimensionType)
    private TemplateSheetFact? FindFactFromRowColCurrency(TemplateSheetInstance sheet, string row, string col, string domMemberValue, bool isUseSignature)
    {

        //more than one fact with the same row,col but with different currency        
        //currency is "EUR" or "USD", ...
        //but search for safety s2c_CU:EUR
        var sqlFact =
      @"
		SELECT *                  
		FROM dbo.TemplateSheetFact fact
		WHERE
		  fact.TemplateSheetId = @sheetId
		  AND fact.Row = @row
		  AND fact.Col = @col                                    
	";
        //s2c_CU:EUR


        var sqlSignaturelike = $"AND fact.DataPointSignature like '%{domMemberValue}%'";
        var sqlFactBySignature =
        @$"
            SELECT *    
            FROM dbo.TemplateSheetFact fact
            WHERE
              fact.TemplateSheetId = @sheetId
              AND fact.Row = @row
              AND fact.Col = @col
            {sqlSignaturelike}
     ";


        var sqlSelectFact = isUseSignature ? sqlFactBySignature : sqlFact;
        


        using var connectionLocalDb = new SqlConnection(_parameterData.SystemConnectionString);

        var fact = connectionLocalDb.QueryFirstOrDefault<TemplateSheetFact>(sqlSelectFact, new { sheetId = sheet.TemplateSheetId, row, col });
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



    private List<MMember> GetSheetDistinctValuesNew(int TemplateSheetId, string TemplateCode, string dimension, string domain)
    {
        //var rgx = new Regex(@"s2c_CU:(.+?)\)");
        //DimensionPrefix : "s2c_CU" Or "s2c_GA"

        //S.04.04.01.02 s2c_dim: LA(* [377; 1238; 0])
        //S.04.02.01.02  s2c_dim: LG(s2c_GA: GR)


        if (string.IsNullOrEmpty(dimension))
        {
            return new List<MMember>();
        }

        var facts = _SqlFunctions.SelectFactsForSheetId(TemplateSheetId);
        if (!facts.Any())
        {
            return new List<MMember>();
        }
        var rgx = string.IsNullOrEmpty(domain) ? new Regex(@$"s2c_dim:{dimension.Trim()}\((.+?:.+?)\)") : new Regex(@$"{dimension.Trim()}\((s2c_{domain.Trim()}:.+?)\)");

        var domainValues = facts
                .SelectMany(fact => rgx.Matches(fact.DataPointSignature)
                .Cast<Match>()
                .SelectMany(match => match.Groups
                    .Cast<Group>()
                    .Skip(1) // Skip group 0 (the entire match)
                    .Select(group => group.Value))
                 ).Distinct()
                 .ToList();

        var members = (domainValues?? new List<string>())
            .Select(dm => _SqlFunctions.SelectMMember(dm))
            .Where(m => m is not null)
            .ToList();

        return members;
    }




    private List<string> GetSheetDistinctValues(int TemplateSheetId, string memberXbrlPrefix, string dimAndDom)
    {
        //var rgx = new Regex(@"s2c_CU:(.+?)\)");
        //DimensionPrefix : "s2c_CU" Or "s2c_GA"
        var rgx = new Regex(@$"\(({memberXbrlPrefix}:.+?)\)");
        var facts = _SqlFunctions.SelectFactsForSheetId(TemplateSheetId);

        var distinctCurrencies = facts
                .SelectMany(fact => rgx.Matches(fact.DataPointSignature)
                .Cast<Match>()
                .SelectMany(match => match.Groups
                    .Cast<Group>()
                    .Skip(1) // Skip group 0 (the entire match)
                    .Select(group => group.Value))
                 ).Distinct()
                 .ToList();

        var pref = dimAndDom.Split("|");
        if (pref.Length == 2)
        {
            //MET(s2md_met:ri2483)|s2c_dim:LA(s2c_GA:x77)|s2c_dim:LG(s2c_GA:GR)|s2c_dim:LR(s2c_GA:x14)|s2c_dim:TZ(s2c_LB:x162)
            //s2c_dim:LG(s2c_GA:GR)
            //LG\((s2c_GA:.+?)\)
            var rgxNewx = $@"{pref[0]}\(sc2_{pref[1]}";



            var rgxNew = new Regex(@$"{pref[0]}\((s2c_{pref[1]}:.+?)\)");
            var distinctCurrencies2 = facts
                    .SelectMany(fact => rgxNew.Matches(fact.DataPointSignature)
                    .Cast<Match>()
                    .SelectMany(match => match.Groups
                        .Cast<Group>()
                        .Skip(1) // Skip group 0 (the entire match)
                        .Select(group => group.Value))
                     ).Distinct()
                     .ToList();

        }

        return distinctCurrencies;
    }

    public List<string> SpecialOrderBy(List<string> list, string firstElement)
    {
        var newList = list.Select(item => item).ToList();

        var index = newList.FindIndex(item => item.Contains(firstElement));
        if (index != -1)
        {
            var blItem = newList.ElementAt(index);
            newList.RemoveAt(index);
            newList.Insert(0, blItem);
        }
        return newList;
    }




}
