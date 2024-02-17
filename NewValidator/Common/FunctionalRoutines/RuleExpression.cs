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
    public required string ExpressionId { get; init; }
    public bool IsNegative { get; set; }
    public FunctionType FunctionType { get; set; }
    public required string ExpressionText { get; init; }

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
        var functionValue = matchFunc.Success ? matchFunc.Groups[2].Value : withoutNot;
        
        return new RuleExpression() {ExpressionId=expressionId, IsNegative=isNot, FunctionType=fnType, ExpressionText= functionValue};
    }

    [GeneratedRegex(@"^\(?not\s?\((.*)\)\)?")]
    private static partial Regex RegexNot();
    [GeneratedRegex("(isNull|matches)\\s?\\((.*)\\)")]
    private static partial Regex RgxFunctionType();
}