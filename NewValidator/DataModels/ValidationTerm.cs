namespace NewValidator.DataModels;
using Shared.DataModels;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.Implementation.Collections.Grouping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


public record struct TermPairSplit(string Key, string Value);
//public enum TermKey { Tab, Zet, Row, Col, Met }
//public record TermPair(TermKey Key, string Value);
public record ValidationRecord
{
    //{t: S.01.01.07.01, r: R0540, c: C0010}
    //{t: S.01.01.07.01, r: R0540, c: C0010, dv: [Default], seq: False, id: v1, f: solvency, fv: solvency2}
    //{ m: [s2md_met:ei1024], seq: False, id: v0} 
    public string Table { get; set; }
    public string Zet { get; set; } 
    public string Row { get; set; }
    public string Col { get; set; }
    public string Dim { get; set; }
    public string Metric { get; set; }
    public string Solv { get; set; }
    public string Solv2 { get; set; }
    public string Fa { get; set; }
    public string Fv { get; set; }
    public string Dv { get; set; }
    public string Id { get; set; }
    public bool IsSeq { get; set; }

    static public List<TermPairSplit> SplitTerm(string text)
    {
        var rgxPair = new Regex(@"(\w{1,3}):\s*?(.*)");
        var rgxTerm = new Regex(@"^\{(.*)\}", RegexOptions.Compiled);
        var match = rgxTerm.Match(text);
        if (!match.Success) return new();


        var cleanText = match.Groups[1].Value;
        var terms = cleanText.Split(",").Select(term =>
        {
            var pair = term.Split(":", StringSplitOptions.RemoveEmptyEntries);
            TermPairSplit res = pair.Length == 2 ? new TermPairSplit(pair[0].Trim(), pair[1].Trim()) : new();
            return res;
        })
        .Where(pair => (!string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value)))
        .ToList();
        return terms;
        //return new ValidationRecord { Table = text, Zet = text, Row = text, Col = text, Solvency = text, };
    }


    public static ValidationRecord? CreateValidationRecord(string text)
    {
        var pairs = SplitTerm(text);
        string zet="", table="", row="", col="", dim="" ,metric="", fa="",fv="" ,dv="",id="";
        bool isSeq = false;
        
        foreach (var pair in pairs)
        {
            switch (pair.Key.ToLower()) // Case-insensitive comparison
            {
                case "z": zet = pair.Value; break;
                case "t": table = pair.Value; break;
                case "r": row = pair.Value; break;
                case "c": col = pair.Value; break;
                //case "s": solv= pair.Value; break;
                case "dim": dim = pair.Value; break;
                case "m": metric = pair.Value; break;
                case "seq": isSeq =  pair.Value=="True"; break;
                case "f": solv = pair.Value; break;
                case "fv": fv = pair.Value; break;
                case "dv": dv = pair.Value; break;
                case "id": id = pair.Value; break;
                default: throw new Exception($"unkown key {pair.Key}");
            }
        }
        var rec = new ValidationRecord()
        {
            Zet = zet,
            Table = table,
            Row = row,
            Col = col,
            Solv = solv,            
            Solv2 = solv2,
            Dim = dim,
            Metric = metric,
            Fa = fa,    
            Fv = fv,
            Id
            IsSeq = isSeq


        };
        return rec;
        
    }
}