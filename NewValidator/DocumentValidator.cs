using Dapper;
using Microsoft.Data.SqlClient;
using NewValidator.ValidationClasses;
using Serilog;
using Shared.DataModels;
using Shared.GeneralUtils;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;
using Syncfusion.Office;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.Implementation;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Schema;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NewValidator;

internal enum ValidStatus { Valid, Error, Waring };
public class DocumentValidator : IDocumentValidator
{
    private readonly IParameterHandler _parameterHandler;
    private readonly ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private DocInstance _documentInstance = new();
    private int DocumentId { get => _documentInstance?.InstanceId ?? 0; }
    private MModule _mModule = new();

    public DocumentValidator(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;

    }
    public int ValidateDocument()
    {
        var doc = _SqlFunctions.SelectDocInstance(_parameterData.DocumentId);
        if (doc is null)
        {
            var message = $"Cannot Find DocInstance  Id:{_parameterData.DocumentId} for fund:{_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
            return 1;
        }
        _documentInstance = doc;

        var module = _SqlFunctions.SelectModuleByCode(_documentInstance.ModuleCode);
        if (module is null)
        {
            var message = $"Invalid module :{_parameterData.ModuleCode}";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
            return 1;
        }
        _mModule = module;
        //A ValidationRule may apply to more than one tables and therefore we may have more than one with the same validationID
        //A validationExpression may be related multiple times with the same table (because the same table is used by more than one module)
        //my solution is to check if for all the tables of the rules exist a sheet

        //729 simple >
        //743 simple isnull
        //4880 matches
        //787 equality of enumaratin
        //1809 for min
        //783 for sum
        //1809 for max and sequence
        //702 dim
        //2050 scope
        //743 else
        //683 open tables
        //2038 closed tables sum
        //655  :filter with match and dim(this(),)
        //699 dates 
        //703 string equality
        //713 dates
        //715 iso countries
        //790 count for closed table
        //648 wrong rule
        var xx = CreateErrorDocument();


        //Select rules only with tables (the other rules check context dims or metrics)
        var validationRules = _SqlFunctions.SelectValidationExpressionsWithTablesForModule(_mModule.ModuleID)
            .OrderBy(rl => rl.ValidationID).ToList();

        validationRules = validationRules.Where(vr => vr.ValidationID == 400).ToList();
        foreach (var validationRule in validationRules)
        {
            Console.WriteLine($"\n***Validating Rule:{validationRule.ValidationID}");
            var tablesInValidation = _SqlFunctions.SelectTablesForValidationRule(validationRule.ValidationID);

            var hasOnlyOpenTables = tablesInValidation.All(tbl => tbl.IsOpenTable);
            var hasOnlyClosedTables = tablesInValidation.All(tbl => !tbl.IsOpenTable);
            var hasMixedTables = tablesInValidation.Any(tbl => tbl.IsOpenTable) && tablesInValidation.Any(tbl => !tbl.IsOpenTable);


            var hasAggregateFunction = new[] { "sum", "count" }.Any(fn => validationRule.Rule.Contains(fn));
            //**todo check if all the sheets exist for this rule??

            //*********** SCOPE 
            var rulex1 = RuleStructure280.CreateRuleStructure(validationRule.ValidationID, validationRule.Rule, validationRule.Filter, validationRule.Scope);
            var scopeRowcols = rulex1.ScopeRowCols;
            var scopeType = rulex1.ScopeType;
            //** if no scope, add one entry to go through
            if (scopeType == ScopeType.None)
            {
                scopeRowcols.Add("");
            }
            var ruleForScope = RuleStructure280.CreateRuleStructure(validationRule.ValidationID, validationRule.Rule, validationRule.Filter, validationRule.Scope);
            foreach (var scopeRowCol in scopeRowcols)
            {
                //this must go , and update in each case
                if (scopeType != ScopeType.None)
                {
                    UpdateRuleTermsWithRowCol(ruleForScope.IfComponent.RuleTerms, "", scopeRowCol, scopeRowCol, ruleForScope.ScopeType);
                    UpdateRuleTermsWithRowCol(ruleForScope.ThenComponent.RuleTerms, "", scopeRowCol, scopeRowCol, ruleForScope.ScopeType);
                    UpdateRuleTermsWithRowCol(ruleForScope.ElseComponent.RuleTerms, "", scopeRowCol, scopeRowCol, ruleForScope.ScopeType);
                    UpdateRuleTermsWithRowCol(ruleForScope.FilterComponent.RuleTerms, "", scopeRowCol, scopeRowCol, ruleForScope.ScopeType);
                }

                //check if no seq and all tables are open                 
                //--if there is a filter => it is an open table 
                //sum with seq 
                //for open tables use all the rows and apply filter and :filter
                //Closed Tables: sum without seq , use the R: to create terms (3780) OR the terms are separated by commas

                var hasAggregateFn = ruleForScope.IfComponent.RuleTerms.Any(rt => rt.IsSequence);
                if ((!hasAggregateFn || hasAggregateFn) && hasOnlyClosedTables)
                {
                    var mainTable = tablesInValidation.FirstOrDefault();
                    var sheets = _SqlFunctions.SelectTemplateSheetsByTableId(DocumentId, mainTable!.TableID);
                    foreach (var sheet in sheets)
                    {
                        var ruleClosed = RuleStructure280.CreateRuleStructure(validationRule.ValidationID, validationRule.Rule, validationRule.Filter, validationRule.Scope);
                        if (scopeType != ScopeType.None)
                        {
                            UpdateRuleTermsWithRowCol(ruleClosed.IfComponent.RuleTerms, "", scopeRowCol, scopeRowCol, ruleClosed.ScopeType);
                            UpdateRuleTermsWithRowCol(ruleClosed.ThenComponent.RuleTerms, "", scopeRowCol, scopeRowCol, ruleClosed.ScopeType);
                            UpdateRuleTermsWithRowCol(ruleClosed.ElseComponent.RuleTerms, "", scopeRowCol, scopeRowCol, ruleClosed.ScopeType);
                            UpdateRuleTermsWithRowCol(ruleClosed.FilterComponent.RuleTerms, "", scopeRowCol, scopeRowCol, ruleClosed.ScopeType);
                        }

                        ruleClosed.ZetValue = sheet.ZDimVal;
                        ruleClosed = FillRuleStructureWithFactValues(ruleClosed);
                        var sumTerm = ruleClosed.IfComponent.RuleTerms.Where(rt => rt.IsSequence).FirstOrDefault();
                        if (sumTerm != null)
                        {
                            var (count, sum) = CalculateSumOfClosedTable(sumTerm,sheet.ZDimVal);
                            ReplaceObjTerm(ruleClosed.IfComponent.ObjectTerms, sumTerm.Letter, -999, sum, count);
                        }

                        var isValidClosedRule = ExpressionEvaluator.ValidateRule(ruleClosed);
                        if (!isValidClosedRule)
                        {
                            CreateRuleError(ruleClosed, validationRule);
                        }
                    }
                }

                if (!hasAggregateFn && (hasMixedTables || hasOnlyOpenTables))
                {
                    //create one rule for each row and apply filter 

                    var mainTable = tablesInValidation.FirstOrDefault(tbl => _SqlFunctions.SelectTableKyrKey(tbl.TableCode)?.FK_TableCode is not null);
                    if (mainTable is null)
                    {
                        mainTable = tablesInValidation.FirstOrDefault(tb => tb.IsOpenTable);
                    }

                    var kyrTable = _SqlFunctions.SelectTableKyrKey(mainTable?.TableCode ?? "");
                    var fklTable = kyrTable?.FK_TableCode ?? "";
                    var fkCol = kyrTable?.FK_TableCol ?? "";

                    var sheets = _SqlFunctions.SelectTemplateSheetsByTableId(DocumentId, mainTable!.TableID);
                    foreach (var sheet in sheets)
                    {
                        var rows = _SqlFunctions.SelectDistinctRowsInSheet(DocumentId, sheet.TemplateSheetId);
                        var prevRowValid = true;
                        foreach (var row in rows)
                        {

                            //find the row from the column that has the foreign key
                            var ruleOpen = RuleStructure280.CreateRuleStructure(validationRule.ValidationID, validationRule.Rule, validationRule.Filter, validationRule.Scope);

                            var relatedRowZZ = _SqlFunctions.SelectFactByRowColTableCode(DocumentId, sheet.TableCode, sheet.ZDimVal, row, fkCol)?.RowForeign ?? "";
                            var relatedRow = _SqlFunctions.SelectFactByRowCol(DocumentId, sheet.TemplateSheetId, row, fkCol)?.RowForeign ?? "";
                            if (relatedRow != relatedRowZZ)
                            {
                                throw new Exception($"related Row:{relatedRow}");
                            }
                            UpdateRuleTermsWithRowCol(ruleOpen.IfComponent.RuleTerms, mainTable.TableCode, row, relatedRow, ScopeType.Rows);
                            UpdateRuleTermsWithRowCol(ruleOpen.ThenComponent.RuleTerms, mainTable.TableCode, row, relatedRow, ScopeType.Rows);
                            UpdateRuleTermsWithRowCol(ruleOpen.ElseComponent.RuleTerms, mainTable.TableCode, row, relatedRow, ScopeType.Rows);
                            UpdateRuleTermsWithRowCol(ruleOpen.FilterComponent.RuleTerms, mainTable.TableCode, row, relatedRow, ScopeType.Rows);

                            ruleOpen.ZetValue = sheet.ZDimVal;
                            ruleOpen = FillRuleStructureWithFactValues(ruleOpen);



                            KleeneValue filterKleeneValue = ruleOpen!.FilterComponent.IsEmpty
                                ? KleeneValue.True
                                : ExpressionEvaluator.EvaluateGeneralBooleanExpression(ruleOpen.RuleId, ruleOpen.FilterComponent.SymbolExpression, ruleOpen.FilterComponent.ObjectTerms);

                            //if filter has terms with null values, it is considered false here                            
                            if (filterKleeneValue != KleeneValue.True)
                            {
                                continue;
                            };

                            var isValidRowRule = ExpressionEvaluator.ValidateRule(ruleOpen);

                            if (!isValidRowRule)
                            {
                                if (prevRowValid) Console.WriteLine("");
                                Console.WriteLine($"{validationRule.Severity} ruleId:{validationRule.ValidationID} row:{row}");
                                CreateRuleError(ruleOpen, validationRule);
                                prevRowValid = false;
                            }
                            else
                            {
                                prevRowValid = true;
                                //Console.WriteLine($"Valid:{validationRule.ValidationID} row:{row}");
                                Console.Write($".");
                            }

                        }
                    }

                }

                if (hasAggregateFn && hasMixedTables)
                {

                    //there are closed and OPEN tables and there is a seq:TRUE (SUM or COUNT) then  
                    //--- Add ONLY the rows of the open table for which the filter is valid. we may need the slave row (foreign key) if more than one open table. 
                    //--- the resulting object will have both the sum and the count because the function is not known  at the time 
                    // Rule 783: {t: S.02.01.02.01, r: R0060, c: C0010, dv: 0, seq: False, id: v1, f: solvency, fv: solvency2} i= isum({t: S.06.02.01.01, c: C0170, z: Z0001, dv: emptySequence(), seq: True, id: v2, f: solvency, fv: solvency2})
                    // Filter matches({t: S.06.02.01.02, c: C0290, z: Z0001, dv: emptySequence(), seq: True, id: v3, f: solvency, fv: solvency2}, "^..((93)|(95)|(96))$") and ({t: S.06.02.01.01, c: C0090, z: Z0001, dv: emptySequence(), seq: True, id: v4, f: solvency, fv: solvency2} = [s2c_LB:x91])

                    var mainTable = tablesInValidation.Where(tbl => !tbl.IsOpenTable).FirstOrDefault();
                    var sheets = _SqlFunctions.SelectTemplateSheetsByTableId(DocumentId, mainTable!.TableID);

                    foreach (var sheet in sheets)
                    {
                        var rule = RuleStructure280.CreateRuleStructure(validationRule.ValidationID, validationRule.Rule, validationRule.Filter, validationRule.Scope);


                        //sumTersm will be evaluated again below
                        //MAKE ZET EMPTY. the closed table has no zet but the open tables for sum do have a zet
                        rule.ZetValue = "";
                        rule = FillRuleStructureWithFactValues(rule);

                        EvaluateSumTerms(rule.RuleId, rule.IfComponent, rule.FilterComponent, string.Empty);
                        EvaluateSumTerms(rule.RuleId, rule.ThenComponent, rule.FilterComponent, string.Empty);
                        EvaluateSumTerms(rule.RuleId, rule.ElseComponent, rule.FilterComponent, string.Empty);

                        var isValidRule = ExpressionEvaluator.ValidateRule(rule);
                        if (!isValidRule)
                        {
                            Console.WriteLine($"{validationRule.Severity} ruleId:{rule.RuleId} ");
                            CreateRuleError(rule, validationRule);
                        }

                    }




                }

                if (hasAggregateFn && hasOnlyOpenTables)
                {
                    throw new Exception("Aggregate Tables and isAllOpenTables. is it possible?");
                    //we may have aggregates but the sum  are within ?
                }
            }


        }
        return 1;

        void EvaluateSumTerms(int ruleId, RuleComponent280 ruleComponent, RuleComponent280 filterComponent, string zetValue)
        {
            var seqTerms = ruleComponent.RuleTerms.Where(rt => rt.IsSequence);
            foreach (var thenSeqTerm in seqTerms)
            {
                var res = CalculateSumofOpenTable(ruleId, thenSeqTerm, filterComponent, zetValue);
                ReplaceObjTerm(ruleComponent.ObjectTerms, thenSeqTerm.Letter, -999, res.sum, res.count);
            }
        }
    }
    ObjectTerm280 ReplaceObjTerm(Dictionary<string, ObjectTerm280> objTerms, string objKey, object value, double sum, int count)
    {
        //maybe I need to set obj to zero instead of sum
        var objTerm = objTerms[objKey];
        var newObjTerm = objTerm with { Obj = sum, sumValue = Convert.ToDouble(sum), countValue = count, DataType = "N" };
        objTerms.Remove(objKey);
        objTerms.Add(objKey, newObjTerm);
        return newObjTerm;
    }

    private static void UpdateRuleTermsWithRowCol(List<RuleTerm280> ruleTerms, string slaveTableCode, string rowCol, string relatedRowCol, ScopeType scopeType)
    {

        if (scopeType == ScopeType.Rows)
        {
            var rTerms = ruleTerms.Where(term => string.IsNullOrEmpty(term.R)).ToList();
            foreach (var term in rTerms)
            {
                term.R = term.T.Trim() == slaveTableCode ? rowCol : relatedRowCol;
            }
        }
        if (scopeType == ScopeType.Cols)
        {
            var cTerms = ruleTerms.Where(term => string.IsNullOrEmpty(term.C)).ToList();
            foreach (var term in cTerms)
            {
                term.C = term.T.Trim() == slaveTableCode ? rowCol : relatedRowCol;
            }
        }

    }

    private static ObjectTerm280 CreateObjectTerm280Empty()
    {
        return new ObjectTerm280("J", 0, false, null, 0, 0, null, true, "");
    }
    private static ObjectTerm280 CreateObjectTerm280(TemplateSheetFact? fact, string defaultValue, double sumValue, int countValue, bool IsTolerance, string filter)
    {
        if (fact == null)
        {
            var defaultDataType = defaultValue.Trim() switch
            {
                "0" => "N",
                "[Default]" => "S",
                "emptySequence()" => "N",
                "CreateDate(1900,01,01)" => "D",
                _ => "S"
            };


            object? objValue = defaultValue.Trim() switch
            {
                "0" => 0,
                "[Default]" => "[Default]",
                "emptySequence()" => 0,
                "CreateDate(1900,01,01)" => new DateOnly(1900, 1, 1),
                _ => null
            };



            return new ObjectTerm280(defaultDataType, 0, IsTolerance, null, 0, 0, null, true, "");
        }


        object obj = fact.DataTypeUse.Trim() switch
        {
            "E" => fact.TextValue,
            "S" => fact.TextValue,
            "I" => fact.NumericValue,
            "M" => fact.NumericValue,
            "N" => fact.NumericValue,
            "P" => fact.NumericValue,
            "B" => fact.BooleanValue,
            "D" => (DateTime)fact.DateTimeValue,
            _ => throw new NotImplementedException()
        };
        var objTerm = new ObjectTerm280(fact.DataTypeUse, fact.Decimals, IsTolerance, obj, sumValue, countValue, fact, false, filter);
        return objTerm;
    }

    private Dictionary<string, ObjectTerm280> ToOjectTerm280UsingFactValues(List<RuleTerm280> ruleTerms, string zetValue)
    {

        //check whether the term does not have a zet value (Z0001)
        var ruleTermsWithZet = ruleTerms.Select(rt => rt with { Z= rt.Z.Contains("Z00")? zetValue:"" });
        Dictionary<string, ObjectTerm280> plainTerms = ruleTermsWithZet
            .Select(rtm => new
            {
                rtm.Letter,
                Zet =rtm.Z,
                Fact = _SqlFunctions.SelectFactByRowColTableCode(DocumentId, rtm.T, rtm.Z, rtm.R, rtm.C),
                ObjectTerm = CreateObjectTerm280(_SqlFunctions.SelectFactByRowColTableCode(DocumentId, rtm.T, rtm.Z, rtm.R, rtm.C), rtm.Dv, 0, 0, rtm.IsTolerance, UpdateRuleTermFilter(rtm.Letter, rtm.Filter))
            })
            .ToDictionary(kd => kd.Letter, kv => kv.ObjectTerm);
        return plainTerms;
    }


    private string UpdateRuleTermFilter(string letter, string filter)
    {
        //not(isNull({t: SR.26.01.01.03, r: R0012; R0014; R0020; R0030; R0040, c: C0010, z: Z0001, dv: emptySequence(), filter: dim(this(), [s2c_dim:PO]) = [s2c_PU:x60] and not(isNull(dim(this(), [s2c_dim:FN]))), seq: True, id: v1, f: solvency, fv: solvency2}))
        //filter: dim(this(), [s2c_dim:PO]) = [s2c_PU:x60] and not(isNull(dim(this(), [s2c_dim:FN])))
        var res = filter.Replace("this()", $"this({letter})");
        return res;
    }

    private (int, double) CalculateSumOfClosedTable(RuleTerm280 ruleTermRec,string zetValue)
    {
        //isum({t: S.23.01.02.01, r: R0300; R0310; R0320; R0330; R0340; R0350; R0360; R0370, z: Z0001, dv: emptySequence(), seq: True,.. )
        //scope({t: S.23.01.02.01, c:C0010;C0040, f: solvency, fv: solvency2})
        var (sumScopeType, rowCols) = ParseRuleTerms(ruleTermRec);
        var sum = 0.0;
        var count = 0;
        foreach (var rowCol in rowCols)
        {
            var row = sumScopeType == ScopeType.Rows ? rowCol : ruleTermRec.R;
            var col = sumScopeType == ScopeType.Cols ? rowCol : ruleTermRec.C;
            var zet = ruleTermRec.Z.Contains("Z000") ? zetValue : "";
            var fact = _SqlFunctions.SelectFactByRowColTableCode(DocumentId, ruleTermRec.T, zet, row, col);
            sum += fact?.NumericValue ?? 0;
            count += fact is not null ? 1 : 0;
        }
        return (count, sum);

        (ScopeType scopeType, List<string> rowCol) ParseRuleTerms(RuleTerm280 ruleTerm)
        {
            var rows = string.IsNullOrEmpty(ruleTerm.R) ? new List<string>() : ruleTerm.R.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var cols = string.IsNullOrEmpty(ruleTerm.C) ? new List<string>() : ruleTerm.C.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            ScopeType sumScopeType = rows switch
            {
                _ when rows.Any() => ScopeType.Rows,
                _ when cols.Any() => ScopeType.Cols,
                _ => ScopeType.None
            };

            var rowCols = sumScopeType switch
            {
                ScopeType.Rows => rows,
                ScopeType.Cols => cols,
                _ => new List<string>()
            };
            return (sumScopeType, rowCols);
        }

    }

    private RuleStructure280 FillRuleStructureWithFactValues(RuleStructure280 ruleStructure)
    {
        //{t: S.23.01.02.02, r: R0700, c: C0060, z: Z0001, dv: 0, seq: False, id: v0, f: solvency, fv: solvency2} i= isum({t: S.23.01.02.02, r: R0710; R0720; R0730; R0740; R0760, c: C0060, z: Z0001, dv: emptySequence(), seq: True, id: v1, f: solvency, fv: solvency2})
        //objectTerm: an object which gets information from the fact and the the RuleTerm ({t:2000} such as sequence 

        Dictionary<string, ObjectTerm280> ifObjectTerms = ToOjectTerm280UsingFactValues(ruleStructure.IfComponent.RuleTerms, ruleStructure.ZetValue);
        ruleStructure.IfComponent.ObjectTerms = ifObjectTerms;


        Dictionary<string, ObjectTerm280> thenObjectTerms = ToOjectTerm280UsingFactValues(ruleStructure.ThenComponent.RuleTerms, ruleStructure.ZetValue);
        ruleStructure.ThenComponent.ObjectTerms = thenObjectTerms;

        Dictionary<string, ObjectTerm280> elseObjectTerms = ToOjectTerm280UsingFactValues(ruleStructure.ElseComponent.RuleTerms, ruleStructure.ZetValue);
        ruleStructure.ElseComponent.ObjectTerms = elseObjectTerms;

        Dictionary<string, ObjectTerm280> filterObjectTerms = ToOjectTerm280UsingFactValues(ruleStructure.FilterComponent.RuleTerms, ruleStructure.ZetValue);
        ruleStructure.FilterComponent.ObjectTerms = filterObjectTerms;

        return ruleStructure;

    }

    private (double sum, int count) CalculateSumofOpenTable(int ruleId, RuleTerm280 seqTableTerm, RuleComponent280 filterComponent, string zetValue)
    {

        var seqTable = seqTableTerm.T;
        var kyrTable = _SqlFunctions.SelectTableKyrKey(seqTableTerm.T);
        var relatedTable = kyrTable?.FK_TableCode ?? "";
        //from the sqTableTErms
        //find the related table.

        var facts = _SqlFunctions.SelectFactsInEveryRowForColumn(DocumentId, seqTableTerm.T, seqTableTerm.Z, seqTableTerm.C); ;
        double sum = 0;
        var count = 0;
        foreach (var fact in facts)
        {
            var row = fact.Row;
            var foreignKeyRow = fact.RowForeign;
            var isFilterValid = EvaluateFilterRow(ruleId, filterComponent, relatedTable, fact.Row, fact.RowForeign, zetValue);
            if (isFilterValid)
            {
                sum += fact.NumericValue;
                count++;
            }
        }
        return (sum, count);
    }

    private bool EvaluateFilterRow(int ruleId, RuleComponent280 filterComponent, string relatedTable, string row, string foreignRow, string zetValue)
    {
        foreach (var filterTerm in filterComponent.RuleTerms)
        {
            filterTerm.R = filterTerm.T.Trim() == relatedTable.Trim()
                ? foreignRow
                : row;
            var term = 1;
        }

        try
        {

            Dictionary<string, ObjectTerm280> filterTerms = ToOjectTerm280UsingFactValues(filterComponent.RuleTerms, zetValue);
            if (filterTerms.Any())
            {
                var res = ExpressionEvaluator.EvaluateGeneralBooleanExpression(ruleId, filterComponent.SymbolExpression, filterTerms);
                return res == KleeneValue.True;
            }


            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"EvaluateFilterRow : relatedTable:{relatedTable} row:{row} foreignRow:{foreignRow} ---- {ex}");
        }

    }



    private int CreateErrorDocument()
    {
        var errorDoc = new ErrorDocumentModel
        {
            IsDocumentValid = true,
            ErrorDocumentId = DocumentId,
            OrganisationId = _parameterData.FundId,
            UserId = _parameterData.UserId.ToString(),
            ErrorCounter = true,
            WarningCounter = true,
        };
        var errorDocument = _SqlFunctions.CreateErrorDocument(errorDoc);
        return errorDocument;
    }

    private int CreateRuleError(RuleStructure280 ruleStructure, VValidationRuleExpressions validationRule)
    {

        var errorRule = new ERROR_Rule
        {
            RuleId = ruleStructure.RuleId,
            ErrorDocumentId = DocumentId,
            //Scope = RegexUtils.TruncateString(rule.Sc, 800),
            DataType = "",
            TableBaseFormula = RegexUtils.TruncateString(ruleStructure.RuleFormula, 990),
            Filter = RegexUtils.TruncateString(ruleStructure.FilterComponent.Expression, 800),
            SheetId = 0,
            SheetCode = "",
            RuleMessage = RegexUtils.TruncateString(validationRule.ErrorMessage, 2490),
            ShortLabel = RegexUtils.TruncateString(validationRule.ShortLabel, 900),
            IsWarning = validationRule.Severity.Trim() == "Warning",
            IsError = validationRule.Severity.Trim() == "Error",
            IsDataError = false,

            FormulaForIf = RegexUtils.TruncateString(BuildComponentValues(ruleStructure.IfComponent), 800),
            FormulaForThen = RegexUtils.TruncateString(BuildComponentValues(ruleStructure.ThenComponent), 800),
            FormulaForElse = RegexUtils.TruncateString(BuildComponentValues(ruleStructure.ElseComponent), 800),
            FormulaForFilter = RegexUtils.TruncateString(BuildComponentValues(ruleStructure.FilterComponent), 800),

        };

        var res = _SqlFunctions.CreateErrorRule(errorRule);
        return res;

        string BuildComponentValues(RuleComponent280 component)
        {
            var val = $"{component.DislayRuleTerms()}";
            return RegexUtils.TruncateString(val, 800);
        }

    }



    public static void UpdateExpressionWithShortLabel(string inputFilePath, string outputFilePath)
    {
        var connectionString = "Data Source = KYR-RYZEN\\MSSQLSERVER01; Initial Catalog =EIOPA_280_Hotfix; Integrated Security = true;TrustServerCertificate=True;";
        using var connectionLocal = new SqlConnection(connectionString);
        // Use 'using' for automatic file closing
        using (StreamReader reader = new StreamReader(inputFilePath))
        {
            using (StreamWriter writer = new StreamWriter(outputFilePath))
            {
                string line;
                // Read line by line until null (end of file)
                //$$$SHORT_LABEL(en) - BV1296: T.99.01 c0070 must not be reported.$$$BV1296
                var rgxLine = new Regex(@"\${3}SHORT_LABEL\(en\).*:(.*)\${3}(.*)");

                while ((line = reader.ReadLine()) != null)
                {
                    var match = rgxLine.Match(line);
                    if (match.Success)
                    {
                        var shortLabel = match.Groups[1].Value;
                        var validationCode = match.Groups[2].Value;
                        var sqlRule = @"update vValidationRuleExpressions set ShortLabel= @shortLabel where ValidationCode = @ValidationCode";

                        writer.WriteLine($"{sqlRule}");
                        var res = connectionLocal.Execute(sqlRule, new { shortLabel, validationCode });
                        var y = 3;
                    }
                    // Write the line wrapped in quotes

                }
            }
        }
    }

    class ValidationRuleComparer : IEqualityComparer<VValidationRuleExpressions>
    {
        public bool Equals(VValidationRuleExpressions? b1, VValidationRuleExpressions? b2)
        {
            if (ReferenceEquals(b1, b2))
                return true;

            if (b2 is null || b1 is null)
                return false;

            return b1.ValidationID == b2.ValidationID;

        }

        public int GetHashCode(VValidationRuleExpressions vr) => vr.ValidationID;
    }




}
