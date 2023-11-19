using Shared.DataModels;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelWriter.DataModels;


public readonly record struct TemplateBundle(string TemplateCode, string TemplateDescription, List<string> TableCodes); 
public record struct SheetDbAndWorksheet(string TableCode,TemplateSheetInstance? DbSheet, IWorksheet? WorkSheet);
//ZetTemplateBundle contains list of lists to accomodate horizontantal grouping
public record struct ZetTemplateBundle(string GroupTableCode, string Zet, string TemplateDescription, List<List<SheetDbAndWorksheet>> SheetsAndWorksheets);