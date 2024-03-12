using Microsoft.Data.SqlClient;
using Shared.DataModels;
using Syncfusion.XlsIO.Implementation.Collections.Grouping;
using Syncfusion.XlsIO.Implementation.PivotAnalysis;
using Syncfusion.XlsIO.Parser.Biff_Records;
using System.Data;
using System.Drawing;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using Z.Expressions;

namespace NewValidator.ValidationClasses;

public enum KleeneValue
{
    True,
    False,
    Unknown
}
public record DoubleObject(bool IsNull, double Value);
public record BooleanObject(bool IsNull, bool Value);
public enum FunctionAggregateTypes { iMin, iMax, iSum, iCount, Max, Plain };
public record FunctionObject(string Letter, FunctionAggregateTypes FunctionType, string FullText, string FunctionArgument, double Value);

public record ObjectTerm280(string DataType, int Decimals, bool IsTolerant, Object? Obj, double sumValue, int countValue, TemplateSheetFact? fact, bool IsNullFact);
public record ZetTerm(string Letter, string Formula, string FunctionArgument, string DataType, int Decimals, bool IsNullFact, FunctionAggregateTypes FunctionType, ObjectTerm280? Object280, Object? ObjectValue, KleeneValue KleenValue);


public partial class ExpressionEvaluator
{
    public enum LogicalOperators { None, IsAnd, IsOR };

    public static bool ValidateRule(RuleStructure280 ruleStructure280)
    {
        //{t: S.23.01.02.02, r: R0700, c: C0060, z: Z0001, dv: 0, seq: False, id: v0, f: solvency, fv: solvency2} i= isum({t: S.23.01.02.02, r: R0710; R0720; R0730; R0740; R0760, c: C0060, z: Z0001, dv: emptySequence(), seq: True, id: v1, f: solvency, fv: solvency2})
        //objectTerm: an object which gets information from the fact and the the RuleTerm ({t:2000} such as sequence 

        var ifResult = ExpressionEvaluator.EvaluateGeneralBooleanExpression(ruleStructure280.IfComponent.SymbolExpression, ruleStructure280.IfComponent.ZetTerms);
        if (ruleStructure280.ThenComponent.IsEmpty)
        {
            return ToBoolean(ifResult);
        }

        //thenComponent EXISTS but no Else component
        if (ruleStructure280.ElseComponent.IsEmpty)
        {
            if (ifResult == KleeneValue.False)
            {
                return false; // if is false and there is no else
            }
            else if (ifResult == KleeneValue.True)
            {
                var thenResult = ExpressionEvaluator.EvaluateGeneralBooleanExpression(ruleStructure280.ThenComponent.SymbolExpression, ruleStructure280.ThenComponent.ZetTerms);
                return ToBoolean(thenResult);
            }
            else // (ifResult == KleeneValue.Unknown)
            {
                return true; //todo need to check this
            }
        }
        else
        {
            //elseComponent EXISTS
            if (ifResult == KleeneValue.True)
            {
                var thenRes = ExpressionEvaluator.EvaluateGeneralBooleanExpression(ruleStructure280.ThenComponent.SymbolExpression, ruleStructure280.ThenComponent.ZetTerms);
                return ToBoolean(thenRes); // if is false and there is no else      
            }
            if (ifResult == KleeneValue.False)
            {
                var elseRes = ExpressionEvaluator.EvaluateGeneralBooleanExpression(ruleStructure280.ElseComponent.SymbolExpression, ruleStructure280.ElseComponent.ZetTerms);

                return ToBoolean(elseRes); // if is false and there is no else
            }
            else //(ifResult == KleeneValue.Unknown)
            {
                return true; //todo need to check this
            }


        }
        bool ToBoolean(KleeneValue kleeneVal) => kleeneVal == KleeneValue.True || kleeneVal == KleeneValue.Unknown;
    }



    public static KleeneValue EvaluateGeneralBooleanExpression(string formula, Dictionary<string, ZetTerm> terms)
    {

        //Recursion to remove outer parenthesis, real evaluation of terms with only a function, evaluation and recurse for  "and", "or", and finally real evaluation of the term
        //1. single function or outer parenthesis => evaluate function or remove parenthesis and recurse       
        //2. if there is "and","or", nothing in this order => evaluate the two terms around "and" or "or" or "nothing"
        //3. arithmetic
        

        var rgxOuter = RgxOuterParenthesis();
        var matchOuter = rgxOuter.Match(formula);
        if (matchOuter.Success)
        {
            var insideParen = matchOuter.Groups[1].Value.Trim();
            var resInside= EvaluateGeneralBooleanExpression(insideParen, terms);
            return resInside;

        }

        //************************** Single Function Or Parenthesis********************************************************
        //Check if this is an outer parenthesis or an Outer function.
        //1. outer parenthesis with or without function (evaluate function or remove parenthesis and recurse if outer parenthesis without function)
        var rgxFn = new Regex(@"^(isNull|matches|not|dim|true|\s|^)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$");
        var match = rgxFn.Match(formula);
        if (match.Success)
        {
            //( ab and matches(cd)) => evaluate ab and matches(cd)
            var fn = match.Groups[1].Value;
            var value = match.Groups[2].Value;

            switch (fn)
            {
                case "not":
                    var resNot = EvaluateGeneralBooleanExpression(value, terms);
                    //return resNot.IsNull ? new BooleanObject(true, false) : new BooleanObject(false, !resNot.Value);
                    return resNot == KleeneValue.Unknown ? KleeneValue.Unknown
                        : resNot == KleeneValue.False ? KleeneValue.True
                        : KleeneValue.False;
                case "isNull":
                    var resn = ValidationFunctions.ValidateIsNull(formula, terms);
                    //return resn;
                    return resn ? KleeneValue.True : KleeneValue.False;
                case "matches":
                    var resm = ValidationFunctions.ValidateMatch(formula, terms);
                    return resm ? KleeneValue.True : KleeneValue.False;
                case "dim":
                    var resdim = ValidationFunctions.ValidateDim(formula, terms);
                    return resdim ? KleeneValue.True : KleeneValue.False;
                case "true":
                    return KleeneValue.True;
                default:
                    //this is executed when there are outer parenthesis around (a=b and (bc==dd) and b=c) => a=b and (bc==dd) and b=c
                    var resN = EvaluateGeneralBooleanExpression(value, terms);
                    return resN;
            }
        }

        var res = SplitAndOrExpression(formula);
        if (res.logicalOperator == LogicalOperators.IsAnd)
        {
            var aAndRes = EvaluateGeneralBooleanExpression(res.left, terms);
            var bAndRes = EvaluateGeneralBooleanExpression(res.Right, terms);
            if (aAndRes == KleeneValue.True && bAndRes == KleeneValue.True)
                return KleeneValue.True;
            else if (aAndRes == KleeneValue.False || bAndRes == KleeneValue.False)
                return KleeneValue.False;
            else
                return KleeneValue.Unknown;
        }


        if (res.logicalOperator == LogicalOperators.IsOR)
        {
            //kleene Logic :
            //-- true if any is true
            //-- false only if both are false
            //-- otherwise unknown
            //Null OR null => NULL
            //False OR null => NULL
            //True OR null =>TRUE

            var orRes1 = EvaluateGeneralBooleanExpression(res.left, terms);
            var orRes2 = EvaluateGeneralBooleanExpression(res.Right, terms);

            if (orRes1 == KleeneValue.True || orRes2 == KleeneValue.True)
                return KleeneValue.True;
            else if (orRes1 == KleeneValue.False && orRes2 == KleeneValue.False)
                return KleeneValue.False;
            else
                return KleeneValue.Unknown;
        }


        if (res.logicalOperator == LogicalOperators.None)
        {
            var regSplit = new Regex(@"(.+?)\s*(>=|>|<=|<|==|=)\s*(.+)");
            var xxx = regSplit.Split(formula);
            var matchSplit = regSplit.Match(formula);
            if (!matchSplit.Success)
            {
                throw new ApplicationException($"Formula cannot be split using <,>,= :{formula}");
            }
            var left = matchSplit.Groups[1].Value;
            var op = matchSplit.Groups[2].Value;
            var right = matchSplit.Groups[3].Value;

            //todo -- only check the terms in the formula
            var isExpressionWithStrings = terms.Any(t => (t.Value?.Object280?.DataType ?? "") == "S");
            if (isExpressionWithStrings)
            {
                var resStr = EvaluateSimpleString(formula, terms);
                return resStr;
            }

            var leftDecimals = terms.ContainsKey(left) ? terms[left]?.Object280?.Decimals ?? 0 : 0;
            var rightDecimals = terms.ContainsKey(right) ? terms[right]?.Object280?.Decimals ?? 0 : 0;

            var resLeftDbl = EvaluateArithmeticRecursively(left, terms);
            var resRightDbl = EvaluateArithmeticRecursively(right, terms);

            if (resLeftDbl.IsNull || resRightDbl.IsNull)
            {
                return KleeneValue.Unknown;
            }

            var intervalResult = IntervalFunctions.IsIntervalExpressionValid(op, resLeftDbl.Value, leftDecimals, resRightDbl.Value, rightDecimals);

            return intervalResult ? KleeneValue.True : KleeneValue.False;
        }

        return KleeneValue.True;

    }




    public static KleeneValue EvaluateGeneralBooleanExpressionOld(string formula, Dictionary<string, ZetTerm> terms)
    {

        //Recursion to remove outer parenthesis, real evaluation of terms with only a function, evaluation and recurse for  "and", "or", and finally real evaluation of the term
        //1. outer parenthesis with or without function =>=> evaluate function or remove parenthesis and recurse
        //2. if there are terms in parenthesis, evaluate each term in the parenthesis. (replace each term in parenthesis with Zxx and its value (1==1 for true, and 1==2 for false)
        //3. if there is "and","or", nothing in this order => evaluate the two terms around "and" or "or" or "nothing"

        var rgxFn = new Regex(@"^(isNull|matches|not|dim|true|\s|^)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$");

        //************************** Single Function Or Parenthesis********************************************************
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
                    var resNot = EvaluateGeneralBooleanExpression(value, terms);
                    //return resNot.IsNull ? new BooleanObject(true, false) : new BooleanObject(false, !resNot.Value);
                    return resNot == KleeneValue.Unknown ? KleeneValue.Unknown
                        : resNot == KleeneValue.False ? KleeneValue.True
                        : KleeneValue.False;
                case "isNull":
                    var resn = ValidationFunctions.ValidateIsNull(formula, terms);
                    //return resn;
                    return resn ? KleeneValue.True : KleeneValue.False;
                case "matches":
                    var resm = ValidationFunctions.ValidateMatch(formula, terms);
                    return resm ? KleeneValue.True : KleeneValue.False;
                case "dim":
                    var resdim = ValidationFunctions.ValidateDim(formula, terms);
                    return resdim ? KleeneValue.True : KleeneValue.False;
                case "true":
                    return KleeneValue.True;
                default:
                    //this is executed when there are outer parenthesis around (a=b and (bc==dd) and b=c) => a=b and (bc==dd) and b=c
                    var res = EvaluateGeneralBooleanExpression(value, terms);
                    return res;
            }
        }


        //************************** Make new formula with zet ********************************************************        
        //if there are terms with parenthesis like  x1<3 or  (x0>3 and X1<4) => x1<3 or Z00
        //replace parenthesis with zet terms. 
        //evaluate each zet 
        //reconstruct the formula using results instead of z        
        //lookahead (?<!\S) is there to avoid matching imax(, imin( but to match 
        var rgxTerm = new Regex(@"(isNull|matches|not|dim|true|\s|^)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");

        var matchesTerms = rgxTerm.Matches(formula.Trim());
        //var ruleTextParenTerms = matchesTerms.Select((match, i) => new ZetTerm($"Z{i:D2}", match.Value, KleeneValue.Unknown)) ?? new List<ZetTerm>();
        var ruleTextParenTerms = matchesTerms.Select((match, i) => new ZetTerm($"Z{i:D2}", match.Value, match.Groups[2].Value, "B", 0, false, ToFunctionType(match.Groups[1].Value), null, null, KleeneValue.Unknown)) ?? new List<ZetTerm>();

        //2. if there are terms in parenthesis, evaluate each term in the parenthesis and return the result 1==1 for true 1==2 for false
        if (ruleTextParenTerms.Any())
        {
            //*** i changed that to remove the trick with 1==1
            //build a new formula where the terms(inside parenthesis) are replaced with the letters of the textTersms
            var formulaParen = ruleTextParenTerms.Aggregate(formula, (currentText, val) =>
            {
                int index = currentText.IndexOf(val.Formula);
                string replacedString = currentText[..index] + " " + val.Letter + " " + currentText[(index + val.Formula.Length)..];
                return replacedString;
            });

            //Evaluate each of these terms 
            var parenthesisTerms = ruleTextParenTerms
             .Select(zz => zz with { KleenValue = EvaluateGeneralBooleanExpression(zz.Formula, terms) })
            .ToList();

            //the new formula replaces each term(boolean) with either 1=1 or 1==2
            var newFormula = Enumerable.Aggregate<ZetTerm, string>(parenthesisTerms, formulaParen, (Func<string, ZetTerm, string>)((currentText, val) =>
            {
                int index = currentText.IndexOf(val.Letter);
                var replacement = (val.KleenValue == KleeneValue.True) ? "1==1" : "1==2";
                string replacedString = currentText[..index] + " " + replacement + " " + currentText[(index + val.Letter.Length)..];
                return replacedString;
            }));

            var res = ValidationFunctions.ValidateArithmetic(newFormula, terms);
            return res;
        }



        //*******************************************************************
        //3. check for "and","or", nothing in this order
        // a. If there is an "and" evaluate the two terms around end,
        // b. if there is an "or" evaluate the two terms around "or"
        // c. if there is  no "and" , "or" evaluate the terms
        var termOperator = formula.Contains("and") ? LogicalOperators.IsAnd
            : formula.Contains("or") ? LogicalOperators.IsOR
            : LogicalOperators.None;

        if (termOperator == LogicalOperators.None)
        {
            var regSplit = new Regex(@"(.+?)\s*(>=|>|<=|<|==|=)\s*(.+)");
            var xxx = regSplit.Split(formula);
            var matchSplit = regSplit.Match(formula);
            if (!matchSplit.Success)
            {
                throw new ApplicationException($"Formula cannot be split using <,>,= :{formula}");
            }
            var left = matchSplit.Groups[1].Value;
            var op = matchSplit.Groups[2].Value;
            var right = matchSplit.Groups[3].Value;

            var isExpressionWithStrings = terms.Any(t => (t.Value?.Object280?.DataType ?? "") == "S");
            if (isExpressionWithStrings)
            {
                var resStr = EvaluateSimpleString(formula, terms);
                return resStr;
            }

            var leftDecimals = terms.ContainsKey(left) ? terms[left]?.Object280?.Decimals ?? 0 : 0;
            var rightDecimals = terms.ContainsKey(right) ? terms[right]?.Object280?.Decimals ?? 0 : 0;

            var resLeftDbl = EvaluateArithmeticRecursively(left, terms);
            var resRightDbl = EvaluateArithmeticRecursively(right, terms);

            if (resLeftDbl.IsNull || resRightDbl.IsNull)
            {
                return KleeneValue.Unknown;
            }

            var intervalResult = IntervalFunctions.IsIntervalExpressionValid(op, resLeftDbl.Value, leftDecimals, resRightDbl.Value, rightDecimals);

            return intervalResult ? KleeneValue.True : KleeneValue.False;
        }

        if (termOperator == LogicalOperators.IsAnd)
        {
            //Kleene Logic:
            //--false if any is false(the other can be null or false)
            //--true only if both are true
            //--otherwise is null 
            //Null AND Null => Null
            //True AND Null => null
            //False AND Null => false

            var resAnd = formula.Split("and", StringSplitOptions.RemoveEmptyEntries);
            var val1 = resAnd[0].Trim();
            var val2 = resAnd[1].Trim();

            var aAndRes = EvaluateGeneralBooleanExpression(val1, terms);
            var bAndRes = EvaluateGeneralBooleanExpression(val2, terms);
            if (aAndRes == KleeneValue.True && bAndRes == KleeneValue.True)
                return KleeneValue.True;
            else if (aAndRes == KleeneValue.False || bAndRes == KleeneValue.False)
                return KleeneValue.False;
            else
                return KleeneValue.Unknown;

        }
        if (termOperator == LogicalOperators.IsOR)
        {
            //kleene Logic :
            //-- true if any is true
            //-- false only if both are false
            //-- otherwise unknown
            //Null OR null => NULL
            //False OR null => NULL
            //True OR null =>TRUE
            var res = formula.Split("or", StringSplitOptions.RemoveEmptyEntries);
            var orRes1 = EvaluateGeneralBooleanExpression(res[0].Trim(), terms);
            var val2 = res[1].Trim();
            var orRes2 = EvaluateGeneralBooleanExpression(val2, terms);

            if (orRes1 == KleeneValue.True || orRes2 == KleeneValue.True)
                return KleeneValue.True;
            else if (orRes1 == KleeneValue.False && orRes2 == KleeneValue.False)
                return KleeneValue.False;
            else
                return KleeneValue.Unknown;
        }

        return KleeneValue.Unknown;


    }


    public static DoubleObject EvaluateArithmeticRecursively(string arithmeticExpression, Dictionary<string, ZetTerm> terms)
    {
        // 1.Create a list of OUTER arithmetic functions (imin,imax,...).
        //  --call evaluateFunction for each
        //  --each function will become a symbol in the formula and a term with a value will be created
        // 2.Add the new terms to the exisiting ones        
        // 3.Use EvaluateSimpleArithmetic (formula with symbols and all symbols have a value in a list)
        //  --@"5 + imin(3) +imax(4)";
        //  --@"7 + imin(imax(3,5),4)";
        // imin(imax(X01, 0) i* 0.25, X02) 
        var rgxTerm = RgxAggregateFunctions(); //"(imin|imax|max|isum)\\s*\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)"
        var matchFunctions = rgxTerm.Matches(arithmeticExpression);


        //5 +  A00  + A01
        //7 +  A00 
        var (formulaWithSymbols, functionObjects) = CreateZetObjectsFromTextFormula(arithmeticExpression, rgxTerm, "V");

        formulaWithSymbols = ReplaceIntervalCharacters(formulaWithSymbols);

        var newObjTerms = functionObjects
            .Select(ft =>
            {
                //var val = EvaluateFunction(ft.FullText, terms);
                var val = EvaluateFunction(ft.Formula, terms);
                //return (ft.Letter, new ObjectTerm280("F", 0, false, val, 0, 0, null, false));
                return (ft.Letter, new ZetTerm("F", "", "", "N", 0, false, FunctionAggregateTypes.Plain, null, val, KleeneValue.Unknown));
            });
        var allTerms = terms.Select(trm => (trm.Key, trm.Value with { FunctionType = FunctionAggregateTypes.Plain })).ToList();
        allTerms.AddRange(newObjTerms);
        var allObjectsDic = allTerms.ToDictionary(x => x.Key, x => x.Item2);

        var val = EvaluateSimpleArithmetic(formulaWithSymbols, allObjectsDic);

        return val;
    }

    static string ReplaceIntervalCharacters(string input)
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

    public static double EvaluateFunction(string functionText, Dictionary<string, ZetTerm> terms)
    {
        //it is not recursive by itself but it uses EvaluateArithmeticRecursively which is recursive
        //EXAMPLE To Test   : imax(imin(3, 7) , 4) 
        //EXAMPLE withREAL  : imin(imax(X01, 0) * 0.25, X02)
        //Takes the inside content of the function and
        //  --construct a List of the nested Functions objects (nestedFunctions) 
        //  --builds a new formula (contentFormulaWithSymbols) and evaluates each term
        //  --the term is evaluated using simpleArithmetic if no nesting and using recursion if more nested functions
        // At the end all the terms are computed, and it uses the original symbol formula

        //string[] functionsSupported = { "imin", "imax", "isum", "icount", "max" };

        functionText = ReplaceIntervalCharacters(functionText);//max(x01,0) i => remove the i. the function has already been marked as interval
        var rgxSingleFunction = RgxAggregateFunctionSingle(); ////"^(imin|imax|max|isum)\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)$")        
        functionText = functionText.Trim();

        //we have a SINGLE function 
        var matchFn = rgxSingleFunction.Match(functionText);
        if (!matchFn.Success) throw new ArgumentException($"Invalid function:{functionText}");

        // the function CONTENT is a list of expressions separated by comma =>imax(X01, 0) * 0.25, X02 and => two expressions: imax(X01, 0) * 0.25  AND   X02
        // 1. Split the terms inside the function 
        // --the proper solution would be to split each expression, call the arithmeticExpressionEvaluator for each BUT due to commas inside functions, I cannot do the split
        // *** So I do this  trick. Replace the functions inside the function with terms ("F") to do the split and then back to their value        
        // 2.Evalueate each function term
        // 3.Finally, call  EvaluateFunctionWithComputedTerms since all the function terms were computed

        var functionContent = matchFn.Groups[2].Value;
        var functionType = ToFunctionType(matchFn.Groups[1].Value);
        var rgxFunctions2 = RgxAggregateFunctions();////"(imin|imax|max|isum)\\s*\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)"
        var (innerSymbolFormula, innerFunctionTerms) = CreateZetObjectsFromTextFormula(functionContent, rgxFunctions2, "F");
        var innerArguments = innerSymbolFormula.Split(",", StringSplitOptions.RemoveEmptyEntries);
        var innerFunctionArguments = innerArguments.Select(argSplit =>
        {
            //here, we are processing each inner term (which are expressions) of the function. For example , x2+3, or even max(x3)+3
            //When all the inner terms are evaluated, we will evalueate the actual function
            //for isum and icount do not recurse 
            foreach (var ft in innerFunctionTerms)
            {
                //replace each Letter "F"  with the actual text. For example, F01=> max(x1,3)
                argSplit = argSplit.Replace(ft.Letter, ft.Formula);
            }
            //if isum or icount do NOT recurse, there are no expressions inside. you just need to keep the value of the old terms which has the sum and count            
            if (functionType == FunctionAggregateTypes.iSum || functionType == FunctionAggregateTypes.iCount)
            {
                var sameObj = terms.FirstOrDefault(tr => tr.Key == functionContent).Value;
                return sameObj;
            }
            var res = EvaluateArithmeticRecursively(argSplit, terms);
            //var obj = new ObjectTerm280("F", 0, false, res, 0, 0, null, false);
            var obj = new ZetTerm("F", "", "", "N", 0, false, FunctionAggregateTypes.Plain, null, null, KleeneValue.Unknown);

            return obj;
        });
        var finalFunctionValue = EvaluateFunctionWithComputedTerms(functionType, innerFunctionArguments);//at the end =>functionType:Max and the terms are : 3, 4 
        return finalFunctionValue;

    }

    static double EvaluateFunctionWithComputedTerms(FunctionAggregateTypes functionType, IEnumerable<ZetTerm> terms)
    {

        switch (functionType)
        {
            case FunctionAggregateTypes.iMin:
                var min = terms.Min(item => item?.Object280?.Obj);
                return Convert.ToDouble(min);

            case FunctionAggregateTypes.iMax:
                var max = terms.Max(item => item?.Object280?.Obj);
                return Convert.ToDouble(max);
            case FunctionAggregateTypes.iSum:
                //there is only ONE terms inside a isum/icount so no worries
                return Convert.ToDouble(terms.FirstOrDefault()?.Object280?.sumValue ?? 0);
            case FunctionAggregateTypes.iCount:
                return Convert.ToDouble(terms.FirstOrDefault()?.Object280?.countValue ?? 0);
            default: return 0;


        }

    }

    static FunctionAggregateTypes ToFunctionType(string functionType) =>
        functionType switch
        {
            "imin" => FunctionAggregateTypes.iMin,
            "imax" => FunctionAggregateTypes.iMax,
            "max" => FunctionAggregateTypes.Max,
            "isum" => FunctionAggregateTypes.iSum,
            "icount" => FunctionAggregateTypes.iCount,
            "plain" => FunctionAggregateTypes.Plain,
            _ => throw new ArgumentException("Invalid function type"),
        };

    public static DoubleObject EvaluateSimpleArithmetic(string symbolFormula, Dictionary<string, ZetTerm> terms)
    {
        var rgxTerm = new Regex(@"([XA]\d\d)");
        var matchTersm = rgxTerm.Match(symbolFormula);
        //what if a term is null ???
        Dictionary<string, DoubleObject> doubleObjects = terms.ToDictionary(item => item.Key, item => stringToDouble(item.Value?.ObjectValue));

        //only check the terms of the formula 
        var formulaTerms = terms.Where(term => symbolFormula.Contains(term.Key));
        var isNull = formulaTerms.Any(ft => ft.Value?.Object280?.IsNullFact ?? false);
        var isNullOld = doubleObjects.Any(x => x.Value.IsNull);
        if (isNull)
        {
            return new DoubleObject(true, 0);
        }
        var result = Eval.Execute<double>(symbolFormula, doubleObjects);
        return new DoubleObject(false, result);


        static DoubleObject stringToDouble(object? obj)
        {
            if (obj is null)
            {
                return new DoubleObject(true, 0);
            }

            try
            {
                return new DoubleObject(false, Convert.ToDouble(obj.ToString()));
            }
            catch (FormatException)
            {
                return new DoubleObject(true, 0.0);
            }

        }

    }

    public static KleeneValue EvaluateSimpleString(string symbolFormula, Dictionary<string, ZetTerm> terms)
    {
        var rgxTerm = new Regex(@"([XA]\d\d)");
        var matchTersm = rgxTerm.Match(symbolFormula);

        var rgxEnum = new Regex(@"\[(.*?)\]");
        string cleanFormula = rgxEnum.Replace(symbolFormula, match => $"\"{match.Groups[1].Value}\"");
        Dictionary<string, object> plainObjects = terms.ToDictionary(item => item.Key, item => item.Value?.Object280?.Obj ?? "");


        var result = Eval.Execute<bool>(cleanFormula, plainObjects);
        return result ? KleeneValue.True : KleeneValue.False;
        //return result;


    }


    public static (string symbolFormula, List<ZetTerm> FunctionTerms) CreateZetObjectsFromTextFormula(string text, Regex regex, string letter)
    {
        //public record FunctionObject(string Letter, FunctionAggregateTypes FunctionType, string FullText, string FunctionArgument, double Value);
        var matchFunctions = regex.Matches(text);
        //var nestedFunctions = matchFunctions.Select((match, i) => new FunctionObject($"{letter}{i:D2}", ToFunctionType(match.Groups[1].Value), match.Value, match.Groups[2].Value, 0)).ToList();
        var nestedFunctions = matchFunctions.Select((match, i) => new ZetTerm($"{letter}{i:D2}", match.Value, match.Groups[2].Value, "N", 0, false, ToFunctionType(match.Groups[1].Value), null, null, KleeneValue.Unknown)).ToList();
        var contentFormulaWithSymbols = nestedFunctions.Aggregate(text, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.Formula);
            string replacedString = currentText[..index] + " " + val.Letter + " " + currentText[(index + val.Formula.Length)..];
            return replacedString;
        });

        return (contentFormulaWithSymbols, nestedFunctions);
    }


    public static (LogicalOperators logicalOperator, string left, string Right) SplitAndOrExpression(string text)
    {
        //public record FunctionObject(string Letter, FunctionAggregateTypes FunctionType, string FullText, string FunctionArgument, double Value);
        //RgxAggregateFunctions
        var rgx = new Regex(@"(imin|imax|max|isum|icount)?\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        //var rgx = RgxAggregateFunctionSingle(); ////"^(imin|imax|max|isum)\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)$")        
        var matchFunctions = rgx.Matches(text);
        var nestedFunctions = matchFunctions.Select((match, i) => ($"Z{i:D2}", match.Value)).ToList();
        var contentFormulaWithSymbols = nestedFunctions.Aggregate(text, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.Value);
            string replacedString = currentText[..index] + "" + val.Item1 + "" + currentText[(index + val.Value.Length)..];
            return replacedString;
        });


        var op = "and";
        var isAnd = contentFormulaWithSymbols.Contains(op);
        if (isAnd)
        {
            string newLeft = RestoreLeftSide(nestedFunctions, contentFormulaWithSymbols, op).Trim();
            string newRight = RestoreRightSide(nestedFunctions, contentFormulaWithSymbols, op).Trim();
            return (LogicalOperators.IsAnd, newLeft, newRight);
        }

        op = "or";
        var isOr = contentFormulaWithSymbols.Contains(op);
        if (isOr)
        {
            string newLeft = RestoreLeftSide(nestedFunctions, contentFormulaWithSymbols, op).Trim();
            string newRight = RestoreRightSide(nestedFunctions, contentFormulaWithSymbols, op).Trim();
            return (LogicalOperators.IsOR, newLeft, newRight);
        }




        return (LogicalOperators.None, text, "");
        //***************************************
        static string RestoreLeftSide(List<(string, string Value)> nestedFunctions, string contentFormulaWithSymbols, string logicalOperator)
        {
            var indexAnd = contentFormulaWithSymbols.IndexOf(logicalOperator);
            var leftSide = contentFormulaWithSymbols[..indexAnd];
            var newLeft = nestedFunctions.Aggregate(leftSide, (currentText, val) =>
            {
                int index = currentText.IndexOf(val.Item1);

                string replacedString = index > -1
                ? currentText[..index] + val.Value + currentText[(index + val.Item1.Length)..]
                : currentText;

                return replacedString;
            });
            return newLeft;
        }

        static string RestoreRightSide(List<(string, string Value)> nestedFunctions, string contentFormulaWithSymbols, string logicalOperator)
        {
            var indexAnd = contentFormulaWithSymbols.IndexOf(logicalOperator);
            var rightSide = contentFormulaWithSymbols[(indexAnd + logicalOperator.Length)..];
            var newRight = nestedFunctions.Aggregate(rightSide, (currentText, val) =>
            {
                int index = currentText.IndexOf(val.Item1);

                string replacedString = index > -1
                ? currentText[..index] + val.Value + currentText[(index + val.Item1.Length)..]
                : currentText;

                return replacedString;
            });
            return newRight;
        }
    }





    [GeneratedRegex(@"^(imin|imax|max|isum|icount)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$")]
    public static partial Regex RgxAggregateFunctionSingle();

    [GeneratedRegex(@"(imin|imax|max|isum|icount)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)")]
    public static partial Regex RgxAggregateFunctions();

    [GeneratedRegex(@"^\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$")]
    public static partial Regex RgxOuterParenthesis();


}

