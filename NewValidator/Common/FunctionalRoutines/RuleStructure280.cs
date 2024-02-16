using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewValidator.Common.FunctionalRoutines;

public class RuleStructure280
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

    private static (string symbolExpression, List<RuleTerm280>) CreateFunctionTerms(string expression, string termLetter)
    {
        //1.Return a new SymbolExpression with term symbols for each FUNCTION (not term)
        //2 Create one new term  for each function     
        //X0=sum(X1) + sum(X2) => X0=Z0 + Z1 and create two new terms 
        //*** Same distinct letter for exactly the same terms ***

        if (string.IsNullOrWhiteSpace(expression))
            return ("", new List<RuleTerm280>());

        //replace
        var distinctMatches = RegexValidationFunctions.FunctionTypesRegex.Matches(expression)
            .Select(item => item.Captures[0].Value.Trim()).ToList()
            .Distinct()
            .ToList();


        var ruleFunctionTerms = distinctMatches
            .Select((item, Idx) =>  RuleTerm280.CreateRawTerm($"{termLetter}{Idx:D2}", item)).ToList();
         
        if (ruleFunctionTerms.Count == 0)
            return (expression, new List<RuleTerm280>());

        var symbolExpression = ruleFunctionTerms
            .Aggregate(expression, (currValue, item) => currValue.Replace(item.TermText, item.Letter));
        return (symbolExpression, ruleFunctionTerms);
    }


    private static (string symbolExpression, List<RuleTerm280>) CreateRuleExpressions(string expression, string termLetter)
    {
        //1.Return a new SymbolExpression with term symbols for each FUNCTION (not term)
        //2 Create one new term  for each function     
        //X0=sum(X1) + sum(X2) => X0=Z0 + Z1 and create two new terms 
        //*** Same distinct letter for exactly the same terms ***

        if (string.IsNullOrWhiteSpace(expression))
            return ("", new List<RuleTerm280>());

        //replace
        var distinctMatches = RegexValidationFunctions.FunctionTypesRegex.Matches(expression)
            .Select(item => item.Captures[0].Value.Trim()).ToList()
            .Distinct()
            .ToList();


        var ruleFunctionTerms = distinctMatches
            .Select((item, Idx) => RuleTerm280.CreateRawTerm($"{termLetter}{Idx:D2}", item)).ToList();

        if (ruleFunctionTerms.Count == 0)
            return (expression, new List<RuleTerm280>());

        var symbolExpression = ruleFunctionTerms
            .Aggregate(expression, (currValue, item) => currValue.Replace(item.TermText, item.Letter));
        return (symbolExpression, ruleFunctionTerms);
    }



}


