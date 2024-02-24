using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace NewValidator.ValidationClasses;

public record ObjectTerm280(string ObjectType, int Decimals, bool IsTolerant, Object Obj);
public record ZetTerm(string Letter, string Formula, bool IsPassed);

public class ExpressionEvaluator
{
    private enum TermOperators { None, IsAnd, IsOR };
    public static bool EvaluateExpression(string formula, Dictionary<string, ObjectTerm280> terms)
    {

        //and has precedence        
        //var regexOr = new Regex(@"(.*)(or)(.*)");
        //1. outer parenthesi with or without function
        //2. new--construct new formula to make parenthesis as Zet terms and create the Zet terms

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
                    var resNot = !EvaluateExpression(value, terms);
                    return resNot;
                case "isNull":
                    //var resn = string.IsNullOrEmpty(value);
                    var resn = ValidationFunctions.ValidateIsNull(value, terms);
                    return resn;
                case "matches":
                    var resm = ValidationFunctions.ValidateMatch(formula, terms);
                    return resm;
                default:
                    //this is executed when there are outer parenthesis around (a=b and (bc==dd) and b=c) => a=b and (bc==dd) and b=c
                    var res = EvaluateExpression(value, terms);
                    return res;
            }
        }


        //////////////////////////////// Make new formula with zet 
        //if there are terms with parenthesis like  x1<3 or  (x0>3 and X1<4) 
        //replace parenthesis with zet terms. 
        //evaluate each zet 
        //reconstruct the formula using results instead of z
        //try again 

        var rgxTerm = new Regex(@"(isNull|matches|not)?\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");        
        var matchesTerms = rgxTerm.Matches(formula);
        var ruleTextZetTerms = matchesTerms.Select((match, i) => new ZetTerm($"Z{i:D2}", match.Value, false)) ?? new List<ZetTerm>();        

        if (ruleTextZetTerms.Any())
        {
            var formulaZet = ruleTextZetTerms.Aggregate(formula, (Func<string, ZetTerm, string>)((currentText, val) =>
            {
                int index = currentText.IndexOf((string)val.Formula);
                string replacedString = currentText.Substring(0, index) + " " + val.Letter + " " + currentText.Substring((int)(index + val.Formula.Length));
                return replacedString;
            }));


            var updatedZets = ruleTextZetTerms
            .Select(zz => zz with { IsPassed = EvaluateExpression(zz.Formula, terms) })
            .ToList();
            var newFormula = updatedZets.Aggregate(formulaZet, (Func<string, ZetTerm, string>)((currentText, val) =>
            {
                int index = currentText.IndexOf((string)val.Letter);
                var replacement = val.IsPassed ? "1==1" : "1==2";
                string replacedString = currentText.Substring(0, index) + " " + replacement + " " + currentText.Substring((int)(index + val.Letter.Length));
                return replacedString;
            }));

            var res = ValidationFunctions.ValidateArithmetic(newFormula, terms);
            return res;
        }
        ////////////////////////////////////////////////////////

        var termOperator = formula.Contains("and") ? TermOperators.IsAnd
            : formula.Contains("or") ? TermOperators.IsOR
            : TermOperators.None;

        if (termOperator == TermOperators.None)
        {
            var res = ValidationFunctions.ValidateArithmetic(formula, terms);
            return res;
        }

        if (termOperator == TermOperators.IsAnd)
        {
            var resAnd = formula.Split("and", StringSplitOptions.RemoveEmptyEntries);
            var val1 = resAnd[0].Trim();
            var val2 = resAnd[1].Trim();
            var res1 = EvaluateExpression(val1, terms);
            var res2 = EvaluateExpression(val2, terms);
            return res1 && res2;
        }
        if (termOperator == TermOperators.IsOR)
        {
            var res = formula.Split("or", StringSplitOptions.RemoveEmptyEntries);
            var bres1 = EvaluateExpression(res[0].Trim(), terms);
            var val2 = res[1].Trim();
            var bres2 = EvaluateExpression(val2, terms);
            return bres1 || bres2;
        }

        return false;


    }


}

