namespace Shared.DataModels;
using Shared.GeneralUtils;
using Shared.Various;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper.Contrib.Extensions;

[Table("TemplateSheetFact")]
public class TemplateSheetFactDataModel
{
    [Key]
    public int FactId { get; set; }
    public int TemplateSheetId { get; set; }
    public string Row { get; set; } = "";
    public string Col { get; set; }="";
    public string Zet { get; set; } = "";
    public string RowForeign { get; set; } = "";

    public int InternalRow { get; set; }
    public int InternalCol { get; set; }
    public string DataPointSignature { get; set; } = "";
    public string DataPointSignatureFilled { get; set; } = "";
    public string OpenRowSignature { get; set; } = "";
    public string Unit { get; set; } = "";
    public int Decimals { get; set; }
    public double NumericValue { get; set; }
    public DateTime DateTimeValue { get; set; }
    public bool BooleanValue { get; set; }
    public string TextValue { get; set; } = "";
    public string DPS { get; set; } = "";
    public int CellID { get; set; }
    public string FieldOrigin { get; set; } = "";
    public int TableID { get; set; }
    public bool IsRowKey { get; set; }
    public bool IsShaded { get; set; }
    public string XBRLCode { get; set; } = "";
    public string DataType { get; set; } = "";
    public string DataTypeUse { get; set; } = "";
    [Computed]
    public string SheetCode { get; set; } = "";
    public bool IsEmpty { get; set; }
    public bool IsConversionError { get; set; }
    public string ZetValues { get; set; } = "";
    public string CurrencyDim { get; set; } = "";


    public int MetricID { get; set; }
    public string ContextId { get; set; } = "";
    public int ContextNumberId { get; set; }

    public string Signature { get; set; } = "";
    public string RowSignature { get; set; } = "";  
    public int InstanceId { get; set; }
    [Computed]
    public string? TableCodeDerived { get; set; }


}
 