using NewValidator.ValidationClasses;
using Serilog;
using Shared.DataModels;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.Implementation;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        //filters 

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

        //729 simple >
        //743 simple isnull
        //4880 matches
        //787 equality of enumaratin
        //1809 for min
        //783 for sum
        //1809 for max and sequence
        //702 dim
        //2050 scope

        var validationRules = _SqlFunctions.SelectValidationRulesForModule(_mModule.ModuleID);
        validationRules = validationRules.Where(vr => vr.ValidationID == 2050).ToList();
        foreach (var validationRule in validationRules)
        {
            var tablesInValidation = _SqlFunctions.SelectTablesForValidationRule(validationRule.ValidationID);
            var HasOpenTable = tablesInValidation.Any(tbl => _SqlFunctions.IsOpenTable(tbl.TableID));
            var hasAggregateFunction = new[] { "sum", "count" }.Any(f => validationRule.Rule.Contains(f));
            //**check if all the tables exist for this rule??

            if (!HasOpenTable)
            {
                var mainTable = tablesInValidation.FirstOrDefault();
                var sheets = _SqlFunctions.SelectTemplateSheetsByTableId(DocumentId, mainTable!.TableID);
                //foreach sheet...


                //*********** SCOPE 
                var rulex1 = RuleStructure280.CreateRuleStructure(validationRule.Rule, validationRule.Filter, validationRule.Scope);
                if (rulex1.ScopeType == ScopeType.None)
                {
                    rulex1.ScopeRowCols.Add("");
                }
                foreach (var scopeRowCol in rulex1.ScopeRowCols)
                {


                }
                if (validationRule.Scope is not null)
                {


                }

                var ruleClosed = RuleStructure280.CreateRuleStructure(validationRule.Rule, validationRule.Filter, validationRule.Scope);
                ruleClosed = FillRuleStructureWithFactValues(ruleClosed);
            }
            else if (HasOpenTable)
            {
                //if there is an open table involved and there is NO seq then start from the master
                //--  create a rule for each row of the m aster (so you have the row )
                //--- fill the row of the slave by using the key
                //if there is an open table and there is a seq:TRUE (SUM or COUNT) then  
                //--- for each row of the seq, check the filter using the row of the slave . 
                //--- the resulting object will have both the sum and the count because the function is not known  at the time 
                var rule = RuleStructure280.CreateRuleStructure(validationRule.Rule, validationRule.Filter, validationRule.Scope);

                //todo *****  maybe the else has a sequence
                var ifSeqTerms = rule.IfComponent.RuleTerms.Where(rt => rt.IsSequence);
                if (ifSeqTerms.Any() && hasAggregateFunction)
                {
                    rule = FillRuleStructureWithFactValues(rule);
                    foreach (var ifSeqTerm in ifSeqTerms)
                    {
                        var (sum, count) = CalculateSumofSequenceTerm(ifSeqTerm, rule.FilterComponent);
                        ReplaceObjTerm(rule.IfComponent.ObjectTerms, ifSeqTerm.Letter, -999, sum, count);
                    }

                    var thenSeqTerms = rule.ThenComponent.RuleTerms.Where(rt => rt.IsSequence);
                    foreach (var thenSeqTerm in thenSeqTerms)
                    {
                        var res = CalculateSumofSequenceTerm(thenSeqTerm, rule.FilterComponent);
                        ReplaceObjTerm(rule.IfComponent.ObjectTerms, thenSeqTerm.Letter, -999, res.sum, res.count);
                    }

                    var elseSeqTerms = rule.ElseComponent.RuleTerms.Where(rt => rt.IsSequence);
                    foreach (var elseSeqTerm in elseSeqTerms)
                    {
                        var res = CalculateSumofSequenceTerm(elseSeqTerm, rule.FilterComponent);
                        ReplaceObjTerm(rule.IfComponent.ObjectTerms, elseSeqTerm.Letter, -999, res.sum, res.count);
                    }

                    var isValidRule = ExpressionEvaluator.ValidateRule(rule);
                };

                if (!ifSeqTerms.Any())
                {


                    var mainTable = tablesInValidation.FirstOrDefault(tbl => _SqlFunctions.SelectTableKyrKey(tbl.TableCode)?.FK_TableCode is not null);
                    var kyrTable = _SqlFunctions.SelectTableKyrKey(mainTable?.TableCode ?? "");
                    var fklTable = kyrTable?.FK_TableCol ?? "";
                    var fkCol = kyrTable?.FK_TableCol ?? "";

                    var sheets = _SqlFunctions.SelectTemplateSheetsByTableId(DocumentId, mainTable!.TableID);
                    foreach (var sheet in sheets)
                    {
                        var rows = _SqlFunctions.SelectDistinctRowsInSheet(DocumentId, sheet.TemplateSheetId);
                        foreach (var row in rows)
                        {
                            //find the row from the column that has the foreign key
                            var ruleOpen = RuleStructure280.CreateRuleStructure(validationRule.Rule, validationRule.Filter, validationRule.Scope);

                            var relatedRow = _SqlFunctions.SelectFactByRowCol(DocumentId, sheet.TemplateSheetId, row, fkCol)?.Row ?? "";
                            UpdateRuleTermsWithRow(ruleOpen.IfComponent.RuleTerms, mainTable.TableCode, row, relatedRow,ScopeType.Rows);
                            UpdateRuleTermsWithRow(ruleOpen.ThenComponent.RuleTerms, mainTable.TableCode, row, relatedRow,ScopeType.Rows);
                            UpdateRuleTermsWithRow(ruleOpen.ElseComponent.RuleTerms, mainTable.TableCode, row, relatedRow,ScopeType.Rows);
                            ruleOpen = FillRuleStructureWithFactValues(ruleOpen);
                            var isValidRowRule = ExpressionEvaluator.ValidateRule(ruleOpen);
                        }
                    }


                }

            }

        }

        return 1;


        ObjectTerm280 ReplaceObjTerm(Dictionary<string, ObjectTerm280> objTerms, string objKey, object value, decimal sum, int count)
        {
            //maybe I need to set obj to zero instead of sum
            var objTerm = objTerms[objKey];
            var newObjTerm = objTerm with { Obj = sum, sumValue = Convert.ToDouble(sum), countValue = count };
            objTerms.Remove(objKey);
            objTerms.Add(objKey, newObjTerm);
            return newObjTerm;
        }

        static void UpdateRuleTermsWithRow(List<RuleTerm280> ruleTerms, string slaveTableCode, string rowCol, string relatedRowCol, ScopeType scopeType)
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

        static ObjectTerm280 CreateObjectTerm280(TemplateSheetFact? fact, string defaultValue, double sumValue, int countValue, bool IsTolerance)
        {
            if (fact == null)
            {
                return new ObjectTerm280("E", 0, IsTolerance, defaultValue, 0, 0, null, true);
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
                "D" => fact.DateTimeValue,
                _ => throw new NotImplementedException()
            };
            var objTerm = new ObjectTerm280(fact.DataTypeUse, fact.Decimals, IsTolerance, obj, sumValue, countValue, fact, false);
            return objTerm;
        }

        Dictionary<string, ObjectTerm280> ToOjectTerm280UsingFactValues(List<RuleTerm280> ruleTerms)
        {
            Dictionary<string, ObjectTerm280> plainTerms = ruleTerms
                .Select(ruleTerm => new
                {
                    ruleTerm.Letter,
                    Zet = ruleTerm.Z,
                    Fact = _SqlFunctions.SelectFactByRowCol(DocumentId, ruleTerm.T, ruleTerm.Z, ruleTerm.R, ruleTerm.C),
                    ObjectTerm = CreateObjectTerm280(_SqlFunctions.SelectFactByRowCol(DocumentId, ruleTerm.T, ruleTerm.Z, ruleTerm.R, ruleTerm.C), ruleTerm.Dv, 0, 0, ruleTerm.IsTolerance)
                })
                .ToDictionary(kd => kd.Letter, kv => kv.ObjectTerm);
            return plainTerms;
        }

         RuleStructure280 FillRuleStructureWithFactValues(RuleStructure280 ruleStructure)
        {
            //{t: S.23.01.02.02, r: R0700, c: C0060, z: Z0001, dv: 0, seq: False, id: v0, f: solvency, fv: solvency2} i= isum({t: S.23.01.02.02, r: R0710; R0720; R0730; R0740; R0760, c: C0060, z: Z0001, dv: emptySequence(), seq: True, id: v1, f: solvency, fv: solvency2})
            //objectTerm: an object which gets information from the fact and the the RuleTerm ({t:2000} such as sequence 

            Dictionary<string, ObjectTerm280> ifObjectTerms = ToOjectTerm280UsingFactValues(ruleStructure.IfComponent.RuleTerms);
            ruleStructure.IfComponent.ObjectTerms = ifObjectTerms;

            Dictionary<string, ObjectTerm280> thenObjectTerms = ToOjectTerm280UsingFactValues(ruleStructure.ThenComponent.RuleTerms);
            ruleStructure.ThenComponent.ObjectTerms = thenObjectTerms;

            Dictionary<string, ObjectTerm280> elseObjectTerms = ToOjectTerm280UsingFactValues(ruleStructure.ElseComponent.RuleTerms);
            ruleStructure.ElseComponent.ObjectTerms = elseObjectTerms;

            return ruleStructure;

        }

        (decimal sum, int count) CalculateSumofSequenceTerm(RuleTerm280 seqTableTerm, RuleComponent280 filterComponent)
        {

            var seqTable = seqTableTerm.T;
            var kyrTable = _SqlFunctions.SelectTableKyrKey(seqTableTerm.T);
            var relatedTable = kyrTable?.FK_TableCode ?? "";
            //from the sqTableTErms
            //find the related table.

            var facts = _SqlFunctions.SelectFactsInEveryRowForColumn(DocumentId, seqTableTerm.T, seqTableTerm.Z, seqTableTerm.C); ;
            decimal sum = 0;
            var count = 0;
            foreach (var fact in facts)
            {
                var row = fact.Row;
                var foreignKeyRow = fact.RowForeign;
                var isFilterValid = EvaluateFilterRow(filterComponent, relatedTable, fact.Row, fact.RowForeign);
                if (isFilterValid)
                {
                    sum += fact.NumericValue;
                    count++;
                }
            }
            return (sum, count);
        }

        bool EvaluateFilterRow(RuleComponent280 filterComponent, string relatedTable, string row, string foreignRow)
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
                Dictionary<string, ObjectTerm280> filterTerms = ToOjectTerm280UsingFactValues(filterComponent.RuleTerms);
                if (filterTerms.Any())
                {
                    var res = ExpressionEvaluator.EvaluateGeneralBooleanExpression(filterComponent.SymbolExpression, filterTerms);
                    return res;
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"EvaluateFilterRow : relatedTable:{relatedTable} row:{row} foreignRow:{foreignRow} ---- {ex}");
            }

        }


        RuleStructure280 FillRuleStructureOpenTablesWithFactValues(RuleStructure280 ruleStructure, MTable slaveTable)
        {
            //{t: S.23.01.02.02, r: R0700, c: C0060, z: Z0001, dv: 0, seq: False, id: v0, f: solvency, fv: solvency2} i= isum({t: S.23.01.02.02, r: R0710; R0720; R0730; R0740; R0760, c: C0060, z: Z0001, dv: emptySequence(), seq: True, id: v1, f: solvency, fv: solvency2})
            //objectTerm: an object which gets information from the fact and the the RuleTerm ({t:2000} such as sequence 

            var ifObjectTermsRelated = ruleStructure.IfComponent.RuleTerms.Where(rt => rt.T == slaveTable.TableCode).ToList();
            Dictionary<string, ObjectTerm280> ifObjectTermsxxx = ToOjectTerm280UsingFactValues(ifObjectTermsRelated);
            ruleStructure.IfComponent.ObjectTerms = ifObjectTermsxxx;
            //find the foreign key of the first object;

            //Dictionary<string, ObjectTerm280> ifObjectTerms = ToOjectTerm280UsingFactValues(ruleStructure.IfComponent.RuleTerms);
            //ruleStructure.IfComponent.ObjectTerms = ifObjectTerms;

            //Dictionary<string, ObjectTerm280> thenObjectTerms = ToOjectTerm280UsingFactValues(ruleStructure.ThenComponent.RuleTerms);
            //ruleStructure.ThenComponent.ObjectTerms = thenObjectTerms;

            //Dictionary<string, ObjectTerm280> elseObjectTerms = ToOjectTerm280UsingFactValues(ruleStructure.ElseComponent.RuleTerms);
            //ruleStructure.ElseComponent.ObjectTerms = elseObjectTerms;

            return ruleStructure;

        }


    }
}