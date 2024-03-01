using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Shared.CommonRoutines;
using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Z.Expressions;
using static System.Net.Mime.MediaTypeNames;

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


    public static bool ValidateMatch(string text, Dictionary<string, ObjectTerm280> terms)
    {
        //matches(X00, "^LEI\/[A-Z0-9]{3}(01|00)$") => X00, "^LEI\/[A-Z0-9]{3}(01|00)$")        

        var qt= "\"";
        var pattern = @$"matches\((.*?)\s*,\s*\{qt}(.*)\{qt}\)";
        var regex = new Regex(pattern);
        var match = regex.Match(text);
        if (!match.Success)
        {
            throw new InvalidOperationException($"invalid match:{text} ");
        }

        //in real life the first term in the text would be X/d/d but take the actual value for testing 
        
        var letter = match.Groups[1].Value;        
        var rgxForTestQ = new Regex($@"\{qt}(.*)\{qt}");

        var value = rgxForTestQ.IsMatch(letter) ? rgxForTestQ.Match(letter).Groups[1].Value : terms[letter].Obj.ToString() ?? "";

        var rgxFromValue = match.Groups[2].Value.Replace(@"/", @"\/"); // ^CAU/(ISIN/.*)=>"^CAU\/(ISIN\/.*)         
        var rgx = new Regex(rgxFromValue, RegexOptions.IgnoreCase);
        var matchValidation = rgx.Match(value);
        return matchValidation.Success;
    }


    public static bool ValidateArithmetic(string text)
    {
        var result = Eval.Execute<bool>(text);
        return result;

    }

    public static bool ValidateArithmetic(string symbolFormula, Dictionary<string,ObjectTerm280> terms)
    {

        symbolFormula = symbolFormula.Replace("or", "||");
        symbolFormula = symbolFormula.Replace("and", "&&");


        //Dictionary<string, object> plainObjects = terms.ToDictionary(item => item.Key, item => item.Value.ObjectType == "E" ? (object)$"[{item.Value.Obj}]" : item.Value.Obj);
        var rgxTerm = new Regex(@"(X\d\d)");
        var matchTersm= rgxTerm.Match(symbolFormula);
        if (matchTersm.Success)
        {
            var term = terms[matchTersm.Value];
            if (term.ObjectType == "E")
            {
                symbolFormula = symbolFormula.Replace("[", "\"");
                symbolFormula= symbolFormula.Replace("]", "\"");
            }
            
        }
        //todo first i need to split using ">,<,=" and then evaluate each part
        //maybe i need to call EvaluateArithmetic here
        Dictionary<string, object> plainObjects = terms.ToDictionary(item => item.Key, item => item.Value.Obj);
        var result = Eval.Execute<bool>(symbolFormula,plainObjects);
        return result;
    }


    public static bool ValidateIsNull(string symbolFormula, Dictionary<string, ObjectTerm280> terms)
    {
        var letter = terms[symbolFormula];
        var xx = letter?.Obj?.ToString();
        var isEmpty = (letter?.Obj?.ToString() ?? "") == "emptySequence()";
        if (letter == null ||  letter?.Obj is null || isEmpty)
        {
            return true;
        }
        return false;
        
    }


    


}
