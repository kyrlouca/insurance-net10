using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewValidator.Common.FunctionalRoutines;

public enum FunctionType{Normal,Matches,IsNull};
public partial class RuleExpression
{
    //an expression is the text between AND and/or OR
    //the one below if has tow terms
    //if ({t: S.05.02.04.06, c: C0280, z: Z0001, dv: 0, seq: False, id: v1, f: solvency, fv: solvency2} * {t: S.05.01.01.02, c: C0300, z: Z0001, dv: 0, seq: False, id: v2, f: solvency, fv: solvency2} > 0) then abs({t: S.05.01.01.02, c: C0300, z: Z0001, dv: 0, seq: False, id: v2, f: solvency, fv: solvency2}) >= abs({t: S.05.02.04.06, c: C0280, z: Z0001, dv: 0, seq: False, id: v1, f: solvency, fv: solvency2}) else true()
    public required string ExpressionId { get; init; }
    public bool IsNegative { get; set; }
    public FunctionType FunctionType { get; set; }
    public required string ExpressionText { get; init; }
    public List<RuleExpressionTerm280> ExpressionTerms { get; init; } = new List<RuleExpressionTerm280>();

    public static RuleExpression CreateRuleExpression(string expressionId, string text)
    {
        //(not(ab))=>ab
        //not(ab)=> ab
        //ab=>ab
        //not(matches("abc")) => abc and function type = matches

        //check if not and it will also remove any parenthesis around
        var rgxNot = RegexNot() ;
        var matchNot = rgxNot.Match(text);
        var isNot= matchNot.Success;
        var withoutNot = matchNot.Success ? matchNot.Groups[1].Value : text;

        var rgxFunc = RgxFunctionType();
        var matchFunc = rgxFunc.Match(withoutNot);
        FunctionType fnType = !matchFunc.Success ? FunctionType.Normal
            : matchFunc.Groups[1].Value == "matches" ? FunctionType.Matches
            : FunctionType.IsNull;               
        var functionText = matchFunc.Success ? matchFunc.Groups[2].Value : withoutNot;

        //var expresionTerms = RuleExpressionTerm280.(functionText);
        return new RuleExpression() {ExpressionId=expressionId, IsNegative=isNot, FunctionType=fnType, ExpressionText= functionText};
    }

    [GeneratedRegex(@"^\(?not\s?\((.*)\)\)?")]
    private static partial Regex RegexNot();
    [GeneratedRegex("(isNull|matches)\\s?\\((.*)\\)")]
    private static partial Regex RgxFunctionType();
}