using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq.Expressions;

namespace NewValidator.Common.FunctionalRoutines;


public record RuleTerm(string Letter,string TermText);
public class RuleComponent280
{
    //if or in else or in then 

    public bool IsValid { get; set; } = true;
    public string Expression { get; set; } = "";

    public static RuleComponent280 CreateRuleComponent(string text)
    {
        var rc = new RuleComponent280() { IsValid = true, Expression = text };
        return rc;
    }

    public static string ParseRule(string text)
    {
        //captures terms inside brackets , takes care of inner brackets in match statements
        //\{\s?[a-z]:([^{}]).*?\}
        //{t: S.28.02.01.04, r: R0210, c: C0090 } i+ {t: S.28.02.01.04, r: R0210, c: C0110} i i>= {t: S.12.01.01.01,  fv: solvency2} i- {t: S.12.01.01.01, r: R0020, c: C0020} i+ {t: S.12.01.01.01, r: R0110,} i
        //@"if matches(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), "^ISIN/[A-Z0-9]{12}$") then isinChecksum(substring(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), 6)";
        text="""if matches(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), "^ISIN/[A-Z0-9]{12}$") then isinChecksum(substring(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), 6)""";
        //text = """ if matches(dim({d: first}) + {d: second} +[ab] """;
        
        var rgxTerm = new Regex(@"\{\s?[a-z]:([^{}]).*?\}");
        var matches= rgxTerm.Matches(text);
        if(matches is null)
        {
            return "";
        }

        var ruleTerms = matches.Select((match,i )=> new RuleTerm( $"X{i:D2}", match.Value)).ToList();
        var formula = ruleTerms.Aggregate(text, (currentText, val) => {
            int index = currentText.IndexOf(val.TermText);
            string replacedString = currentText.Substring(0, index) + val.Letter + currentText.Substring(index + val.TermText.Length);
            return replacedString;
        }
        );
        return formula;
    }


}
