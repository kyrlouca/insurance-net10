using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewValidator.ValidationClasses;

public class IntervalFunctions
{

    public static bool IsIntervalExpressionValid(string operatorI, double left, int leftDecimals, double right, int rightDecimals)
    {
        //todo if any operand is null the result is NULL
        var resInterval = operatorI switch
        {
            "=" => IntervalFunctions.IsIntervalEQ(left, leftDecimals, right, rightDecimals),
            "==" => IntervalFunctions.IsIntervalEQ(left, leftDecimals, right, rightDecimals),
            ">" => IntervalFunctions.IsIntervalGT(left, leftDecimals, right, rightDecimals),
            ">=" => IntervalFunctions.IsIntervalGTE(left, leftDecimals, right, rightDecimals),
            "<" => IntervalFunctions.IsIntervalLT(left, leftDecimals, right, rightDecimals),
            "<=" => IntervalFunctions.IsIntervalLTE(left, leftDecimals, right, rightDecimals),
            "!=" => IntervalFunctions.IsIntervalNE(left, leftDecimals, right, rightDecimals),
            _ => false
        };
        return resInterval;
    }

    private static bool IsIntervalEQ(double left, int leftDecimals, double right, int rightDecimals)
    {
        //==: abs(centre(left) – centre(right)) <= radius(left) + radius(right).
        var leftSide = Math.Abs(left - right);
        var rightSide = Radius(leftDecimals) + Radius(rightDecimals);
        return leftSide <= rightSide;
    }

    private static bool IsIntervalGT(double left, int leftDecimals, double right, int rightDecimals)
    {
        //>: centre(left) > centre(right) - (radius(left) + radius(right)).
        var leftSide = left;
        var rightSide = right - (Radius(leftDecimals) + Radius(rightDecimals));
        return leftSide > rightSide;
    }

    private static bool IsIntervalGTE(double left, int leftDecimals, double right, int rightDecimals)
    {
        //>: centre(left) >= centre(right) - (radius(left) + radius(right)).
        var leftSide = left;
        var rightSide = right - (Radius(leftDecimals) + Radius(rightDecimals));
        return leftSide >= rightSide;
    }

    private static bool IsIntervalLT(double left, int leftDecimals, double right, int rightDecimals)
    {
        //< : centre(left) – centre(right) < radius(left) + radius(right).
        var leftSide = left - right;
        var rightSide = Radius(leftDecimals) + Radius(rightDecimals);
        return leftSide < rightSide;
    }

    private static bool IsIntervalLTE(double left, int leftDecimals, double right, int rightDecimals)
    {
        //< : centre(left) – centre(right) < radius(left) + radius(right).
        var leftSide = left - right;
        var rightSide = Radius(leftDecimals) + Radius(rightDecimals);
        return leftSide <= rightSide;
    }

    private static bool IsIntervalNE(double left, int leftDecimals, double right, int rightDecimals)
    {
        //For intervals: abs(centre(left) – centre(right)) > radius(left) + radius(right)
        
        var leftSide = Math.Abs( left - right);
        var rightSide = Radius(leftDecimals) + Radius(rightDecimals);
        return leftSide > rightSide;
    }

    private static double Radius(int decimals)
    {
        var val = 1 / Math.Pow(10, decimals) / 2;
        return val;
    }

}
