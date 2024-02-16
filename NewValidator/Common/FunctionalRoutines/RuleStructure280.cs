using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewValidator.Common.FunctionalRoutines;

public record RuleStructure280
{
    public static (bool isIfExpression, string ifExpression, string thenExpression,string elseExpression) SplitIfThenElse(string stringExpression)
    {
        //split if then expression            
        //if(A) then B=> A, B            

        var rgxIfThenElse = @"if\s*(.*)\s*then(.*)\s*else(.*)";
        

        var terms = RegexUtils.GetRegexSingleMatchManyGroups(rgxIfThenElse, stringExpression);
        if (terms.Count != 4)
        {
            return (false, "", "","");
        }


        return (true, terms[1].Trim(), terms[2].Trim() , terms[3].Trim());
    }
}


