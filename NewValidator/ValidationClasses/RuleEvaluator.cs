using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewValidator.ValidationClasses;

public class RuleEvaluator
{
    private enum TermOperators { None, IsAnd, IsOR };
    public static bool EvaluateRule(string text)
    {
        //check for and, or
        //--if found split and call evaluate for both parts
        //if not found check for function, call evaluate
        //if not found check for >, = call evaluate
        //if not found check for +, - 
        //if found call again the evaluete rule for each

        //and has precedence        
        //var regexOr = new Regex(@"(.*)(or)(.*)");


        var rgxFn = new Regex(@"^(isNull|matches|not)?\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$");
       
        var match = rgxFn.Match(text);

        if (match.Success)
        {
            //( ab and matches(cd)) => evaluate ab and matches(cd)
            var fn = match.Groups[1].Value;
            var value = match.Groups[2].Value;

            switch (fn)
            {
                case "not":
                    var resNot = !EvaluateRule(value);
                    return resNot;
                case "isNull":
                    var resn = string.IsNullOrEmpty(value);
                    return resn;
                case "matches":
                    var resm = ValidationFunctions.ValidateMatch(text);
                    return resm;
                default:
                    var res = EvaluateRule(value);
                    return res;
            }
        }


        var termOperator = text.Contains("and") ? TermOperators.IsAnd
            : text.Contains("or") ? TermOperators.IsOR
            : TermOperators.None;

        if (termOperator == TermOperators.None)
        {
            var res = ValidationFunctions.ValidateArithmetic(text);
            return res;
        }

        if (termOperator == TermOperators.IsAnd)
        {
            var resAnd = text.Split("and", StringSplitOptions.RemoveEmptyEntries);
            var val1 = resAnd[0].Trim();
            var val2 = resAnd[1].Trim();
            var res1 = EvaluateRule(val1);
            var res2 = EvaluateRule(val2);
            return res1 && res2;
        }
        if (termOperator == TermOperators.IsOR)
        {
            var res = text.Split("or", StringSplitOptions.RemoveEmptyEntries);
            var bres1 = EvaluateRule(res[0].Trim());
            var val2 = res[1].Trim();
            var bres2 = EvaluateRule(val2);
            return bres1 || bres2;
        }

        return false;

    }

}
