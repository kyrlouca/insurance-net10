using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewValidator;

public class EvaluateRuler
{
    private enum BooleanOperator { None, IsAnd, IsOR };
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
        var rgxEmptyParen = new Regex(@"^\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$");

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
                case "mathces":
                    var resm = value == "found";
                    return resm;
                default:
                    var res = EvaluateRule(value);
                    return res;
            }
        }


        var booleanType = text.Contains("and") ? BooleanOperator.IsAnd
            : text.Contains("or") ? BooleanOperator.IsOR
            : BooleanOperator.None;

        if (booleanType == BooleanOperator.None)
        {
            var res = text == "found";
            return res;
        }

        if (booleanType == BooleanOperator.IsAnd)
        {
            var resAnd = text.Split("and", StringSplitOptions.RemoveEmptyEntries);
            var res1 = EvaluateRule(resAnd[0].Trim());
            var res2 = EvaluateRule(resAnd[1].Trim());
            return res1 && res2;
        }
        if (booleanType == BooleanOperator.IsOR)
        {
            var res = text.Split("or", StringSplitOptions.RemoveEmptyEntries);
            var bres1 = EvaluateRule(res[0].Trim());
            var bres2 = EvaluateRule(res[1].Trim());
            return bres1 || bres2;
        }

        return false;

    }

}
