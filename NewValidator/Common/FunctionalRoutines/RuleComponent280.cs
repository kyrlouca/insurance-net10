using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace NewValidator.Common.FunctionalRoutines;


public class RuleComponent280
{
    //if or in else or in then 

    public bool IsValid { get; set; } = true;
    public string Expression { get; set; } = "";
    


    public static RuleComponent280 CreateRuleComponent(string text)
    {
        var rc= new RuleComponent280() { IsValid = true,Expression=text };               
        return rc;
    }

    

}
