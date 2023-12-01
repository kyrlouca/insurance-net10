namespace Shared.SpecialRoutines;
using Shared.GeneralUtils;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;



public class DimDom
	{


		public string Dim { get; internal set; } = "";//OC
		public string Dom { get; internal set; } = "";//CU
		public string DomAndVal { get; internal set; } = "";//CU:GBP
		public string DomValue { get; internal set; } = "";//USD
		public string DomAndValRaw { get; internal set; } = "";// s2c_CU:USD
		public string Signature { get; internal set; } //"s2c_dim:OC(s2c_CU:GBP)"
		public bool IsWild { get; internal set; } = false;
		public bool IsOptional { get; internal set; } = false;
		private DimDom() { }
		private void GetTheParts()
		{
			//Signature = @"s2c_dim:OC(s2c_CU:USD)";
			//Signature = @"s2c_dim:OC(ID:USD)";
			//Signature = @"s2c_dim:OC(*[xxxx])";            


			var res = RegexUtils.GetRegexSingleMatchManyGroups(@"s2c_dim:(\w\w)\((.*?)\)", Signature);
			if (res.Count != 3)
			{
				return;
			}

			Dim = res[1];
			DomAndValRaw = res[2];
			var domParts = DomAndValRaw.Split(":");
			if (domParts.Length == 2)
			{
				DomAndVal = res[2].Replace("s2c_", "");
				Dom = domParts[0].Replace("s2c_", "");

				DomValue = domParts[1];
			}

			IsWild = Signature.Contains('*');
			IsOptional = Signature.Contains('?');
		}
		private DimDom(string signature)
		{
			Signature = signature;
		}
		public static DimDom GetParts(string signature)
		{
			var dimDom = new DimDom(signature);
			dimDom.GetTheParts();
			return dimDom;
		}


	}

public record RowColRecord(string AddressR1C1, int Row, int Col, int LastRow, int LastCol);
public class NewUtils
{
    public static RowColRecord? CreateRowColRecord(string addreessR1C1)
    {
        //public const string ColRowRegEx = @"[A-Z]{1,3}\d{4}";//c0010, r0010
        var rg = new Regex("R(\\d*)C(\\d*)");
        var match = rg.Matches(addreessR1C1);
        if (match is null)
        {
            return null;
        }
        var row = int.Parse(match[0].Groups[1].Value);
        var col = int.Parse(match[0].Groups[2].Value);

        RowColRecord? rowColRecord = match.Count switch
        {
            1 => new RowColRecord(addreessR1C1, row, col, row, col),
            2 => new RowColRecord(addreessR1C1, row, col, int.Parse(match[1].Groups[1].Value), int.Parse(match[1].Groups[2].Value)),
            _ => null
        };
        return rowColRecord;

    }


}

