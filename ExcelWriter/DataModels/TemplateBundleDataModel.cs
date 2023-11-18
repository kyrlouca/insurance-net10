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
