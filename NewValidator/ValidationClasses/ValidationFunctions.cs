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


    public static bool ValidateMatch(string text, Dictionary<string, ObjectTerm280> terms,string filterTerm)
    {
        //matches may have a term with filter, a metric m: with filter , a dim , a term with filter
        //1. matches(X00, "^LEI\/[A-Z0-9]{3}(01|00)$") => X00, "^LEI\/[A-Z0-9]{3}(01|00)$")        
        //2. matches(dim(this(), [s2c_dim:UI]), "^CAU/.*")  //this one is only found inside a filter
        //3. matches({t: S.06.02.07.02, c: C0290, z: Z0001, filter: matches(dim(this(), [s2c_dim:UI]), "^CAU/.*") and not(matches(dim(this(), [s2c_dim:UI]), "^CAU/(ISIN/.*)|(INDEX/.*)")), seq: False, id: v1, f: solvency, fv: solvency2}, "^((XL)|(XT))..$")
        
        //This one is NOT used //XX. matches({ m: [s2md_met:si1558], filter:not(isNull(dim(this(), [s2c_dim:NF]))), seq: False, id: v0}, "^LEI/[A-Z0-9]{20}$")
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

        var leftPartValue = "";

        var leftPart = match.Groups[1].Value; //dim(this(), [s2c_dim:UI]) OR X00
        var isDimLeft = leftPart.Contains("dim");
        if(isDimLeft )
        {
            var dimTerm = terms.FirstOrDefault(tr=>tr.Key==filterTerm);
            leftPartValue = ExtractDimValueFormFact(dimTerm.Value.fact?.DataPointSignature??"",terms,"");
            
        }
        else
        {
            var termLeft= terms[leftPart];
            var filter = termLeft.Filter;
            if (!string.IsNullOrEmpty(filter))
            {
                var isFilterValid = ExpressionEvaluator.EvaluateGeneralBooleanExpression(0, filter, terms, leftPart);
            }
            

            leftPartValue = termLeft?.Obj?.ToString()??"";
        }
                
              

        var rgxFromValue = match.Groups[2].Value.Replace(@"/", @"\/"); // ^CAU/(ISIN/.*)=>"^CAU\/(ISIN\/.*)         
        var rgx = new Regex(rgxFromValue, RegexOptions.IgnoreCase);
        var matchValidation = rgx.Match(leftPartValue);
        return matchValidation.Success;
    }


    public static string ExtractDimValueFormFact(string dimText,  Dictionary<string, ObjectTerm280> terms, string dimTerm)
    {
        //if you find this()=> the term must be dimTerm
        //dim(this(), [s2c_dim:UI])
        //dim(X00, [s2c_dim:UI])
        //MET(s2md_met:si1554)|s2c_dim:SU(s2c_MC:x168)|s2c_dim:UI(ID:CAU/INST/1888-1891 LX64W1_IPROP)  //this i do NOT process

        var rgxFilterDim = new Regex(@"dim\((.*),\s*\[(.*)\]\)");
        var matchDim = rgxFilterDim.Match(dimText);
        if (!matchDim.Success)
        {
            return "";
        }
        var isThisTerm = matchDim.Groups[1].Value.Trim() == "this()";
        var term = isThisTerm 
            ? dimTerm 
            : matchDim.Groups[1].Value.Trim();
        var dim= matchDim.Groups[2].Value.Trim();
        if (!terms.ContainsKey(term))
        {
            throw (new Exception($"Invalid dimFunction : {dimTerm}"));
        }

        var dataSignature = terms[term].fact?.DataPointSignature ?? "";
        var rgxPattern = @$"{matchDim.Groups[2].Value.Trim()}\((.*?)\)";
       
        var rgxFactDim = new Regex( rgxPattern);
        //rgxFactDim = new Regex(@"s2c_dim:NF\((.*?)\)");
        var matchDimValue = rgxFactDim.Match(dataSignature);

        if (!matchDimValue.Success)
        {
            return "";
        }
        return matchDimValue.Groups[1].Value;

    }

    


    public static bool ValidateIsNull(string symbolFormula, Dictionary<string, ObjectTerm280> terms)
    {
        var rgxNull = new Regex(@"isNull\((.*)\)");
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
