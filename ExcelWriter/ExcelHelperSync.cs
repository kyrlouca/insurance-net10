namespace ExcelWriter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Syncfusion.XlsIO;


internal class ExcelHelperSync
{
	public static IWorkbook? OpenExistingExcelWorkbook(string fileName)
	{
		Console.WriteLine($"getWorkbook fileName:{fileName}");
		ExcelEngine excelEngine = new();
		if (string.IsNullOrEmpty(fileName))
		{
			return null;
		}
		try
		{
			using var inputStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
			IWorkbook workbook = excelEngine.Excel.Workbooks.Open(inputStream);
			return workbook;
		}
		catch (FileNotFoundException fnf)
		{
			Console.WriteLine($"The file xx:+{fileName}+ could not be found :{fnf.Message}");
			return null;
		}
		catch (IOException e)
		{
			Console.WriteLine($"The file xx: +{fileName}+ could not be opened: {e.Message}");
			return null;
		}
		catch (Exception e)
		{
			Console.WriteLine($"The file xx: +{fileName}+ is NOT a valid EXCEL file: {e.Message}");
			return null;
		}

	}

	public static IWorkbook? CreateExcelWorkbook(ExcelEngine excelEngine)
	{		

		IApplication application = excelEngine.Excel;
		application.DefaultVersion = ExcelVersion.Xlsx;
		
		try
		{
			IWorkbook workbook = application.Workbooks.Create(0);
			//Creating a Sheet
			IWorksheet sheet = workbook.Worksheets.Create();
			return workbook;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"{ex.Message}");
			return null;
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

	public static void CopyFile(string originFileName, string destFileName)
	{

		try
		{
			System.IO.File.Copy(originFileName, destFileName, true);
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
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


}
