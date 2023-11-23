using Shared.DataModels;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelWriter.DataModels;


public readonly record struct TemplateBundle(string TemplateCode, string TemplateDescription, List<string> TableCodes); 

///////////
public record struct TableExtensiveInfo(string TableCode,TemplateSheetInstance? DbSheet, IWorksheet? WorkSheet);
public record struct HorizontalLine(List<TableExtensiveInfo> HorizontalTables);

//ZetTemplateBundle contains list of lists to accomodate a list of horizontal tables layout
//Each outer vertical list contains a horizontal line of tables
public record struct xxZetTemplateBundleListxx(List<ZetTemplateBundle> ZetBundleList);
public record struct ZetTemplateBundle(string GroupTableCode, string Zet, string TemplateDescription, List<HorizontalLine> TableMatrix);

public static class SPT
{
    public static List<TableExtensiveInfo> Records { get; }

    static SPT()
    {
        Records = new List<TableExtensiveInfo>()
        {
            //new SheetDbAndWorksheet("S.02.02.01","zet", "S.02.02.01", new List<List<SheetDbAndWorksheet>>() { new List<SheetDbAndWorksheet>() { new SheetDbAndWorksheet() { "S.02.02.01.01" ,null,null} }),
        };
    }
}