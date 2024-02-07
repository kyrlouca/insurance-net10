namespace ExcelWriter.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.Implementation.PivotAnalysis;

internal class HelperRoutines
{
    //***If the new instance for ExcelEngine is created in using statement, then there is no need to closing workbook and disposing excelEngine.
    public static (IWorkbook? workbook, string errorMessage) OpenExistingExcelWorkbook(ExcelEngine excelEngine, string fileName)
    {
        //**  excel engine will be disposed by caller
        var message = "";
        Console.WriteLine($"getWorkbook fileName:{fileName}");
        //ExcelEngine excelEngine = new();

        if (string.IsNullOrEmpty(fileName))
        {
            message = $"filename is empty";
            return (null, message);
        }
        try
        {
            using var inputStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            IWorkbook workbook = excelEngine.Excel.Workbooks.Open(inputStream);
            return (workbook, "");
        }
        catch (FileNotFoundException fnf)
        {
            message = $"The file xx:+{fileName}+ could not be found :{fnf.Message}";
            Console.WriteLine(message);
            return (null, message);
        }
        catch (IOException e)
        {
            message = $"The file xx: +{fileName}+ could not be opened: {e.Message}";
            Console.WriteLine(message);
            return (null, message);
        }
        catch (Exception e)
        {
            message = $"The file xx: +{fileName}+ is NOT a valid EXCEL file: {e.Message}";
            Console.WriteLine(message);
            return (null, message);
        }

    }

    public static (IWorkbook?, string message) CreateExcelWorkbook(ExcelEngine excelEngine)
    {

        IApplication application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        try
        {
            IWorkbook workbook = application.Workbooks.Create(0);
            return (workbook, "");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}");
            return (null, ex.Message);
        }


    }


    static public (bool isValid, string message) SaveWorkbook(IWorkbook? workbook, string path)
    {
        if (workbook == null) { return (false, "workbook is null"); }
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
        try
        {
            workbook.SaveAs(fileStream);

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return (false, $"{ex.Message}");
        }
        return (true, "");
    }



    public static string JoinRowCells(IRange? row)
    {
        if (row is null) { return ""; };
        var cellList = row?.Cells.ToList().Select(a => a.Value2.ToString()) ?? new List<string>();
        var result = string.Join("#", cellList);
        return result;
    }

    public record RangeCoordinates(int StartRow, int StartCol, int EndRow, int EndCol);
    public static RangeCoordinates OffsetRange(IRange range, int startRow, int startCol)
    {
        var endRow = range.LastRow - (range.Row - startRow);
        var endCol = range.LastColumn - (range.Column - startCol);
        return new RangeCoordinates(startRow, startCol, endRow, endCol);
    }

    public static IRange ExtendRangeRowCols(IRange range, int rowInc, int colInc)
    {
        //works with negative numbers as well
        //extends to the right (colInc) 
        var lastRow = Math.Max(0, range.LastRow + rowInc);
        var lastCol = Math.Max(0, range.LastColumn + colInc);

        var newRange = range.Worksheet.Range[range.Row, range.Column, lastRow, lastCol];
        return newRange;
    }

    public enum HorizontalDirection
    {
        Left, Right
    };

    public enum VerticalDirection
    {
        Up, Down
    };

    public static IRange ExtendRangeRowColsDirectional(IRange range, int rowInc, int colInc, HorizontalDirection horizontalDirection, VerticalDirection verticalDirection)
    {
        //works with negative numbers as well
        //extends horizontally colInc (right or left) and vertically (up or down) (rowInc) 

        var startRow = verticalDirection == VerticalDirection.Up ? Math.Max(0, range.Row - rowInc) : range.Row;
        var endRow = verticalDirection == VerticalDirection.Up ? range.LastRow : Math.Max(0, range.LastRow + rowInc);

        var startCol = horizontalDirection == HorizontalDirection.Left ? Math.Max(0, range.Column - colInc) : range.Column;
        var endCol = horizontalDirection == HorizontalDirection.Left ? range.LastColumn : Math.Max(0, range.LastColumn + colInc);


        var newRange = range.Worksheet.Range[startRow, startCol, endRow, endCol];
        return newRange;
    }


    public record RowColObject(string AddressR1C1, int Row, int Col, int LastRow, int LastCol);
    public static RowColObject? CreateRowColObject(string addreessR1C1)
    {
        var rg = new Regex("R(\\d*)C(\\d*)");
        var match = rg.Matches(addreessR1C1);
        if (match is null)
        {
            return null;
        }
        var row = int.Parse(match[0].Groups[1].Value);
        var col = int.Parse(match[0].Groups[2].Value);

        RowColObject? rowcolObject = match.Count switch
        {
            1 => new RowColObject(addreessR1C1, row, col, row, col),
            2 => new RowColObject(addreessR1C1, row, col, int.Parse(match[1].Groups[1].Value), int.Parse(match[1].Groups[2].Value)),
            _ => null
        };
        return rowcolObject;

    }
}
