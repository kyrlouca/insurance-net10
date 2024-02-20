using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Shared.CommonRoutines;
using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Z.Expressions;

namespace NewValidator.ValidationClasses;
internal class ValidationFunctions
{
    public static bool ValidateMatch(string text)
    {
        //matches("LEI/12A01", "^LEI\/[A-Z0-9]{3}(01|00)$")
        //matches\(\"(.*?)\"\s*,\s*\"(.*?)\"\)
        var regex = new Regex("""matches\(\"(.*?)\"\s*,\s*\"(.*?)\"\)""");
        var match = regex.Match(text);
        if (!match.Success)
        {
            throw new InvalidOperationException($"invalid match:{text} ");
        }

        var value = match.Groups[1].Value;
        var rgxExpression = match.Groups[2].Value.Replace(@"/", @"\/"); // ^CAU/(ISIN/.*)=>"^CAU\/(ISIN\/.*)         
        var rgx= new Regex(rgxExpression, RegexOptions.IgnoreCase);
        var matchValidation= rgx.Match(value);
        return matchValidation.Success;

    }

    public static bool ValidateArithmetic(string text)
    {
        var result = Eval.Execute<bool>(text);
        return result;

    }
}
