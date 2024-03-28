// See https://aka.ms/new-console-template for more information
using System.Text.RegularExpressions;
using Shared.SpecialRoutines;
using System.Linq;
using NewValidator.ValidationClasses;




var inputfile = "C:\\Users\\kyrlo\\soft\\dotnet\\insurance-project\\TestingXbrl280\\ShortLabels.txt";
var outputfile = "C:\\Users\\kyrlo\\soft\\dotnet\\insurance-project\\TestingXbrl280\\ShortLabelsSql.txt";
NewValidator.DocumentValidator.UpdateExpressionWithShortLabel(inputfile, outputfile);
return;
var zz1 = "abc (acx or x3) or imin(x1>x3 and x3<x1) + x3";

//655	BV780-5	matches({t: S.06.02.01.02, c: C0290, z: Z0001, filter: matches(dim(this(), [s2c_dim:UI]), "^CAU/.*") and not(matches(dim(this(), [s2c_dim:UI]), "^CAU/(ISIN/.*)|(INDEX/.*)")), seq: False, id: v1, f: solvency, fv: solvency2}, "^((XL)|(XT))..$")

string text2 = @"{t: S.06.02.01.02, c: C0290, z: Z0001, filter: matches(dim(this(), [s2c_dim:UI]), ""^CAU/.*"") and not(matches(dim(this(), [s2c_dim:UI]), ""^CAU/(ISIN/.*)|(INDEX/.*)"")), seq: False, id: v1, f: solvency, fv: solvency2}";

var res33 = FormulaSimplification.Simplify(text2);


var terms = new List<(string, string)>(){ ( "Z01", "cc" ),("Z00","bbb") };
var fomula = "match(Z00,X1) and filter(Z01,C3) ";
var newFormula = FormulaSimplification.ReplaceTerms(fomula, terms);

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


