using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq.Expressions;

namespace NewValidator.ValidationClasses;


public record RuleTextTerm(string Letter, string TermText);
public class RuleComponent280
{
    //Either the component of the if, else, then

    public bool IsValid { get; set; } = true;
    public string Expression { get; set; } = "";
    public List<RuleTerm280> RuleTerms { get; set; } = new();
    public string SymbolExpression { get; set; } = "";
    

    public static RuleComponent280 CreateComponent(string text)
    {
        //captures terms inside brackets , takes care of inner brackets in match statements
        //\{\s?[a-z]:([^{}]).*?\}
        //{t: S.28.02.01.04, r: R0210, c: C0090 } i+ {t: S.28.02.01.04, r: R0210, c: C0110} i i>= {t: S.12.01.01.01,  fv: solvency2} i- {t: S.12.01.01.01, r: R0020, c: C0020} i+ {t: S.12.01.01.01, r: R0110,} i
        //@"if matches(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), "^ISIN/[A-Z0-9]{12}$") then isinChecksum(substring(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), 6)";
        //text = """if matches(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), "^ISIN/[A-Z0-9]{12}$") then isinChecksum(substring(dim({d: [s2c_dim:IW], seq: False, id: v0},[s2c_dim:IW]), 6)""";
        //text = """{t: S.28.02.01.04, r: R0210, c: C0090 } i+ {t: S.28.02.01.04, r: R0210, c: C0110} i i>= {t: S.12.01.01.01,  fv: solvency2} i- {t: S.12.01.01.01, r: R0020, c: C0020} i+ {t: S.12.01.01.01, r: R0110,} i""";
        //text = """ if matches(dim({d: first}) + {d: second} +[ab] """;

        var rgxTerm = new Regex(@"\{\s?[a-z]:([^{}]).*?\}");
        var matches = rgxTerm.Matches(text);
        if (matches is null)
        {
            return new RuleComponent280() { Expression = text, SymbolExpression = "", RuleTerms = new List<RuleTerm280>() };
        }

        var ruleTextTerms = matches.Select((match, i) => new RuleTextTerm($"X{i:D2}", match.Value)) ?? new List<RuleTextTerm>();
        var formula = ruleTextTerms.Aggregate(text, (currentText, val) =>
        {
            int index = currentText.IndexOf(val.TermText);
            string replacedString = currentText.Substring(0, index) + val.Letter + currentText.Substring(index + val.TermText.Length);
            return replacedString;
        });

        var ruleTerms = ruleTextTerms.Select(rt => RuleTerm280.CreateRuleTerm280(rt.Letter, rt.TermText))
            .Where(rt => rt is not null)
            .ToList();

        var rc = new RuleComponent280() { Expression = text, SymbolExpression = formula, RuleTerms = ruleTerms };
        return rc;
    }


}
