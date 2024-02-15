using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewValidator.Common.FunctionalRoutines;

public record RuleParser
{
    public record ValidationRecord
    {
        //{t: S.01.01.07.01, r: R0540, c: C0010}
        //{t: S.01.01.07.01, r: R0540, c: C0010, dv: [Default], seq: False, id: v1, f: solvency, fv: solvency2}
        //{ m: [s2md_met:ei1024], seq: False, id: v0} 
        public string Table { get; set; } = "";
        public string Zet { get; set; } = string.Empty;
        public string Row { get; set; } = "";

    }
}