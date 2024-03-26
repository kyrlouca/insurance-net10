using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Shared.CommonRoutines;
using Shared.GeneralUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Z.Expressions;
using static System.Net.Mime.MediaTypeNames;

namespace NewValidator.ValidationClasses;
internal class ValidationFunctions
{
    public static bool ValidateMatch(string text)
    {
        //matches("LEI/12A01", "^LEI\/[A-Z0-9]{3}(01|00)$")
        //matches\(\"(.*?)\"\s*,\s*\"(.*?)\"\)
        var regex = new Regex("""matches\(\"(.*?)\"\s*,\s*\"(.*?)\"\)""");
        var match = regex.Match(text);
        if (!match.Success)
        {
            throw new InvalidOperationException($"invalid match:{text} ");
        }

        var value = match.Groups[1].Value;
        var rgxExpression = match.Groups[2].Value.Replace(@"/", @"\/"); // ^CAU/(ISIN/.*)=>"^CAU\/(ISIN\/.*)         
        var rgx = new Regex(rgxExpression, RegexOptions.IgnoreCase);
        var matchValidation = rgx.Match(value);
        return matchValidation.Success;

    }


    public static bool ValidateMatch(string text, Dictionary<string, ObjectTerm280> terms)
    {
        //matches may be inside a formula or in a filter, a metric m: with filter , a dim , a term with filter
        //1. inside the term matches : matches(X00, "^LEI\/[A-Z0-9]{3}(01|00)$") => X00, "^LEI\/[A-Z0-9]{3}(01|00)$")                        
        //2. inside and outside the filter :matches({t: S.06.02.07.02, c: C0290, z: Z0001, filter: matches(dim(this(), [s2c_dim:UI]), "^CAU/.*") and not(matches(dim(this(), [s2c_dim:UI]), "^CAU/(ISIN/.*)|(INDEX/.*)")), seq: False, id: v1, f: solvency, fv: solvency2}, "^((XL)|(XT))..$")
        //3. NOT used no XXX. matches({ m: [s2md_met:si1558], filter:not(isNull(dim(this(), [s2c_dim:NF]))), seq: False, id: v0}, "^LEI/[A-Z0-9]{20}$")
        //So the term may need the value of the fact, the metric of the fact, a dim of the fact. 
        //The term may have a filter which may have a match inside. this() is ONLY found in filters where there is DIM(

        var qt = "\"";
        var pattern = @$"matches\((.*?)\s*,\s*\{qt}(.*)\{qt}\)";
        var regex = new Regex(pattern);
        var match = regex.Match(text);
        if (!match.Success)
        {
            throw new InvalidOperationException($"invalid match:{text} ");
        }

        object? termValue ;

        //match function may check for dim 
        var leftPart = match.Groups[1].Value; //dim(this(X00), [s2c_dim:UI]) OR X00
        var hasDimOnTheLeft = leftPart.Contains("dim");
        if(hasDimOnTheLeft )
        {
            //*** you are in a filter. Do NOT check for filter since you are using the same term
            //find the  dim
            termValue = ExtractDimValueFormFact(leftPart,terms);            
            //check the match
        }
        else
        {
            //**check first the filter
            var termLeft= terms[leftPart];
            var filter = termLeft.Filter;
            if (!string.IsNullOrEmpty(filter))
            {
                var isFilterValid = ExpressionEvaluator.EvaluateGeneralBooleanExpression(0, filter, terms);
                //if the filter is false, no need to check the rule and return a match
                if ( isFilterValid != KleeneValue.True)
                {                    
                    return true;
                };                
            }            
            termValue = termLeft.Obj;
        }
                
        if(termValue is null)
        {
            //match could return a kleene value?
            return true;
        }
        

        var rgxFromValue = match.Groups[2].Value.Replace(@"/", @"\/"); // ^CAU/(ISIN/.*)=>"^CAU\/(ISIN\/.*)         
        if (rgxFromValue.Contains("ISO 8601") && termValue is DateTime){
            return true; //since termValue is of type DateTime, it means it was processed successfully and we do not care about formatting
        }

        var rgx = new Regex(rgxFromValue, RegexOptions.IgnoreCase);
        var matchValidation = rgx.Match((string)termValue!);
        return matchValidation.Success;
    }


    public static string ExtractDimValueFormFact(string dimText,  Dictionary<string, ObjectTerm280> terms)
    {
        //if you find this()=> the term must be dimTerm
        //dim(this(X00), [s2c_dim:UI])
        //dim(X00, [s2c_dim:UI])
        //MET(s2md_met:si1554)|s2c_dim:SU(s2c_MC:x168)|s2c_dim:UI(ID:CAU/INST/1888-1891 LX64W1_IPROP)  //this i do NOT process
        //2. inside filter: matches(dim(this(), [s2c_dim:UI]), "^CAU/.*")  //this one is only found inside a filter

        var rgxFilterDim = new Regex(@"dim\((.*),\s*\[(.*)\]\)"); //=> X00 or this(X00) , s2c_dim:UI
        var matchDim = rgxFilterDim.Match(dimText);
        if (!matchDim.Success)
        {
            return "";
        }
        
        //the term 
        var theTerm = matchDim.Groups[1].Value;//=> X00 or this(X00)
        var rgxThis = new Regex(@"this\((.*?)\)");
        var matchThisInside= rgxThis.Match(theTerm);
        var term = matchThisInside.Success
            ? matchThisInside.Groups[1].Value
            : theTerm;        
        
        if (!terms.ContainsKey(term))
        {
            throw (new Exception($"Invalid dimfunction: {dimText}. Cannot find term :{term}"));
        }
        
        var dataSignature = terms[term].fact?.DataPointSignature ?? "";
        var rgxPattern = @$"{matchDim.Groups[2].Value.Trim()}\(\w\w:(.*?)\)"; 
        var rgxFactDim = new Regex( rgxPattern); //rgxFactDim = //s2c_dim:UI\(\w\w:(.*?)\)
        var matchDimValue = rgxFactDim.Match(dataSignature); //s2c_dim:UI(ID:CAU/INST/1888-1891 LX64W1_IPROP) => CAU/INST/1888-1891 LX64W1_IPROP

        if (!matchDimValue.Success)
        {
            return "";
        }
        return matchDimValue.Groups[1].Value;

    }

    


    public static bool ValidateIsNull(string symbolFormula, Dictionary<string, ObjectTerm280> terms)
    {
        var rgxNull = new Regex(@"is[Nn]ull\((.*)\)");
        var match = (rgxNull.Match(symbolFormula));
        if (!match.Success)
        {
            throw new Exception($"ValidateIsNull function cannot parse the formule :{symbolFormula} ");
        }
        var zetRc = terms[match.Groups[1].Value];
        //var objTostr = zetObj?.Obj.ToString() ?? "";
        var objTostr = zetRc?.Obj?.ToString() ?? "";
        var isNull = (zetRc is null || (zetRc?.IsNullFact ?? true) || (objTostr == "emptySequence()"));

        return isNull;

    }





}
