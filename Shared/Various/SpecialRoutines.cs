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

public record CellRowColRecord(string businessCode, string TableCode, string Zet, string Row, string Col,bool IsOpen, bool IsValid);
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
        //businessCode = "{S.05.01.02.01,R1210,C0200,Z0001}";
        //businessCode = "{S.01.01.02.01,R0010,C0010}"; //=> tableCode=S.01.01.02.01 zet ="" row=R0010 col=C0010                
        //businessCode = "{S.06.02.01.01,C0100,Z0001}"; //tableCode=S.01.01.02.01 zet =Z001 row="" col=C0010                
        //businessCode = "{S.01.02.01.02,C0070}";

        Match match;


        //{S.05.01.02.01,R1210,C0200,Z0001}
        var rgAll = new Regex(@"^\{(S(?:\.\d\d){4})(?:,(R\d{4}))(?:,(C\d{4}))(?:,(Z\d{4}))\}");
        match = rgAll.Match(businessCode.Trim());
        if (match.Success)
        {
            return new CellRowColRecord(businessCode, match.Groups[1].Value, match.Groups[4].Value, match.Groups[2].Value, match.Groups[3].Value, false, true);
        }

        //{S.01.01.02.01,R0010,C0010}
        var rgRowCol = new Regex(@"^\{(S(?:\.\d\d){4})(?:,(R\d{4}))(?:,(C\d{4}))\}");
        match = rgRowCol.Match(businessCode.Trim());
        if (match.Success)
        {
            return new CellRowColRecord(businessCode, match.Groups[1].Value, "", match.Groups[2].Value, match.Groups[3].Value, false, true);
        }

        //{S.06.02.01.01,C0100,Z0001}        
        var rgZet = new Regex(@"^\{(S(?:\.\d\d){4})(?:,(C\d{4}))(?:,(Z\d{4}))\}");
        match = rgZet.Match(businessCode.Trim());
        if (match.Success)
        {
            return new CellRowColRecord(businessCode, match.Groups[1].Value, match.Groups[3].Value, "", match.Groups[2].Value, true, true);
        }

        //"{S.01.02.01.02,C0070}"
        var rgOnlyCol = new Regex(@"^\{(S(?:\.\d\d){4})(?:,(C\d{4}))\}");
        match = rgOnlyCol.Match(businessCode.Trim());
        if (match.Success)
        {
            return new CellRowColRecord(businessCode, match.Groups[1].Value, "", "", match.Groups[2].Value, false, false);
        }
        throw (new Exception($"invalid businessCode-{businessCode}"));


    }

}
