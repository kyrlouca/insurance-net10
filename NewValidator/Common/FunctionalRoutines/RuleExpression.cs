using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewValidator.Common.FunctionalRoutines;
public class RuleExpression
{
    public bool IsNegative { get; set; }
    public string ExpressionText { get; init; } = "";

    public static RuleExpression CreateRuleExpression(string text)
    {
        //(not(ab))=>ab
        //not(ab)=> ab

        var rgxNot = RegexNot();
        var matchNot = rgxNot.Match(text);
        var isNot= matchNot.Success;
        var withoutNot = matchNot.Success ? matchNot.Groups[1].Value : text;
        
        //remove parenthesis if around 
        var rgxParen = RegexParenthesis();
        var match = rgxParen.Match(withoutNot);
        var cleanStr = match.Success ? match.Groups[1].Value : withoutNot;        

        return new RuleExpression() {IsNegative=isNot, ExpressionText=cleanStr};
    }

    [GeneratedRegex("not\\s?\\((.*)\\)")]
    private static partial Regex RegexNot();
    [GeneratedRegex("\\((.*)\\)")]
    private static partial Regex RegexParenthesis();
}