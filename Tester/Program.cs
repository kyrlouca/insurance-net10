// See https://aka.ms/new-console-template for more information
using System.Text.RegularExpressions;
using Shared.SpecialRoutines;
using System.Linq;
using NewValidator.ValidationClasses;



var left = 22.1;
var leftDecimals = 2;
var x2x = 1 / Math.Pow(10, leftDecimals) / 2;
var rightSide = (left + 1 / Math.Pow(10, leftDecimals) / 2);

var xminus3 = 1 / Math.Pow(10, -3) / 2;
//var zz1 = RuleComponent280.CreateComponent("a");

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


