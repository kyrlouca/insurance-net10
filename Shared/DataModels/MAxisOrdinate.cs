using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DataModels;
public class MAxisOrdinate
{
    public int AxisID { get; set; }
    public int OrdinateID { get; set; }
    public string OrdinateLabel { get; set; } = "";
    public string OrdinateCode { get; set; } = "";
    public bool IsDisplayBeforeChildren { get; set; }
    public bool IsAbstractHeader { get; set; }
    public bool IsRowKey { get; set; }
    public int Level { get; set; }
    public int Order { get; set; }
    public int ParentOrdinateID { get; set; }
    public int ConceptID { get; set; }
    public string TypeOfKey { get; set; } = "";
}