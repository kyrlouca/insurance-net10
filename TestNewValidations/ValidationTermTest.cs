namespace TestNewValidations;

using NewValidator.ValidationClasses;
using Shared.SpecialRoutines;

public class ValidationTermTest
{
   

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
        var record = RuleTerm280.CreateRuleTerm280("X0",text);
        string[] expectedValues = { "S.02.01.07.01", "", "R0690", "" };
        string[] actualValues = { record.T, record.Z, record.R, record.C };

        Assert.Equal(expectedValues, actualValues);


        var text2 = @"{t: S.23.01.05.01, r: R0570, z: Z0001, dv: 0, seq: False, id: v1, f: solvency, fv: solvency2}";
        record = RuleTerm280.CreateRuleTerm280("X0",text2);
        expectedValues = new string[] { "S.23.01.05.01", "Z0001", "R0570", ""
           ,"v1" , "solvency", "solvency2",  };
        actualValues = new string[] { record.T, record.Z, record.R, record.C
            ,record.Id ,record.F, record.Fv  };
        Assert.Equal(expectedValues, actualValues);
    }

    [Fact]
    public void TestRuleStructure()
    {
        //var text0 = "{t: S.02.01.01.01, r: R0210, c: C0010, dv: 0, seq: False, id: v1, f: solvency, fv: solvency2} i= isum({t: S.06.02.01.01, c: C0170, z: Z0001, dv: emptySequence(), seq: True, id: v2, f: solvency, fv: solvency2})";
        //var record0 = RuleStructure280.CreateRuleStructure(text0);
        //var expectedVal0 = "isNull({t: T.99.01.01.01, c: C0100, seq: False, id: v0, f: solvency, fv: solvency2})";




        //var text = @"if not(isNull({d: [s2c_dim:LG], filter:dim(this(), [s2c_dim:LG]) = [s2c_GA:x113], seq: False, id: v0})) then false() else true()";
        //var record = RuleStructure280.CreateRuleStructure(text);        
        //var expectedVal = "not(isNull({d: [s2c_dim:LG], filter:dim(this(), [s2c_dim:LG]) = [s2c_GA:x113], seq: False, id: v0}))";
        



        //text = "isNull({t: T.99.01.01.01, c: C0100, seq: False, id: v0, f: solvency, fv: solvency2})";
        //record = RuleStructure280.CreateRuleStructure(text);
        //expectedVal = "isNull({t: T.99.01.01.01, c: C0100, seq: False, id: v0, f: solvency, fv: solvency2})";
        

        //text = "not(isNull({t: S.06.02.07.01, c: C0170, z: Z0001, dv: emptySequence(), seq: False, id: v1, f: solvency, fv: solvency2})) and not(isNull({t: S.01.03.01.01, c: C0050, dv: emptySequence(), seq: False, id: v2, f: solvency, fv: solvency2}))";
        //record = RuleStructure280.CreateRuleStructure(text);
        //expectedVal = "not(isNull({t: S.06.02.07.01, c: C0170, z: Z0001, dv: emptySequence(), seq: False, id: v1, f: solvency, fv: solvency2})) and not(isNull({t: S.01.03.01.01, c: C0050, dv: emptySequence(), seq: False, id: v2, f: solvency, fv: solvency2}))";
        
    }



    [Fact]
    public void TestEvaluateGeneralBooleanExpression()
    {

        var text = @"5>2 and 4>3";
        var res = ExpressionEvaluator.EvaluateGeneralBooleanExpression(0,text,new());
        Assert.True(res==KleeneValue.True);

        text = @"(2>1 or 1<2) and (2>1)";
        res = ExpressionEvaluator.EvaluateGeneralBooleanExpression(0, text,new());
        Assert.True(res == KleeneValue.True);

        text = @"(2>1 or 1<2) and (1>2)";
        res = ExpressionEvaluator.EvaluateGeneralBooleanExpression(0, text, new());
        Assert.False(res == KleeneValue.False);

        text = @"(2>1 or 1<2) and not(1>2)";
        res = ExpressionEvaluator.EvaluateGeneralBooleanExpression(0,text, new());
        Assert.True(res == KleeneValue.True);

        var qt = "\"";
        var x = "{";
        var y = "}";

        //text = @"(1>2 or matches(""LEI/12301"", ""^LEI/[A-Z0-9]{3}(01|00)$"")) and not(1>2)";
        text = @$"(1>2 or matches({qt}LEI/12301{qt}, {qt}^LEI/[A-Z0-9]{x}3{y}(01|00)${qt})) and not(1>2)";
        res = ExpressionEvaluator.EvaluateGeneralBooleanExpression(0, text, new());
        Assert.True(res == KleeneValue.True);


        text = @$"(1>2 or matches({qt}LEI/12301{qt}, {qt}^LEI/[A-Z0-9]{x}3{y}(01|00)${qt})) and (matches({qt}Lei248{qt},{qt}Lei\d\d\d{qt}))";
        res = ExpressionEvaluator.EvaluateGeneralBooleanExpression(0,text, new());
        Assert.True(res == KleeneValue.True);

    }



    [Fact]
    public void TestEvalFunction()
    {

        var text = @"imin(3, 4, 1 +1)";
        var res = ExpressionEvaluator.EvaluateFunction(text, new(), "");
        Assert.Equal(new OptionialObject(false, 2), res);


        text = @"imax(imin(3, 7) , 4) ";
        res = ExpressionEvaluator.EvaluateFunction(text, new(), "");
        Assert.Equal(false, res.IsNull);
        Assert.Equal(4, res.Value);
        


    }



    [Fact]
    public void TestArithmetic()
    {

        var text = @"5 + imin(3) +imax(4)";
        var res = ExpressionEvaluator.EvaluateGeneralExpressionRecursively(text, new(),"");
        Assert.Equal(12, res.Value);


        text = @"7 + imin(imax(3,5),4)";
        res = ExpressionEvaluator.EvaluateGeneralExpressionRecursively(text, new(), "");
        Assert.Equal(11, res.Value);


    }

    [Fact]
    public void TestSplitAndOrExpression()
    {

        var text = @"5 + imin(3)+(a>3 or b<4) and imax(4+x3)> 5";
        var res = ExpressionEvaluator.SplitAndOrExpression(text);
        Assert.Equal(res.logicalOperator, ExpressionEvaluator.LogicalOperators.IsAnd);
        Assert.Equal(res.left, "5 + imin(3)+(a>3 or b<4)");
        Assert.Equal(res.Right, "imax(4+x3)> 5");


        text = @"5 + imin(3)+(a>3 and b<4) or imax(4+x3)> 5";
        res = ExpressionEvaluator.SplitAndOrExpression(text);
        Assert.Equal(res.logicalOperator, ExpressionEvaluator.LogicalOperators.IsOR);
        Assert.Equal(res.left, "5 + imin(3)+(a>3 and b<4)");
        Assert.Equal(res.Right, "imax(4+x3)> 5");

        text = @"5 + imin(3)+ imax(4+x3)> 5";
        res = ExpressionEvaluator.SplitAndOrExpression(text);
        Assert.Equal(res.logicalOperator, ExpressionEvaluator.LogicalOperators.None);
        Assert.Equal(res.left, "5 + imin(3)+ imax(4+x3)> 5");
        Assert.Equal(res.Right, "");



    }



    [Fact]
    public void TestSplitPlusOrMinusArithmetic()
    {

        var text = @"5 - 4 + (3 * X2)";
        var res = ExpressionEvaluator.SplitArithmeticExpression(text);
        Assert.Equal(res.arithmeticOperator, ExpressionEvaluator.ArithmeticOperators.Minus);
        Assert.Equal(res.left, "5");
        Assert.Equal(res.right, "4 + (3 * X2)");

        text = @"5 - 4 * X1 + (3 * X2)";
        res = ExpressionEvaluator.SplitArithmeticExpression(text);
        Assert.Equal(res.arithmeticOperator, ExpressionEvaluator.ArithmeticOperators.Multiply);
        Assert.Equal(res.left, "5 - 4");
        Assert.Equal(res.right, "X1 + (3 * X2)");

        text = @"X3";
        res = ExpressionEvaluator.SplitArithmeticExpression(text);
        Assert.Equal(res.arithmeticOperator, ExpressionEvaluator.ArithmeticOperators.None);
        Assert.Equal(res.left, "X3");
        Assert.Equal(res.right, "");



    }

    [Fact]
    public void TestEvaluateArithmeticExpression()
    {

        var text0 = @"3 + imin(4,3,2) * 7";
        var res0 = ExpressionEvaluator.EvaluateGeneralExpressionRecursively(text0, new(), "");
        Assert.True(res0.IsNull == false);
        Assert.True((double)res0.Value == 17);


        var text = @"3 * (2+4)";
        var res = ExpressionEvaluator.EvaluateGeneralExpressionRecursively(text, new(), "");
        Assert.True(res.IsNull == false);
        Assert.True((double)res.Value == 18);

        text = @"3 + (2 * 4 -7)";
        res = ExpressionEvaluator.EvaluateGeneralExpressionRecursively(text, new(), "");
        Assert.True(res.IsNull == false);
        Assert.True((double)res.Value == 4);

        string text2 = @"{t: S.06.02.01.02, c: C0290, z: Z0001, filter: matches(dim(this(), [s2c_dim:UI]), ""^CAU/.*"") and not(matches(dim(this(), [s2c_dim:UI]), ""^CAU/(ISIN/.*)|(INDEX/.*)"")), seq: False, id: v1, f: solvency, fv: solvency2}";
    }


    [Fact]
    public void TestSimplifyFormula()
    {

        var text3 = @"(isum({t: S.06.02.01.01, c: C0170, z: Z0001, seq: True, id: v1, f: solvency, fv: solvency2}) i> (0.3 i* ({t: S.02.01.02.01, r: R0070, c: C0010, dv: 0, seq: False, id: v2, f: solvency, fv: solvency2} i+ {t: S.02.01.02.01, r: R0220, c: C0010, dv: 0, seq: False, id: v3, f: solvency, fv: solvency2})) and {t: S.02.01.02.01, r: R0070, c: C0010, dv: 0, seq: False, id: v2, f: solvency, fv: solvency2} != 0 and {t: S.02.01.02.01, r: R0220, c: C0010, dv: 0, seq: False, id: v3, f: solvency, fv: solvency2} != 0)";
        var res3 = FormulaSimplification.Simplify(text3);


        var text2 = @"{t: S.06.02.01.01, c: C0130, z: Z0001, dv: emptySequence(), seq: False, id: v1, f: solvency, fv: solvency2}";
        var res2 = FormulaSimplification.Simplify(text2);
        Assert.Equal(@"{t: S.06.02.01.01, c: C0130, z: Z0001, dv: emptySequence(), seq: False, id: v1, f: solvency, fv: solvency2}", res2.Formula);

        var text1 = "m:x3 and x=33";
        var res1 = FormulaSimplification.Simplify(text1);
        Assert.Equal("m:x3 and x=33", res1.Formula );
        Assert.Empty(res1.FormulaTerms);
        
        //put it back and check if like origingl
        var finalText1 = FormulaSimplification.ReplaceTerms(res1.Formula, res1.FormulaTerms);
        Assert.Equal( text1, finalText1);
        

        string text0 = @"{t: S.06.02.01.02, c: C0290, z: Z0001, filter: matches(dim(this(), [s2c_dim:UI]), ""^CAU/.*"") and not(matches(dim(this(), [s2c_dim:UI]), ""^CAU/(ISIN/.*)|(INDEX/.*)"")), seq: False, id: v1, f: solvency, fv: solvency2}";        
        var res0 = FormulaSimplification.Simplify(text0);        
        Assert.Equal(res0.Formula, @"{t: S.06.02.01.02, c: C0290, z: Z0001, filter: matches(XYZ00) and not(XYZ01), seq: False, id: v1, f: solvency, fv: solvency2}");
        Assert.Equal(res0.FormulaTerms[0].content, @"dim(this(), [s2c_dim:UI]), ""^CAU/.*""");
        Assert.Equal(res0.FormulaTerms[0].letter, @"XYZ00");

        var finalText0 = FormulaSimplification.ReplaceTerms(res0.Formula, res0.FormulaTerms);
        Assert.Equal(text0, finalText0);

        


    }



}