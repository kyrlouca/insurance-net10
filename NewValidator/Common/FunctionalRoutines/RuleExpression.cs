using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewValidator.Common.FunctionalRoutines;

public enum ExpressionFunctionType{normal,matches,isNill};
public partial class RuleExpression
{
    //an expression is the text between AND and/or OR
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

        
        return new RuleExpression() {IsNegative=isNot, ExpressionText=withoutNot};
    }

    [GeneratedRegex(@"^\(?not\s?\((.*)\)\)?")]
    private static partial Regex RegexNot();
   
}