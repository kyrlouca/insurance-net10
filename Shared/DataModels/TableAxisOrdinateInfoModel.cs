using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DataModels;
public class TableAxisOrdinateInfoModel
{
    
    public int OrdinateID { get; set; }
    public string Col { get; set; } = "";
    public string Signature { get; set; } = "";
    public string AxisOrientation { get; set; } = "";
    public string AxisLabel { get; set; } = "";
    public bool IsRowKey { get; set; }
    public bool IsOpenAxis { get; set; }
    public bool OptionalKey { get; set; }
    
    
    
}