namespace NewValidator.ValidationClasses;

using Shared.SpecialRoutines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;


public enum ValueType { Number, String, Boolean, Date, Integer };
public record TermAttribute(string Key, string Value);
//public enum TermKey { Tab, Zet, Row, Col, Met }
//public record TermPair(TermKey Key, string Value);



public record RuleTerm280
{
    //{t: S.01.01.07.01, r: R0540, c: C0010}
    //{t: S.01.01.07.01, r: R0540, c: C0010, dv: [Default], seq: False, id: v1, f: solvency, fv: solvency2}
    //{ m: [s2md_met:ei1024], seq: False, id: v0} 
    public string Letter { get; set; } = "";
    public string TermText { get; set; } = "";    
    public ValueType ValueType { get; set; }
    public string T { get; set; } = "";
    public string Z { get; set; } = string.Empty;
    public string R { get; set; } = "";
    public string C { get; set; } = "";
    public string Id { get; set; } = "";

    public string Dim { get; set; } = "";
    public string Metric { get; set; } = "";
    public string Filter { get; set; } = "";
    public string F { get; set; } = "";
    public string Fv { get; set; } = "";
    public string Dv { get; set; } = "";
    public string Seq { get; set; } = "";
    public string ToleranceChar { get; set; } = "";
    public bool IsSequence { get=>Seq.ToLower()=="true";  }
    public bool IsTolerance { get=>ToleranceChar=="Y"; }

    public static RuleTerm280? CreateRuleTerm280(string letter, string formula)
    {
        //use reflection to update the fields of the record                
        var record = new RuleTerm280();
        record.Letter = letter;
        var recordType = record.GetType();

        //the pairs represent the key/value attributes of the specific ruleTerm
        var propertyPairs = CreateTermProperties(formula);
        foreach (var propertyPair in propertyPairs)
        {
            var keyValue = recordType.GetProperty(propertyPair.Key, BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
            if (keyValue is null)
            {
                throw new Exception($"Property of RuleTerm does NOT exist: {propertyPair.Key}");
            }            
            keyValue.SetValue(record, propertyPair.Value);
        }        
        return record;
    }

    public string ToStringValue()
    {
        var res = $"{Letter}-{T}-{R}-{C}";
        return res;
    }

    static public List<TermAttribute> CreateTermPropertiesOld(string text)
    {

        //split each pair (ex. r: R0540) to create term attribute :  key and value
        //{t: S.01.01.07.01, r: R0540, c: C0010}=> { {"T":"S.01.01.07.01"},{"R","R0540"}..        
        //will also fill the isTolerance at the end
        var rgxPair = new Regex(@"(\w{1,3}):\s*?(.*)", RegexOptions.Compiled);
        var rgxTerm = new Regex(@"^\{(.*)\}", RegexOptions.Compiled);
        var match = rgxTerm.Match(text);
        if (!match.Success) return new();

        var cleanText = match.Groups[1].Value;
        var terms = cleanText.Split(",").Select(term =>
        {
            var pair = term.Split(":", StringSplitOptions.RemoveEmptyEntries);
            TermAttribute res = pair.Length == 2 ? new TermAttribute(pair[0].Trim(), pair[1].Trim()) : new TermAttribute("", "");
            return res;
        })
        .Where(pair => !string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value))
        .ToList();

        //{t: S.02.01.02.01, r: R0100, c: C0010,  dv: 0, seq: False, id: v1, f: solvency, fv: solvency2} i => check the i after the term for Tolerance
        var rgxTermi = new Regex(@"\{.*?\}( i)", RegexOptions.Compiled);
        var matchi = rgxTermi.Match(text);
        var ToleranceChar = matchi.Success ? "Y" : "N";        
        terms.Add(new TermAttribute("ToleranceChar", ToleranceChar));

        return terms;
    }

    static public List<TermAttribute> CreateTermProperties(string termText)
    {

        //split each pair (ex. r: R0540) to create term attribute :  key and value
        //{t: S.01.01.07.01, r: R0540, c: C0010}=> { {"T":"S.01.01.07.01"},{"R","R0540"}..        
        //will also fill the isTolerance at the end

        var (text,xyzTerms) = FormulaSimplification.Simplify(termText);

        var rgxPair = new Regex(@"(\w{1,20}):\s*?(.*)", RegexOptions.Compiled);
        var rgxTerm = new Regex(@"^\{(.*)\}", RegexOptions.Compiled);
        var match = rgxTerm.Match(text);
        if (!match.Success) return new();
        var cleanText = match.Groups[1].Value;


        var pairs = cleanText.Split(",").Select(term =>
        {
            var pair = term.Split(":", StringSplitOptions.RemoveEmptyEntries);
            TermAttribute res = pair.Length == 2 
            ? new TermAttribute(pair[0].Trim(), FormulaSimplification.ReplaceTerms( pair[1].Trim(),xyzTerms)) 
            : new TermAttribute("", "");
            
            return res;
        })
        .Where(pair => !string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value))
        .ToList();
        

        //{t: S.02.01.02.01, r: R0100, c: C0010,  dv: 0, seq: False, id: v1, f: solvency, fv: solvency2} i => check the i after the term for Tolerance
        var rgxTermi = new Regex(@"\{.*?\}( i)", RegexOptions.Compiled);
        var matchi = rgxTermi.Match(text);
        var ToleranceChar = matchi.Success ? "Y" : "N";
        pairs.Add(new TermAttribute("ToleranceChar", ToleranceChar));

        return pairs;
    }

}
