using Shared.DataModels;
using Syncfusion.XlsIO.Implementation.Collections.Grouping;
using Syncfusion.XlsIO.Implementation.PivotAnalysis;
using System.Data;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Z.Expressions;

namespace NewValidator.ValidationClasses;

public enum FunctionTypes { iMin, iMax, iSum, Max };
public record FunctionObject(string Letter, FunctionTypes FunctionType, string FullText, string FunctionArgument, double Value);

public record ObjectTerm280(string ObjectType, int Decimals, bool IsTolerant, Object Obj, bool IsNullFact, List<TemplateSheetFact> SeqFacts);
public record ZetTerm(string Letter, string Formula, bool IsPassed);
public record ArTerm(string Letter, string Formula, double ValueReal, string ValueString);

public partial class ExpressionEvaluator
{
    private enum BooleanOperators { None, IsAnd, IsOR };

    public static bool ValidateRule(RuleStructure280 ruleStructure280)
    {
        //{t: S.23.01.02.02, r: R0700, c: C0060, z: Z0001, dv: 0, seq: False, id: v0, f: solvency, fv: solvency2} i= isum({t: S.23.01.02.02, r: R0710; R0720; R0730; R0740; R0760, c: C0060, z: Z0001, dv: emptySequence(), seq: True, id: v1, f: solvency, fv: solvency2})
        //objectTerm: an object which gets information from the fact and the the RuleTerm ({t:2000} such as sequence 
        var ifComponent = ruleStructure280.IfComponent;         
        var isValidIf = ExpressionEvaluator.EvaluateGeneralBooleanExpression(ifComponent.SymbolExpression, ifComponent.ObjectTerms);
        return isValidIf;

        if (1 == 2)
        {
            //var thenComponent = rule.ThenComponent;
            //Dictionary<string, ObjectTerm280> thenObjectTerms = ToOjectTerm280UsingFactValues(thenComponent);
            //var isValidThen = ExpressionEvaluator.EvaluateGeneralBooleanExpression(thenComponent.SymbolExpression, thenObjectTerms);

            //var elseComponent = rule.ElseComponent;
            //Dictionary<string, ObjectTerm280> elseObjectTerms = ToOjectTerm280UsingFactValues(elseComponent);
            //var isValidElse = ExpressionEvaluator.EvaluateGeneralBooleanExpression(elseComponent.SymbolExpression, elseObjectTerms);

            //var isPlainRule = ifComponent.IsValid && !elseComponent.IsValid && !thenComponent.IsValid;
            //var isCompleteRule =
            //    ifComponent.IsValid && elseComponent.IsValid && thenComponent.IsValid
            //    || ifComponent.IsValid && !elseComponent.IsValid && !thenComponent.IsValid;
        }

    }


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
        var termOperator = formula.Contains("and") ? BooleanOperators.IsAnd
            : formula.Contains("or") ? BooleanOperators.IsOR
            : BooleanOperators.None;

        if (termOperator == BooleanOperators.None)
        {
            var regSplit = new Regex(@"(.+)(>|>=|<|<=|=)(.+)");
            var matchSplit = regSplit.Match(formula);
            if (!matchSplit.Success)
            {
                throw new ApplicationException($"Formula cannot be split using <,>,= :{formula}");
            }
            var left = matchSplit.Groups[1].Value;
            var resLeft = EvaluateArithmeticRecursively(left, terms);
            var op = matchSplit.Groups[2].Value;
            var right = matchSplit.Groups[3].Value;
            var resRight = EvaluateArithmeticRecursively(right, terms);


            var formulaLR = $"L0 {op} R0";
            var formulaLRObjects = new Dictionary<string, object>
            {
                { "L0", resLeft },
                { "R0", resRight }
            };
            var res = Eval.Execute<bool>(formulaLR, formulaLRObjects);
            return res;
        }

        if (termOperator == BooleanOperators.IsAnd)
        {
            var resAnd = formula.Split("and", StringSplitOptions.RemoveEmptyEntries);
            var val1 = resAnd[0].Trim();
            var val2 = resAnd[1].Trim();
            var res1 = EvaluateGeneralBooleanExpression(val1, terms);
            var res2 = EvaluateGeneralBooleanExpression(val2, terms);
            return res1 && res2;
        }
        if (termOperator == BooleanOperators.IsOR)
        {
            var res = formula.Split("or", StringSplitOptions.RemoveEmptyEntries);
            var bres1 = EvaluateGeneralBooleanExpression(res[0].Trim(), terms);
            var val2 = res[1].Trim();
            var bres2 = EvaluateGeneralBooleanExpression(val2, terms);
            return bres1 || bres2;
        }

        return false;


    }


    public static double EvaluateArithmeticRecursively(string arithmeticExpression, Dictionary<string, ObjectTerm280> terms)
    {
        //will create a list of OUTER arithmetic functions (imin,imax,...).
        //Then, it will call evaluateFunction for each
        //Then , will use EvaluateSimpleArithmetic (formula with symbols and all symbols have a value in a list)
        // @"5 + imin(3) +imax(4)";
        // @"7 + imin(imax(3,5),4)";
        // imin(imax(X01, 0) i* 0.25, X02) 
        var rgxTerm = RgxAggregateFunctions(); //"(imin|imax|max|isum)\\s*\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)"
        var matchFunctions = rgxTerm.Matches(arithmeticExpression);

        //var functionTerms = matchFunctions.Select((match, i) => new ArTerm($"A{i:D2}", match.Value, 0, "")) ?? new List<ArTerm>();        
        //var formulaWithSymbols = functionTerms.Aggregate(arithmeticExpression, (currentText, val) =>
        //{
        //    int index = currentText.IndexOf(val.Formula);
        //    string replacedString = currentText[..index] + " " + val.Letter + " " + currentText[(index + val.Formula.Length)..];
        //    return replacedString;
        //});

        //5 +  A00  + A01
        //7 +  A00 
        var (formulaWithSymbols, functionObjects) = ToFunctionObjectsFromTextFormula(arithmeticExpression, rgxTerm, "V");

        formulaWithSymbols = ReplaceIntervalOperators(formulaWithSymbols);

        var newObjTerms = functionObjects
            .Select(ft =>
            {                
                var val = EvaluateFunction(ft.FullText, terms);
                return (ft.Letter, new ObjectTerm280("F", 0, false, val, false,new List<TemplateSheetFact>()));
            });

        var allTermsx = terms.Select(trm => (trm.Key, trm.Value with { Decimals = 9 })).ToList();
        allTermsx.AddRange(newObjTerms);
        var allObjectsDic = allTermsx.ToDictionary(x => x.Key, x => x.Item2);
        var val = EvaluateSimpleArithmetic(formulaWithSymbols, allObjectsDic);

        return val;
    }

    static string ReplaceIntervalOperators(string input)
    {
        // imin(imax(X01, 0) i* 0.25, X02) =>// imin(imax(X01, 0) * 0.25, X02) 
        // Define the regular expressions
        Regex rgxStar = new Regex(@"i\*");
        Regex rgxPlus = new Regex(@"i\+");
        Regex rgxMinus = new Regex(@"i\-");

        // Replace occurrences of "i*" and "i+"
        string result = rgxStar.Replace(input, "*");
        result = rgxPlus.Replace(result, "+");
        result = rgxMinus.Replace(result, "-");

        return result;
    }

    public static double EvaluateFunction(string functionText, Dictionary<string, ObjectTerm280> terms)
    {
        //it is not recursive by itself but it uses EvaluateArithmeticRecursively which is recursive
        //EXAMPLE To Test   : imax(imin(3, 7) , 4) 
        //EXAMPLE withREAL  : imin(imax(X01, 0) * 0.25, X02)
        //Takes the inside content of the function and
        //  --construct a List of the nested Functions objects (nestedFunctions) 
        //  --builds a new formula (contentFormulaWithSymbols) and evaluates each term
        //  --the term is evaluated using simpleArithmetic if no nesting and using recursion if more nested functions
        // At the end all the terms are computed, and it uses the original symbol formula

        string[] functionsSupported = { "imin", "imax", "isum", "max" };

        functionText = ReplaceIntervalOperators(functionText);
        var rgxSingleFunction = RgxAggregateFunctionSingle(); ////"^(imin|imax|max|isum)\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)$")        
        functionText = functionText.Trim();

        var matchFn = rgxSingleFunction.Match(functionText);
        if (!matchFn.Success) throw new ArgumentException($"Invalid function:{functionText}");


        var functionContent = matchFn.Groups[2].Value;
        var functionType = ToFunctionType(matchFn.Groups[1].Value);
        // the function contents is a list of expressions separated by comma =>imax(X01, 0) * 0.25, X02
        // *** I have a trick here
        // *** need to split the expressions but it is difficult because inside the functions there are commas also
        // *** so replace the functions with letters to be able to split with comma and then replace the letters with function text again
        var rgxFunctions2 = RgxAggregateFunctions();////"(imin|imax|max|isum)\\s*\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)"
        var (innerSymbolFormula, innerFunctionTerms) = ToFunctionObjectsFromTextFormula(functionContent, rgxFunctions2, "F");
        var innerArguments = innerSymbolFormula.Split(",", StringSplitOptions.RemoveEmptyEntries);
        var innerResults = innerArguments.Select(r =>
        {
            foreach (var ft in innerFunctionTerms)
            {
                r = r.Replace(ft.Letter, ft.FullText);
            }            
            var res = EvaluateArithmeticRecursively(r, terms);
            var obj = new ObjectTerm280("F", 0, false, res, false,new List<TemplateSheetFact>());
            return obj;
        });
        var final2 = EvaluateFunctionWithComputedTerms(functionType, innerResults);//at the end =>functionType:Max and the terms are : 3, 4 
        return final2;
        //*****************************************


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
        Dictionary<string, object> plainObjects = terms.ToDictionary(item => item.Key, item => stringToDouble(item.Value.Obj));

        var result = Eval.Execute<double>(symbolFormula, plainObjects);
        return result;


        static object stringToDouble(object obj)
        {
            var type = obj.GetType();
            var result = type==typeof(string) ? Convert.ToDouble(obj) : obj;
            return result;
        }

    }


    public static (string symbolFormula, List<FunctionObject> FunctionTerms) ToFunctionObjectsFromTextFormula(string text, Regex regex, string letter)
    {
        var matchFunctions = regex.Matches(text);
        var nestedFunctions = matchFunctions.Select((match, i) => new FunctionObject($"{letter}{i:D2}", ToFunctionType(match.Groups[1].Value), match.Value, match.Groups[2].Value, 0)).ToList();
        var contentFormulaWithSymbols = nestedFunctions.Aggregate(text, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.FullText);
            string replacedString = currentText[..index] + " " + val.Letter + " " + currentText[(index + val.FullText.Length)..];
            return replacedString;
        });

        return (contentFormulaWithSymbols, nestedFunctions);
    }


    [GeneratedRegex(@"^(imin|imax|max|isum)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$")]
    private static partial Regex RgxAggregateFunctionSingle();

    [GeneratedRegex(@"(imin|imax|max|isum)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)")]
    private static partial Regex RgxAggregateFunctions();




}

