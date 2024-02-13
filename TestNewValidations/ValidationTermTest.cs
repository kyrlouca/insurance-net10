namespace TestNewValidations;
using  NewValidator.DataModels;

public class ValidationTermTest
{
    [Fact]
    public void TestParser()
    {
        var text = @"{t: S.02.01.07.01, r: R0690, dv: 0, seq: False, id: v4, f: solvency, fv: solvency2}";
        var record = ValidationRecord.CreateValidationRecord(text);
        string[] expectedValues = { "S.02.01.07.01","", "R0690", "" };
        string[] actualValues = { record.Table, record.Zet, record.Row, record.Col };

        Assert.Equal(expectedValues, actualValues);
        
    }



}