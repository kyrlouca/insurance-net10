// See https://aka.ms/new-console-template for more information
using System.Text.RegularExpressions;
using Shared.SpecialRoutines;
using NewValidator;
using System.Linq;
using NewValidator.Common.FunctionalRoutines;



var text = @"{t: S.01.01.07.01, r: R0540, c: C0010}";
//[BV321_1-3];[scope((t: S.02.01.02.01, c:C0010))];[(t: S.02.01.02.01, r: R0510) reported as {$v1} = (t: S.02.01.02.01, r: R0520) reported as {$v2} + (t: S.02.01.02.01, r: R0560) reported as {$v3}]
//if not(isNull({d: [s2c_dim:DZ], filter:dim(this(), [s2c_dim:DZ]) = [s2c_GA:x114], seq: False, id: v0})) then false() else true()
//{t: S.17.03.01.01, r: R0010, dv: 0, seq: False, id: v1, f: solvency, fv: solvency2} i+ {t: S.17.03.01.01, r: R0020, dv: 0, seq: False, id: v2, f: solvency, fv: solvency2} i+ {t: S.17.03.01.01, r: R0030, dv: 0, seq: False, id: v3, f: solvency, fv: solvency2} i+ isum({t: S.17.03.01.02, r: R0100, z: Z0001, dv: emptySequence(), filter: dim(this(), [s2c_dim:TB]) = [s2c_LB:x28], seq: True, id: v4, f: solvency, fv: solvency2}) i<= {t: S.17.01.01.01, r: R0020, dv: 0, seq: False, id: v5, f: solvency, fv: solvency2} i+ {t: S.17.01.01.01, r: R0070, dv: 0, seq: False, id: v6, f: solvency, fv: solvency2} i+ {t: S.17.01.01.01, r: R0170, dv: 0, seq: False, id: v7, f: solvency, fv: solvency2}
//var text = @"{}";
var xx1 = RuleTerm280.CreateRuleTerm(text);

return;

Console.WriteLine("Hello, World!");
var rg = new Regex("(R\\d*)(C\\d*)");
var match = rg.Match("R13C14");
if (match.Success)
{
	Console.WriteLine(match.Groups[0]);
	Console.WriteLine(match.Groups[1]);
}

var rowcol = "C0023";

var xx = DimUtils.CreateRowCol(rowcol);

var y = 5;


