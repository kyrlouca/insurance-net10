using NewValidator.ValidationClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Validator.ValidationClasses;
public record ExpressionInfoType(string op, OptionalObject leftExpression, OptionalObject rightExpression);


public record ExpressionInfoWithIntervalsType(string op, double leftBase,  double leftMin, double leftMax, double rightBase, double rightMin,double rightMax);
