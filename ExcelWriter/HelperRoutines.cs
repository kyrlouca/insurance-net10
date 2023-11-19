namespace ExcelWriter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.Implementation.PivotAnalysis;

internal class HelperRoutines
{
    //***If the new instance for ExcelEngine is created in using statement, then there is no need to closing workbook and disposing excelEngine.
    public static (IWorkbook? workbook,string errorMessage ) OpenExistingExcelWorkbook(ExcelEngine excelEngine, string fileName)
	{
		//**  excel engine will be disposed by caller
		var message = "";
		Console.WriteLine($"getWorkbook fileName:{fileName}");
		//ExcelEngine excelEngine = new();
		
		if (string.IsNullOrEmpty(fileName))
		{
			message=$"filename is empty";
			return (null,message);
		}
		try
		{
			using var inputStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
			IWorkbook workbook = excelEngine.Excel.Workbooks.Open(inputStream);
			return (workbook,"");
		}
		catch (FileNotFoundException fnf)
		{
			message = $"The file xx:+{fileName}+ could not be found :{fnf.Message}";
			Console.WriteLine(message);
			return (null, message);
		}
		catch (IOException e)
		{
			message= $"The file xx: +{fileName}+ could not be opened: {e.Message}";
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

	public static (IWorkbook?,string message ) CreateExcelWorkbook(ExcelEngine excelEngine)
	{		

		IApplication application = excelEngine.Excel;
		application.DefaultVersion = ExcelVersion.Xlsx;
		
		try
		{
			IWorkbook workbook = application.Workbooks.Create(0);
			//Creating a Sheet
			//IWorksheet sheet = workbook.Worksheets.Create();
			return (workbook,"");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"{ex.Message}");
			return (null,ex.Message);
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

	public static (bool isValid, string message)  CopyFile(string originFileName, string destFileName)
	{

		try
		{
			System.IO.File.Copy(originFileName, destFileName, true);
			return (true, "");
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			return (false, ex.Message);
			throw;
		}

	}

	public static string JoinRowCells(IRange? row)
	{
		if (row is null) { return ""; };
		var cellList = row?.Cells.ToList().Select(a => a.Value2.ToString()) ?? new List<string>();
		var result = string.Join("#", cellList);
		return result;
	}

	public record RangeCoordinates(int StartRow,int StartCol,int EndRow, int EndCol);
	public static RangeCoordinates OffsetRange(IRange range, int startRow, int startCol)
	{ 		
		var endRow = range.LastRow - (range.Row - startRow);
		var endCol = range.LastColumn - (range.Column - startCol);
		return new RangeCoordinates(startRow, startCol, endRow,endCol);
	}

	public static IRange ExtendRangeLowerCorner(IRange range, int rowInc, int colInc)
	{		
		var lastRow =  Math.Max(0, range.LastRow + rowInc);
		var lastCol = Math.Max( 0, range.LastColumn + colInc);

		var newRange= range.Application.Range[range.Row,range.Column, lastRow, lastCol];
		return newRange;				
	}

	public record RowColObject(string AddressR1C1, int Row, int Col);
	public static RowColObject? CreateRowColObject(string addreessR1C1)
	{
		var rg = new Regex("R(\\d*)C(\\d*)");		
		var match = rg.Match(addreessR1C1);
		if (!match.Success) return null;
		return new RowColObject(addreessR1C1, int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));		
	}
}
