namespace ExcelWriter;
using ExcelWriter.DataModels;
using Shared.SharedHost;
using Shared.DataModels;
using Shared.CommonRoutines;
using Shared.HostRoutines;

using ExcelWriter.DataModels;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using Shared.HostRoutines;
using Serilog;
using Syncfusion.XlsIO;

public record struct IndexSheetListItem
{


	//public ISheet Sheet { get; init; }
	public string TabSheetName { get; init; }
	public string Description { get; init; }
	public IndexSheetListItem(string tabSheetName, string description)
	{
		TabSheetName = tabSheetName;
		Description = description;
	}
}
public class IndexSheetList
{
	private readonly IParameterHandler _parameterHandler;
	ParameterData _parameterData = new();
	private readonly ILogger _logger;
	private readonly ICommonRoutines _commonRoutines;
	public string SheetName { get; init; }
	public string SheetDescription { get; init; }
	List<IndexSheetListItem> SheetRecords { get; set; } = new List<IndexSheetListItem>();
	private IWorkbook? _workbook;	
	
	public IndexSheetList(IParameterHandler parametersHandler, ILogger logger, ICommonRoutines commonRoutines)
	{
		_parameterHandler = parametersHandler;
		_logger = logger;
		_commonRoutines = commonRoutines;
		
		//SheetName = sheetName;
		//SheetDescription = sheetDescription;
		//IndexSheet = ExcelBook.CreateSheet(sheetName);
	}



	public List<IndexSheetListItem> CreateSheetRecordsFromDb(List<TemplateSheetInstance> dbSheets)
	{

		var list = new List<IndexSheetListItem>();
		using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

		foreach (var dbSsheet in dbSheets)
		{
			var sheetName = dbSsheet.SheetTabName.Trim();

			var sqlTab = @"select tab.TableLabel,tab.TableCode from mTable tab where tab.TableID = @tableId";
			var tab = connectionEiopa.QuerySingleOrDefault<MTable>(sqlTab, new { dbSsheet.TableID });

			var tableCodeList = tab.TableCode.Split(".").Take(4);
			var templateCode = string.Join(".", tableCodeList);
			var sqlTemplate = @"select  TemplateOrTableLabel from mTemplateOrTable tt where tt.TemplateOrTableCode = @templateCode ";

			var templateLabel = connectionEiopa.QuerySingleOrDefault<string>(sqlTemplate, new { templateCode });
			var desc = $"{templateLabel} ## {tab.TableLabel}";

			list.Add(new IndexSheetListItem(sheetName, desc));
		}
		SheetRecords = list;
		return list;
	}


	public IWorksheet PopulateIndexSheet(IWorkbook workbook)
	{
		_workbook=workbook;
		var indexSheet= _workbook.Worksheets.Create("IndexSheet");
		//var titleRow = IndexSheet.CreateRow(0);
		//var title = titleRow.CreateCell(0);

		indexSheet["A1"].Text = "IndexList";
		

		var index = 2;
		foreach (var sheetRecord in SheetRecords)
		{
			//var row = IndexSheet.CreateRow(index++);
			//var cell = row.CreateCell(0);
			//cell.SetCellValue(sheetRecord.TabSheetName);

			//var link = new XSSFHyperlink(HyperlinkType.Document)
			//{
			//	Address = @$"'{sheetRecord.TabSheetName}'!A1"
			//};
			//cell.Hyperlink = link;
			//cell.CellStyle = WorkbookStyles?.HyperStyle;

			//var titleCell = row.CreateCell(1);
			//titleCell.SetCellValue(sheetRecord.Description);
			//IndexSheet.SetColumnWidth(0, 7000);

		}
		return indexSheet;

	}
	public void AddSheetRecord(IndexSheetListItem sheetRecord)
	{
		SheetRecords.Add(sheetRecord);
	}

	public void RemoveSheet(string tabSheetName)
	{
		SheetRecords = SheetRecords.Where(r => r.TabSheetName != tabSheetName).ToList();
	}
	public void RemoveSheets(List<string> tabSheetNames)
	{
		foreach (var tabSheetName in tabSheetNames)
		{
			var shIdx = _workbook.Worksheets[tabSheetName];
			if (shIdx is null)
			{
				continue;
			}
			_workbook.Worksheets.Remove(shIdx);
			 
			var shrIdx = SheetRecords.FirstOrDefault(r => r.TabSheetName == tabSheetName);

			if (shIdx is null)
			{
				SheetRecords.Remove(shrIdx);
			}
			else
			{
				Console.WriteLine($"sheet {tabSheetName} not found");
			}

		}
	}

	public void SortSheetRecords()
	{
		SheetRecords.Sort((IndexSheetListItem a, IndexSheetListItem b) => string.Compare(a.TabSheetName, b.TabSheetName));
		//SheetRecords.ForEach(sr => ExcelBook.SetSheetOrder(sr.TabSheetName.Trim(), SheetRecords.IndexOf(sr)));
	}
}
