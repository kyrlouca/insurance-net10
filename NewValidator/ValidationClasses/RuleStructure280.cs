using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewValidator.ValidationClasses;

public class RuleStructure280
{
    public string  RuleFormula {get;init;}
    public bool IsComplete { get; init; }
    public bool IsBooleanRule { get; init; }
    public RuleComponent280 IfComponent { get; init; }
    public RuleComponent280 ThenComponent { get; init; }
    public RuleComponent280 ElseComponent { get; init; }

    private RuleStructure280(string ruleFormula, bool isBooleanRule, RuleComponent280 ifComponent, RuleComponent280 thenComponent, RuleComponent280 elseComponent)
    {        
        RuleFormula = ruleFormula;
        IsBooleanRule = isBooleanRule;
        IfComponent = ifComponent;
        ThenComponent = thenComponent;
        ElseComponent = elseComponent;
    }

    public static (bool isBooleanRule, string ifExpression, string thenExpression, string elseExpression) SplitIfThenElse(string stringExpression)
    {
        //split if then  else expression
        //we may have an "if" , "then" without "else"
        //we may have no "if" and the rule is not boolean - it is an expression with left and right (A==B) or (A>B)
        //if(A) then B else C => A, B, C 
        

        if (string.IsNullOrEmpty(stringExpression))
        {
            return (false,"", "", "");
        }

        var rgxIfThenElse = @"if\s*(.*)\s*then(.*)\s*else(.*)";
        var terms = RegexUtils.GetRegexSingleMatchManyGroups(rgxIfThenElse, stringExpression);

        var res = terms.Count switch
        {
            4 => (true,terms[1].Trim(), terms[2].Trim(), terms[3].Trim()),
            3 => (true, terms[1].Trim(), terms[2].Trim(), ""),
            0 => (false, stringExpression.Trim(), "", ""),
            _ => (false, "", "", "")//does not happen but i could check for optional then ,els
        };

        return res;
    }

    public static RuleStructure280 CreateRuleStructure(string ruleFormula)
    {
        //text = """if matches(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), "^ISIN/[A-Z0-9]{12}$") then isinChecksum(substring(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), 6)""";
        //@"if matches(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), "^ISIN/[A-Z0-9]{12}$") then isinChecksum(substring(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), 6)";

        //create three components (if, then, else)
        var (isBoolean, ifExpression, thenExpression, elseExpression) = SplitIfThenElse(ruleFormula);
        var ifComponent = RuleComponent280.CreateComponent(ifExpression);
        var thenComponent = RuleComponent280.CreateComponent(thenExpression);
        var elseComponent = RuleComponent280.CreateComponent(elseExpression);

               

        var rec = new RuleStructure280(ruleFormula, isBoolean, ifComponent, thenComponent, elseComponent);
        return rec;
    }



}


