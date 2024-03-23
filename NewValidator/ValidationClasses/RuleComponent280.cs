using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using Shared.SpecialRoutines;

namespace NewValidator.ValidationClasses;



public class RuleComponent280
{
    //Either the component of the if, else, then
    //It has the original Expression and the symbolExpression X01 + X02 >= X03 - X04 + X05  
    //it has a list of RuleTerms {t: S.28.02.01.04, r: R0210, c: C0090 ... }
    //it has a Dictionary of ObjectTerms which correspond to each ruleTerm but they get the value from the fact (or from the sum/count if the ruleterm is seq:true)
    //The key of each objectTerm is the same as the letter of the corresponding RuleTerm

    public bool IsEmpty { get; init; }
    public bool IsValid { get; set; } = true;
    public string Expression { get; init; } 
    public List<RuleTerm280> RuleTerms { get; set; } = new();
    public string SymbolExpression { get; set; } = "";
    public Dictionary<string, ObjectTerm280> ObjectTerms { get; set; } = new();
      
    public static RuleComponent280 CreateComponent(string textExpression)
    {
        //captures terms inside brackets , takes care of inner brackets in match statements        
        //text : {t: S.28.02.01.04, r: R0210, c: C0090 ... } i+ {t: S.28.02.01.04, r: R0210, c: C0110 ...}   i>= {t: S.12.01.01.01,  fv: solvency2} i- {t: S.12.01.01.01, r: R0020, c: C0020} i+ {t: S.12.01.01.01, r: R0110,} i
        //=> X01 + X02 >= X03 - X04 + X05  
        //=> creates the RuleTerms280
        //also checks for the i interval and marks the term as interval          
        

        if (string.IsNullOrEmpty(textExpression))
        {
            return new RuleComponent280() {IsEmpty=true, Expression=textExpression};
        }
        

        /////////////////////////////////////////
        
        //var rgxTerm = new Regex(@"\{\s?[a-z]:([^{}]).*?\}( i)?");
        //var matches = rgxTerm.Matches(textExpression);
        //if (matches is null)
        //{
        //    return new RuleComponent280() {IsEmpty=false,IsValid=false, Expression = textExpression, SymbolExpression = "", RuleTerms = new List<RuleTerm280>() };
        //}

        //var ruleTextTerms = matches.Select((match, i) => new RuleTextTerm($"X{i:D2}", match.Value)) ?? new List<RuleTextTerm>();
        //var formula = ruleTextTerms.Aggregate(textExpression, (currentText, val) =>
        //{
        //    int index = currentText.IndexOf(val.TermText);
        //    string replacedString = currentText.Substring(0, index) + val.Letter + currentText.Substring(index + val.TermText.Length);
        //    return replacedString;
        //});

        var(formula,ruleTextTerms) = TermsExtraction.ExtractTerms(textExpression);

        if (ruleTextTerms.Count==0)
        {
            return new RuleComponent280() { IsEmpty = false, IsValid = false, Expression = textExpression, SymbolExpression = formula , RuleTerms = new List<RuleTerm280>() };
        }


        var ruleTerms = ruleTextTerms.Select(rt => RuleTerm280.CreateRuleTerm280(rt.Letter, rt.TermText))
            .Where(rt => rt is not null)
            .ToList();

        formula = formula.Replace(" = ", " == ");
        
        var rc = new RuleComponent280() {IsEmpty=false, Expression = textExpression, SymbolExpression = formula, RuleTerms = ruleTerms };
        return rc;
    }

    
    public string DislayRuleTerms()
    {
        var vals = RuleTerms.Aggregate("", (current, value) => {
            var obj = ObjectTerms[value.Letter];    
            return $"{value.Letter}={value.T}:{value.R}:{value.C}##{obj.Obj},"; 
        });
        return $"{SymbolExpression}***{vals}";


    }
}
