using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Validator.Common.ParsingRoutines.OperatorManager;

namespace Validator.Common.ParsingRoutines;
public enum ArithmeticOperators { Multiply, Plus, Minus, None, UnaryMinus };
public static class OperatorManager
{

    public record OperatorRecord(char op, ArithmeticOperators arithmeticOperator, int position);
    public static List<OperatorRecord> PlaceOperatorsInList(string text, char[] allOperators, char[] operatorsToCheck)
    {
        //remember: find first the + and -, then *, and the unary -
        List<OperatorRecord> opList = text
       .Select((c, pos) => new { Character = c, Position = pos })
       .Where(item => operatorsToCheck.Contains(item.Character))
       .Select(item =>
       {
           var isUnary = IsOperatorUnary(text, allOperators, item.Position);
           var arithmeticOperator = GetArithmeticOperator(item.Character, isUnary);
           return new OperatorRecord(item.Character, arithmeticOperator, item.Position);
       })
       .ToList();
        return opList;
    }



    public static List<OperatorManager.OperatorRecord> PlaceOperatorsInOrderedList(string contentFormulaWithSymbols)
    {
        //we place first the  mulitpy, then the plus,minus, then the unary
        //then you should use the last operator to split the expression
        //7 - 4*3 + 5 => *,-,+  and 7 - 4*3 (+) 5  
        //7 - 4*3=> *,- => 7 (-) 4*3 
        
        char[] multiplyOps = { '*' };
        char[] minusPlusOps = { '+', '-' };

        char[] allOps = minusPlusOps.Concat(multiplyOps).ToArray();


        
        var opPlusOrMinus = OperatorManager.PlaceOperatorsInList(contentFormulaWithSymbols, allOps, minusPlusOps);
        var opMulti = OperatorManager.PlaceOperatorsInList(contentFormulaWithSymbols, allOps, multiplyOps);

        var concatAndOrdered = new List<OperatorManager.OperatorRecord>()
                .Concat(opPlusOrMinus.Where(op => op.arithmeticOperator != ArithmeticOperators.UnaryMinus))
                .Concat(opMulti)
                .Concat(opPlusOrMinus.Where(op => op.arithmeticOperator == ArithmeticOperators.UnaryMinus))
                .ToList();
        return concatAndOrdered;
    }


    public static bool IsOperatorUnary(string text, char[] allOperators, int index)
    {
        var left = text[..index];
        left = left.Replace(" ", "");
        var res = string.IsNullOrEmpty(left) || allOperators.Contains(left.Last());
        return res;
    }

    private static ArithmeticOperators GetArithmeticOperator(char currentChar, bool isUnary)
    {
        return currentChar switch
        {
            '+' => ArithmeticOperators.Plus,
            '-' when isUnary => ArithmeticOperators.UnaryMinus,
            '-' => ArithmeticOperators.Minus,
            '*' => ArithmeticOperators.Multiply,
            _ => ArithmeticOperators.None
        };
    }

}