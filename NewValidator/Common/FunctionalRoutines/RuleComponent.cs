using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace NewValidator.Common.FunctionalRoutines;


public class RuleComponent
{
    //a rule component is a part of a rule separated by AND or OR
    //ab AND cd OR ff AND cc => ComponentFormula : x0 OR x1 AND And x2   ComponentExpressions: ab, cd, cc
    //todo change the expressions later on

    public bool HasValue { get; set; } = true;
    public string ComponentText { get; set; } = "";
    public string ComponentFormula { get; set; } = "";
    public Dictionary<string,string> ComponentExpressions { get; set; }= new Dictionary<string, string>();

    public  static  (bool hasValue, string componentFormula, Dictionary<string,string> expressions  ) ParseComponent(string text)
    {
        //"aa and bb or cc and aa" => X01 and X02 or X03 and X04
        if (string.IsNullOrEmpty(text))
        {
            return (false, "", new Dictionary<string, string>());
        }

        string[] delimiters = { "and", "or" };            
        string[] result = text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

        var expressions = result.Select((item, index) => new { Key = $"X{index:D2}", Value = item })
                              .ToDictionary(x => x.Key, x => x.Value.Trim());

        //replace  just the first occurance
        var formula = expressions.Aggregate(text, (currentText, val) =>  new Regex(val.Value).Replace(currentText, val.Key,1));
        return (true,formula,expressions);
    }

    public static RuleComponent CreateRuleComponent(string text)
    {
        var (hasValue, componentFormula, expressions) = ParseComponent(text);
        var ruleExpressions = expressions.Select(exp => RuleExpression.CreateRuleExpression(exp.Key,exp.Value));
        var rc = new RuleComponent() {HasValue=hasValue, ComponentText = text, ComponentFormula = componentFormula, ComponentExpressions = expressions };
        return rc;
    }

    

}
