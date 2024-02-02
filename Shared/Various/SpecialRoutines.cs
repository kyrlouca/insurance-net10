namespace Shared.SpecialRoutines;
using Shared.GeneralUtils;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;



public class DimDom
{


    public string Dim { get; internal set; } = "";//OC
    public string Dom { get; internal set; } = "";//CU
    public string DomAndVal { get; internal set; } = "";//CU:GBP
    public string DomValue { get; internal set; } = "";//USD
    public string DomAndValRaw { get; internal set; } = "";// s2c_CU:USD
    public string Signature { get; internal set; } //"s2c_dim:OC(s2c_CU:GBP)"
    public bool IsWild { get; internal set; } = false;
    public bool IsOptional { get; internal set; } = false;
    private DimDom() { }
    private void GetTheParts()
    {
        //Signature = @"s2c_dim:OC(s2c_CU:USD)";
        //Signature = @"s2c_dim:OC(ID:USD)";
        //Signature = @"s2c_dim:OC(*[xxxx])";            


        var res = RegexUtils.GetRegexSingleMatchManyGroups(@"s2c_dim:(\w\w)\((.*?)\)", Signature);
        if (res.Count != 3)
        {
            return;
        }

        Dim = res[1];
        DomAndValRaw = res[2];
        var domParts = DomAndValRaw.Split(":");
        if (domParts.Length == 2)
        {
            DomAndVal = res[2].Replace("s2c_", "");
            Dom = domParts[0].Replace("s2c_", "");

            DomValue = domParts[1];
        }

        IsWild = Signature.Contains('*');
        IsOptional = Signature.Contains('?');
    }
    private DimDom(string signature)
    {
        Signature = signature;
    }
    public static DimDom GetParts(string signature)
    {
        var dimDom = new DimDom(signature);
        dimDom.GetTheParts();
        return dimDom;
    }


}

public record RowColRecord(string rowcol, string Row, string Col, bool IsValid, bool HasOnlyCol);

public record CellRowColRecord(string businessCode, string TableCode, string Zet, string Row, string Col, bool IsValid, bool isOpen);
public class DimUtils
{
    public static RowColRecord CreateRowCol(string RowCol)
    {
        //R0120C0080=> row=R0120 col=C0080        
        var rg = new Regex(@"^(R\d{4})?(C\d{4})$");
        var match = rg.Match(RowCol.Trim());
        if (!match.Success)
        {
            return new RowColRecord(RowCol, "", "", false, false);
        }
        var rowcol = match.Groups[0].Value;
        var row = match.Groups[1].Value;
        var col = match.Groups[2].Value;

        var hasOnlyCol = string.IsNullOrEmpty(match.Groups[1].Value);
        var rowColRecord = new RowColRecord(rowcol, row, col, true, hasOnlyCol);

        return rowColRecord;

    }

    public static string ExtractXbrl(string metXblr)
    {

        var rg = new Regex(@"MET\((.*?)\)");
        var match = rg.Match(metXblr);
        if (!match.Success)
        {
            return "";
        }
        return match.Groups[1].Value;
    }

    public static CellRowColRecord ParseCellRowCol(string businessCode)
    {
        //businessCode = "{S.01.01.02.01,R0010,C0010}"; //=> tableCode=S.01.01.02.01 zet ="" row=R0010 col=C0010                
        //businessCode = "{S.06.02.01.01,C0100,Z0001}"; //tableCode=S.01.01.02.01 zet =Z001 row="" col=C0010                

        var isOpen = Regex.IsMatch(businessCode, @"Z\d{4}");
        var rgRow = new Regex(@"^\{(S(?:\.\d\d){4})(?:,(R\d{4}))(?:,(C\d{4}))\}");
        var rgZet = new Regex(@"^\{(S(?:\.\d\d){4})(?:,(C\d{4}))(?:,(Z\d{4}))\}");
        var rg = isOpen ? rgZet: rgRow;

        var match = rg.Match(businessCode.Trim());
        if (!match.Success)
        {
            throw (new Exception($"invalid businessCode-{businessCode}"));
            return new CellRowColRecord(  businessCode,"","", "", "", false, false);
        }
        var  wholeMatch = match.Groups[0].Value;
        var tableCode = match.Groups[1].Value;
        var firstItem = match.Groups[2].Value;
        var secondItem = match.Groups[3].Value;
        var zet = isOpen ? secondItem : "";
        var row = isOpen ? "" : firstItem;
        var col = isOpen ? firstItem : secondItem;
        



        var rowColRecord = new CellRowColRecord(wholeMatch,tableCode,zet, row, col, true, isOpen);

        return rowColRecord;

    }

}
