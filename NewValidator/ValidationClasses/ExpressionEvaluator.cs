using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileSystemGlobbing;
using Shared.DataModels;
using Shared.SpecialRoutines;
using Validator.ValidationClasses;
using Validator.Common.ParsingRoutines;

using Syncfusion.XlsIO.Implementation.Collections.Grouping;
using Syncfusion.XlsIO.Implementation.PivotAnalysis;
using Syncfusion.XlsIO.Parser.Biff_Records;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using System.Transactions;
using Z.Expressions;
using System.Linq;
using System.Diagnostics.Tracing;

namespace NewValidator.ValidationClasses;

public enum FunctionTypes { iMin, iMax, iSum, Max };
public record FunctionObject(string Letter, FunctionTypes FunctionType, string FullText, string FunctionArgument, double Value);

public enum IntervalType { None, Min, Max }
public record OptionalObject(bool IsNull, object? Value);

public record BooleanObject(bool IsNull, bool Value);
public enum FunctionAggregateTypes { iMin, iMax, iSum, Count, Max, Plain, Exp, Abs };
public record FunctionObject(string Letter, FunctionAggregateTypes FunctionType, string FullText, string FunctionArgument, double Value);

public record ObjectTerm280(string DataType, int Decimals, bool IsTolerant, Object? Obj, double sumValue, int countValue, TemplateSheetFact? fact, bool IsNullFact, string Filter);

public partial class GeneralEvaluator
{

    //ExpressionInfoType is only used to display the final expression in the errorRul presented to the user    
    public static ExpressionInfoWithIntervalsType? expressionInfo;
    public enum LogicalOperators { None, IsAnd, IsOR };
        
    public static bool ValidateRule(RuleStructure280 ruleStructure280)
    {
        //{t: S.23.01.02.02, r: R0700, c: C0060, z: Z0001, dv: 0, seq: False, id: v0, f: solvency, fv: solvency2} i= isum({t: S.23.01.02.02, r: R0710; R0720; R0730; R0740; R0760, c: C0060, z: Z0001, dv: emptySequence(), seq: True, id: v1, f: solvency, fv: solvency2})
        //objectTerm: an object which gets information from the fact and the the RuleTerm ({t:2000} such as sequence 
        var ifComponent = ruleStructure280.IfComponent;         
        var isValidIf = ExpressionEvaluator.EvaluateGeneralBooleanExpression(ifComponent.SymbolExpression, ifComponent.ObjectTerms);
        return isValidIf;

        GeneralEvaluator.expressionInfo = null;
        var ifResult = GeneralEvaluator.EvaluateBooleanExpression(ruleStructure280.RuleId, ruleStructure280.IfComponent.SymbolExpression, ruleStructure280.IfComponent.ObjectTerms);
        ruleStructure280.IfComponent.ExpressionInfo = GeneralEvaluator.expressionInfo;

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
                GeneralEvaluator.expressionInfo = null;
                var thenResult = GeneralEvaluator.EvaluateBooleanExpression(ruleStructure280.RuleId, ruleStructure280.ThenComponent.SymbolExpression, ruleStructure280.ThenComponent.ObjectTerms);
                ruleStructure280.ThenComponent.ExpressionInfo = GeneralEvaluator.expressionInfo;
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
                GeneralEvaluator.expressionInfo = null;
                var thenRes = GeneralEvaluator.EvaluateBooleanExpression(ruleStructure280.RuleId, ruleStructure280.ThenComponent.SymbolExpression, ruleStructure280.ThenComponent.ObjectTerms);
                ruleStructure280.ThenComponent.ExpressionInfo = GeneralEvaluator.expressionInfo;
                return ToBoolean(thenRes); // if is false and there is no else      
            }
            if (ifResult == KleeneValue.False)
            {
                GeneralEvaluator.expressionInfo = null;
                var elseRes = GeneralEvaluator.EvaluateBooleanExpression(ruleStructure280.RuleId, ruleStructure280.ElseComponent.SymbolExpression, ruleStructure280.ElseComponent.ObjectTerms);
                ruleStructure280.ElseComponent.ExpressionInfo = GeneralEvaluator.expressionInfo;
                return ToBoolean(elseRes); // if is false and there is no else
            }
            else //(ifResult == KleeneValue.Unknown)
            {
                return true; //todo need to check this
            }
        }
        
    }
    
    public static KleeneValue EvaluateBooleanExpression(int ruleId, string formula, Dictionary<string, ObjectTerm280> terms)
    {
        //Recursion to remove outer parenthesis, real evaluation of terms with only a function, evaluation and recurse for  "and", "or", and finally real evaluation of the term
        //1. outer parenthesis
        //2. single function (boolean)
        //---- if there is "and","or", nothing in this order => evaluate the two terms around "and" or "or" or "nothing"
        //3. arithmetic

        if (string.IsNullOrEmpty(formula))
        {
            throw new ArgumentNullException("Hey1: Formula is Null or Empty");
        }

        var rgxOuter = RgxOuterParenthesis();
        var matchOuter = rgxOuter.Match(formula);
        if (matchOuter.Success)
        {
            var insideParen = matchOuter.Groups[1].Value.Trim();
            var resInside = EvaluateBooleanExpression(ruleId, insideParen, terms);
            return resInside;
        }

        //************************** Single Function********************************************************
        //
        //2. function (evaluate function or remove parenthesis and recurse if outer parenthesis without function)               
        //var rgxFnOld = new Regex(@"^(isNull|isnull|matches|not|true|false|\s|^)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$", RegexOptions.Compiled);
        var rgxFn = RgxBooleanFunction();

        var match = rgxFn.Match(formula);
        if (match.Success)
        {
            //( ab and matches(cd)) => evaluate ab and matches(cd)
            var fn = match.Groups[1].Value;
            var fnArgument = match.Groups[2].Value;

            switch (fn)
            {
                case "not":
                    var resNot = EvaluateBooleanExpression(ruleId, fnArgument, terms);
                    return resNot == KleeneValue.Unknown ? KleeneValue.Unknown
                        : resNot == KleeneValue.False ? KleeneValue.True
                        : KleeneValue.False;
                case "isNull":
                case "isnull":
                    //if value is a function, then call evaluatefunction to find the value of the function and then call IsNull
                    var rgxDim = new Regex(@"dim\((.*?)\)",RegexOptions.Compiled);
                    var matchDim = rgxDim.Match(fnArgument);
                    if (matchDim.Success)
                    {
                        //isNull(dim(X00,[s2c_dim:NF]))
                        //isNull(dim({t: S.06.02.07.01, c: C0060, z: Z0001, seq: False, id: v0, f: solvency, fv: solvency2},[s2c_dim:NF])
                        var dimValue = ValidationFunctions.ExtractDimValueFormFact(matchDim.Value, terms);
                        return string.IsNullOrEmpty(dimValue) ? KleeneValue.True : KleeneValue.False;
                    }
                    var resn = ValidationFunctions.ValidateIsNull(formula, terms);
                    return resn ? KleeneValue.True : KleeneValue.False;
                case "matches":
                    var resm = ValidationFunctions.ValidateMatch(formula, terms);
                    //return resm ? KleeneValue.True : KleeneValue.False;
                    return resm;
                //case "dim":
                //it is not a function returning boolean. It is only used inside isNull
                //    var resdim = ValidationFunctions.ExtractDimValueFormFact(formula,"", terms);
                //    return string.IsNullOrEmpty(resdim) ? KleeneValue.True : KleeneValue.False;
                case "true":
                    return KleeneValue.True;
                case "false":
                    return KleeneValue.False;
                default:
                    //this is executed when there are outer parenthesis around (a=b and (bc==dd) and b=c) => a=b and (bc==dd) and b=c
                    var resN = EvaluateBooleanExpression(ruleId, fnArgument, terms);
                    return resN;
            }
        }

        var res = SplitAndOrExpression(formula);
        
        if (res.logicalOperator == LogicalOperators.IsAnd)
        {
            var aAndRes = EvaluateBooleanExpression(ruleId, res.left, terms);
            var bAndRes = EvaluateBooleanExpression(ruleId, res.Right, terms);
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

            var orRes1 = EvaluateBooleanExpression(ruleId, res.left, terms);
            var orRes2 = EvaluateBooleanExpression(ruleId, res.Right, terms);

            if (orRes1 == KleeneValue.True || orRes2 == KleeneValue.True)
                return KleeneValue.True;
            else if (orRes1 == KleeneValue.False && orRes2 == KleeneValue.False)
                return KleeneValue.False;
            else
                return KleeneValue.Unknown;
        }

        //3. ****************************************arithmetic
        if (res.logicalOperator == LogicalOperators.None)
        {
            var regSplit = new Regex(@"(.+?)\s*(!=|>=|>|<=|<|==|=)\s*(.+)");
            var matchSplit = regSplit.Match(formula);
            if (!matchSplit.Success)
            {
                throw new ApplicationException($"Formula cannot be split using <,>,= :{formula}");
            }
            var left = matchSplit.Groups[1].Value;
            var op = matchSplit.Groups[2].Value;
            var right = matchSplit.Groups[3].Value;

            //Check if it is a string expression
            var isExpressionWithStrings = terms
                .Where(term => formula.Contains(term.Key))
                .Any(t => (t.Value?.DataType ?? "") == "S" || (t.Value?.DataType ?? "") == "E"); //check for "E"
            if (isExpressionWithStrings)
            {
                var resStr = EvaluateSimpleString(formula, terms);
                return resStr;                
            }

            
            var resLeftDbl = isExpressionWithStrings? 0: EvaluateArithmeticRecursively(left, terms);            
            var resRightDbl = isExpressionWithStrings? 0: EvaluateArithmeticRecursively(right, terms);
            


            var formulaLR = $"L0 {op} R0";
            var formulaLRObjects = new Dictionary<string, object>
            {
                { "L0",  resLeftDbl },
                { "R0",  resRightDbl }
            };
            var res = Eval.Execute<bool>(formulaLR, formulaLRObjects);
            return res;
        }

            //if (resLeftDbl.Value is string || resRightDbl.Value is string)
            if (resLeftMin.Value is string || resRightMin.Value is string)
            {
                var resString = resLeftMin.Value == resRightMin.Value;
                return resString ? KleeneValue.True : KleeneValue.False;
            }
        }

        return KleeneValue.True;
        
    }

    public static OptionalObject EvaluateArithmeticExpressionRecursively(string generalExpression, Dictionary<string, ObjectTerm280> terms, IntervalType intervalType)
    {
        //1. Outer parenthesis, 2. single term (x1), 3. number as a string,   4.Single function,  5. Plus or minus         
        //var regStartingFunction = RgxAggregateStartingFunction();
        var rgxToFindSingleFunction = RgxSingleFunction();

        generalExpression = generalExpression.Trim();
        generalExpression = FormulaCharacters.RemoveWeirdFormulaCharacters(generalExpression);

        //*** Remove Outer parenthesis
        var rgxOuter = RgxOuterParenthesis();
        var matchOuter = rgxOuter.Match(generalExpression);
        if (matchOuter.Success)
        {
            var res = EvaluateArithmeticExpressionRecursively(matchOuter.Groups[1].Value, terms, intervalType);
            return res;
        }

        //*** just a number
        var numberResult = ConvertToNumberUsingUSCulture(generalExpression);
        if (!numberResult.IsNull)
        {
            return numberResult;
        }

        //***we could also test for just date, for [xx] enum , but I did it otherwise and in the future I might  revisit this code

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

        var allTerms = terms.Select(trm => (trm.Key, trm.Value with { Decimals = 9 })).ToList();        
        allTerms.AddRange(newObjTerms);        
        var allObjectsDic = allTerms.ToDictionary(x => x.Key, x => x.Item2);
               

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
        //it is not recursive by itself but i
        luateArithmeticRecursively which is recursi
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
    
    public static (LogicalOperators logicalOperator, string left, string Right) SplitAndOrExpression(string text)
    {
        //We have "and", "or" inside parenthesis or other functions. We need to find the first valid "And" or the first valid "or"
        //Then, split the expression to left and right and return.
        //If no logical operator is found=> put everything in the left
        //The trick is to replace the parenthesis with letters and then find the split 
        //RgxAggregateFunctions
        var rgx = new Regex(@"(imin|imax|max|isum|count)?\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
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

                return replacedString;
            });
            return newLeft;
        }

        static object stringToDouble(object obj)
        {
            var type = obj.GetType();
            var result = type==typeof(string) ? Convert.ToDouble(obj) : obj;
            return result;
        }

    }

    public static bool EvaluateSimpleString(string symbolFormula, Dictionary<string, ObjectTerm280> terms)
    {
        var rgxTerm = new Regex(@"([XA]\d\d)");
        var matchTersm = rgxTerm.Match(symbolFormula);

        var rgxEnum = new Regex(@"\[(.*?)\]");        
        string cleanFormula = rgxEnum.Replace(symbolFormula, match => $"\"{match.Groups[1].Value}\"");        
        Dictionary<string, object> plainObjects = terms.ToDictionary(item => item.Key, item =>  item.Value.Obj );
               

        var result = Eval.Execute<bool>(cleanFormula, plainObjects);
        return result;        

                

                return replacedString;
            });
            return newRight.Trim();
        }





        static OperatorManager.OperatorRecord selectOperatorToProcessOld(string contentFormulaWithSymbols)
        {
            char[] minusPlusOps = { '+', '-' };
            char[] multiplyOps = { '*' };
            char[] allOps = minusPlusOps.Concat(multiplyOps).ToArray();

            var opPlusOrMinus = OperatorManager.PlaceOperatorsInList(contentFormulaWithSymbols, allOps, minusPlusOps);
            var opMulti = OperatorManager.PlaceOperatorsInList(contentFormulaWithSymbols, allOps, multiplyOps);

            var ordered = new List<OperatorManager.OperatorRecord>()
                    .Concat(opPlusOrMinus.Where(op => op.arithmeticOperator != ArithmeticOperators.UnaryMinus))
                    .Concat(opMulti)
                    .Concat(opPlusOrMinus.Where(op => op.arithmeticOperator == ArithmeticOperators.UnaryMinus))
                    .ToList();
            var opNew = ordered.FirstOrDefault();

            return opNew;
        }
    }

    public static (string symbolFormula, List<FunctionObject> FunctionTerms) ToFunctionObjectsFromTextFormula(string text, Regex regex, string letter)
    {
        var matchFunctions = regex.Matches(text);
        var nestedFunctions = matchFunctions.Select((match, i) => new FunctionObject($"{letter}{i:D2}", ToFunctionType(match.Groups[1].Value), match.Value, match.Groups[2].Value, 0)).ToList();
        var contentFormulaWithSymbols = nestedFunctions.Aggregate(text, (currentText, val) =>
        {
   
    ndex = currentText.IndexOf(val.FullText);
string replacedString = currentText[..index] + " " + val.Lette
    entText[(index + val.F
ngth)..] ;
return replacedString;
        });

return (contentFormulaWithSymbols, nestedFunctions);
    }


    static FunctionAggregateTypes ToFunctionType(string functionType) =>
        functionType switch
        {
            "imin" => FunctionAggregateTypes.iMin,
            "imax" => FunctionAggregateTypes.iMax,
            "max" => FunctionAggregateTypes.Max,
            "isum" => FunctionAggregateTypes.iSum,
            "count" => FunctionAggregateTypes.Count,
            "plain" => FunctionAggregateTypes.Plain,
            "exp" => FunctionAggregateTypes.Exp,
            "iabs" => FunctionAggregateTypes.Abs,
            _ => throw new ArgumentException("Invalid function type"),
        };

    private static OptionalObject ToOptionalObject(string letter, Dictionary<string, ObjectTerm280> terms, IntervalType intervalType)
    {
        //Here is the only Place we need the interval type
        //for numeric terms it will add or subtract the radius from the value of the object term
        var term = terms.FirstOrDefault(trm => trm.Key == letter).Value;

        var resTerm = term is null ? new OptionalObject(true, null)
            : term.Obj is null ? new OptionalObject(true, null)
            : term.DataType == "D" ? new OptionalObject(false, ((DateTime)term.Obj).ToOADate())
            //: new OptionalObject(false, Convert.ToDouble(term.Obj));
            : new OptionalObject(false, ConvertToDoubleUsingInterval(term, intervalType));


        return resTerm;

        static double ConvertToDoubleUsingInterval(ObjectTerm280 term, IntervalType intervalType)
        {
            if (term == null || term.Obj is null)
            {
                return 0;
            }
            var baseValue = Convert.ToDouble(term!.Obj);
            var val = intervalType switch
            {
                IntervalType.Min => baseValue - IntervalFunctions.Radius(term.Decimals),
                IntervalType.Max => baseValue + IntervalFunctions.Radius(term.Decimals),
                _ => baseValue,
            };
            return val;
        }

    }


    public static bool ToBoolean(KleeneValue kleeneVal) => kleeneVal == KleeneValue.True || kleeneVal == KleeneValue.Unknown;


    static OperatorManager.OperatorRecord selectOperatorToProcessNotUsed(string contentFormulaWithSymbols)
    {
        char[] minusPlusOps = { '+', '-' };
        char[] multiplyOps = { '*' };
        char[] allOps = minusPlusOps.Concat(multiplyOps).ToArray();


        var opMulti = OperatorManager.PlaceOperatorsInList(contentFormulaWithSymbols, allOps, multiplyOps);
        var opPlusOrMinus = OperatorManager.PlaceOperatorsInList(contentFormulaWithSymbols, allOps, minusPlusOps);

        var concatAndOrdered = new List<OperatorManager.OperatorRecord>()
                .Concat(opPlusOrMinus.Where(op => op.arithmeticOperator != ArithmeticOperators.UnaryMinus))
                .Concat(opMulti)
                .Concat(opPlusOrMinus.Where(op => op.arithmeticOperator == ArithmeticOperators.UnaryMinus))
                .ToList();
        var opNew = concatAndOrdered.LastOrDefault();
        return opNew;
    }


    static OptionalObject ConvertToNumberUsingUSCulture(string stringNumber)
    {
        // Specify US culture
        CultureInfo usCulture = CultureInfo.CreateSpecificCulture("en-US");
        var res = 0.0;
        try
        {
            res = Convert.ToDouble(stringNumber, usCulture);
            return new OptionalObject(false, res);
        }
        catch
        {
            return new OptionalObject(true, 0);
        }

    }




    [GeneratedRegex(@"^(imin|imax|max|isum|count|exp|iabs)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)")]
    public static partial Regex RgxStartingFunction();

    [GeneratedRegex(@"^(imin|imax|max|isum)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$")]
    private static partial Regex RgxAggregateFunctionSingle();

    [GeneratedRegex(@"(imin|imax|max|isum)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)")]
    private static partial Regex RgxAggregateFunctions();

    [GeneratedRegex(@"(imin|imax|max|isum|count|exp|iabs)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)")]
    public static partial Regex RgxAggregateFunctions();

    [GeneratedRegex(@"^\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$")]
    public static partial Regex RgxOuterParenthesis();


    [GeneratedRegex(@"^(isNull|isnull|matches|not|true|false|\s|^)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$")]
    public static partial Regex RgxBooleanFunction();
}

