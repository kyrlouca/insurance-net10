using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelWriter.ExcelDataModels;

public record ZHorizontalTables(List<string> TablesList);
public record ZMatrix(List<ZHorizontalTables> Matrix);


public class SpecialTemplateLayout
{
    public string TemplateCode { get; init; }
    public string TemplateSheetName { get; init; }
    public string TemplateSheetDescription { get; init; }
    public bool IsZetImportant { get; init; }
    public String[][] TableCodesMatrix { get; init; }
    public ZMatrix ZMatrix { get; init; }
    public List<List<string>> TableCodes { get; init; }
    public SpecialTemplateLayout(string templateCode, string templateSheetName, string templateSheetDescription, bool isZetImportant, string[][] tableCodes)
    {
        TemplateCode = templateCode;
        TemplateSheetName = templateSheetName;
        TemplateSheetDescription = templateSheetDescription;
        IsZetImportant = isZetImportant;
        TableCodesMatrix = tableCodes;
        TableCodes = TableCodesMatrix.Select(tc => tc.ToList()).ToList();
        ZMatrix = new ZMatrix(tableCodes.Select(codeList => new ZHorizontalTables(codeList.ToList())).ToList());
    }
}


public static class SpecialTemplateList
{

    public static List<SpecialTemplateLayout> SpecialLayoutsNew = new()
        {
            new SpecialTemplateLayout("S.06.02.01", "S.06.02.01_Combined","List of assets",false, new[] { new string[] { "S.06.02.01.01", "S.06.02.01.02" } }),
            new SpecialTemplateLayout("ZS.06.02.01.01_single", "S.06.02.01.01","List of assets-Information on positions held",false, new[] { new string[] { "S.06.02.01.01" } }),
            new SpecialTemplateLayout("ZS.06.02.01.02_single", "S.06.02.01.02","List of assets-Information on assets",false, new[] { new string[] { "S.06.02.01.02" } }),

            new SpecialTemplateLayout("S.22.06.01", "S.22.06.01", "Best estimate subject to volatility adjustment by country and currency", true, new[]
                {
                    new string[] { "S.22.06.01.01", "S.22.06.01.01" },
                    new string[] { "S.22.06.01.03", "S.22.06.01.04" }
            }),         
    };

    public static List<string> SingleTableGroupsNew = new List<string> {
            "S.04.04.01",
            "S.05.01.02",            
            "S.05.01.01",
            "S.05.01.02",            
            "S.14.01.01",
            "S.30.01.01",
            "S.30.01.02",
            "S.30.01.03",
            "S.30.01.04",
            "S.30.01.04",
        };

     

    

    public static SpecialTemplateLayout? FindSpecialTemplateLayoutByCodeNew(string templateCode)
    {
        var rec = SpecialTemplateList.SpecialLayoutsNew.FirstOrDefault(line => line.TemplateCode == templateCode.Trim());
        return rec;
    }

    
}
