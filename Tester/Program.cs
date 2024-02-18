// See https://aka.ms/new-console-template for more information
using System.Text.RegularExpressions;
using Shared.SpecialRoutines;
using NewValidator;
using System.Linq;
using NewValidator.Common.FunctionalRoutines;




//var yy1 = EvaluateRuler.EvaluateRule("1 and 2 or  found");
var yy2 = EvaluateRuler.EvaluateRule("found and found or 3");


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


