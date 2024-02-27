using Syncfusion.XlsIO.Implementation.PivotAnalysis;
using System.Data;
using System.Text.RegularExpressions;
using Z.Expressions;

namespace NewValidator.ValidationClasses;

public enum FunctionTypes { Min, Max, Sum };
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

        var rgxFn = new Regex(@"^(isNull|matches|not)?\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$");

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
        //if there are terms with parenthesis like  x1<3 or  (x0>3 and X1<4) 
        //replace parenthesis with zet terms. 
        //evaluate each zet 
        //reconstruct the formula using results instead of z
        //try again 
        var rgxTerm = new Regex(@"(isNull|matches|not)?\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        var matchesTerms = rgxTerm.Matches(formula);
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
            var res = ValidationFunctions.ValidateArithmetic(formula, terms);
            return res;
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


    //***********************arithmetic
    public static double EvaluateGeneralArithmeticExpression(string formula, Dictionary<string, ObjectTerm280> terms)
    {
        //{t: S.23.01.01.01, r: R0540 ... } i= imin(imax({t: S.23.01.01.01, r: R0540, ... }, 0) i* 0.25, {t: S.23.01.01.01, r: R0500,... })
        //Recursion until no functions. Then call Zexpression to evaluate
        //1. outer parenthesis with or without function =>=> evaluate function or remove parenthesis and recurse
        //2. if there are terms in parenthesis, evaluate each term in the parenthesis. (replace each term in parenthesis with Axx 
        //3. if there is "and","or", nothing in this order => evaluate the two terms around "and" or "or" or "nothing"

        var rgxFn = new Regex(@"^(imin|imax|isum)?\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$");

        //1. outer parenthesis with or without function (evaluate function or remove parenthesis and recurse if outer parenthesis without function)
        var match = rgxFn.Match(formula);
        if (match.Success)
        {
            //( ab and matches(cd)) => evaluate ab and matches(cd)
            var fn = match.Groups[1].Value;
            var value = match.Groups[2].Value;

            //check if the functions have an inner function. If not =>evaluate
            switch (fn)
            {
                case "isum":
                    var resSum = EvaluateGeneralArithmeticExpression(value, terms);
                    return resSum;
                case "imax":
                    //var resn = string.IsNullOrEmpty(value);
                    var resMax = EvaluateGeneralArithmeticExpression(value, terms); ;
                    return resMax;
                case "imin":
                    //var resn = string.IsNullOrEmpty(value);
                    var resMin = 3;
                    return resMin;
                default:
                    //this is executed when there are outer parenthesis around (a=b and (bc==dd) and b=c) => a=b and (bc==dd) and b=c)
                    var res = EvaluateGeneralArithmeticExpression(value, terms);
                    return res;
            }
        }


        //////////////////////////////// Make new formula with zet 
        //if there are terms with parenthesis like  x1<3 or  (x0>3 and X1<4) 
        //replace parenthesis with  terms. 
        //evaluate each zet 
        //reconstruct the formula using results instead of z
        //try again 
        var rgxTerm = new Regex(@"(imin|imax|isum)?\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        var matchesTerms = rgxTerm.Matches(formula);
        var ruleTextParenTerms = matchesTerms.Select((match, i) => new ZetTerm($"A{i:D2}", match.Value, false)) ?? new List<ZetTerm>();



        return 1;


    }


    public static double EvaluateArithmeticRecursively(string functionFormula, Dictionary<string, ObjectTerm280> terms)
    {
        //initial formula : 5 + imin(3) +imax(4)

        var rgxTerm = new Regex(@"(imin|imax|isum)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        var matchFunctions = rgxTerm.Matches(functionFormula);
        var functionTerms = matchFunctions.Select((match, i) => new ArTerm($"A{i:D2}", match.Value, 0, "")) ?? new List<ArTerm>();


        //1.  just an expression , has no functions and therefore evaluate 
        if (!matchFunctions.Any())
        {
            var resExp = EvaluateSimpleArithmetic(functionFormula, terms);
            return resExp;
        }

        var formulaWithSymbols = functionTerms.Aggregate(functionFormula, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.Formula);
            string replacedString = currentText[..index] + " " + val.Letter + " " + currentText[(index + val.Formula.Length)..];
            return replacedString;
        });


        //2 the formula is just a single function WITHOUT any nested functions
        //evaluate
        var rgxSingleFunction = new Regex(@"^(imin|imax|isum)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$");
        var matchSingle = rgxSingleFunction.Match(functionFormula);
        string[] functionsSupported = { "imin", "imax", "isum" };
        if (matchSingle.Success)
        {
            var valueInside = matchSingle.Groups[2].Value;//3
            if (!functionsSupported.Any(fn => valueInside.Contains(fn)))
            {
                //for example imin(3)
                var fn = matchSingle.Groups[1].Value; //imin                
                var functionResult = fn switch
                {
                    //the functions will use the terms
                    //evaluate imin with valueInside above
                    "imin" => 3,
                    "imax" => 4,
                    "isum" => 9,
                    _ => throw new NotImplementedException()
                };
                return functionResult;
            }
        }

        //3. create a new formula where functions such as imin(x0+4),imax(3,3),isum(x+3)) are replaced with letters (A00,A01,...)
        //--create the  terms 
        //--then evaluate each term 

        if (functionTerms.Any())
        {

            //build a new formula where the terms(inside of functions) are replaced with the letters of the textTersms
            //ex:initial formula ://5 + imin(3) +imax(4)  result : 5 +  A00  + A01 

            //create the A terms which are now in the formula {A00,imin(3),...}, {A01,imax(4),...}
            var Aterms = functionTerms
            .Select(fn => new ArTerm(fn.Letter, fn.Formula, 0, ""))
            .ToList();


            //Evaluate each of these terms with the formula INSIDE the function and create objectTerms

            var newTerms = Aterms.Select(tm =>
            {
                var isNestedTerm = false;
                var formulaInsideFunction = "";
                var termValue = 0.0;
                var functionFormula = tm.Formula;
                var matchInside = rgxSingleFunction.Match(tm.Formula);
                if (matchInside.Success)
                {
                    formulaInsideFunction = matchInside.Groups[2].Value;
                    isNestedTerm = functionsSupported.Any(fn => formulaInsideFunction.Contains(fn));
                }
                else
                {
                    throw (new NotImplementedException($"term is not matched{functionFormula}"));
                }

                if (!isNestedTerm)
                {
                    termValue = EvaluateArithmeticRecursively(tm.Formula, terms);
                }
                else
                {
                    var resInside = EvaluateArithmeticRecursively(formulaInsideFunction, terms);
                    termValue = EvaluateArithmeticRecursively(tm.Formula, terms);

                }
                //There are nested functions inside the formula, evaluate the inside and then the term

                return (tm.Letter, new ObjectTerm280(tm.Letter, 0, false, termValue));
            })
            .ToList();

            var allTerms = terms.Select(tm =>
            {
                var obj = tm.Value with { Decimals = -1 };
                return (tm.Key, obj);
            })
            .ToList();  //create new to avoid affecting the old             
            allTerms.AddRange(newTerms);

            var dicTerms = allTerms?.ToDictionary(at => at.Key, at => at.obj) ?? new();

            var res = EvaluateSimpleArithmetic(formulaWithSymbols, dicTerms);
            return res;
        }






        return 0;
    }



    public static double EvaluateArithmeticNew(string functionFormula, Dictionary<string, ObjectTerm280> terms)
    {
        //initial formula : 5 + imin(3) +imax(4)

        var rgxTerm = new Regex(@"(imin|imax|isum)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        var rgxSingleFunction = new Regex(@"^(imin|imax|isum)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$");
        var matchFunctions = rgxTerm.Matches(functionFormula);
        var functionTerms = matchFunctions.Select((match, i) => new ArTerm($"A{i:D2}", match.Value, 0, "")) ?? new List<ArTerm>();
        

        var formulaWithSymbols = functionTerms.Aggregate(functionFormula, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.Formula);
            string replacedString = currentText[..index] + " " + val.Letter + " " + currentText[(index + val.Formula.Length)..];
            return replacedString;
        });

        List<(string,ObjectTerm280)> newObjTerms = new();
        
        foreach(var fnTerm in functionTerms)
        {
            var val2 = EvaluateFunction(fnTerm.Formula, terms);
            newObjTerms.Add(new (fnTerm.Letter, new ObjectTerm280("F",0,false,val2)) );             
        }


        var allTermsx = terms.Select(trm  => ( trm.Key, trm.Value with { Decimals=9})  ).ToList();
        allTermsx.AddRange(newObjTerms);
        var allObjectsDic = allTermsx.ToDictionary(x => x.Key, x => x.Item2);
        var val = EvaluateSimpleArithmetic(formulaWithSymbols, allObjectsDic);

          
        return val;
    }


    public static double EvaluateFunction(string functionText, Dictionary<string, ObjectTerm280> terms)
    {
        string[] functionsSupported = { "imin", "imax", "isum" };
        var rgxSingleFunction = new Regex(@"^(imin|imax|isum)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$");
        var rgxTerms = new Regex(@"(imin|imax|isum)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        functionText = functionText.Trim();

        var matchFn = rgxSingleFunction.Match(functionText);
        if (!matchFn.Success) throw new ArgumentException($"Invalid function:{functionText}");

        var funtionTypeStr = matchFn.Groups[1].Value;
        var functionContent = matchFn.Groups[2].Value;
        var functionType = ToFunctionType(funtionTypeStr);
        var matchFunctions = rgxTerms.Matches(functionContent);
        var nestedFunctions = matchFunctions.Select((match, i) => new FunctionObject($"F{i:D2}", ToFunctionType(match.Groups[1].Value), match.Value, match.Groups[2].Value, 0));

        var contentFormulaWithSymbols = nestedFunctions.Aggregate(functionContent, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.FullText);
            string replacedString = currentText[..index] + " " + val.Letter + " " + currentText[(index + val.FullText.Length)..];
            return replacedString;
        });

        var functionTerms = contentFormulaWithSymbols.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        //X0, Z0, F01, X0+2, ....  the F terms will be evaluated recursively
        var regexZet = new Regex(@"^(F\d{2})");                
        var functionObjectTerms = functionTerms
                .Select(functionTerm => regexZet.IsMatch(functionTerm)
                    ? new ObjectTerm280("F", 0, false, EvaluateFunction(nestedFunctions.FirstOrDefault(nf => nf.Letter == functionTerm)!.FullText, terms))
                    : new ObjectTerm280("V", 0, false, EvaluateSimpleArithmetic(functionTerm, terms))
                )
                .ToList();
        var val = EvaluateFunctionWithComputedTerms(functionType, functionObjectTerms);
        return val;
    }

    static double EvaluateFunctionWithComputedTerms(FunctionTypes functionType, IEnumerable<ObjectTerm280> terms)
    {
        switch (functionType)
        {
            case FunctionTypes.Min:
                var min = terms.Min(item => item.Obj);
                return Convert.ToDouble(min);

            case FunctionTypes.Max:
                var max = terms.Max(item => item.Obj);
                return Convert.ToDouble(max);

            case FunctionTypes.Sum:
                return 0;
            default: return 0;


        }

    }

    static FunctionTypes ToFunctionType(string functionType) =>
        functionType switch
        {
            "imin" => FunctionTypes.Min,
            "imax" => FunctionTypes.Max,
            "isum" => FunctionTypes.Sum,
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

