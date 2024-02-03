using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DataModels;
public class mOrdinateCategorisationDataModel
{
    public int OrdinateID { get; set; }
    public int DimensionID { get; set; }
    public int MemberID { get; set; }
    public string DimensionMemberSignature { get; set; } = "";
    public string Source { get; set; } = "";
    public string DPS { get; set; } = "";
}