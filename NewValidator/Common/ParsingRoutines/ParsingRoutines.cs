using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Validator.Common.ParsingRoutines;
public enum ArithmeticOperators { Multiply, Plus, Minus, None, UnaryMinus };
public static class OperatorManager
{
    
    public record opRecord(char op, int position);
    public static List<opRecord> FindOperators(string text, char[] operators)
    {
        int pos = 0;
        List<opRecord> opList = new();
        foreach (char c in text)
        {
            if (operators.Contains(c))
            {
                opList.Add(new opRecord(c, pos));
            }
            pos++;
        }

        return opList;
    }

    public static bool IsOperatorUnary(string text, char[] operators, int index)
    {
        var left = text[..index];
        left = left.Replace(" ", "");
        var res = string.IsNullOrEmpty(left) || operators.Contains(left.Last());
        return res;
    }

    public static ArithmeticOperators ToArithmeticOperator(string text, char[] operators, int index)
    {
        var left = text[..index];
        left = left.Replace(" ", "");
        var isUnary = string.IsNullOrEmpty(left) || operators.Contains(left.Last());        
        var op = text[index];
        var res = op switch
        {
            '+' => ArithmeticOperators.Plus,
            '-' when isUnary => ArithmeticOperators.UnaryMinus,
            '-' => ArithmeticOperators.Minus,
            '*' => ArithmeticOperators.Multiply,
            _ => ArithmeticOperators.None,
        };
        return res;
    }

}