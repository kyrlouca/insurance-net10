namespace NewValidator.DataModels;
using Shared.DataModels;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public  record struct ValidationRecord
{
    //{t: S.01.01.07.01, r: R0540, c: C0010}
    //{t: S.01.01.07.01, r: R0540, c: C0010, dv: [Default], seq: False, id: v1, f: solvency, fv: solvency2}
    //{ m: [s2md_met:ei1024], seq: False, id: v0} 
    public string Table { get; init; }
    public string Zet { get; init; }
    public string Row { get; init; }
    public string Col { get; init; }

    public string Solvency { get; init; }
    public bool IsSequence { get; init; }

    static public ValidationRecord? ParseValidationRecord(string text)
    {
        var rgxTerm = new Regex(@"^\{.*\}", RegexOptions.Compiled);
        var match = rgxTerm.Match(text);
        if (!match.Success) return null;
        var terms = match.Value.Split(",").Select(term=> term.Split(":"));
        foreach ( var term in terms )
        {
            var x = 3;
        }

        return new ValidationRecord { Table = text, Zet = text, Row = text, Col = text, Solvency = text, };
    }

}