using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewValidator.ValidationClasses;

public record ObjectTerm280(string ObjectType, int Decimals, Object Obj);

public class ExpressionEvaluator
{
    private enum TermOperators { None, IsAnd, IsOR };
    public static bool EvaluateExpression(string formula, Dictionary<string,ObjectTerm280> terms)
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

        var match = rgxFn.Match(formula);

        if (match.Success)
        {
            //( ab and matches(cd)) => evaluate ab and matches(cd)
            var fn = match.Groups[1].Value;
            var value = match.Groups[2].Value;

            switch (fn)
            {
                case "not":
                    var resNot = !EvaluateExpression(value,terms);
                    return resNot;
                case "isNull":
                    //var resn = string.IsNullOrEmpty(value);
                    var resn= ValidationFunctions.ValidateIsNull(value, terms);
                    return resn;
                case "matches":
                    var resm = ValidationFunctions.ValidateMatch(formula,terms);
                    return resm;
                default:
                    var res = EvaluateExpression(value,terms);
                    return res;
            }
        }


        var termOperator = formula.Contains("and") ? TermOperators.IsAnd
            : formula.Contains("or") ? TermOperators.IsOR
            : TermOperators.None;

        if (termOperator == TermOperators.None)
        {
            var res = ValidationFunctions.ValidateArithmetic(formula,terms);
            return res;
        }

        if (termOperator == TermOperators.IsAnd)
        {
            var resAnd = formula.Split("and", StringSplitOptions.RemoveEmptyEntries);
            var val1 = resAnd[0].Trim();
            var val2 = resAnd[1].Trim();
            var res1 = EvaluateExpression(val1,terms);
            var res2 = EvaluateExpression(val2,terms);
            return res1 && res2;
        }
        if (termOperator == TermOperators.IsOR)
        {
            var res = formula.Split("or", StringSplitOptions.RemoveEmptyEntries);
            var bres1 = EvaluateExpression(res[0].Trim(),terms);
            var val2 = res[1].Trim();
            var bres2 = EvaluateExpression(val2,terms);
            return bres1 || bres2;
        }

        return false;
        

    }

    
}

