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
        

        text = @"if a ";
        record = RuleStructure280.SplitIfThenElse(text);
         string[] expectedValues2 = { "if a", "", "" };
        string[] actualValues2 = { record.ifExpression, record.thenExpression, record.elseExpression };
        Assert.Equal(expectedValues2, actualValues2);
        
    }
    
    [Fact]
    public void TestRuleTerm()
    {
        var text = @"{t: S.02.01.07.01, r: R0690, dv: 0, seq: False, id: v4, f: solvency, fv: solvency2}";
        //var record = RuleTerm280.CreateRuleTerm(text);
        var record = RuleExpressionTerm280.CreateRuleExpressionTerm(text);
        string[] expectedValues = { "S.02.01.07.01", "", "R0690", "" };
        string[] actualValues = { record.T, record.Z, record.R, record.C };

        Assert.Equal(expectedValues, actualValues);


        var text2 = @"{t: S.23.01.05.01, r: R0570, z: Z0001, dv: 0, seq: False, id: v1, f: solvency, fv: solvency2}";
        record = RuleExpressionTerm280.CreateRuleExpressionTerm(text2);
        expectedValues = new string[] { "S.23.01.05.01", "Z0001", "R0570", ""
           ,"v1" , "solvency", "solvency2",  };
        actualValues = new string[] { record.T, record.Z, record.R, record.C
            ,record.Id ,record.F, record.Fv  };
        Assert.Equal(expectedValues, actualValues);
    }

    [Fact]
    public void TestRuleStructure()
    {
        var text = @"if not(isNull({d: [s2c_dim:LG], filter:dim(this(), [s2c_dim:LG]) = [s2c_GA:x113], seq: False, id: v0})) then false() else true()";
        var record = RuleStructure280.CreateRuleStructure(text);        
        var expectedVal = "not(isNull({d: [s2c_dim:LG], filter:dim(this(), [s2c_dim:LG]) = [s2c_GA:x113], seq: False, id: v0}))";
        Assert.Equal(expectedVal, record.IfComponent.ComponentFormula);
        Assert.False(record.IsPlainRule);

        text = "isNull({t: T.99.01.01.01, c: C0100, seq: False, id: v0, f: solvency, fv: solvency2})";
        record = RuleStructure280.CreateRuleStructure(text);
        expectedVal = "isNull({t: T.99.01.01.01, c: C0100, seq: False, id: v0, f: solvency, fv: solvency2})";
        Assert.Equal(expectedVal, record.IfComponent.ComponentFormula);
        Assert.True(record.IsPlainRule);

        text = "not(isNull({t: S.06.02.07.01, c: C0170, z: Z0001, dv: emptySequence(), seq: False, id: v1, f: solvency, fv: solvency2})) and not(isNull({t: S.01.03.01.01, c: C0050, dv: emptySequence(), seq: False, id: v2, f: solvency, fv: solvency2}))";
        record = RuleStructure280.CreateRuleStructure(text);
        expectedVal = "not(isNull({t: S.06.02.07.01, c: C0170, z: Z0001, dv: emptySequence(), seq: False, id: v1, f: solvency, fv: solvency2})) and not(isNull({t: S.01.03.01.01, c: C0050, dv: emptySequence(), seq: False, id: v2, f: solvency, fv: solvency2}))";
        Assert.Equal(expectedVal, record.IfComponent.ComponentFormula);
        Assert.True(record.IsPlainRule);
        Assert.Equal(2, record.IfComponent.ComponentExpressions.Count);

    }


    [Fact]
    public void TestRuleExpression()
    {
        

        var text = @"not(isNull({d: [s2c_dim:LG], filter:dim(this(), [s2c_dim:LG]) = [s2c_GA:x113], seq: False, id: v0}))";
        var record = RuleExpression.CreateRuleExpression("X0",text);
        var expectedVal = "isNull({d: [s2c_dim:LG], filter:dim(this(), [s2c_dim:LG]) = [s2c_GA:x113], seq: False, id: v0})";
        Assert.Equal(expectedVal, expectedVal);
        Assert.True(record.IsNegative);
        
        text = "(not(isNull({t: S.07.01.01.01, c: C0060, z: Z0001, dv: emptySequence(), seq: False, id: v1, f: solvency, fv: solvency2}))";
        record = RuleExpression.CreateRuleExpression("X1",text);
        expectedVal =     @"{t: S.07.01.01.01, c: C0060, z: Z0001, dv: emptySequence(), seq: False, id: v1, f: solvency, fv: solvency2}";
        Assert.Equal(expectedVal,record.ExpressionText);
        Assert.True(record.IsNegative);
        Assert.Equal( FunctionType.IsNull, record.FunctionType);

        
        text = """matches({t: S.06.02.04.02, c: C0290, z: Z0001, filter: matches(dim(this(), [s2c_dim:UI]), "^CAU/.*") and not(matches(dim(this(), [s2c_dim:UI]), "^CAU/(ISIN/.*)|(INDEX/.*)")), seq: False, id: v1, f: solvency, fv: solvency2}, ^((XL)|(XT))..$"!) """;
        record = RuleExpression.CreateRuleExpression("X1", text);
        expectedVal =  """{t: S.06.02.04.02, c: C0290, z: Z0001, filter: matches(dim(this(), [s2c_dim:UI]), "^CAU/.*") and not(matches(dim(this(), [s2c_dim:UI]), "^CAU/(ISIN/.*)|(INDEX/.*)")), seq: False, id: v1, f: solvency, fv: solvency2}, ^((XL)|(XT))..$"!""";
        Assert.Equal(expectedVal, record.ExpressionText);
        Assert.False(record.IsNegative);
        Assert.Equal( FunctionType.Matches, record.FunctionType);
        

        text = "abc";
        record = RuleExpression.CreateRuleExpression("X2",text);
        expectedVal = "abc";
        Assert.Equal(expectedVal, record.ExpressionText);
        Assert.False(record.IsNegative);


    }


}