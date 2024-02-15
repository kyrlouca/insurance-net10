namespace TestNewValidations;

using NewValidator.Common.FunctionalRoutines;

public class ValidationTermTest
{
    [Fact]
    public void TestParser()
    {
        var text = @"{t: S.02.01.07.01, r: R0690, dv: 0, seq: False, id: v4, f: solvency, fv: solvency2}";
        var record = ValidationRecord.CreateValidationRecord(text);
        string[] expectedValues = { "S.02.01.07.01", "", "R0690", "" };
        string[] actualValues = { record.Table, record.Zet, record.Row, record.Col };

        Assert.Equal(expectedValues, actualValues);


        var text2 = @"{t: S.23.01.05.01, r: R0570, z: Z0001, dv: 0, seq: False, id: v1, f: solvency, fv: solvency2}";
        record = ValidationRecord.CreateValidationRecord(text2);
        expectedValues = new string[] { "S.23.01.05.01", "Z0001", "R0570", "" 
           ,"v1" , "solvency", "solvency2",  };
        actualValues = new string[] { record.Table, record.Zet, record.Row, record.Col 
            ,record.Id ,record.F, record.Fv  };
        Assert.Equal(expectedValues, actualValues);
    }



}