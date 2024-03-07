

using Shared.CommonRoutines;

namespace Validations;

//public enum ValueTypes { String, Numeric, Boolean }
public class DbValue
{
    public int FactId { get; }
    public string TextValue { get;  }
    public double DecimalValue { get; }
    public int NumberOfDecimals { get; }
    public DateTime DateValue { get; }
    public bool BoolValue { get;   }
    public DataTypeMajorUU DataTypeEnumMajorUU  { get; set; }
    public bool IsMissing { get; }

    private  DbValue() { }
    public DbValue(int factId, string textValue, double decimalValue, int numberOfDecimals,DateTime dateValue, bool boolValue,  DataTypeMajorUU dataTypeEnumMajor, bool isMissing)
    {
        FactId = factId;
        TextValue = textValue;
        DecimalValue = decimalValue;
        DecimalValue = decimalValue;
        NumberOfDecimals = numberOfDecimals;
        DateValue = dateValue;
        BoolValue = boolValue;            
        DataTypeEnumMajorUU = dataTypeEnumMajor;
        IsMissing = isMissing;            
    }

}
