namespace TestNewValidations;

using NewValidator.ValidationClasses;
using Shared.SpecialRoutines;
using System.Numerics;
using Validator.Common.ParsingRoutines;


public class ValidationTermTest
{
    [Fact]
    public void TestParseCellRowColNew()
    {
        //businessCode = "{S.05.01.02.01,R1210,C0200,Z0001}";
        //businessCode = "{S.01.01.02.01,R0010,C0010}"; //=> tableCode=S.01.01.02.01 zet ="" row=R0010 col=C0010                
        //businessCode = "{S.06.02.01.01,C0100,Z0001}"; //tableCode=S.01.01.02.01 zet =Z001 row="" col=C0010                
        //businessCode = "{S.01.02.01.02,C0070}";

        var text4 = "{SR.06.02.01.01,NC0010}";
        var rcr4 = DimUtils.ParseCellRowCol(text4);//tablecode, zet,row,col, isOpen, isValid
        var expected4 = new CellRowColRecord(text4, "SR.06.02.01.01", "", "", "NC0010", true, true);
        Assert.Equal(expected4, rcr4);


        var text3 = "{S.06.02.01.01,NC0010}";
        var rcr3 = DimUtils.ParseCellRowCol(text3);//tablecode, zet,row,col, isOpen, isValid
        var expected3 = new CellRowColRecord(text3, "S.06.02.01.01", "", "", "NC0010", true, true);
        Assert.Equal(expected3, rcr3);


        var text2 = "{S.06.02.01.01,C0010}";
        var rcr2 = DimUtils.ParseCellRowCol(text2);//tablecode, zet,row,col, isOpen, isValid
        var expected2 = new CellRowColRecord(text2, "S.06.02.01.01", "", "", "C0010", true, true);
        Assert.Equal(expected2, rcr2);


        var text1 = "{S.06.02.01.01,C0010,Z0001}";
        var rcr1 = DimUtils.ParseCellRowCol(text1);
        var expected1 = new CellRowColRecord(text1, "S.06.02.01.01", "Z0001", "", "C0010", true, true);
        Assert.Equal(expected1, rcr1);

        var text = "{S.06.02.01.01,R0200,C0010,Z0001}";
        var rcr = DimUtils.ParseCellRowCol(text);
        var expected0 = new CellRowColRecord(text, "S.06.02.01.01", "Z0001", "R0200", "C0010", false, true);
        Assert.Equal(expected0, rcr);

    }


    [Fact]
    public void TestParseCellRowColNew2()
    {
        //businessCode = "{S.05.01.02.01,R1210,C0200,Z0001}";
        //businessCode = "{S.01.01.02.01,R0010,C0010}"; //=> tableCode=S.01.01.02.01 zet ="" row=R0010 col=C0010                
        //businessCode = "{S.06.02.01.01,C0100,Z0001}"; //tableCode=S.01.01.02.01 zet =Z001 row="" col=C0010                
        //businessCode = "{S.01.02.01.02,C0070}";

        var text4 = "{SR.06.02.01.01,NC0010}";
        var rcr4 = DimUtils.ParseCellRowColNew(text4);//tablecode, zet,row,col, isOpen, isValid
        var expected4 = new CellRowColRecord(text4, "SR.06.02.01.01", "", "", "NC0010", true, true);
        Assert.Equal(expected4, rcr4);


        var text3 = "{S.06.02.01.01,NC0010}";
        var rcr3 = DimUtils.ParseCellRowColNew(text3);//tablecode, zet,row,col, isOpen, isValid
        var expected3 = new CellRowColRecord(text3, "S.06.02.01.01", "", "", "NC0010", true, true);
        Assert.Equal(expected3, rcr3);


        var text2 = "{S.06.02.01.01,C0010}";
        var rcr2 = DimUtils.ParseCellRowColNew(text2);//tablecode, zet,row,col, isOpen, isValid
        var expected2 = new CellRowColRecord(text2, "S.06.02.01.01", "", "", "C0010", true, true);
        Assert.Equal(expected2, rcr2);


        var text1 = "{S.06.02.01.01,C0010,Z0001}";
        var rcr1 = DimUtils.ParseCellRowColNew(text1);
        var expected1 = new CellRowColRecord(text1, "S.06.02.01.01", "Z0001", "", "C0010", true, true);
        Assert.Equal(expected1, rcr1);

        var text = "{S.06.02.01.01,R0200,C0010,Z0001}";
        var rcr = DimUtils.ParseCellRowColNew(text);
        var expected0 = new CellRowColRecord(text, "S.06.02.01.01", "Z0001", "R0200", "C0010", false, true);
        Assert.Equal(expected0, rcr);

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
        string[] expectedValues1 = { "abc", "", "" };
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

        var text3 = @"{t: SR.02.01.07.01, r: AR0690, dv: 0, seq: False, id: v4, f: solvency, fv: solvency2}";
        //var record = RuleTerm280.CreateRuleTerm(text);
        var record3 = RuleTerm280.CreateRuleTerm280("X0", text3);
        string[] expectedValues3 = { "SR.02.01.07.01", "", "AR0690", "" };
        string[] actualValues3 = { record3.T, record3.Z, record3.R, record3.C };

        Assert.Equal(expectedValues3, actualValues3);


        var text = @"{t: S.02.01.07.01, r: R0690, dv: 0, seq: False, id: v4, f: solvency, fv: solvency2}";
        //var record = RuleTerm280.CreateRuleTerm(text);
        var record = RuleTerm280.CreateRuleTerm280("X0", text);
        string[] expectedValues = { "S.02.01.07.01", "", "R0690", "" };
        string[] actualValues = { record.T, record.Z, record.R, record.C };

        Assert.Equal(expectedValues, actualValues);


        var text2 = @"{t: S.23.01.05.01, r: R0570, z: Z0001, dv: 0, seq: False, id: v1, f: solvency, fv: solvency2}";
        record = RuleTerm280.CreateRuleTerm280("X0", text2);
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
        var res = GeneralEvaluator.EvaluateBooleanExpression(0, text, new());
        Assert.True(res == KleeneValue.True);

        text = @"(2>1 or 1<2) and (2>1)";
        res = GeneralEvaluator.EvaluateBooleanExpression(0, text, new());
        Assert.True(res == KleeneValue.True);

        text = @"(2>1 or 1<2) and (1>2)";
        res = GeneralEvaluator.EvaluateBooleanExpression(0, text, new());
        Assert.True(res == KleeneValue.False);

        text = @"(2>1 or 1<2) and not(1>2)";
        res = GeneralEvaluator.EvaluateBooleanExpression(0, text, new());
        Assert.True(res == KleeneValue.True);

        var qt = "\"";
        var x = "{";
        var y = "}";

        //text = @"(1>2 or matches(""LEI/12301"", ""^LEI/[A-Z0-9]{3}(01|00)$"")) and not(1>2)";
        text = @$"(1>2 or matches({qt}LEI/12301{qt}, {qt}^LEI/[A-Z0-9]{x}3{y}(01|00)${qt})) and not(1>2)";
        res = GeneralEvaluator.EvaluateBooleanExpression(0, text, new());
        //Assert.True(res == KleeneValue.True);


        text = @$"(1>2 or matches({qt}LEI/12301{qt}, {qt}^LEI/[A-Z0-9]{x}3{y}(01|00)${qt})) and (matches({qt}Lei248{qt},{qt}Lei\d\d\d{qt}))";
        res = GeneralEvaluator.EvaluateBooleanExpression(0, text, new());
        //Assert.True(res == KleeneValue.True);

    }



    [Fact]
    public void TestEvalFunction()
    {

        var text = @"imin(3, 4, 1 +1)";
        //var res = GeneralEvaluator.EvaluateFunction(text, new(), "");
        //Assert.Equal(new OptionalObject(false, (double)2), res);


        //text = @"imax(imin(3, 7) , 4) ";
        //res = GeneralEvaluator.EvaluateFunction(text, new(), "");
        //Assert.Equal(false, res.IsNull);
        //Assert.Equal(4.0, res.Value);



    }



    [Fact]
    public void TestArithmetic()
    {

        var text = @"5 + imin(3) +imax(4)";
        //var res = GeneralEvaluator.EvaluateArithmeticExpressionRecursively(text, new(), "");
        //Assert.True(!res.IsNull);
        //Assert.Equal(12, (double)(res?.Value ?? 0));


        //text = @"7 + imin(imax(3,5),4)";
        //res = GeneralEvaluator.EvaluateArithmeticExpressionRecursively(text, new(), "");
        //Assert.Equal(11, (double)(res?.Value ?? 0));


    }

    [Fact]
    public void TestSplitAndOrExpression()
    {

        var text = @"5 + imin(3)+(a>3 or b<4) and imax(4+x3)> 5";
        var res = GeneralEvaluator.SplitAndOrExpression(text);
        Assert.Equal(res.logicalOperator, GeneralEvaluator.LogicalOperators.IsAnd);
        Assert.Equal(res.left, "5 + imin(3)+(a>3 or b<4)");
        Assert.Equal(res.Right, "imax(4+x3)> 5");


        text = @"5 + imin(3)+(a>3 and b<4) or imax(4+x3)> 5";
        res = GeneralEvaluator.SplitAndOrExpression(text);
        Assert.Equal(res.logicalOperator, GeneralEvaluator.LogicalOperators.IsOR);
        Assert.Equal(res.left, "5 + imin(3)+(a>3 and b<4)");
        Assert.Equal(res.Right, "imax(4+x3)> 5");

        text = @"5 + imin(3)+ imax(4+x3)> 5";
        res = GeneralEvaluator.SplitAndOrExpression(text);
        Assert.Equal(res.logicalOperator, GeneralEvaluator.LogicalOperators.None);
        Assert.Equal(res.left, "5 + imin(3)+ imax(4+x3)> 5");
        Assert.Equal(res.Right, "");



    }



    [Fact]
    public void TestSplitPlusOrMinusArithmetic()
    {

        var text = @"5 - 4 + (3 * X2)";
        var res = GeneralEvaluator.SplitArithmeticExpression(text);
        Assert.Equal(res.arithmeticOperator, ArithmeticOperators.Minus);
        Assert.Equal(res.left, "5");
        Assert.Equal(res.right, "4 + (3 * X2)");

        text = @"5 * X1 + (3 * X2)";
        res = GeneralEvaluator.SplitArithmeticExpression(text);
        Assert.Equal(res.arithmeticOperator, ArithmeticOperators.Plus);
        Assert.Equal(res.left, "5 * X1");
        Assert.Equal(res.right, "(3 * X2)");

        text = @"X3";
        res = GeneralEvaluator.SplitArithmeticExpression(text);
        Assert.Equal(res.arithmeticOperator, ArithmeticOperators.None);
        Assert.Equal(res.left, "X3");
        Assert.Equal(res.right, "");



    }

    [Fact]
    public void TestEvaluateArithmeticExpression()
    {

        var text0 = @"3 + imin(4,3,2) * 7";
        //var res0 = GeneralEvaluator.EvaluateArithmeticExpressionRecursively(text0, new(), "");
        //Assert.True(res0.IsNull == false);
        //Assert.True((double)res0.Value == 17);


        //var text = @"3 * (2+4)";
        //var res = GeneralEvaluator.EvaluateArithmeticExpressionRecursively(text, new(), "");
        //Assert.True(res.IsNull == false);
        //Assert.True((double)res.Value == 18);

        //text = @"3 + (2 * 4 -7)";
        //res = GeneralEvaluator.EvaluateArithmeticExpressionRecursively(text, new(), "");
        //Assert.True(res.IsNull == false);
        //Assert.True((double)res.Value == 4);

        string text2 = @"{t: S.06.02.01.02, c: C0290, z: Z0001, filter: matches(dim(this(), [s2c_dim:UI]), ""^CAU/.*"") and not(matches(dim(this(), [s2c_dim:UI]), ""^CAU/(ISIN/.*)|(INDEX/.*)"")), seq: False, id: v1, f: solvency, fv: solvency2}";
    }


    [Fact]
    public void TestSimplifyFormula()
    {



        var text2 = @"{t: S.06.02.01.01, c: C0130, z: Z0001, dv: emptySequence(), seq: False, id: v1, f: solvency, fv: solvency2}";
        var res2 = FormulaSimplification.Simplify(text2);
        Assert.Equal(@"{t: S.06.02.01.01, c: C0130, z: Z0001, dv: emptySequence(), seq: False, id: v1, f: solvency, fv: solvency2}", res2.Formula);

        var text1 = "m:x3 and x=33";
        var res1 = FormulaSimplification.Simplify(text1);
        Assert.Equal("m:x3 and x=33", res1.Formula);
        Assert.Empty(res1.FormulaTerms);

        //put it back and check if like origingl
        var finalText1 = FormulaSimplification.ReplaceTerms(res1.Formula, res1.FormulaTerms);
        Assert.Equal(text1, finalText1);


        string text0 = @"{t: S.06.02.01.02, c: C0290, z: Z0001, filter: matches(dim(this(), [s2c_dim:UI]), ""^CAU/.*"") and not(matches(dim(this(), [s2c_dim:UI]), ""^CAU/(ISIN/.*)|(INDEX/.*)"")), seq: False, id: v1, f: solvency, fv: solvency2}";
        var res0 = FormulaSimplification.Simplify(text0);
        Assert.Equal(res0.Formula, @"{t: S.06.02.01.02, c: C0290, z: Z0001, filter: matches(XYZ00) and not(XYZ01), seq: False, id: v1, f: solvency, fv: solvency2}");
        Assert.Equal(res0.FormulaTerms[0].content, @"dim(this(), [s2c_dim:UI]), ""^CAU/.*""");
        Assert.Equal(res0.FormulaTerms[0].letter, @"XYZ00");

        var finalText0 = FormulaSimplification.ReplaceTerms(res0.Formula, res0.FormulaTerms);
        Assert.Equal(text0, finalText0);


    }

    [Fact]
    public void TestTermsExtractions()
    {

        var text3 = @"{t: S.06.02.01.01, c: C0170, z: Z0001, seq: False, id: v2, f: solvency, fv: solvency2} i= {t: S.06.02.01.01, c: C0140, z: Z0001, seq: False, id: v1, f: solvency, fv: solvency2} i* {t: S.06.02.01.02, c: C0380, z: Z0001, dv: 0, seq: False, id: v3, f: solvency, fv: solvency2} i+ {t: S.06.02.01.01, c: C0180, z: Z0001, dv: 0, seq: False, id: v4, f: solvency, fv: solvency2}";
        var res3 = TermsExtraction.ExtractTerms(text3);

        var text1 = "ab > 232";
        var res1 = TermsExtraction.ExtractTerms(text1);
        Assert.Equal("ab > 232", res1.Formula);
        Assert.Equal(0, res1.formulaTerms.Count);

        var text0 = @"(isum({t: S.06.02.01.01, c: C0170, z: Z0001, seq: True, id: v1, f: solvency, fv: solvency2}) i> (0.3 i* ({t: S.02.01.02.01, r: R0070, c: C0010, dv: 0, seq: False, id: v2, f: solvency, fv: solvency2} i+ {t: S.02.01.02.01, r: R0220, c: C0010, dv: 0, seq: False, id: v3, f: solvency, fv: solvency2})) and {t: S.02.01.02.01, r: R0070, c: C0010, dv: 0, seq: False, id: v2, f: solvency, fv: solvency2} != 0 and {t: S.02.01.02.01, r: R0220, c: C0010, dv: 0, seq: False, id: v3, f: solvency, fv: solvency2} != 0)";
        var res0 = TermsExtraction.ExtractTerms(text0);
        Assert.Equal("(isum(X00) > (0.3 * (X01 + X02)) and X03 != 0 and X04 != 0)", res0.Formula);
        Assert.Equal("{t: S.02.01.02.01, r: R0070, c: C0010, dv: 0, seq: False, id: v2, f: solvency, fv: solvency2}", res0.formulaTerms[1].TermText);


    }

    [Fact]
    public void TestFindOperators()
    {
        char[] additionSymbols = { '+', '-' };
        char[] multiplicationSymbols = { '*' };
        char[] allOps = additionSymbols.Concat(multiplicationSymbols).ToArray();        

        var text = "";
        var operators = OperatorManager.PlaceOperatorsInList(text, allOps, multiplicationSymbols);
        Assert.Equal(0, operators.Count());

        text = "+";
        operators = OperatorManager.PlaceOperatorsInList(text, allOps, multiplicationSymbols);
        Assert.Equal(0, operators.Count());

        text = "+";
        operators = OperatorManager.PlaceOperatorsInList(text, allOps, additionSymbols);
        Assert.Equal(1, operators.Count());
        Assert.Equal(0, operators[0].position);

        text = "-1";
        operators = OperatorManager.PlaceOperatorsInList(text, allOps, additionSymbols);
        Assert.Equal(1, operators.Count());
        Assert.Equal(ArithmeticOperators.UnaryMinus, operators[0].arithmeticOperator);
        Assert.Equal(0, operators[0].position);


        text = "3 -2 + 4x";
        operators = OperatorManager.PlaceOperatorsInList(text, allOps, additionSymbols);
        Assert.Equal(2, operators.Count());
        Assert.Equal(ArithmeticOperators.Minus, operators[0].arithmeticOperator);
        Assert.Equal(2, operators[0].position);
        Assert.Equal(ArithmeticOperators.Plus, operators[1].arithmeticOperator);
        Assert.Equal(5, operators[1].position);

        text = "4 - 3 + 2 * 3x";
        operators = OperatorManager.PlaceOperatorsInList(text, allOps, multiplicationSymbols);
        operators = OperatorManager.PlaceOperatorsInList(text, allOps, additionSymbols);
        Assert.Equal(2, operators.Count());
        Assert.Equal(ArithmeticOperators.Minus, operators[0].arithmeticOperator);
        Assert.Equal(2, operators[0].position);
        Assert.Equal(ArithmeticOperators.Plus, operators[1].arithmeticOperator);
        Assert.Equal(5, operators[1].position);



        text = "3 * -2+4x";
        operators = OperatorManager.PlaceOperatorsInList(text, allOps, additionSymbols);
        Assert.Equal(2, operators.Count());
        Assert.Equal(ArithmeticOperators.UnaryMinus, operators[0].arithmeticOperator);
        Assert.Equal(4, operators[0].position);
        Assert.Equal(ArithmeticOperators.Plus, operators[1].arithmeticOperator);
        Assert.Equal(6, operators[1].position);

        text = "3 * -2+4x";
        operators = OperatorManager.PlaceOperatorsInList(text, allOps, multiplicationSymbols);
        Assert.Equal(1, operators.Count());
        Assert.Equal(ArithmeticOperators.Multiply, operators[0].arithmeticOperator);
        Assert.Equal(2, operators[0].position);




    }

    [Fact]
    public void TestPlaceOperatorsInOrderedList()
    {                

        var formula = "";
        var operators = OperatorManager.OperatorsInOrderedList(formula);
        Assert.Equal(0, operators.Count());

        


        formula = "3-2+4x";
        operators = OperatorManager.OperatorsInOrderedList(formula);
        Assert.Equal(2, operators.Count());
        Assert.Equal(ArithmeticOperators.Minus, operators[0].arithmeticOperator);
        Assert.Equal(1, operators[0].position);
        Assert.Equal(ArithmeticOperators.Plus, operators[1].arithmeticOperator);
        Assert.Equal(3, operators[1].position);


        formula = "3 * -4x";
        operators = OperatorManager.OperatorsInOrderedList(formula);
        Assert.Equal(2, operators.Count());
        Assert.Equal(ArithmeticOperators.UnaryMinus, operators[0].arithmeticOperator);
        Assert.Equal(4, operators[0].position);
        Assert.Equal(ArithmeticOperators.Multiply, operators[1].arithmeticOperator);
        Assert.Equal(2, operators[1].position);



        formula = "2+3 * -4 - 6";
        operators = OperatorManager.OperatorsInOrderedList(formula);
        Assert.Equal(4, operators.Count());
        Assert.Equal(ArithmeticOperators.UnaryMinus, operators[0].arithmeticOperator);
        Assert.Equal(6, operators[0].position);
        Assert.Equal(ArithmeticOperators.Multiply, operators[1].arithmeticOperator);
        Assert.Equal(4, operators[1].position);
        Assert.Equal(ArithmeticOperators.Plus, operators[2].arithmeticOperator);
        Assert.Equal(1, operators[2].position);
        Assert.Equal(ArithmeticOperators.Minus, operators[3].arithmeticOperator);
        Assert.Equal(9, operators[3].position);




        


    }

}