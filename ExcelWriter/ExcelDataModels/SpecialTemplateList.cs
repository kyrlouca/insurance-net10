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
            new SpecialTemplateLayout("AS.06.02.01.01_single", "S.06.02.01.01","List of assets",false, new[] { new string[] { "S.06.02.01.01" } }),
            new SpecialTemplateLayout("AS.06.02.01.02_single", "S.06.02.01.02","List of assets",false, new[] { new string[] { "S.06.02.01.02" } }),
            new SpecialTemplateLayout("S.06.02.01", "S.06.02.01_Combined","List of assets",false, new[] { new string[] { "S.06.02.01.01", "S.06.02.01.02" } }),
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

   


    public static List<SpecialTemplateLayout> Records { get; }
    static SpecialTemplateList()
    {
        Records = new()
        {
            //todo replace 5.01.02 with 12.
            new SpecialTemplateLayout("S.05.01.02.01", "S.05.01.02.01","Premiums, claims and expenses by line of business - non life",false, new[] { new string[] { "S.05.01.02.01" } }),
            new SpecialTemplateLayout("S.05.01.02.02", "S.05.01.02.02","Premiums, claims and expenses by line of business - life",false, new[] { new string[] { "S.05.01.02.02" } }),
            new SpecialTemplateLayout("S.06.02.01.01_Single", "S.06.02.01.01","List of assets-Information on positions held",false, new[] { new string[] { "S.06.02.01.01" } }),
            new SpecialTemplateLayout("S.06.02.01.02_Single", "S.06.02.01.02","List of assets-Information on assets",false, new[] { new string[] { "S.06.02.01.02" } }),
            new SpecialTemplateLayout("S.06.02.01", "S.06.02.01_Combined","List of assets",false, new[] { new string[] { "S.06.02.01.01", "S.06.02.01.02" } }),
            new SpecialTemplateLayout("S.02.02.01", "S.02.02.01","Liabilities by currency",true, new[] { new string[] { "S.02.02.01.01", "S.02.02.01.02" } }),
            new SpecialTemplateLayout("S.04.01.01", "S.04.01.01","XX", true, new[] { new string[] { "S.04.01.01.01", "S.04.01.01.02", "S.04.01.01.03", "S.04.01.01.04" } }),
            new SpecialTemplateLayout("S.05.02.01", "S.05.02.01","xx", true, new[] { new string[] { "S.05.02.01.01", "S.05.02.01.02", "S.05.02.01.03" }, new string[] { "S.05.02.01.04", "S.05.02.01.05", "S.05.02.01.06" } }),
            new SpecialTemplateLayout("XS.19.01.01", "S.19.01.01","Non-life insurance claims", true, new[] {
                new string[] { "S.19.01.01.01", "S.19.01.01.02", "S.19.01.01.03", "S.19.01.01.04", "S.19.01.01.05" ,"S.19.01.01.06" },
                new string[] { "S.19.01.01.07", "S.19.01.01.08", "S.19.01.01.09", "S.19.01.01.10", "S.19.01.01.11" ,"S.19.01.01.12" },
                new string[] { "S.19.01.01.13", "S.19.01.01.14", "S.19.01.01.15", "S.19.01.01.16", "S.19.01.01.17" ,"S.19.01.01.18" },
                new string[] { "S.19.01.01.19" },
                new string[] { "S.19.01.01.20" },
                new string[] { "S.19.01.01.21" },
            }),
            new SpecialTemplateLayout("S.19.01.21", "S.19.01.21","Non-life insurance claims", true, new[] { new string[] { "S.19.01.21.01", "S.19.01.21.02" , "S.19.01.21.03" , "S.19.01.21.04" } }),
            new SpecialTemplateLayout("S.22.06.01", "S.22.06.01", "Best estimate subject to volatility adjustment by country and currency", true, new[]
                {
                    new string[] { "S.22.06.01.01", "S.22.06.01.01" },
                    new string[] { "S.22.06.01.03", "S.22.06.01.04" }
            }),
            //Minimum Capital Requirement - Only life or only non-life insurance or reinsurance activity
            new SpecialTemplateLayout("S.28.01.01", "S.28.01.01", "Minimum Capital Requirement - Only life or only non-life insurance or reinsurance activity", false, new[]
                {
                    new string[] { "S.28.01.01.01" },
                    new string[] { "S.28.01.01.02" },
                    new string[] { "S.28.01.01.03" },
                    new string[] { "S.28.01.01.04" },
                    new string[] { "S.28.01.01.05" }
            }),
            new SpecialTemplateLayout("S.28.02.01", "S.28.02.01", "Minimum Capital Requirement - Both life and non-life insurance activity", false, new[]
                {
                    new string[] { "S.28.02.01.01" },
                    new string[] { "S.28.02.01.02" },
                    new string[] { "S.28.02.01.03" },
                    new string[] { "S.28.02.01.04" },
                    new string[] { "S.28.02.01.05" }
            })
        };
    }

    

    public static SpecialTemplateLayout? FindSpecialTemplateLayoutByCode(string templateCode)
    {
        var rec = Records.FirstOrDefault(line => line.TemplateCode == templateCode.Trim());
        return rec;
    }

    public static SpecialTemplateLayout? FindSpecialTemplateLayoutByCodeNew(string templateCode)
    {
        var rec = SpecialTemplateList.SpecialLayoutsNew.FirstOrDefault(line => line.TemplateCode == templateCode.Trim());
        return rec;
    }

    
}
