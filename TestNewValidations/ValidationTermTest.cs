namespace TestNewValidations;

using NewValidator.Common.FunctionalRoutines;

public class ValidationTermTest
{

    [Fact]
    public void TestRuleComponent()
    {
        var text = @"aa and bb or cc and ee";
        var record = RuleComponent.ParseComponent(text);
        Assert.Equal("X00 and X01 or X02 and X03", record.componentFormula);
        Assert.Equal("aa", record.expressions["X00"]);

        text = @"bc and aa";
        record = RuleComponent.ParseComponent(text);
        Assert.Equal("X00 and X01", record.componentFormula);
        Assert.Equal("bc", record.expressions["X00"]);

        text = @"aa vv aa";
        record = RuleComponent.ParseComponent(text);
        Assert.Equal("X00", record.componentFormula);
        Assert.Equal("aa vv aa", record.expressions["X00"]);
    }



    [Fact]
    public void TestIfThenElse()
    {
        var text = @"if a then b else c";
        var record = RuleStructure280.SplitIfThenElse(text);
        string[] expectedValues = { "a", "b", "c" };
        string[] actualValues = { record.ifExpression, record.thenExpression, record.elseExpression };
        Assert.Equal(expectedValues, actualValues);


        text = @"abc";
        record = RuleStructure280.SplitIfThenElse(text);
        string[] expectedValues1 = { "abc","","" };
        string[] actualValues1 = { record.ifExpression, record.thenExpression, record.elseExpression };
        Assert.Equal(expectedValues1, actualValues1);
        Assert.True(record.isIfExpression);

        text = @"if a ";
        record = RuleStructure280.SplitIfThenElse(text);
         string[] expectedValues2 = { "if a", "", "" };
        string[] actualValues2 = { record.ifExpression, record.thenExpression, record.elseExpression };
        Assert.Equal(expectedValues2, actualValues2);
        Assert.True(record.isIfExpression);
    }


    [Fact]
    public void TestParser()
    {
        var text = @"{t: S.02.01.07.01, r: R0690, dv: 0, seq: False, id: v4, f: solvency, fv: solvency2}";
        var record = RuleTerm280.CreateValidationRecord(text);
        string[] expectedValues = { "S.02.01.07.01", "", "R0690", "" };
        string[] actualValues = { record.Table, record.Zet, record.Row, record.Col };

        Assert.Equal(expectedValues, actualValues);


        var text2 = @"{t: S.23.01.05.01, r: R0570, z: Z0001, dv: 0, seq: False, id: v1, f: solvency, fv: solvency2}";
        record = RuleTerm280.CreateValidationRecord(text2);
        expectedValues = new string[] { "S.23.01.05.01", "Z0001", "R0570", ""
           ,"v1" , "solvency", "solvency2",  };
        actualValues = new string[] { record.Table, record.Zet, record.Row, record.Col
            ,record.Id ,record.F, record.Fv  };
        Assert.Equal(expectedValues, actualValues);
    }



}