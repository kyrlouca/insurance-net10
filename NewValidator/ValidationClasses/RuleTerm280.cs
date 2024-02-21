namespace NewValidator.ValidationClasses;
using Shared.DataModels;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.Implementation.Collections.Grouping;
using Syncfusion.XlsIO.Implementation.XmlSerialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


public enum ValueType { IsNumber, IsString, IsBoolean };
public record struct TermPairSplit(string Key, string Value);
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

    public string F { get; set; } = "";
    public string Fv { get; set; } = "";
    public string Dv { get; set; } = "";
    public string Seq { get; set; } = "";
    public bool IsSeq { get; set; }

    static public List<TermPairSplit> SplitAttribute(string text)
    {

        //split the pairs and TitleCase the key
        //{t: S.01.01.07.01, r: R0540, c: C0010}=> { {"T":"S.01.01.07.01"},{"R","R0540"}..
        TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
        var rgxPair = new Regex(@"(\w{1,3}):\s*?(.*)");
        var rgxTerm = new Regex(@"^\{(.*)\}", RegexOptions.Compiled);
        var match = rgxTerm.Match(text);
        if (!match.Success) return new();


        var cleanText = match.Groups[1].Value;
        var terms = cleanText.Split(",").Select(term =>
        {
            var pair = term.Split(":", StringSplitOptions.RemoveEmptyEntries);
            //TermPairSplit res = pair.Length == 2 ? new TermPairSplit(textInfo.ToTitleCase( pair[0].Trim()), pair[1].Trim()) : new();
            TermPairSplit res = pair.Length == 2 ? new TermPairSplit(pair[0].Trim(), pair[1].Trim()) : new();
            return res;
        })
        .Where(pair => !string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value))
        .ToList();
        return terms;
        //return new ValidationRecord { Table = text, Zet = text, Row = text, Col = text, Solvency = text, };
    }

    public static RuleTerm280? CreateRawTerm(string letter, string termText)
    {
        var rec = new RuleTerm280()
        {
            Letter = letter,
            TermText = termText

        };
        return rec;
    }

    public static RuleTerm280? CreateRuleTerm280(string letter, string text)
    {
        //use reflection to update the fields of the record

        var record = new RuleTerm280();
        record.Letter = letter;
        var recordType = record.GetType();

        var pairs = SplitAttribute(text);
        foreach (var pair in pairs)
        {
            var fieldInfo = recordType.GetProperty(pair.Key, BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
            if (fieldInfo is null)
            {
                throw new Exception($"Property of RuleTerm does NOT exist: {pair.Key}");
                continue;
            }
            fieldInfo.SetValue(record, pair.Value);
        }
        return record;
    }
}
