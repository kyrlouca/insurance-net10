using NewValidator.ValidationClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Validator.ValidationClasses;


public record ExpressionInfoWithIntervalsType(string op, bool isAllDoubles , OptionalObject leftBase, OptionalObject leftMin, OptionalObject leftMax, OptionalObject rightBase, OptionalObject rightMin, OptionalObject rightMax);

public static class ExpressionInfo
{
    public static ExpressionInfoWithIntervalsType Create(string op, bool isAllDoubles, OptionalObject leftBase, OptionalObject leftMin, OptionalObject leftMax, OptionalObject rightBase, OptionalObject rightMin, OptionalObject rightMax)
    {
            
            return new(op,isAllDoubles, leftBase, leftMin, leftMax, rightBase, rightMin, rightMax);
  
    }
}
