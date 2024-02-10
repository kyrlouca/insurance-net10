using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DataModels;



public class MDimensionModel
{
    public int DimensionID { get; set; }
    public string DimensionLabel { get; set; } = "";
    public string DimensionCode { get; set; } = "";
    public string DimensionDescription { get; set; } = "";
    public string DimensionXBRLCode { get; set; } = "";
    public int DomainID { get; set; }
    public bool IsTypedDimension { get; set; }
    public int ConceptID { get; set; }
}
