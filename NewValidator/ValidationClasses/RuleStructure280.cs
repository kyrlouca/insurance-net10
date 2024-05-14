using Shared.DataModels;
using Shared.GeneralUtils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewValidator.ValidationClasses;

public enum ScopeType { Rows, Cols, None }
public class RuleStructure280
{
    public int RuleId { get; init; }
    public string RuleFormula { get; init; }
    public RuleComponent280 IfComponent { get; init; }
    public RuleComponent280 ThenComponent { get; init; }
    public RuleComponent280 ElseComponent { get; init; }
    public RuleComponent280 FilterComponent { get; init; }
    public List<string> ScopeRowCols { get; init; }
    public ScopeType ScopeType { get; init; }
    public string ZetValue { get; set; }

    public List<MTable> RuleTables { get; set; } = new List<MTable>();
    private RuleStructure280(int ruleId, List<MTable> ruleTables, string ruleFormula, RuleComponent280 ifComponent, RuleComponent280 thenComponent, RuleComponent280 elseComponent, RuleComponent280 filter, List<string> rowsCols, ScopeType scopeType)
    {
        RuleId = ruleId;
        RuleFormula = ruleFormula;
        IfComponent = ifComponent;
        ThenComponent = thenComponent;
        ElseComponent = elseComponent;
        FilterComponent = filter;
        ScopeType = scopeType;
        ScopeRowCols = rowsCols;
        ZetValue = "";
        RuleTables = ruleTables;

    }

    public static (string ifExpression, string thenExpression, string elseExpression) SplitIfThenElse(string stringExpression)
    {
        //split if then  else expression
        //we may have an "if" , "then" without "else"
        //we may have no "if" and the rule is not boolean - it is an expression with left and right (A==B) or (A>B)
        //if(A) then B else C => A, B, C 


        if (string.IsNullOrEmpty(stringExpression))
        {
            return ("", "", "");
        }

        var rgxIfThenElse = @"if\s*(.*)\s*then(.*)\s*else(.*)";
        var terms = RegexUtils.GetRegexSingleMatchManyGroups(rgxIfThenElse, stringExpression);

        var res = terms.Count switch
        {
            4 => (terms[1].Trim(), terms[2].Trim(), terms[3].Trim()),
            3 => (terms[1].Trim(), terms[2].Trim(), ""),
            0 => (stringExpression.Trim(), "", ""),
            _ => ("", "", "")//does not happen but i could check for optional then ,els
        };

        return res;
    }

    public static RuleStructure280 CreateRuleStructure(int ruleId, List<MTable> ruleTables, string ruleFormula, string filterFormula, string scopeFormula)
    {
        //text = """if matches(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), "^ISIN/[A-Z0-9]{12}$") then isinChecksum(substring(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), 6)""";
        //@"if matches(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), "^ISIN/[A-Z0-9]{12}$") then isinChecksum(substring(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), 6)";

        //create three components (if, then, else)

        var (ifExpression, thenExpression, elseExpression) = SplitIfThenElse(ruleFormula);
        var ifComponent = RuleComponent280.CreateComponent(ifExpression);
        var thenComponent = RuleComponent280.CreateComponent(thenExpression);
        var elseComponent = RuleComponent280.CreateComponent(elseExpression);
        var filter = RuleComponent280.CreateComponent(filterFormula);
        var scope = RuleComponent280.CreateComponent(scopeFormula);
        var (scopeType, scopeRowCols) = GetScopeItems(scope);
        

        var rec = new RuleStructure280(ruleId,ruleTables, ruleFormula, ifComponent, thenComponent, elseComponent, filter, scopeRowCols, scopeType);
        return rec;
    }

    private static (ScopeType scopeType, List<string> rowsCols) GetScopeItems(RuleComponent280 scopeComponent)
    {
        var scope = scopeComponent.RuleTerms.FirstOrDefault();
        if (scope == null)
        {
            return (ScopeType.None, new List<string>());
        }
        var rows = (scope == null) ? new List<string>() : scope.R.Split(";", StringSplitOptions.RemoveEmptyEntries).ToList();
        var cols = (scope == null) ? new List<string>() : scope.C.Split(";", StringSplitOptions.RemoveEmptyEntries).ToList();

        ScopeType scopeType = rows switch
        {
            _ when rows.Any() => ScopeType.Rows,
            _ when cols.Any() => ScopeType.Cols,
            _ => ScopeType.None
        };

        var rowCols = scopeType switch
        {
            ScopeType.Rows => rows,
            ScopeType.Cols => cols,
            _ => new List<string>()
        };
        return (scopeType, rowCols);

    }


}


