using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewValidator.Common.FunctionalRoutines;

public class RuleStructure280
{
    public bool IsComplete { get; init; }
    public bool IsPlainRule { get; init; }
    public RuleComponent IfComponent { get; init; }
    public RuleComponent ThenComponent { get; init; }
    public RuleComponent ElseComponent { get; init; }

    private RuleStructure280(bool isComplete, bool isPlainRule, RuleComponent ifComponent, RuleComponent thenComponent, RuleComponent elseComponent)
    {
        IsComplete = isComplete;
        IsPlainRule = isPlainRule;
        IfComponent = ifComponent;
        ThenComponent = thenComponent;
        ElseComponent = elseComponent;
    }

    public static ( string ifExpression, string thenExpression, string elseExpression) SplitIfThenElse(string stringExpression)
    {
        //split if then  else expression            
        //if(A) then B else C => A, B, C (it is complete and NOT plain
        // abc =>abc (is complete AND plain)

        if (string.IsNullOrEmpty(stringExpression))
        {
            return ( "", "", "");
        }

        var rgxIfThenElse = @"if\s*(.*)\s*then(.*)\s*else(.*)";
        var terms = RegexUtils.GetRegexSingleMatchManyGroups(rgxIfThenElse, stringExpression);

        var res = terms.Count switch
        {
            4 => ( terms[1].Trim(), terms[2].Trim(), terms[3].Trim()),
            0 => ( stringExpression.Trim(), "", ""),
            _ => ( "", "", "")//does not happen but i could check for optional then ,els
        };

        return res;
    }

    public static RuleStructure280 CreateRuleStructure(string text)
    {
        var (ifExpression, thenExpression, elseExpression) = SplitIfThenElse(text);
        var ifComponent = RuleComponent.CreateRuleComponent(ifExpression);
        var thenComponent = RuleComponent.CreateRuleComponent(thenExpression);
        var elseComponent = RuleComponent.CreateRuleComponent(elseExpression);

        var isPlainRule = ifComponent.HasValue && !elseComponent.HasValue && !thenComponent.HasValue;
        var isCompleteRule =
            (ifComponent.HasValue && elseComponent.HasValue && thenComponent.HasValue)
            || (ifComponent.HasValue && !elseComponent.HasValue && !thenComponent.HasValue);
            

        var rec = new RuleStructure280(isCompleteRule, isPlainRule, ifComponent, thenComponent, elseComponent);
        return rec;
    }



}


