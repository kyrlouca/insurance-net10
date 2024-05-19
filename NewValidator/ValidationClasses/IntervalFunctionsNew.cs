using NewValidator.ValidationClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Validator.ValidationClasses;

internal class IntervalFunctionsNew
{


    public static bool IsIntervalExpressionValid(string operatorI, OptionalObject leftMin, OptionalObject leftMax , OptionalObject rightMin, OptionalObject rightMax)
    {
        //todo if any operand is null the result is NULL
        var lmin = (double)(leftMin?.Value ?? 0.0);
        var lmax = (double)(leftMax?.Value ?? 0.0);
        var rmin = (double)(rightMin?.Value ?? 0.0);
        var rmax = (double)(rightMax?.Value ?? 0.0);
        var resInterval = operatorI switch
        {
            "=" => IsIntervalEQ(lmin, lmax, rmin, rmax),
            "==" => IsIntervalEQ(lmin, lmax, rmin, rmax),
            ">" => IsIntervalGT(lmin, lmax, rmin, rmax),
            ">=" => IsIntervalGTE(lmin, lmax, rmin, rmax),
            "<" => IsIntervalLT(lmin, lmax, rmin, rmax),
            "<=" => IsIntervalLTE(lmin, lmax, rmin, rmax),
            "!=" => IsIntervalNE(lmin, lmax, rmin, rmax),
            _ => false
        };
        return resInterval;
    }

    
    
    public static bool IsIntervalEQ( double leftMin, double leftMax, double rightMin, double rightMax)    
    {

        var isValid = (leftMin <= rightMax) && (rightMin <= leftMax);
        return isValid;
    }
    public static bool IsIntervalGT(double leftMin, double leftMax, double rightMin, double rightMax)
    {
        //return x1Min > x2Max;
        var isValid = (leftMin > rightMax);
        return isValid;
    }
    public static bool IsIntervalGTE(double leftMin, double leftMax, double rightMin, double rightMax)
    {
        //this will NOT work!!! x1Min > x2Max;
        var isValid = IsIntervalGT(leftMin, leftMax, rightMin, rightMax) || IsIntervalEQ(leftMin, leftMax, rightMin, rightMax);
        
        return isValid;
    }
    public static bool IsIntervalLT(double leftMin, double leftMax, double rightMin, double rightMax)
    {
        //return x1Max < x2Min;
        var isValid = (leftMax < rightMin);
        return isValid;
    }
    public static bool IsIntervalLTE(double leftMin, double leftMax, double rightMin, double rightMax)
    {
        //this will NOT!! work x1Max <= x2Min;
        var isValid = IsIntervalLT(leftMin, leftMax, rightMin, rightMax) || IsIntervalEQ(leftMin, leftMax, rightMin, rightMax);
        return isValid;
    }
    public static bool IsIntervalNE(double leftMin, double leftMax, double rightMin, double rightMax)
    {
        //   return x1Max < x2Min || x1Min > x2Max;
        var isValid = (leftMax < rightMin) || (leftMin>rightMax);
        return isValid;
    }
    

}

