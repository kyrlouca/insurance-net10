using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelWriter.DataModels;

public record ZHorizontalTables(List<string> TablesList);
public record ZMatrix(List<ZHorizontalTables> Matrix);



public class SpecialTemplateLayout
{
    public string TemplateCode { get; init; }
    public string TemplateSheetName { get; init; }
    public String[][] TableCodesMatrix { get; init; }
    public ZMatrix ZMatrix { get; init; }
    public List<List<string>> TableCodes { get; init; }
    public SpecialTemplateLayout(string templateCode, string templateSheetName, string[][] tableCodes)
    {
        TemplateCode = templateCode;
        TemplateSheetName = templateSheetName;
        TableCodesMatrix = tableCodes;
        TableCodes = TableCodesMatrix.Select(tc => tc.ToList()).ToList();        
        ZMatrix = new ZMatrix(tableCodes.Select(codeList => new ZHorizontalTables(codeList.ToList())).ToList());
    }    
}


public static class SpecialTemplateList
{

    public static List<SpecialTemplateLayout> Records { get; }
    static SpecialTemplateList()
    {
        Records = new()
        {
            new SpecialTemplateLayout("S.06.02.01.01_Single", "S.06.02.01.01", new[] { new string[] { "S.06.02.01.01" } }),
            new SpecialTemplateLayout("S.06.02.01.02_Single", "S.06.02.01.02", new[] { new string[] { "S.06.02.01.02" } }),
            new SpecialTemplateLayout("S.06.02.01", "S.06.02.01", new[] { new string[] { "S.06.02.01.01", "S.06.02.01.02" } }),
            new SpecialTemplateLayout("S.02.02.01", "S.02.02.01", new[] { new string[] { "S.02.02.01.01", "S.02.02.01.02" } }),
            new SpecialTemplateLayout("S.04.01.01", "S.04.01.01", new[] { new string[] { "S.04.01.01.01", "S.04.01.01.02", "S.04.01.01.03", "S.04.01.01.04" } }),
            new SpecialTemplateLayout("S.05.02.01", "S.05.02.01", new[] { new string[] { "S.05.02.01.01", "S.05.02.01.02", "S.05.02.01.03" }, new string[] { "S.05.02.01.04", "S.05.02.01.05", "S.05.02.01.06" } }),
            new SpecialTemplateLayout("S.19.01.01", "S.19.01.01", new[] {
                new string[] { "S.19.01.01.01", "S.19.01.01.02", "S.19.01.01.03", "S.19.01.01.04", "S.19.01.01.05" ,"S.19.01.01.06" },
                new string[] { "S.19.01.01.07", "S.19.01.01.08", "S.19.01.01.09", "S.19.01.01.10", "S.19.01.01.11" ,"S.19.01.01.12" },
                new string[] { "S.19.01.01.13", "S.19.01.01.14", "S.19.01.01.15", "S.19.01.01.16", "S.19.01.01.17" ,"S.19.01.01.18" },
                new string[] { "S.19.01.01.19" },
                new string[] { "S.19.01.01.20" },
                new string[] { "S.19.01.01.21" },
            }),
            new SpecialTemplateLayout("S.19.01.21", "S.19.01.21", new[] { new string[] { "S.19.01.21.01", "S.19.01.21.02" , "S.19.01.21.03" , "S.19.01.21.04" } }),
            new SpecialTemplateLayout("S.22.06.01", "S.22.06.01", new[]
                {
                    new string[] { "S.22.06.01.01", "S.22.06.01.01" },
                    new string[] { "S.22.06.01.03", "S.22.06.01.04" }
            }),
            new SpecialTemplateLayout("S.28.01.01", "S.28.01.01", new[]
                {
                    new string[] { "S.28.01.01.01" },
                    new string[] { "S.28.01.01.02" },
                    new string[] { "S.28.01.01.03", "S.28.01.01.04" },
                    new string[] { "S.28.01.01.05" }
            })
        };
    }

    public static SpecialTemplateLayout? FindSpecialTemplateLayout(string templateCode)
    {
        var rec = Records.FirstOrDefault(line => line.TemplateCode == templateCode.Trim());
        return rec;
    }
}
//S.02.02.01-S.02.02.01.01,S.02.02.01.02
//S.04.01.01-S.04.01.01.01,S.04.01.01.02,S.04.01.01.03,S.04.01.01.04
//S.19.01.21-S.19.01.21.01,S.19.01.21.02,S.19.01.21.03,S.19.01.21.04
//S.22.06.01-S.22.06.01.01,S.22.06.01.02 +S.22.06.01.03,S.22.06.01.04
