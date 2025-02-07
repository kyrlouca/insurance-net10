namespace Shared.SpecialRoutines;

using Microsoft.Extensions.FileSystemGlobbing;
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


public record CellDim
{


    public string Signature { get; init; } = ""; //s2c_dim:VC(*?[481;1655;1])
    public string Dim { get; init; } = "";
                                                  
    public bool IsValid { get; init; } 
    public bool IsWild { get; init; }
    public bool IsOptional { get; init; } 
    public int HierarchyId { get; init; } 
    public int HierarchyDefaultMember { get; init; } 
    public int HierarchyZeroOrOne { get; init; } 
    
    
    public static CellDim ParseHierarchy(string cellSignature)
    {

        //Signature = @"s2c_dim:OC(s2c_CU:USD)";
        //Signature = @"s2c_dim:OC(ID:USD)";
        //Signature = @"s2c_dim:OC(*[xxxx])";            
        //Signature= s2c_dim:VC(*?[481;1655;1])


        var res = new Regex(@"s2c_dim\:(\w\w)\((\*?)(\??)\[(\d+)\;(\d+)\;(\d+)\]\)",RegexOptions.Compiled);
        var match= res.Match(cellSignature);
        if (!match.Success)
        {
            return new CellDim() {IsValid=false };
        }

        var dim = match.Groups[1].Value;
        var isOptional = match.Groups[2].Value == "*";
        var isWild = match.Groups[3].Value == "?";
        var hierarchyId = int.TryParse(match.Groups[4].Value, out int hiVal) ? hiVal : 0;
        var hierarchyDefaultMember = int.TryParse(match.Groups[5].Value, out int hdVal) ? hdVal : 0;
        var hierarchyLastNumber = int.TryParse(match.Groups[6].Value, out int hmVal) ? hmVal : 0;
        return new CellDim() {IsValid=true, Signature= cellSignature,Dim=dim,IsOptional=isOptional,IsWild=isWild
            ,HierarchyId=hierarchyId,HierarchyDefaultMember=hierarchyDefaultMember,HierarchyZeroOrOne=hierarchyLastNumber };
        
    }
        

}


public record RowColRecord(string rowcol, string Row, string Col, bool IsValid, bool HasOnlyCol);

public record CellRowColRecord(string businessCode, string TableCode, string Zet, string Row, string Col, bool IsOpen, bool IsValid);
public class DimUtils
{
    public static RowColRecord CreateRowCol(string RowCol)
    {
        //R0120C0080=> row=R0120 col=C0080        
        var rg = new Regex(@"^(R\d{4})?(C\d{4})$", RegexOptions.Compiled);
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

        var rg = new Regex(@"MET\((.*?)\)", RegexOptions.Compiled);
        var match = rg.Match(metXblr);
        if (!match.Success)
        {
            return "";
        }
        return match.Groups[1].Value;
    }

    public static CellRowColRecord ParseCellRowCol(string businessCode)
    {
        //For 282 they can have many columns. The first art the fucking keys. The last c is the Column
        //businessCode = "{S.05.01.02.01,R1210,C0200,Z0001}";
        //businessCode = "{S.01.01.02.01,R0010,C0010}"; //=> tableCode=S.01.01.02.01 zet ="" row=R0010 col=C0010                
        //businessCode = "{S.06.02.01.01,C0100,Z0001}"; //tableCode=S.01.01.02.01 zet =Z001 row="" col=C0010                
        //businessCode = "{S.01.02.01.02,C0070}";

        Match match;        
        //{S.05.01.02.01,R1210,C0200,Z0001}
        var rgAll = new Regex(@"\{(S[REP]?[V]?(?:\.\d\d){4})(,[AE]?[E]?R\d{4})?(,[A]?[NE]?C\d{4})?(,Z\d{4})?}", RegexOptions.Compiled);
        match = rgAll.Match(businessCode.Trim());
        if (!match.Success)
        {
            throw (new Exception($"invalid businessCode-{businessCode}"));
        }
        
        var tableCode = match.Groups[1].Value.Trim();
        var row = match.Groups[2].Value.Replace(",", "");
        var col = match.Groups[3].Value.Replace(",", "");
        var zet = match.Groups[4].Value.Replace(",", "");
        var isOpen = string.IsNullOrEmpty(row);
        var isValid = !string.IsNullOrEmpty(col);

        return new CellRowColRecord(businessCode,tableCode, zet,row,col, isOpen, true);
    }


    public static CellRowColRecord ParseCellRowColNew(string businessCode)
    {
        //For 282 they can have many columns. The first art the fucking keys. The last c is the Column
        //businessCode = "{S.05.01.02.01,R1210,C0200,Z0001}";
        //businessCode = "{S.01.01.02.01,R0010,C0010}"; //=> tableCode=S.01.01.02.01 zet ="" row=R0010 col=C0010                
        //businessCode = "{S.06.02.01.01,C0100,Z0001}"; //tableCode=S.01.01.02.01 zet =Z001 row="" col=C0010                
        //businessCode = "{S.01.02.01.02,C0070}";

        var cleanRgx = new Regex(@"\{(.*)\}");
        var match2 = cleanRgx.Match(businessCode);
        if (!match2.Success)
        {
            throw (new Exception($"invalid businessCode-{businessCode}"));
        }
        var clean = match2.Groups[1].Value.Trim();
        var parts= clean.Split(",");
        var tableCode = parts[0].Trim();

        var zet = parts.FirstOrDefault(pt => CommonRoutines.RegexConstants.ZetRegExP.IsMatch(pt))??"";
        var row = parts.FirstOrDefault(pt => CommonRoutines.RegexConstants.RowRegExP.IsMatch(pt)) ?? "";
        var cols = parts.Where(pt => CommonRoutines.RegexConstants.ColRegExP.IsMatch(pt));
        var col = cols.LastOrDefault() ?? "";

        var isOpen = string.IsNullOrEmpty(row);

        return new CellRowColRecord(businessCode, tableCode, zet, row, col, isOpen, true); ;
    }


}

public class FormulaCharacters
{
    public static string RemoveWeirdFormulaCharacters(string input)
    {
        // imin(imax(X01, 0) i* 0.25, X02) =>// imin(imax(X01, 0) * 0.25, X02) 
        // Define the regular expressions

        Regex rgx = new Regex(@"i([*+\-><=!])");
        string result = rgx.Replace(input, match =>
        {
            // Replace 'i' followed by a character in the specified set with just that character
            return match.Groups[1].Value;
        });

        //Regex rgxStar = new Regex(@"i\*");
        //Regex rgxPlus = new Regex(@"i\+");
        //Regex rgxMinus = new Regex(@"i\-");        
        //Regex rgxEqual = new Regex(@"i\=");

        //string result = rgxStar.Replace(input, "*");
        //result = rgxPlus.Replace(result, "+");
        //result = rgxMinus.Replace(result, "-");
        //result = rgxEqual.Replace(result, "=");

        return result;
    }
}

public record RuleTextTerm(string Letter, string TermText);
public class TermsExtraction
{
    public static (string Formula, List<RuleTextTerm> formulaTerms) ExtractTerms(string textExpression)
    {
        var rgx = new Regex(@"\{\s?[a-z]:([^{}]).*?\}");
        var matches = rgx.Matches(textExpression);
        var ruleTextTerms = matches.Select((match, i) => new RuleTextTerm($"X{i:D2}", match.Value)).ToList() ?? new List<RuleTextTerm>();
        var formula = ruleTextTerms.Aggregate(textExpression, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.TermText);
            string replacedString = currentText.Substring(0, index) + val.Letter + currentText.Substring(index + val.TermText.Length);
            return replacedString;
        });
        var symbolFormula = FormulaCharacters.RemoveWeirdFormulaCharacters(formula).Trim();

        return (symbolFormula, ruleTextTerms);

    }
}
public class FormulaSimplification
{
    public static (string Formula, List<(string letter, string content)> FormulaTerms) Simplify(string text)
    {

        //{t: S.06.02.01.02, c: C0290, z: Z0001, filter: matches(dim(this(), [s2c_dim:UI]), "^CAU/.*") and not(matches(dim(this(), [s2c_dim:UI]), "^CAU/(ISIN/.*)|(INDEX/.*)")), seq: False, id: v1, f: solvency, fv: solvency2}
        //=> {t: S.06.02.01.02, c: C0290, z: Z0001, filter: matches(XYZ00) and not(XYZ01), seq: False, id: v1, f: solvency, fv: solvency2}
        //=>XYZ00: dim(this(), [s2c_dim:UI]), "^CAU/.*", XYZ01:matches(dim(this(), [s2c_dim:UI]), "^CAU/(ISIN/.*)|(INDEX/.*)")

        var rgx = new Regex(@"\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        var matchParenthesis = rgx.Matches(text);
        var nestedParenthesis = matchParenthesis
            .Where(match => !string.IsNullOrEmpty(match.Groups[1].Value))
            .Select((match, i) => ($"XYZ{i:D2}", match.Groups[1].Value)).ToList();
        var symbolFormula = nestedParenthesis.Aggregate(text, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.Value);
            string replacedString = currentText[..index] + "" + val.Item1 + "" + currentText[(index + val.Value.Length)..];
            return replacedString;
        });

        return (symbolFormula, nestedParenthesis);
    }
    public static string ReplaceTerms(string formula, List<(string letter, string content)> terms)
    {

        var fullFormula = terms.Aggregate(formula, (currentText, term) =>
        {
            int index = currentText.IndexOf(term.letter);

            string replacedString = index > -1
            ? currentText[..index] + term.content + currentText[(index + term.letter.Length)..]
            : currentText;

            return replacedString;
        });

        return fullFormula;
    }


}