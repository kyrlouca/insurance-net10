// See https://aka.ms/new-console-template for more information
using System.Text.RegularExpressions;
using Shared.SpecialRoutines;
using NewValidator;
using NewValidator.DataModels;
using System.Linq;

var text = @"{t: S.01.01.07.01, r: R0540, c: C0010}";
var xx1 = ValidationRecord.ParseValidationRecord(text);

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


