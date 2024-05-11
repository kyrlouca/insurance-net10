using NewValidator.ValidationClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Validator.ValidationClasses;
public record ExpressionInfoType(string op, OptionalObject leftExpression, OptionalObject rightExpression);
