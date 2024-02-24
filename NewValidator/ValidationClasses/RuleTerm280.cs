namespace NewValidator.ValidationClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;


public enum ValueType { Number, String, Boolean,Date,Integer };
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

    public string ValueStr { get; set; }
    public double ValueDecimal { get; set; }
    public DateOnly ValueDate { get; set; }
    public bool ValueBoolean { get; set; }
    public int ValueInt { get; set; }
    public ValueType ValueType { get; set; }
    public string T { get; set; } = "";
    public string Z { get; set; } = string.Empty;
    public string R { get; set; } = "";
    public string C { get; set; } = "";
    public string Id { get; set; } = "";

    public string Dim { get; set; } = "";
    public string Metric { get; set; } = "";

    public string F { get; set; } = "";
    public string Fv { get; set; } = "";
    public string Dv { get; set; } = "";
    public string Seq { get; set; } = "";
    public bool IsSeq { get; set; }
    public bool IsTolerance { get; set; }

    public static RuleTerm280? CreateRuleTerm280(string letter, string text)
    {
        //use reflection to update the fields of the record

        var record = new RuleTerm280();
        record.Letter = letter;
        var recordType = record.GetType();

        var pairs = CreateTermAttributes(text);
        foreach (var pair in pairs)
        {
            var fieldInfo = recordType.GetProperty(pair.Key, BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
            if (fieldInfo is null)
            {
                throw new Exception($"Property of RuleTerm does NOT exist: {pair.Key}");             
            }
            fieldInfo.SetValue(record, pair.Value);
        }
        return record;
    }

    static public List<TermAttribute> CreateTermAttributes(string text)
    {

        //split each pair (ex. r: R0540) to create term attribute :  key and value
        //{t: S.01.01.07.01, r: R0540, c: C0010}=> { {"T":"S.01.01.07.01"},{"R","R0540"}..        
        //will also fill the isTolerance at the end
        var rgxPair = new Regex(@"(\w{1,3}):\s*?(.*)");
        var rgxTerm = new Regex(@"^\{(.*)\}", RegexOptions.Compiled);
        var match = rgxTerm.Match(text);
        if (!match.Success) return new();

        var cleanText = match.Groups[1].Value;
        var terms = cleanText.Split(",").Select(term =>
        {
            var pair = term.Split(":", StringSplitOptions.RemoveEmptyEntries);            
            TermAttribute res = pair.Length == 2 ? new TermAttribute(pair[0].Trim(), pair[1].Trim()) : new TermAttribute("","") ;
            return res;
        })
        .Where(pair => !string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value))
        .ToList();

        //{t: S.02.01.02.01, r: R0100, c: C0010, dv: 0, seq: False, id: v1, f: solvency, fv: solvency2} i => check the i for Tolerance
        var rgxTermi = new Regex(@"\{.*?\}( i)");
        var matchi = rgxTermi.Match(text);
        var isTolerance = matchi.Success? "Y" : "N";
        terms.RemoveAt(terms.FindIndex(itm => itm.Key == "IsTolerance"));
        terms.Add(new TermAttribute("IsTolerance", isTolerance));

        return terms;        
    }

}
