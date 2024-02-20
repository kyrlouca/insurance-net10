using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Shared.CommonRoutines;
using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewValidator.ValidationClasses;
internal class ValidationFunctions
{
    public static bool ValidateMatch(string text)
    {
        //matches("LEI/12A01", "^LEI\/[A-Z0-9]{3}(01|00)$")

        var res = text.Split(",", StringSplitOptions.RemoveEmptyEntries);
        if(!res.Any())
        {
            throw new Exception($"match not valid:{text}");
        }
        
        var rgxText = res[1].Replace(@"/", @"\/"); //^CAU/(ISIN/.*)=>"^CAU\/(ISIN\/.*)         
        var rgx= new Regex(rgxText, RegexOptions.IgnoreCase);
        var match= rgx.Match(res[0].Trim());
        return match.Success;

    }
}
