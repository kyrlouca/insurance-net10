using Shared.DataModels;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelWriter.DataModels;

public record struct xxZetTemplateBundleListxx(List<ZetTemplateLayout> ZetBundleList);

//ZetTemplateBundle contains list of lists to accomodate a list of horizontal tables layout
//Each outer vertical list contains a horizontal line of tables

public readonly record struct TableGroup(string TemplateCode, string TemplateDescription, List<string> TableCodes); 

public record struct SheetExtensiveInfo(string TableCode,TemplateSheetInstance? DbSheet, IWorksheet? WorkSheet,string TableDescription);
public record struct HorizontalLine(List<SheetExtensiveInfo> HorizontalSheetInfo);

public record struct ZetTemplateLayout(string GroupTableCode, string SheetCodeZet, bool IsOnlyZet, string SheetName, string TemplateDescription, List<HorizontalLine> TableMatrix);
