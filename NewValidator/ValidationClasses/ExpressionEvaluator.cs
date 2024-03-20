using Microsoft.Data.SqlClient;
using Shared.DataModels;
using Syncfusion.XlsIO.Implementation.Collections.Grouping;
using Syncfusion.XlsIO.Implementation.PivotAnalysis;
using Syncfusion.XlsIO.Parser.Biff_Records;
using System.Data;
using System.Drawing;
using System.Globalization;
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

public record ObjectTerm280(string DataType, int Decimals, bool IsTolerant, Object? Obj, double sumValue, int countValue, TemplateSheetFact? fact, bool IsNullFact ,string Filter);
 
public partial class ExpressionEvaluator
{
    public enum LogicalOperators { None, IsAnd, IsOR };
    public enum ArithmeticOperators { Multiply, Plus, Minus, None };

    public static bool ValidateRule(RuleStructure280 ruleStructure280)
    {
        //{t: S.23.01.02.02, r: R0700, c: C0060, z: Z0001, dv: 0, seq: False, id: v0, f: solvency, fv: solvency2} i= isum({t: S.23.01.02.02, r: R0710; R0720; R0730; R0740; R0760, c: C0060, z: Z0001, dv: emptySequence(), seq: True, id: v1, f: solvency, fv: solvency2})
        //objectTerm: an object which gets information from the fact and the the RuleTerm ({t:2000} such as sequence 

        var ifResult = ExpressionEvaluator.EvaluateGeneralBooleanExpression(ruleStructure280.RuleId, ruleStructure280.IfComponent.SymbolExpression, ruleStructure280.IfComponent.ObjectTerms, "");
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
                var thenResult = ExpressionEvaluator.EvaluateGeneralBooleanExpression(ruleStructure280.RuleId, ruleStructure280.ThenComponent.SymbolExpression, ruleStructure280.ThenComponent.ObjectTerms, "");
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
                var thenRes = ExpressionEvaluator.EvaluateGeneralBooleanExpression(ruleStructure280.RuleId, ruleStructure280.ThenComponent.SymbolExpression, ruleStructure280.ThenComponent.ObjectTerms, "");
                return ToBoolean(thenRes); // if is false and there is no else      
            }
            if (ifResult == KleeneValue.False)
            {
                var elseRes = ExpressionEvaluator.EvaluateGeneralBooleanExpression(ruleStructure280.RuleId, ruleStructure280.ElseComponent.SymbolExpression, ruleStructure280.ElseComponent.ObjectTerms, "");

                return ToBoolean(elseRes); // if is false and there is no else
            }
            else //(ifResult == KleeneValue.Unknown)
            {
                return true; //todo need to check this
            }


        }
        bool ToBoolean(KleeneValue kleeneVal) => kleeneVal == KleeneValue.True || kleeneVal == KleeneValue.Unknown;
    }



    public static KleeneValue EvaluateGeneralBooleanExpression(int ruleId, string formula, Dictionary<string, ObjectTerm280> terms,string filterTerm)
    {

        //Recursion to remove outer parenthesis, real evaluation of terms with only a function, evaluation and recurse for  "and", "or", and finally real evaluation of the term
        //1. outer parenthesis
        //2. single function        
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
            var resInside = EvaluateGeneralBooleanExpression(ruleId, insideParen, terms, "");
            return resInside;

        }

        //************************** Single Function********************************************************
        //
        //2. function (evaluate function or remove parenthesis and recurse if outer parenthesis without function)
        //var rgxFn = new Regex(@"^(isNull|matches|not|dim|true|\s|^)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$");
        var rgxFn = new Regex(@"^(isNull|matches|not|true|\s|^)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)\s*$");
        var match = rgxFn.Match(formula);
        if (match.Success)
        {
            //( ab and matches(cd)) => evaluate ab and matches(cd)
            var fn = match.Groups[1].Value;
            var fnArgument = match.Groups[2].Value;

            switch (fn)
            {
                case "not":
                    var resNot = EvaluateGeneralBooleanExpression(ruleId, fnArgument, terms, filterTerm);                    
                    return resNot == KleeneValue.Unknown ? KleeneValue.Unknown
                        : resNot == KleeneValue.False ? KleeneValue.True
                        : KleeneValue.False;
                case "isNull":
                    //if value is a function, then call evaluatefunction to find the value of the function and then call IsNull
                    var rgxDim= new Regex(@"dim\((.*?)\)");
                    var matchDim= rgxDim.Match(fnArgument);                                        
                    if (matchDim.Success) 
                    {
                        //isNull(dim(X00,[s2c_dim:NF]))
                        //isNull(dim({t: S.06.02.07.01, c: C0060, z: Z0001, seq: False, id: v0, f: solvency, fv: solvency2},[s2c_dim:NF])
                        var dimValue = ValidationFunctions.ExtractDimValueFormFact(matchDim.Value, terms, filterTerm);
                        return string.IsNullOrEmpty(dimValue)?KleeneValue.True:KleeneValue.False ;
                    }
                    var resn = ValidationFunctions.ValidateIsNull(formula, terms);                                                                                           
                    return resn ? KleeneValue.True : KleeneValue.False;
                case "matches":
                    var resm = ValidationFunctions.ValidateMatch(formula, terms, filterTerm);
                    return resm ? KleeneValue.True : KleeneValue.False;
                //case "dim":
                //it is not a function returning boolean. It is only used inside isNull
                //    var resdim = ValidationFunctions.ExtractDimValueFormFact(formula,"", terms);
                //    return string.IsNullOrEmpty(resdim) ? KleeneValue.True : KleeneValue.False;
                case "true":
                    return KleeneValue.True;
                default:
                    //this is executed when there are outer parenthesis around (a=b and (bc==dd) and b=c) => a=b and (bc==dd) and b=c
                    var resN = EvaluateGeneralBooleanExpression(ruleId, fnArgument, terms, filterTerm);
                    return resN;
            }
        }

        var res = SplitAndOrExpression(formula);
        if (res.logicalOperator == LogicalOperators.IsAnd)
        {
            var aAndRes = EvaluateGeneralBooleanExpression(ruleId, res.left, terms, "");
            var bAndRes = EvaluateGeneralBooleanExpression(ruleId, res.Right, terms, "");
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

            var orRes1 = EvaluateGeneralBooleanExpression(ruleId, res.left, terms, "");
            var orRes2 = EvaluateGeneralBooleanExpression(ruleId, res.Right, terms, "");

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

            var isExpressionWithStrings = terms
                .Where(term => formula.Contains(term.Key))
                .Any(t => (t.Value?.DataType ?? "") == "S" || (t.Value?.DataType ?? "") == "E"); //check for "E"
            if (isExpressionWithStrings)
            {
                var resStr = EvaluateSimpleString(formula, terms);
                return resStr;
            }

            var leftDecimals = terms.ContainsKey(left) ? terms[left]?.Decimals ?? 0 : 0;
            var rightDecimals = terms.ContainsKey(right) ? terms[right]?.Decimals ?? 0 : 0;

            var resLeftDbl = EvaluateArithmeticRecursively(left, terms,"");
            var resRightDbl = EvaluateArithmeticRecursively(right, terms, "");

             if (resLeftDbl.IsNull || resRightDbl.IsNull)
            {
                return KleeneValue.Unknown;
            }

            var intervalResult = IntervalFunctions.IsIntervalExpressionValid(op, resLeftDbl.Value, leftDecimals, resRightDbl.Value, rightDecimals);

            return intervalResult ? KleeneValue.True : KleeneValue.False;
        }

        return KleeneValue.True;

    }

    public static DoubleObject EvaluateArithmeticRecursively(string arithmeticExpression, Dictionary<string, ObjectTerm280> terms,string thisTerm)
    {
        //1. Outer parenthesis, 2. single term (x1), 3. number as a string,   4.Single function,  5. Plus or minus 
        //var rgx = new Regex(@"(imin|imax|max|isum|icount)?\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        //var regStartingFunction = RgxAggregateStartingFunction();
        var regSingleFunction = RgxSingleFunction();

        arithmeticExpression = arithmeticExpression.Trim();
        arithmeticExpression = ReplaceIntervalCharacters(arithmeticExpression);

        //*** Remove Outer parenthesis
        var rgxOuter = RgxOuterParenthesis();
        var matchOuter = rgxOuter.Match(arithmeticExpression);
        if (matchOuter.Success)
        {
            var res = EvaluateArithmeticRecursively(matchOuter.Groups[1].Value, terms,"");
            return res;
        }

        //*** Just a Term X01
        var rgxTerm = new Regex(@"^X\d{2}$");
        var matchTerm = rgxTerm.Match(arithmeticExpression);
        if (matchTerm.Success)
        {
            var term = terms.FirstOrDefault(trm => trm.Key == matchTerm.Value);
            var resTerm = term.Value.IsNullFact ? new DoubleObject(true, 0) : new DoubleObject(false, Convert.ToDouble(term.Value.Obj));
            return resTerm;
        }

        //*** A single function imin(imax(3,X01),X02
        var matchSingleFunction = regSingleFunction.Match(arithmeticExpression);
        if (matchSingleFunction.Success)
        {
            var res = EvaluateFunction(arithmeticExpression, terms,"");
            return res;
        }
        
        //Try to split the expression         
        var resM = SplitArithmeticExpression(arithmeticExpression);
        //*** it is an expression and above we checked that there is no operator, it is not function , it is not a term=>Should be a number
        if (resM.arithmeticOperator == ArithmeticOperators.None)
        {
            //*** number as Text
            Double numberFromText;
            try
            {
                CultureInfo usCulture = new CultureInfo("en-US");
                CultureInfo.CurrentCulture = usCulture;

                numberFromText = Convert.ToDouble(arithmeticExpression, usCulture);
                return new DoubleObject(false, numberFromText);
            }
            catch
            {
                throw new Exception($"expression:{arithmeticExpression} Text is not a Number");
            }
        }


        //*** Multiply , add, subtract
        if (resM.arithmeticOperator != ArithmeticOperators.None)
        {
            var matchLeftFunction = regSingleFunction.Match(resM.left);
            var leftRes= matchLeftFunction.Success
                ? EvaluateFunction(resM.left, terms, "")
                : EvaluateArithmeticRecursively(resM.left, terms, "");

            var matchRightFunction = regSingleFunction.Match(resM.right);
            var rightRes = matchRightFunction.Success
                ? EvaluateFunction(resM.right, terms, "")
                : EvaluateArithmeticRecursively(resM.right, terms, "");
            
            if (leftRes.IsNull || rightRes.IsNull)
            {
                return new DoubleObject(true, 0);
            }
            switch (resM.arithmeticOperator)
            {
                case ArithmeticOperators.Multiply: return new DoubleObject(false, leftRes.Value * rightRes.Value);
                case ArithmeticOperators.Plus: return new DoubleObject(false, leftRes.Value + rightRes.Value);
                case ArithmeticOperators.Minus: return new DoubleObject(false, leftRes.Value - rightRes.Value);
                default: return new DoubleObject(true, 0);
            }
        }

        //*** Should not come here
        throw new Exception($"Expression:{arithmeticExpression}. Can not decifer Arithmetic Expression");        
        //return new DoubleObject(true, 0);              
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

    public static DoubleObject EvaluateFunction(string functionText, Dictionary<string, ObjectTerm280> terms,string filterTerm )
    {
        //it is not recursive by itself but it uses EvaluateArithmeticRecursively which is recursive
        //EXAMPLE To Test   : imax(imin(3, 7) , 4) 
        //EXAMPLE withREAL  : imin(imax(X01, 0) * 0.25, X02)
        //Takes the inside content of the function and
        //  -- create an array with the result of each inner term
        //The final result is computed by evaluating the function using the list of computed inner terms


        functionText = ReplaceIntervalCharacters(functionText);//max(x01,0) i => remove the i. the function has already been marked as interval
        var rgxSingleFunction = RgxSingleFunction(); ////"^(imin|imax|max|isum)\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)$")        
        functionText = functionText.Trim();

        //we have a SINGLE function 
        //it is difficult to split with commas because functions have commas inside
        // the function CONTENT is a list of expressions separated by comma =>imax(X01, 0) * 0.25, X02 and => two expressions: imax(X01, 0) * 0.25  AND   X02
        // 1. Split the terms inside the function 
        // --the proper solution would be to split each expression, call the arithmeticExpressionEvaluator for each BUT due to commas inside functions, I cannot do the split
        // *** So I do this  trick. Replace the functions inside the function with terms ("F") to do the split and then back to their value        
        // 2.Evalueate each function term
        // 3.Finally, call  EvaluateFunctionWithComputedTerms since all the function terms were computed


        var matchFn = rgxSingleFunction.Match(functionText);
        if (!matchFn.Success) throw new ArgumentException($"Invalid function:{functionText}");
        
        var functionContent = matchFn.Groups[2].Value;
        var functionType = ToFunctionType(matchFn.Groups[1].Value);

        if (functionType == FunctionAggregateTypes.iSum || functionType == FunctionAggregateTypes.iCount)
        {
            var fterms = terms.Where(trm => functionText.Contains(trm.Key)).ToDictionary(tm => tm.Key, tm => tm.Value);
            //todo sum
            var resSumOrCount = EvaluateSumOrCount(functionType, fterms);
            return resSumOrCount;
        }

        var rgxFunctions2 = RgxAggregateFunctions();////"(imin|imax|max|isum)\\s*\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)"
        var (innerSymbolFormula, innerFunctionTerms) = ToFunctionObjectsFromTextFormula(functionContent, rgxFunctions2, "F");
        var innerArguments = innerSymbolFormula.Split(",", StringSplitOptions.RemoveEmptyEntries);
                
        var count=innerArguments.Length;
        IEnumerable<DoubleObject> innerFunctionArguments = innerArguments.Select(argSplit =>
        {
            //here, we are processing each inner term (which are expressions) of the function. For example , x2+3, or even max(x3)+3
            //When all the inner terms are evaluated, we will evaluate the actual function
            
            foreach (var ft in innerFunctionTerms)
            {
                //replace each Letter "F"  with the actual text. For example, F01=> max(x1,3)
                argSplit = argSplit.Replace(ft.Letter, ft.FullText);
            }
            var res = EvaluateArithmeticRecursively(argSplit, terms, "");
            return res;
        });
        var finalFunctionValue = EvaluateFunctionWithComputedTerms(functionType, innerFunctionArguments);//at the end =>functionType:Max and the terms are : 3, 4 
        return finalFunctionValue;
    }



    static DoubleObject EvaluateSumOrCount(FunctionAggregateTypes functionType, Dictionary<string, ObjectTerm280> terms)
    {

        switch (functionType)
        {

            case FunctionAggregateTypes.iSum:
                //there is only ONE terms inside a isum/icount so no worries
                var sumTerm = terms.FirstOrDefault();                
                var resSum = sumTerm.Key is null
                    ? new DoubleObject(true, 0)
                    : new DoubleObject(false, Convert.ToDouble(sumTerm.Value.sumValue));
                return resSum;
            case FunctionAggregateTypes.iCount:
                var countTerm = terms.FirstOrDefault();
                var resCount = countTerm.Key is null
                    ? new DoubleObject(true, 0)
                    : new DoubleObject(false, Convert.ToDouble(countTerm.Value.countValue));
                return resCount;
            default: return new DoubleObject(true, 0);

        }

    }


    static DoubleObject EvaluateFunctionWithComputedTerms(FunctionAggregateTypes functionType, IEnumerable<DoubleObject> terms)
    {

        switch (functionType)
        {
            case FunctionAggregateTypes.iMin:
                //var min = terms.Min(item => item?.Obj);
                var hasNullTermMin = terms.Any(item => item.IsNull);
                var resMin = hasNullTermMin
                    ? new DoubleObject(true, 0)
                    : new DoubleObject(false, terms.Min(item => item.Value));
                return resMin;
            case FunctionAggregateTypes.iMax:
                //var max = terms.Max(item => item?.Obj);
                var hasNullTermMax = terms.Any(item => item.IsNull);
                var resMax = hasNullTermMax
                    ? new DoubleObject(true, 0)
                    : new DoubleObject(false, terms.Max(item => item.Value));
                return resMax;

            default: return new DoubleObject(true, 0);


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

    public static DoubleObject EvaluateSimpleArithmetic(string symbolFormula, Dictionary<string, ObjectTerm280> terms)
    {
        var rgxTerm = new Regex(@"([XA]\d\d)");
        var matchTersm = rgxTerm.Match(symbolFormula);

        //only check the terms of the formula 
        var formulaTerms = terms.Where(term => symbolFormula.Contains(term.Key));
        var isAnyTermNull = formulaTerms.Any(ft => ft.Value?.IsNullFact ?? false);

        if (isAnyTermNull)
        {
            return new DoubleObject(true, 0);
        }

        Dictionary<string, object> numericObjects = formulaTerms.ToDictionary(item => item.Key, item => FromStringToObj(item.Value?.Obj));
        var result = Eval.Execute<double>(symbolFormula, numericObjects);
        return new DoubleObject(false, result);



        static Object FromStringToObj(object? obj)
        {
            if (obj is null)
            {
                return 0;
            }
            if (obj is string)
            {
                try
                {
                    return Convert.ToDouble(obj.ToString());
                }
                catch (FormatException)
                {
                    throw new Exception($"obj:{obj.ToString} Cannot convert string object to double");
                }
            }
            return obj;


        }



    }

    public static KleeneValue EvaluateSimpleString(string symbolFormula, Dictionary<string, ObjectTerm280> terms)
    {
        var rgxTerm = new Regex(@"([XA]\d\d)");
        var matchTersm = rgxTerm.Match(symbolFormula);

        var rgxEnum = new Regex(@"\[(.*?)\]");
        string cleanFormula = rgxEnum.Replace(symbolFormula, match => $"\"{match.Groups[1].Value}\"");
        Dictionary<string, object> plainObjects = terms.ToDictionary(item => item.Key, item => item.Value?.Obj ?? "");


        var result = Eval.Execute<bool>(cleanFormula, plainObjects);
        return result ? KleeneValue.True : KleeneValue.False;
        //return result;


    }

    public static (string symbolFormula, List<FunctionObject> FunctionTerms) ToFunctionObjectsFromTextFormula(string text, Regex regex, string letter)
    {
        //max(min(X01,3,X03), X2,0),X2 => F01,X2
        //X01+X02=> X01+X02
        var matchFunctions = regex.Matches(text);
        if (matchFunctions.Count == 0)
        {
            return (text,new List<FunctionObject>());
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


    public static (LogicalOperators logicalOperator, string left, string Right) SplitAndOrExpression(string text)
    {
        //We have "and", "or" inside parenthesis or other functions. We need to find the first valid "And" or the first valid "or"
        //Then, split the expression to left and right and return.
        //If no logical operator is found=> put everything in the left
        //The trick is to replace the parenthesis with letters and then find the split 
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


    public static (ArithmeticOperators arithmeticOperator, string left, string right) SplitArithmeticExpression(string text)
    {
        //We have "*", "+", "-" inside parenthesis or other functions. We need to find the first valid "*","+","-"
        //Then, split the expression to left and right and return.
        //If no logical operator is found=> put everything in the left
        //The trick is to replace the parenthesis with letters and then find the split 

        //1. Outer parenthesis, 2. single term (x1), 3. number as a string,   4.Single function,  5. Plus or minus 
        //var rgx = new Regex(@"(imin|imax|max|isum|icount)?\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        //var rgx = RgxAggregateFunctionSingle(); ////"^(imin|imax|max|isum)\\(((?>\\((?<c>)|[^()]+|\\)(?<-c>))*(?(c)(?!)))\\)$")        
        var rgx = new Regex(@"\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)");
        var matchParenthesis = rgx.Matches(text);
        var nestedFunctions = matchParenthesis.Select((match, i) => ($"Z{i:D2}", match.Value)).ToList();
        var contentFormulaWithSymbols = nestedFunctions.Aggregate(text, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.Value);
            string replacedString = currentText[..index] + "" + val.Item1 + "" + currentText[(index + val.Value.Length)..];
            return replacedString;
        });

        

        char[] opeatorsToFind = { '+', '-' };
        var plusOrMinusPosition = contentFormulaWithSymbols.IndexOfAny(opeatorsToFind);

        var multiplyPosition = contentFormulaWithSymbols.IndexOf("*");

        //yes break for + or minus , so that * has precedence
        var operatorPosition = plusOrMinusPosition > -1
            ? plusOrMinusPosition
            : multiplyPosition > -1
            ? multiplyPosition
            : -1;

        if (operatorPosition == -1)
        {
            return (ArithmeticOperators.None, text, "");
        }

        var op = contentFormulaWithSymbols[operatorPosition].ToString();

        if (op == "*")
        {
            string newLeft = RestoreLeftSide(nestedFunctions, contentFormulaWithSymbols, op).Trim();
            string newRight = RestoreRightSide(nestedFunctions, contentFormulaWithSymbols, op).Trim();
            return (ArithmeticOperators.Multiply, newLeft, newRight);
        }

        if (op == "+")
        {
            string newLeft = RestoreLeftSide(nestedFunctions, contentFormulaWithSymbols, op).Trim();
            string newRight = RestoreRightSide(nestedFunctions, contentFormulaWithSymbols, op).Trim();
            return (ArithmeticOperators.Plus, newLeft, newRight);
        }

        if (op == "-")
        {
            string newLeft = RestoreLeftSide(nestedFunctions, contentFormulaWithSymbols, op).Trim();
            string newRight = RestoreRightSide(nestedFunctions, contentFormulaWithSymbols, op).Trim();
            return (ArithmeticOperators.Minus, newLeft, newRight);
        }



        return (ArithmeticOperators.None, text, "");
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
            return newLeft.Trim();
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
            return newRight.Trim();
        }
    }



    [GeneratedRegex(@"^(imin|imax|max|isum|icount)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)")]
    public static partial Regex RgxStartingFunction();

    [GeneratedRegex(@"^(imin|imax|max|isum|icount)\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$")]
    public static partial Regex RgxSingleFunction();

    [GeneratedRegex(@"(imin|imax|max|isum|icount)\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)")]
    public static partial Regex RgxAggregateFunctions();

    [GeneratedRegex(@"^\s*\(((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$")]
    public static partial Regex RgxOuterParenthesis();


}

