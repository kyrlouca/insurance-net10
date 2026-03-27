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

public enum KleeneValue
{
    True,
    False,
    Unknown
}

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
        //4. fuck99 , I changed this on 27/3/20206 so that if any single term has a null fact the result is unkown.
        
        if (terms.Any(tr => tr.Value.fact is null && tr.Value.IsNullFact ))
        {
            var ret = KleeneValue.Unknown;
            //return ret;
        }


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
            /////////////////////


            switch (fn)
            {
                case "not":
                    var resNot = EvaluateBooleanExpression(ruleId, fnArgument, terms);
                    return resNot switch
                    {
                        KleeneValue.Unknown => KleeneValue.Unknown,
                        KleeneValue.False => KleeneValue.True,
                        _ => KleeneValue.False
                    };
                    
                case "isNull":
                case "isnull":
                    //if value is a function, then call evaluatefunction to find the value of the function and then call IsNull
                    var rgxDim = new Regex(@"dim\((.*?)\)", RegexOptions.Compiled);
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
                var resStr = EvaluateSimpleStringExpression(formula, terms);
                return resStr;
            }


            var resLeftMin = EvaluateArithmeticExpressionRecursively(left, terms, IntervalType.Min);
            var resLeftMax = EvaluateArithmeticExpressionRecursively(left, terms, IntervalType.Max);
            var resRightMin = EvaluateArithmeticExpressionRecursively(right, terms, IntervalType.Min);
            var resRightMax = EvaluateArithmeticExpressionRecursively(right, terms, IntervalType.Max);


            if (resLeftMin.IsNull || resRightMin.IsNull)
            {
                var resLeftBase = EvaluateArithmeticExpressionRecursively(left, terms, IntervalType.None);
                var resRightBase = EvaluateArithmeticExpressionRecursively(right, terms, IntervalType.None);
                GeneralEvaluator.expressionInfo = ExpressionInfo.Create(op, false, resLeftBase, resLeftMin, resLeftMax, resRightBase, resRightMin, resRightMax);

                return KleeneValue.Unknown;
            }

            if (resLeftMin.Value is double || resRightMin.Value is double)
            {
                var intervalResult = IntervalFunctionsNew.IsIntervalExpressionValid(op, resLeftMin, resLeftMax, resRightMin, resRightMax);

                if (!intervalResult)
                {
                    var resLeftBase = EvaluateArithmeticExpressionRecursively(left, terms, IntervalType.None);
                    var resRightBase = EvaluateArithmeticExpressionRecursively(right, terms, IntervalType.None);
                    GeneralEvaluator.expressionInfo = ExpressionInfo.Create(op, true, resLeftBase, resLeftMin, resLeftMax, resRightBase, resRightMin, resRightMax);
                }
                return intervalResult ? KleeneValue.True : KleeneValue.False;
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

        //*** Just a Term X01 (could be double,date, string, or null. Dates are converted to double)
        var rgxTerm = new Regex(@"^X\d{2}$", RegexOptions.Compiled);
        var matchTerm = rgxTerm.Match(generalExpression);
        if (matchTerm.Success)
        {
            var resTermD = ToOptionalObject(matchTerm.Value, terms, intervalType);
            return resTermD;
        }


        //*** A single function imin(imax(3,X01),X02
        var matchSingleFunction = rgxToFindSingleFunction.Match(generalExpression);
        if (matchSingleFunction.Success)
        {
            var res = EvaluateFunction(generalExpression, terms, intervalType);
            return res;
        }

        //nothing from the above, it is an expression which must be evaluated recursively
        //Split the expression         

        var resM = SplitArithmeticExpression(generalExpression);

        if (resM.arithmeticOperator == ArithmeticOperators.None)
        {

            if (generalExpression is null)
            {
                return new OptionalObject(true, null);
            }

            var rgxBrackets = new Regex(@"\[(.*)\]", RegexOptions.Compiled);
            var matchBrackets = rgxBrackets.Match(generalExpression);
            if (matchBrackets.Success)
            {
                var operandString = (string)matchBrackets.Value;
                return new OptionalObject(false, operandString);
            }

            //date was converted to double when creating objecttERMS            
            try
            {
                CultureInfo usCulture = new CultureInfo("en-US");
                CultureInfo.CurrentCulture = usCulture;

                var operand = Convert.ToDouble(generalExpression, usCulture);
                return new OptionalObject(false, operand);
            }
            catch
            {
                throw new Exception($"expression:{generalExpression} Text is not a Number");
            }
        }


        //*** Multiply , add, subtract
        if (resM.arithmeticOperator != ArithmeticOperators.None)
        {

            var leftRes = new OptionalObject(true, 0.0);
            var rightRes = new OptionalObject(true, 0.0);

            if (resM.arithmeticOperator != ArithmeticOperators.UnaryMinus)
            {
                //unary minus can only have  right expression 
                var matchLeftFunction = rgxToFindSingleFunction.Match(resM.left);
                leftRes = matchLeftFunction.Success
                ? EvaluateFunction(resM.left, terms, intervalType)
                : EvaluateArithmeticExpressionRecursively(resM.left, terms, intervalType);
            }


            var matchRightFunction = rgxToFindSingleFunction.Match(resM.right);
            rightRes = matchRightFunction.Success
                ? EvaluateFunction(resM.right, terms, intervalType)
                : EvaluateArithmeticExpressionRecursively(resM.right, terms, intervalType);

            var theResult = resM.arithmeticOperator switch
            {
                ArithmeticOperators.Multiply => new OptionalObject(false, ((double)(leftRes?.Value ?? 0.0)) * ((double)(rightRes?.Value ?? 0.0))),
                ArithmeticOperators.Plus => new OptionalObject(false, (double)(leftRes?.Value ?? 0.0) + (double)(rightRes?.Value ?? 0.0)),
                ArithmeticOperators.Minus => new OptionalObject(false, (double)(leftRes?.Value ?? 0.0) - (double)(rightRes?.Value ?? 0.0)),
                ArithmeticOperators.UnaryMinus => new OptionalObject(false, -(double)(rightRes?.Value ?? 0.0)),
                _ => new OptionalObject(true, 0.0)
            };

            return theResult;
        }

        //*** Should not reach up to here
        throw new Exception($"Expression:{generalExpression}. Can not decifer Arithmetic Expression");
        //return new DoubleObject(true, 0);              
    }

    public static OptionalObject EvaluateFunction(string functionText, Dictionary<string, ObjectTerm280> terms, IntervalType intervalType)
    {
        //it is not recursive by itself but it uses EvaluateArithmeticRecursively which is recursive
        //EXAMPLE To Test   : imax(imin(3, 7) , 4) 
        //EXAMPLE withREAL  : imin(imax(X01, 0) * 0.25, X02)
        //Takes the inside content of the function and
        //  -- create an array with the result of each inner term
        //The final result is computed by evaluating the function using the list of computed inner terms


        functionText = FormulaCharacters.RemoveWeirdFormulaCharacters(functionText);//max(x01,0) i => remove the i. the function has already been marked as interval
        var rgxSingleFunction = RgxSingleFunction(); ////"^(imin|imax|max|isum)\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)$")        
        functionText = functionText.Trim();

        //we have a SINGLE function 
        //it is difficult to split with commas because functions have commas inside
        // the function CONTENT is a list of expressions separated by comma =>imax(X01, 0) * 0.25, X02 and => two expressions: imax(X01, 0) * 0.25  AND   X02
        // 1. Split the terms inside the function 
        // --the proper solution would be to split each expression, call the arithmeticExpressionEvaluator for each BUT due to commas inside functions, I cannot do the split
        // *** So I do this  trick. Replace the functions inside the function with terms ("F") to do the split and then back to their value        
        // 2.Evaluate each function term
        // 3.Finally, call  EvaluateFunctionWithComputedTerms since all the function terms were computed


        var matchFn = rgxSingleFunction.Match(functionText);
        if (!matchFn.Success) throw new ArgumentException($"Invalid function:{functionText}");

        var functionContent = matchFn.Groups[2].Value;
        var functionType = ToFunctionType(matchFn.Groups[1].Value);

        if (functionType == FunctionAggregateTypes.Count)
        {
            var fterms = terms.Where(trm => functionText.Contains(trm.Key)).ToDictionary(tm => tm.Key, tm => tm.Value);
            //todo sum
            var resSumOrCount = EvaluateCount(functionType, fterms);
            return resSumOrCount;
        }

        var rgxFunctions2 = RgxAggregateFunctions();////"(imin|imax|max|isum|exp)\\s*\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)"
        var (innerSymbolFormula, innerFunctionTerms) = ToFunctionObjectsFromTextFormula(functionContent, rgxFunctions2, "F");
        var innerArguments = innerSymbolFormula.Split(",", StringSplitOptions.RemoveEmptyEntries);

        var count = innerArguments.Length;
        IEnumerable<OptionalObject> innerFunctionArguments = innerArguments.Select(argSplit =>
        {
            //here, we are processing each inner term (which are expressions) of the function. For example , x2+3, or even max(x3)+3
            //When all the inner terms are evaluated, we will evaluate the actual function

            foreach (var ft in innerFunctionTerms)
            {
                //replace each Letter "F"  with the actual text. For example, F01=> max(x1,3)
                argSplit = argSplit.Replace(ft.Letter, ft.FullText);
            }
            var res = EvaluateArithmeticExpressionRecursively(argSplit, terms, intervalType);
            return res;
        });
        var finalFunctionValue = EvaluateFunctionWithComputedTerms(functionType, innerFunctionArguments, intervalType);//at the end =>functionType:Max and the terms are : 3, 4 
        return finalFunctionValue;
    }

    static OptionalObject EvaluateCount(FunctionAggregateTypes functionType, Dictionary<string, ObjectTerm280> terms)
    {

        switch (functionType)
        {

            case FunctionAggregateTypes.Count:
                var countTerm = terms.FirstOrDefault();
                var resCount = countTerm.Key is null
                    ? new OptionalObject(true, 0)
                    : new OptionalObject(false, Convert.ToDouble(countTerm.Value.countValue));
                return resCount;
            default: return new OptionalObject(true, 0);

        }

    }

    static OptionalObject EvaluateFunctionWithComputedTerms(FunctionAggregateTypes functionType, IEnumerable<OptionalObject> terms, IntervalType intervalType)
    {

        var termsWithoutNull = terms.Select(term => term.IsNull ? term with { IsNull = false, Value = 0.0 } : term);
        switch (functionType)
        {
            case FunctionAggregateTypes.iSum:
                var sum = terms
                    .Where(term => !term.IsNull)
                    .Aggregate(0.0, (sm, val) => sm + (double)(val?.Value ?? 0));
                return new OptionalObject(false, sum);
            case FunctionAggregateTypes.iMin:
                var resMin = new OptionalObject(false, termsWithoutNull.Min(item => item.Value));
                return resMin;
            case FunctionAggregateTypes.iMax:
                var resMax = new OptionalObject(false, termsWithoutNull.Max(item => item.Value));
                return resMax;
            case FunctionAggregateTypes.Exp:
                try
                {
                    double value = Convert.ToDouble(termsWithoutNull?.FirstOrDefault()?.Value ?? 0.0);
                    return new OptionalObject(false, Math.Exp(value));
                }
                catch
                {
                    return new OptionalObject(true, 0.0);
                }


            case FunctionAggregateTypes.Abs:

                double absValue = Math.Abs(Convert.ToDouble(termsWithoutNull?.FirstOrDefault()?.Value ?? 0.0));
                return new OptionalObject(false, absValue);

            default: return new OptionalObject(true, 0.0);


        }

    }

    public static KleeneValue EvaluateSimpleStringExpression(string symbolFormula, Dictionary<string, ObjectTerm280> terms)
    {
        var rgxTerm = new Regex(@"([XA]\d\d)", RegexOptions.Compiled);
        var matchTersm = rgxTerm.Match(symbolFormula);
        
        var isAnyTermNull = terms.Any(ft => ft.Value.Obj is null);

        if (isAnyTermNull)
        {
            return KleeneValue.Unknown;
        }

        var rgxEnum = new Regex(@"\[(.*?)\]", RegexOptions.Compiled);
        string cleanFormula = rgxEnum.Replace(symbolFormula, match => $"\"{match.Groups[1].Value}\"");
        Dictionary<string, object> plainObjects = terms.ToDictionary(item => item.Key, item => item.Value?.Obj ?? "");


        var result = Eval.Execute<bool>(cleanFormula, plainObjects);
        return result ? KleeneValue.True : KleeneValue.False;

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


    ////////////////////////////////////////***********    

    public static (ArithmeticOperators arithmeticOperator, string left, string right) SplitArithmeticExpression(string text)
    {
        //We have "*", "+", "-" inside parenthesis or other functions. We need to find the first valid "*","+","-"
        //Precedence works in reverse: first split (-,+) then * and then unary -
        //Then, split the expression to left and right and return.
        //If no logical operator is found=> put everything in the left
        //The trick is to replace the parenthesis with letters and then find the split 
        //"3 * -2+4x";=> "+", "3 * -2", "4x";
        //"3 * -2";=> "*", "3", "-2";


        var rgx = new Regex(@"\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        var matchParenthesis = rgx.Matches(text);
        var nestedFunctions = matchParenthesis.Select((match, i) => ($"Z{i:D2}", match.Value)).ToList();
        var contentFormulaWithSymbols = nestedFunctions.Aggregate(text, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.Value);
            string replacedString = currentText[..index] + "" + val.Item1 + "" + currentText[(index + val.Value.Length)..];
            return replacedString;
        });


        var operators = OperatorManager.OperatorsInOrderedList(contentFormulaWithSymbols); ;

        var arOperator = operators.LastOrDefault();
        var xLeft = "";
        var xRight = "";
        if (arOperator is not null)
        {
            xLeft = RestoreLeftSideNew(nestedFunctions, contentFormulaWithSymbols, arOperator.position).Trim();
            xRight = RestoreRightSideNew(nestedFunctions, contentFormulaWithSymbols, arOperator.position).Trim();
            return (arOperator.arithmeticOperator, xLeft, xRight);
        }
        else
        {
            return (ArithmeticOperators.None, text, "");
        }



        static string RestoreLeftSideNew(List<(string, string Value)> nestedFunctions, string contentFormulaWithSymbols, int position)
        {

            var leftSide = contentFormulaWithSymbols[..position];
            var newLeft = nestedFunctions.Aggregate(leftSide, (currentText, val) =>
            {
                int index = currentText.IndexOf(val.Item1);

                string replacedString = index > -1
                ? currentText[..index] + val.Value + currentText[(index + val.Item1.Length)..]
                : currentText;

                return replacedString;
            });
            return newLeft.Trim();
        }

        static string RestoreRightSideNew(List<(string, string Value)> nestedFunctions, string contentFormulaWithSymbols, int position)
        {

            var rightSide = contentFormulaWithSymbols[(position + 1)..];
            var newRight = nestedFunctions.Aggregate(rightSide, (currentText, val) =>
            {
                int index = currentText.IndexOf(val.Item1);

                string replacedString = index > -1
                ? currentText[..index] + val.Value + currentText[(index + val.Item1.Length)..]
                : currentText;

                return replacedString;
            });
            return newRight.Trim();
        }



    }

    public static (string symbolFormula, List<FunctionObject> FunctionTerms) ToFunctionObjectsFromTextFormula(string text, Regex regex, string letter)
    {
        //max(min(X01,3,X03), X2,0),X2 => F01,X2
        //X01+X02=> X01+X02
        var matchFunctions = regex.Matches(text);
        if (matchFunctions.Count == 0)
        {
            return (text, new List<FunctionObject>());
        }
        var nestedFunctions = matchFunctions.Select((match, i) => new FunctionObject($"{letter}{i:D2}", ToFunctionType(match.Groups[1].Value), match.Value, match.Groups[2].Value, 0)).ToList();
        var contentFormulaWithSymbols = nestedFunctions.Aggregate(text, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.FullText);
            string replacedString = currentText[..index] + " " + val.Letter + " " + currentText[(index + val.FullText.Length)..];
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

    [GeneratedRegex(@"^(imin|imax|max|isum|count|exp|iabs)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$")]
    public static partial Regex RgxSingleFunction();


    [GeneratedRegex(@"(imin|imax|max|isum|count|exp|iabs)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)")]
    public static partial Regex RgxAggregateFunctions();

    [GeneratedRegex(@"^\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$")]
    public static partial Regex RgxOuterParenthesis();


    [GeneratedRegex(@"^(isNull|isnull|matches|not|true|false|\s|^)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$")]
    public static partial Regex RgxBooleanFunction();
}

