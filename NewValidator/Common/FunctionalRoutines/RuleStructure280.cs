using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewValidator.Common.FunctionalRoutines;

public class RuleStructure280
{
    bool IsValid { get; set; }
    public RuleComponent IfComponent { get; init; }
    public RuleComponent ThenComponent { get; init; }
    public RuleComponent ElseComponent { get; init; }

    private RuleStructure280(bool isValid, RuleComponent ifComponent, RuleComponent thenComponent, RuleComponent elseComponent)
    {
        IsValid = isValid;
        IfComponent = ifComponent;
        ThenComponent = thenComponent;
        ElseComponent = elseComponent;
    }

    public static (bool isIfExpression, string ifExpression, string thenExpression, string elseExpression) SplitIfThenElse(string stringExpression)
    {
        //split if then expression            
        //if(A) then B=> A, B            

        var rgxIfThenElse = @"if\s*(.*)\s*then(.*)\s*else(.*)";
        var terms = RegexUtils.GetRegexSingleMatchManyGroups(rgxIfThenElse, stringExpression);

        var res = terms.Count switch
        {
            4 => (true, terms[1].Trim(), terms[2].Trim(), terms[3].Trim()),
            0 => (true, stringExpression.Trim(), "", ""),
            _ => (false, "", "", "")//does not happen but i could check for optional then ,els
        }; 
        
        return res;
    }

    public static RuleStructure280 CreateRuleStructure(string text)
    {
        var (isIfExpression, ifExpression, thenExpression, elseExpression) = SplitIfThenElse(text);
        var ifComponent = RuleComponent.CreateRuleComponent( ifExpression);
        var thenComponent = RuleComponent.CreateRuleComponent( thenExpression);
        var elseComponent = RuleComponent.CreateRuleComponent(elseExpression);
        
        var rec = new RuleStructure280(isIfExpression, ifComponent, thenComponent, elseComponent);
        return rec;
    }



}


