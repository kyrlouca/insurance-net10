using Syncfusion.XlsIO.Implementation.PivotAnalysis;
using System.Data;
using System.Text.RegularExpressions;
using Z.Expressions;

namespace NewValidator.ValidationClasses;

public enum FunctionTypes { iMin, iMax, iSum,Max };
public record FunctionObject(string Letter, FunctionTypes FunctionType, string FullText, string Parameter, double Value);

public record ObjectTerm280(string ObjectType, int Decimals, bool IsTolerant, Object Obj);
public record ZetTerm(string Letter, string Formula, bool IsPassed);
public record ArTerm(string Letter, string Formula, double ValueReal, string ValueString);

public class ExpressionEvaluator
{
    private enum TermOperators { None, IsAnd, IsOR };
    public static bool EvaluateGeneralBooleanExpression(string formula, Dictionary<string, ObjectTerm280> terms)
    {
        
        //Recursion to remove outer parenthesis, real evaluation of terms with only a function, evaluation and recurse for  "and", "or", and finally real evaluation of the term
        //1. outer parenthesis with or without function =>=> evaluate function or remove parenthesis and recurse
        //2. if there are terms in parenthesis, evaluate each term in the parenthesis. (replace each term in parenthesis with Zxx and its value (1==1 for true, and 1==2 for false)
        //3. if there is "and","or", nothing in this order => evaluate the two terms around "and" or "or" or "nothing"

        //var rgxFn = new Regex(@"^(isNull|matches|not)?\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$");        
        var rgxFn = new Regex(@"^(isNull|matches|not|\s|^)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$");

        //Check if this is an outer parenthesis or an Outer function.
        //1. outer parenthesis with or without function (evaluate function or remove parenthesis and recurse if outer parenthesis without function)
        var match = rgxFn.Match(formula);
        if (match.Success)
        {
            //( ab and matches(cd)) => evaluate ab and matches(cd)
            var fn = match.Groups[1].Value;
            var value = match.Groups[2].Value;

            switch (fn)
            {
                case "not":
                    var resNot = !EvaluateGeneralBooleanExpression(value, terms);
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
                    var res = EvaluateGeneralBooleanExpression(value, terms);
                    return res;
            }
        }


        //////////////////////////////// Make new formula with zet 
        //if there are terms with parenthesis like  x1<3 or  (x0>3 and X1<4) => x1<3 or Z00
        //replace parenthesis with zet terms. 
        //evaluate each zet 
        //reconstruct the formula using results instead of z        
        //lookahead (?<!\S) is there to avoid matching imax(, imin( but to match 
        var rgxTerm = new Regex(@"(isNull|matches|not|\s|^)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");

        var matchesTerms = rgxTerm.Matches(formula.Trim());
        var ruleTextParenTerms = matchesTerms.Select((match, i) => new ZetTerm($"Z{i:D2}", match.Value, false)) ?? new List<ZetTerm>();

        //2. if there are terms in parenthesis, evaluate each term in the parenthesis and return the result 1==1 for true 1==2 for false
        if (ruleTextParenTerms.Any())
        {
            //build a new formula where the terms(inside parenthesis) are replaced with the letters of the textTersms
            var formulaParen = ruleTextParenTerms.Aggregate(formula, (currentText, val) =>
            {
                int index = currentText.IndexOf(val.Formula);
                string replacedString = currentText[..index] + " " + val.Letter + " " + currentText[(index + val.Formula.Length)..];
                return replacedString;
            });

            //Evaluate each of these terms 
            var parenthesisTerms = ruleTextParenTerms
            .Select(zz => zz with { IsPassed = EvaluateGeneralBooleanExpression(zz.Formula, terms) })
            .ToList();

            //the new formula replaces each term(boolean) with either 1=1 or 1==2
            var newFormula = parenthesisTerms.Aggregate(formulaParen, (currentText, val) =>
            {
                int index = currentText.IndexOf(val.Letter);
                var replacement = val.IsPassed ? "1==1" : "1==2";
                string replacedString = currentText[..index] + " " + replacement + " " + currentText[(index + val.Letter.Length)..];
                return replacedString;
            });

            var res = ValidationFunctions.ValidateArithmetic(newFormula, terms);
            return res;
        }


        //*******************************************************************
        //3. check for "and","or", nothing in this order
        // a. If there is an "and" evaluate the two terms around end,
        // b. if there is an "or" evaluate the two terms around "or"
        // c. if there is  no "and" , "or" evaluate the terms
        var termOperator = formula.Contains("and") ? TermOperators.IsAnd
            : formula.Contains("or") ? TermOperators.IsOR
            : TermOperators.None;

        if (termOperator == TermOperators.None)
        {
            var regSplit = new Regex(@"(.+)(>|>=|<|<=|=)(.+)");
            var matchSplit = regSplit.Match(formula);
            if (!matchSplit.Success)
            {
                throw new ApplicationException($"Formula cannot be split using <,>,= :{formula}");
            }
            var left = matchSplit.Groups[1].Value;
            var resLeft = EvaluateArithmeticNew(left, terms);
            var op= matchSplit.Groups[2].Value;
            var right = matchSplit.Groups[3].Value;
            var resRight = EvaluateArithmeticNew(right, terms);

            //var res = ValidationFunctions.ValidateArithmetic(formula, terms);

            return true;
        }

        if (termOperator == TermOperators.IsAnd)
        {
            var resAnd = formula.Split("and", StringSplitOptions.RemoveEmptyEntries);
            var val1 = resAnd[0].Trim();
            var val2 = resAnd[1].Trim();
            var res1 = EvaluateGeneralBooleanExpression(val1, terms);
            var res2 = EvaluateGeneralBooleanExpression(val2, terms);
            return res1 && res2;
        }
        if (termOperator == TermOperators.IsOR)
        {
            var res = formula.Split("or", StringSplitOptions.RemoveEmptyEntries);
            var bres1 = EvaluateGeneralBooleanExpression(res[0].Trim(), terms);
            var val2 = res[1].Trim();
            var bres2 = EvaluateGeneralBooleanExpression(val2, terms);
            return bres1 || bres2;
        }

        return false;


    }


    public static double EvaluateArithmeticNew(string functionFormula, Dictionary<string, ObjectTerm280> terms)
    {
        // @"5 + imin(3) +imax(4)";
        // @"7 + imin(imax(3,5),4)";
        var rgxTerm = new Regex(@"(imin|imax|max|isum)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        var rgxSingleFunction = new Regex(@"^(imin|imax|isum)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$");
        var matchFunctions = rgxTerm.Matches(functionFormula);
        var functionTerms = matchFunctions.Select((match, i) => new ArTerm($"A{i:D2}", match.Value, 0, "")) ?? new List<ArTerm>();

        //5 +  A00  + A01
        //7 +  A00 
        var formulaWithSymbols = functionTerms.Aggregate(functionFormula, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.Formula);
            string replacedString = currentText[..index] + " " + val.Letter + " " + currentText[(index + val.Formula.Length)..];
            return replacedString;
        });
        
        var newObjTerms = functionTerms
            .Select(ft => {
                var val = EvaluateFunction(ft.Formula, terms);
                return (ft.Letter, new ObjectTerm280("F", 0, false, val));
             });


        var allTermsx = terms.Select(trm => (trm.Key, trm.Value with { Decimals = 9 })).ToList();
        allTermsx.AddRange(newObjTerms);
        var allObjectsDic = allTermsx.ToDictionary(x => x.Key, x => x.Item2);
        var val = EvaluateSimpleArithmetic(formulaWithSymbols, allObjectsDic);

        return val;
    }


    public static double EvaluateFunction(string functionText, Dictionary<string, ObjectTerm280> terms)
    {
        //uses recursion.
        //Takes the content of the function and
        //  --construct a List of the nested Functions terms (nestedFunctions) 
        //  --builds a new formula (contentFormulaWithSymbols) and evaluates each term
        //  --the term is evaluated using simpleArithmetic if no nesting and using recursion if more nested functions
        // At the end all the terms are computed, and it uses the original symbol formula
        //EXAMPLE : imax(imin(3, 7) , 4) 
        string[] functionsSupported = { "imin", "imax", "isum", "max" };
        var rgxSingleFunction = new Regex(@"^(imin|imax|max|isum)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$");
        var rgxTerms = new Regex(@"(imin|imax|isum)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        functionText = functionText.Trim();

        var matchFn = rgxSingleFunction.Match(functionText);
        if (!matchFn.Success) throw new ArgumentException($"Invalid function:{functionText}");

        var funtionTypeStr = matchFn.Groups[1].Value;
        var functionContent = matchFn.Groups[2].Value;
        var functionType = ToFunctionType(funtionTypeStr);
        var matchFunctions = rgxTerms.Matches(functionContent);
        var nestedFunctions = matchFunctions.Select((match, i) => new FunctionObject($"F{i:D2}", ToFunctionType(match.Groups[1].Value), match.Value, match.Groups[2].Value, 0));

        // F00  , 4   
        var contentFormulaWithSymbols = nestedFunctions.Aggregate(functionContent, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.FullText);
            string replacedString = currentText[..index] + " " + val.Letter + " " + currentText[(index + val.FullText.Length)..];
            return replacedString;
        });

        //F00, 4
        var functionTerms = contentFormulaWithSymbols.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        //the F terms will be evaluated recursively such as the F00 and terms without functions such as the "4" will be evaluated immediateley with EvaluateSimpleArithmetic
        var regexZet = new Regex(@"^(F\d{2})");
        var functionObjectTerms = functionTerms
                .Select(functionTerm => regexZet.IsMatch(functionTerm)
                    ? new ObjectTerm280("F", 0, false, EvaluateFunction(nestedFunctions.FirstOrDefault(nf => nf.Letter == functionTerm)!.FullText, terms))
                    : new ObjectTerm280("V", 0, false, EvaluateSimpleArithmetic(functionTerm, terms))
                )
                .ToList();
        var val = EvaluateFunctionWithComputedTerms(functionType, functionObjectTerms);//at the end =>functionType:Max and the terms are : 3, 4 
        return val;
    }

    static double EvaluateFunctionWithComputedTerms(FunctionTypes functionType, IEnumerable<ObjectTerm280> terms)
    {
        switch (functionType)
        {
            case FunctionTypes.iMin:
                var min = terms.Min(item => item.Obj);
                return Convert.ToDouble(min);

            case FunctionTypes.iMax:
                var max = terms.Max(item => item.Obj);
                return Convert.ToDouble(max);

            case FunctionTypes.iSum:
                return 0;
            default: return 0;


        }

    }

    static FunctionTypes ToFunctionType(string functionType) =>
        functionType switch
        {
            "imin" => FunctionTypes.iMin,
            "imax" => FunctionTypes.iMax,
            "max" => FunctionTypes.Max,
            "isum" => FunctionTypes.iSum,
            _ => throw new ArgumentException("Invalid function type"),
        };

    public static double EvaluateSimpleArithmetic(string symbolFormula, Dictionary<string, ObjectTerm280> terms)
    {
        var rgxTerm = new Regex(@"([XA]\d\d)");
        var matchTersm = rgxTerm.Match(symbolFormula);
        Dictionary<string, object> plainObjects = terms.ToDictionary(item => item.Key, item => item.Value.Obj);
        var result = Eval.Execute<double>(symbolFormula, plainObjects);
        return result;
    }

}

