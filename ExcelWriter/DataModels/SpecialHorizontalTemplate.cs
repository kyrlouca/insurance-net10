using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelWriter.DataModels;

public class SpecialHorizontalTemplate
{
	public string TemplateCode { get; init; }
	public string TemplateSheetName { get; init; }
	public String[][] TableCodesArray { get; init; }
	public List<List<string>> TableCodes { get; init; }
	public SpecialHorizontalTemplate(string templateCode, string templateSheetName, string[][] tableCodes)
	{
		TemplateCode = templateCode;
		TemplateSheetName = templateSheetName;
		TableCodesArray = tableCodes;
		TableCodes = TableCodesArray.Select(tc => tc.ToList()).ToList();
	}

}