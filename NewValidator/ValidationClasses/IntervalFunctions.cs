using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewValidator.ValidationClasses;

public class IntervalFunctions
{
    public static bool IsIntervalEQ(double left, int leftDecimals, double right, int rightDecimals)
    {
        //==: abs(centre(left) – centre(right)) <= radius(left) + radius(right).
        var leftSide = Math.Abs(left - right);
        var rightSide = Radius(leftDecimals) + Radius(rightDecimals);
        return leftSide <= rightSide;
    }

    public static bool IsIntervalGT(double left, int leftDecimals, double right, int rightDecimals)
    {
        //>: centre(left) > centre(right) - (radius(left) + radius(right)).
        var leftSide = left;
        var rightSide = right - (Radius(leftDecimals) + Radius(rightDecimals));
        return leftSide > rightSide;
    }

    public static bool IsIntervalGTE(double left, int leftDecimals, double right, int rightDecimals)
    {
        //>: centre(left) >= centre(right) - (radius(left) + radius(right)).
        var leftSide = left;
        var rightSide = right - (Radius(leftDecimals) + Radius(rightDecimals));
        return leftSide >= rightSide;
    }

    public static bool IsIntervalLT(double left, int leftDecimals, double right, int rightDecimals)
    {
        //< : centre(left) – centre(right) < radius(left) + radius(right).
        var leftSide = left - right;
        var rightSide = Radius(leftDecimals) + Radius(rightDecimals);
        return leftSide < rightSide;
    }

    public static bool IsIntervalLTE(double left, int leftDecimals, double right, int rightDecimals)
    {
        //< : centre(left) – centre(right) < radius(left) + radius(right).
        var leftSide = left - right;
        var rightSide = Radius(leftDecimals) + Radius(rightDecimals);
        return leftSide <= rightSide;
    }

    public static double Radius(int decimals)
    {
        var val = 1 / Math.Pow(10, decimals) / 2;
        return val;
    }

}
