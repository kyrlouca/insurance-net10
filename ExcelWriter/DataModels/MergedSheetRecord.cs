namespace ExcelWriter.DataModels;
using Shared.DataModels;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal readonly record struct MergedSheetRecord
{

	public bool IsValid { get; init; }
	public IWorksheet? TabSheet { get; init; }
	public List<TemplateSheetInstance> ChildrenSheetInstances { get; init; }
	public string SheetDescription { get; init; }
	public MergedSheetRecord(IWorksheet? tabSheet, string sheetDescription, List<TemplateSheetInstance> childrenSheetInstances, bool isMerged)
	{
		TabSheet = tabSheet;
		SheetDescription = sheetDescription;
		ChildrenSheetInstances = childrenSheetInstances;
		IsValid = isMerged;
	}
}