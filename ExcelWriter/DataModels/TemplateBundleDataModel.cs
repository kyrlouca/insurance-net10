using Shared.DataModels;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelWriter.DataModels;


public readonly record struct TemplateBundle
{
	public string TemplateCode { get; init; }
	public string TemplateDescription { get; init; }
	public List<String> TableCodes { get; init; }
	public TemplateBundle(string templateTableCode, string templateDescription, List<string> tableCodes)
	{
		TemplateCode = templateTableCode;
		TemplateDescription = templateDescription;
		TableCodes = tableCodes;
	}
}

public readonly record struct TemplateBundleNew(string TemplateTableCode, string TemplateDescription, List<string> TableCodes);
 
public record struct SheetDbAndWorksheet(string TableCode,TemplateSheetInstance? DbSheet, IWorksheet? WorkSheet);
public record struct ZetTemplateBundle(string GroupTableCode, string Zet, string TemplateDescription, List<List<SheetDbAndWorksheet>> SheetsAndWorksheets);