using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;
using NewValidator.ValidationClasses;
using Serilog;
using Serilog.Sinks.File;
using Shared.CommonRoutines;
using Shared.DataModels;
using Shared.GeneralUtils;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;
using Syncfusion.Office;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.Implementation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Schema;
using Validator.ValidationClasses;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace NewValidator;

internal enum ValidStatus { Valid, Error, Waring };
public record RelatedRowRecord(string TableCode, string RowRelated);
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

        //729 simple >2
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

        var errorCount = 0;
        var warningCount = 0;

        _SqlFunctions.UpdateDocumentStatus(_documentInstance.InstanceId, "P");

        var xx = CreateErrorDocument();


        //Select rules only with tables (the other rules check context dims or metrics)
        var validationRules = _SqlFunctions.SelectValidationExpressionsWithTablesForModule(_mModule.ModuleID)
            .Where(rl => rl.IsEnabled)
            .OrderBy(rl => rl.ValidationID).ToList();


        //************************************************************* Testing 
        var isTesting = false;
        if (_parameterData.IsDevelop && isTesting)
        {
            var exempted = new[] { 0 };
            validationRules = validationRules.Where(vr => !exempted.Contains(vr.ValidationID)).OrderBy(rl => rl.ValidationID).ToList();
        }
        var testingRuleId = 0;
        //testingRuleId = 1253;
        //testingId = 1698;
        //testingId = 1428;
        //testingRuleId = 4916;
        //testingRuleId = 1696;
        testingRuleId = 4643;


        if (_parameterData.IsDevelop && testingRuleId > 0)
        {
            Console.WriteLine($"\n***DEGUGGING ONLY Rule:{testingRuleId}");
            validationRules = validationRules.Where(vr => vr.ValidationID == testingRuleId).ToList();
        }
        //************************************************************* 

        foreach (var validationRule in validationRules)
        {
            //testingRuleId = 470 cannot validate this rule and was disabled;
            Console.WriteLine($"\n***Validating Rule:{validationRule.ValidationID}");
            var tablesInValidation = _SqlFunctions.SelectTablesForValidationRule(validationRule.ValidationID)
                .DistinctBy(tbl => tbl.TableID)
                .ToList();

            //Todo
            //282 has introduced validations without tables (validation 135)
            //at the moment skip those
            if (tablesInValidation.Count == 0)
            {
                continue;
            }
            var hasOnlyOpenTables = tablesInValidation.All(tbl => tbl.IsOpenTable);
            var hasOnlyClosedTables = tablesInValidation.All(tbl => !tbl.IsOpenTable);
            var hasMixedTables = tablesInValidation.Any(tbl => tbl.IsOpenTable) && tablesInValidation.Any(tbl => !tbl.IsOpenTable);


            //var hasAggregateFunctionOld = new[] { "sum", "count" }.Any(fn => validationRule.Rule.Contains(fn));
            //**todo check if all the sheets exist for this rule??

            //*********** SCOPE 
            var ruleForScope = RuleStructure280.CreateRuleStructure(validationRule.ValidationID, tablesInValidation, validationRule.Rule, validationRule.Filter, validationRule.Scope);
            var scopeRowcols = ruleForScope.ScopeRowCols;
            var scopeType = ruleForScope.ScopeType;
            //** if no scope, add one entry to go through
            if (scopeType == ScopeType.None)
            {
                scopeRowcols.Add("");
            }

            foreach (var scopeRowCol in scopeRowcols)
            {

                //check if no seq and all tables are open                 
                //--if there is a filter => it is an open table 
                //sum with seq 
                //for open tables use all the rows and apply filter and :filter
                //Closed Tables: sum without seq , use the R: to create terms (3780) OR the terms are separated by commas

                var hasAggregateFn = ruleForScope.IfComponent.RuleTerms.Any(rt => rt.IsSequence) || ruleForScope.ThenComponent.RuleTerms.Any(rt => rt.IsSequence);

                if ((!hasAggregateFn || hasAggregateFn) && hasOnlyClosedTables)
                {

                    ///////////////////////////////////////////
                    // Collect all terms
                    var allTermsForRule = ruleForScope.IfComponent.RuleTerms
                        .Concat(ruleForScope.ThenComponent.RuleTerms)
                        .Concat(ruleForScope.ElseComponent.RuleTerms)
                        .Concat(ruleForScope.FilterComponent.RuleTerms);
                    

                    var mainTable = GetMainOpenTable(ruleForScope, allTermsForRule, tablesInValidation);
                    if (mainTable is null)
                    {
                        var message = $"Missing entry of main table:{ ruleForScope.RuleFormula} ";
                        _logger.Error(message);
                        continue;
                    }
                    ////////////////////////////////////////////


                    var sheets = _SqlFunctions.SelectTemplateSheetsByTableId(DocumentId, mainTable.TableID);
                    foreach (var sheet in sheets)
                    {
                        //if any sheet (with this zet) is null do NOT check the rule
                        var sheetsWithSameZet = tablesInValidation.DistinctBy(tbl => tbl.TableID).Select(tbl => _SqlFunctions.SelectTemplateSheetByZetValue(DocumentId, tbl.TableCode, sheet.ZDimVal));
                        if (sheetsWithSameZet.Any(sh => sh is null))
                        {
                            continue;
                        }
                        var ruleClosed = RuleStructure280.CreateRuleStructure(validationRule.ValidationID, tablesInValidation, validationRule.Rule, validationRule.Filter, validationRule.Scope);
                        //if (scopeType != ScopeType.None)
                        //{
                        //    UpdateRuleTermsWithRowCol(ruleClosed.IfComponent.RuleTerms, mainTableCode, "", scopeRowCol, scopeRowCol, ruleClosed.ScopeType, true);
                        //    UpdateRuleTermsWithRowCol(ruleClosed.ThenComponent.RuleTerms, mainTableCode, "", scopeRowCol, scopeRowCol, ruleClosed.ScopeType, true);
                        //    UpdateRuleTermsWithRowCol(ruleClosed.ElseComponent.RuleTerms, mainTableCode, "", scopeRowCol, scopeRowCol, ruleClosed.ScopeType, true);
                        //    UpdateRuleTermsWithRowCol(ruleClosed.FilterComponent.RuleTerms, mainTableCode, "", scopeRowCol, scopeRowCol, ruleClosed.ScopeType, true);
                        //}

                        ////////////////////////////////////////
                        var allTerms = ruleClosed.IfComponent.RuleTerms
                                .Concat(ruleClosed.ThenComponent.RuleTerms)
                                .Concat(ruleClosed.ElseComponent.RuleTerms)
                                .Concat(ruleClosed.FilterComponent.RuleTerms);

                        foreach (var term in allTerms)
                        {
                            UpdateClosedTableTerm(term, ruleClosed.ScopeType, scopeRowCol);
                        }

                        /////////////////////////////////////////


                        //MAKE usingZet to true to  find facts using zet. 
                        ruleClosed.ZetValue = sheet.ZDimVal;
                        ruleClosed = FillRuleStructureWithFactValues(ruleClosed);
                        //var objs = ruleClosed.IfComponent.ObjectTerms.Select(ot => ot.Value.Obj).ToList();
                        var sumTerm = ruleClosed.IfComponent.RuleTerms.Where(rt => rt.IsSequence).FirstOrDefault();

                        if (sumTerm != null)
                        {
                            var (count, sum, sumDecimals) = CalculateSumOfClosedTable(sumTerm, sheet.ZDimVal);
                            ReplaceObjTerm(ruleClosed.IfComponent.ObjectTerms, sumTerm.Letter, sum, sum, count, sumDecimals);
                        }

                        var isValidClosedRule = GeneralEvaluator.ValidateRule(ruleClosed);
                        if (!isValidClosedRule)
                        {
                            CreateRuleError(ruleClosed, validationRule);
                            IncrementErrorOrWarning(validationRule.Severity);
                        }
                    }
                }

                if (!hasAggregateFn && (hasMixedTables || hasOnlyOpenTables))
                {
                    //create one rule for each row and apply filter
                    //there is no aggregate. Therefore do not check for zets for open or closed terms 


                    var ruleTest = RuleStructure280.CreateRuleStructure(validationRule.ValidationID, tablesInValidation, validationRule.Rule, validationRule.Filter, validationRule.Scope);

                    var allRuleTerms = ruleTest.IfComponent.RuleTerms
                          .Concat(ruleTest.ThenComponent.RuleTerms)
                          .Concat(ruleTest.ElseComponent.RuleTerms)
                          .Concat(ruleTest.FilterComponent.RuleTerms);

                    var mainTable = GetMainOpenTable(ruleForScope, allRuleTerms, tablesInValidation);
                    var mainTableCode = mainTable?.TableCode.Trim() ?? "";

                    var kyrTablesEntries = _SqlFunctions.SelectTableKyrKeys(mainTableCode)
                                        .Where(kt =>
                                            !string.IsNullOrEmpty(kt.TableCol) &&
                                            !string.IsNullOrEmpty(kt.FK_TableCode) &&
                                            !string.IsNullOrEmpty(kt.FK_TableCol))
                                        .ToList();


                    var isErrorUnmatched = LogUnmatchedForeignTables(mainTableCode, tablesInValidation, kyrTablesEntries, _logger);
                    if (isErrorUnmatched)
                    {
                        continue;
                    }

                    var sheets = _SqlFunctions.SelectTemplateSheetsByTableId(DocumentId, mainTable!.TableID);

                    foreach (var sheet in sheets)
                    {
                        //mixed or open tables => do not check zet
                        //if any sheet (regardless of zet) is null do NOT check the rule                        
                        var sheetsInDocument = tablesInValidation
                            .DistinctBy(tbl => tbl.TableID)
                            .SelectMany(tbl => _SqlFunctions.SelectTemplateSheetsByTableId(DocumentId, tbl.TableID))//this should not happen but I have used selectMany anyway
                            .DistinctBy(sheet => sheet.TableID);

                        if (sheetsInDocument.Count() != tablesInValidation.Count())
                        {
                            continue;
                        }


                        var rows = _SqlFunctions.SelectDistinctRowsInSheet(DocumentId, sheet.TemplateSheetId);
                        //rows = rows.Take(1).ToList();
                        var prevRowValid = true;

                        foreach (var row in rows)
                        {
                            //create one rule for each rule
                            RuleStructure280 ruleOpen = RuleStructure280.CreateRuleStructure(validationRule.ValidationID, tablesInValidation, validationRule.Rule, validationRule.Filter, validationRule.Scope);

                            if (ruleOpen is null)
                            {
                                var message = $"Cannot create rule structure for rule:{validationRule.ValidationID} ";
                                _logger.Error(message);
                                continue;
                            }

                            var allTerms = ruleOpen.IfComponent.RuleTerms
                                .Concat(ruleOpen.ThenComponent.RuleTerms)
                                .Concat(ruleOpen.ElseComponent.RuleTerms)
                                .Concat(ruleOpen.FilterComponent.RuleTerms);


                            var distinctTerms = allTerms
                                .DistinctBy(rt => rt.T);

                            // find and UPDATE the related row for each term table using KyrTable (only for open tables)                                                               
                            var derivedRows = GetDerivedRows(distinctTerms, mainTable, tablesInValidation, kyrTablesEntries, DocumentId, ruleOpen.ZetValue, row);


                            //update the row of each term for open tables
                            //if you find a derived row use it, otherwise use the current row
                            foreach (var term in allTerms)
                            {
                                if (!string.IsNullOrWhiteSpace(term.R))
                                {
                                    continue;
                                }
                                var derivedRow = derivedRows.FirstOrDefault(dr => dr.TableCode == term.T.Trim());
                                term.R = derivedRow?.RowRelated ?? row;
                            }

                            //**HERE WAS THE OLD UPDATING of related terms**

                            //some terms do not have a row. This is because the main table has a foreign key to a table but the rule does not use this table
                            //therefore the term has no row. 
                            //example rule 1253 with tables S.  

                            ruleOpen.ZetValue = sheet.ZDimVal;
                            //MAKE usingZet to false to avoid finding facts using zet. the closed table has no zet but the open tables for sum do have a zet
                            ruleOpen = FillRuleStructureWithFactValues(ruleOpen);

                            KleeneValue filterKleeneValue = ruleOpen!.FilterComponent.IsEmpty
                                ? KleeneValue.True
                                : GeneralEvaluator.EvaluateBooleanExpression(ruleOpen.RuleId, ruleOpen.FilterComponent.SymbolExpression, ruleOpen.FilterComponent.ObjectTerms);

                            //if filter has terms with null values, it is considered false here                            
                            if (filterKleeneValue != KleeneValue.True)
                            {
                                continue;
                            }

                            if (ruleOpen.IsInvalidOptionalKey)
                            {
                                continue;
                            }

                            var isValidRowRule = GeneralEvaluator.ValidateRule(ruleOpen);

                            if (!isValidRowRule)
                            {
                                if (prevRowValid) Console.WriteLine("");
                                Console.WriteLine($"{validationRule.Severity} ruleId:{validationRule.ValidationID} row:{row}");
                                CreateRuleError(ruleOpen, validationRule);
                                IncrementErrorOrWarning(validationRule.Severity);
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

                    //mixed or open tables => do not check zet
                    //if any sheet (regardless of zet) is null do NOT check the rule                        
                    var sheetsInDocument = tablesInValidation
                        .DistinctBy(tbl => tbl.TableID)
                        .SelectMany(tbl => _SqlFunctions.SelectTemplateSheetsByTableId(DocumentId, tbl.TableID))//this should not happen but I have used selectMany anyway
                        .DistinctBy(sheet => sheet.TableID);

                    if (sheetsInDocument.Count() != tablesInValidation.Count())
                    {
                        continue;
                    }


                    var mainTable = tablesInValidation.Where(tbl => !tbl.IsOpenTable).FirstOrDefault();
                    var xxxxmainTableCode = mainTable?.TableCode.Trim() ?? "";


                    var sheets = _SqlFunctions.SelectTemplateSheetsByTableId(DocumentId, mainTable!.TableID);

                    foreach (var sheet in sheets)
                    {
                        var rule = RuleStructure280.CreateRuleStructure(validationRule.ValidationID, tablesInValidation, validationRule.Rule, validationRule.Filter, validationRule.Scope);


                        //sumTersm will be evaluated again below
                        //MAKE ZET EMPTY. the closed table has no zet but the open tables for sum do have a zet
                        rule.ZetValue = "";
                        rule = FillRuleStructureWithFactValues(rule);

                        EvaluateSumTerms(rule.RuleId, tablesInValidation, rule.IfComponent, rule.FilterComponent, string.Empty);
                        EvaluateSumTerms(rule.RuleId, tablesInValidation, rule.ThenComponent, rule.FilterComponent, string.Empty);
                        EvaluateSumTerms(rule.RuleId, tablesInValidation, rule.ElseComponent, rule.FilterComponent, string.Empty);

                        var isValidRule = GeneralEvaluator.ValidateRule(rule);
                        if (!isValidRule)
                        {
                            Console.WriteLine($"{validationRule.Severity} ruleId:{rule.RuleId} ");
                            CreateRuleError(rule, validationRule);
                            IncrementErrorOrWarning(validationRule.Severity);
                        }

                    }


                }

                if (hasAggregateFn && hasOnlyOpenTables)
                {
                    //create one rule for each row and apply filter 

                    ///////////////////////////////////////////
                    // Collect all terms
                    var allTermsForRule = ruleForScope.IfComponent.RuleTerms
                        .Concat(ruleForScope.ThenComponent.RuleTerms)
                        .Concat(ruleForScope.ElseComponent.RuleTerms)
                        .Concat(ruleForScope.FilterComponent.RuleTerms);

                    var mainTable = GetMainOpenTable(ruleForScope, allTermsForRule, tablesInValidation);
                    var mainTableCode = mainTable?.TableCode.Trim() ?? "";
                    if (mainTable is null)
                    {
                        var message = $"Missing entry in TablesInValidation for main table:{mainTable?.TableCode} ";
                        _logger.Error(message);
                        continue;
                    }
                    ////////////////////////////////////////////


                    var kyrTables = _SqlFunctions.SelectTableKyrKeys(mainTableCode)
                                .Where(kt => tablesInValidation.Any(table => table.TableCode.Trim() == (kt.FK_TableCode ?? "").Trim()))
                                .ToList();

                    if (!kyrTables.Any())
                    {
                        kyrTables.Add(new MTableKyrKeys() { TableCode = mainTableCode });
                    }


                    var sheets = _SqlFunctions.SelectTemplateSheetsByTableId(DocumentId, mainTable!.TableID);
                    foreach (var sheet in sheets)
                    {
                        var rows = _SqlFunctions.SelectDistinctRowsInSheet(DocumentId, sheet.TemplateSheetId);
                        var prevRowValid = true;
                        var ruleOpen = RuleStructure280.CreateRuleStructure(validationRule.ValidationID, tablesInValidation, validationRule.Rule, validationRule.Filter, validationRule.Scope);


                        foreach (var row in rows)
                        {
                            //ToDo get derived rows for each row and then update the terms directly without using the tables



                            foreach (var kyrTbl in kyrTables)
                            {
                                //update the row number for each related table
                                var relatedTableCode = kyrTbl?.FK_TableCode?.Trim() ?? "";
                                var relatedTableCol = kyrTbl?.FK_TableCol?.Trim() ?? "";
                                var mainTableCol = kyrTbl?.TableCol?.Trim() ?? "";

                                var factFromMain = _SqlFunctions.SelectFactByRowColTableCode(DocumentId, mainTableCode, sheet.ZDimVal, row, mainTableCol);
                                var factFromMainValue = factFromMain?.TextValue.Trim() ?? "";
                                var relatedRowNew = _SqlFunctions.SelectFactsByColAndTextValue(DocumentId, relatedTableCode, relatedTableCol, factFromMainValue).FirstOrDefault();
                                var relatedRow = relatedRowNew?.Row?.Trim() ?? "";


                                UpdateRuleTermsWithRowCol(ruleOpen.IfComponent.RuleTerms, mainTableCode, relatedTableCode, row, relatedRow, ScopeType.Rows);
                                UpdateRuleTermsWithRowCol(ruleOpen.ThenComponent.RuleTerms, mainTableCode, relatedTableCode, row, relatedRow, ScopeType.Rows);
                                UpdateRuleTermsWithRowCol(ruleOpen.ElseComponent.RuleTerms, mainTableCode, relatedTableCode, row, relatedRow, ScopeType.Rows);
                                UpdateRuleTermsWithRowCol(ruleOpen.FilterComponent.RuleTerms, mainTableCode, relatedTableCode, row, relatedRow, ScopeType.Rows);

                            }
                            if (1 == 2)
                            {
                                //###############################################
                                //NEED To test this before removing the old code
                                var allTerms = ruleOpen.IfComponent.RuleTerms
                                    .Concat(ruleOpen.ThenComponent.RuleTerms)
                                    .Concat(ruleOpen.ElseComponent.RuleTerms)
                                    .Concat(ruleOpen.FilterComponent.RuleTerms);


                                var distinctTerms = allTerms
                                    .DistinctBy(rt => rt.T);


                                var derivedRows = GetDerivedRows(allTerms, mainTable, tablesInValidation, kyrTables, DocumentId, sheet.ZDimVal, row);
                                foreach (var term in allTerms)
                                {
                                    if (!string.IsNullOrWhiteSpace(term.R))
                                    {
                                        continue;
                                    }
                                    var derivedRow = derivedRows.FirstOrDefault(dr => dr.TableCode == term.T.Trim());
                                    term.RowTest = derivedRow?.RowRelated ?? row;
                                }
                                if (allTerms.Any(t => t.R != t.RowTest))
                                {
                                    //this should not happen

                                    var message = $"DIFF ROW ";
                                    _logger.Error(message);
                                }
                            }



                            //MAKE usingZet to false to avoid finding facts using zet. the closed table has no zet but the open tables for sum do have a zet
                            ruleOpen.ZetValue = sheet.ZDimVal;
                            ruleOpen = FillRuleStructureWithFactValues(ruleOpen);
                            ///Sum for closed terms
                            EvaluateSumTermsForFixedCells(ruleOpen.RuleId, ruleOpen.IfComponent, ruleOpen.FilterComponent, ruleOpen.ZetValue);
                            EvaluateSumTermsForFixedCells(ruleOpen.RuleId, ruleOpen.ThenComponent, ruleOpen.FilterComponent, ruleOpen.ZetValue);
                            EvaluateSumTermsForFixedCells(ruleOpen.RuleId, ruleOpen.ElseComponent, ruleOpen.FilterComponent, ruleOpen.ZetValue);


                            KleeneValue filterKleeneValue = ruleOpen!.FilterComponent.IsEmpty
                                ? KleeneValue.True
                                : GeneralEvaluator.EvaluateBooleanExpression(ruleOpen.RuleId, ruleOpen.FilterComponent.SymbolExpression, ruleOpen.FilterComponent.ObjectTerms);

                            //if filter has terms with null values, it is considered false here                            
                            if (filterKleeneValue != KleeneValue.True)
                            {
                                continue;
                            }
                            ;

                            var isValidRowRule = GeneralEvaluator.ValidateRule(ruleOpen);

                            if (!isValidRowRule)
                            {
                                if (prevRowValid) Console.WriteLine("");
                                Console.WriteLine($"{validationRule.Severity} ruleId:{validationRule.ValidationID} row:{row}");
                                CreateRuleError(ruleOpen, validationRule);
                                IncrementErrorOrWarning(validationRule.Severity);
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

                    //we may have aggregates but the sum  are within ?
                }

            }


        }
        var status = errorCount == 0 ? "V" : "E";
        _SqlFunctions.UpdateDocumentStatus(_documentInstance.InstanceId, status);
        return 1;

        void EvaluateSumTerms(int ruleId, List<MTable> ruleTables, RuleComponent280 ruleComponent, RuleComponent280 filterComponent, string zetValue)
        {
            var seqTerms = ruleComponent.RuleTerms.Where(rt => rt.IsSequence);
            foreach (var seqTerm in seqTerms)
            {
                var res = CalculateSumofOpenTable(ruleId, ruleTables, seqTerm, filterComponent, zetValue);
                ReplaceObjTerm(ruleComponent.ObjectTerms, seqTerm.Letter, res.sum, res.sum, res.count, res.decimals);
            }
        }

        void EvaluateSumTermsForFixedCells(int ruleId, RuleComponent280 ruleComponent, RuleComponent280 filterComponent, string zetValue)
        {
            var seqTerms = ruleComponent.RuleTerms.Where(rt => rt.IsSequence);
            foreach (var thenSeqTerm in seqTerms)
            {
                var res = CalculateSumOfClosedTable(thenSeqTerm, zetValue);
                ReplaceObjTerm(ruleComponent.ObjectTerms, thenSeqTerm.Letter, res.sum, res.sum, res.count, res.decimals);
            }
        }
        void IncrementErrorOrWarning(string severity)
        {
            if (severity.Trim() == "Error")
            {
                errorCount++;
            }
            else
            {
                warningCount++;
            }
        }
    }


    private (MTable? MainTable, string MainTableCode) GetMainOpenTableOld(
    RuleStructure280 ruleForScope,
    IEnumerable<MTable> tablesInValidation)
    {
        // Collect all terms
        var allTermsForRule = ruleForScope.IfComponent.RuleTerms
            .Concat(ruleForScope.ThenComponent.RuleTerms)
            .Concat(ruleForScope.ElseComponent.RuleTerms)
            .Concat(ruleForScope.FilterComponent.RuleTerms);

        // Determine main table code
        var mainTableCode = ruleForScope.ScopeTable?.Trim();

        if (string.IsNullOrEmpty(mainTableCode))
        {
            mainTableCode = allTermsForRule.FirstOrDefault()?.T?.Trim() ?? "";
        }

        // Find the table
        var mainTable = tablesInValidation
            .Where(t => t.IsOpenTable)
            .FirstOrDefault(tb => tb.TableCode.Trim() == mainTableCode);

        return (mainTable, mainTableCode);
    }




    //private static void UpdateRuleTermsWithRowCol(List<RuleTerm280> ruleTerms, string mainTableCode, string slaveTableCode, string rowCol, string relatedRowCol, ScopeType scopeType, bool IsClosedTables = false)
    private static void UpdateRuleTermsWithRowCol(List<RuleTerm280> ruleTerms, string mainTableCode, string slaveTableCode, string rowCol, string relatedRowCol, ScopeType scopeType)
    {
        //We do not have the concept of master-slave table for closed tables.
        //Therefore, the rowcol of the main table as difened in the scope, should be applied to the other closed tables
        if (scopeType == ScopeType.Rows)
        {
            var rTerms = ruleTerms.Where(term => string.IsNullOrEmpty(term.R)).ToList();
            foreach (var term in rTerms)
            {
                //if (term.T.Trim() == mainTableCode.Trim() || IsClosedTables)
                if (term.T.Trim() == mainTableCode.Trim())
                {
                    term.R = rowCol;
                }
                else if (term.T.Trim() == slaveTableCode.Trim())
                {
                    term.R = relatedRowCol;
                }

            }
        }
        if (scopeType == ScopeType.Cols)
        {
            var cTerms = ruleTerms.Where(term => string.IsNullOrEmpty(term.C)).ToList();

            foreach (var term in cTerms)
            {
                //if (term.T.Trim() == mainTableCode.Trim() || IsClosedTables)
                if (term.T.Trim() == mainTableCode.Trim())
                {
                    term.C = rowCol;
                }
                else if (term.T.Trim() == slaveTableCode.Trim())
                {
                    term.C = relatedRowCol;
                }

            }

        }

    }

    private static ObjectTerm280 CreateObjectTerm280(TemplateSheetFact? fact, string defaultValue, double sumValue, int countValue, bool IsTolerance, string filter)
    {
        if (fact == null || (fact.FactId == 0))
        {
            if ((fact?.FactId ?? 0) == 0)
            {
                //var xxh = 32;
            }
            var defaultDataType = defaultValue.Trim() switch
            {
                "0" => "N",
                "[Default]" => "S",
                "emptySequence()" => "N",
                "CreateDate(1900,01,01)" => "D",
                _ => "S"
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

        var numericTypes = new[] { "I", "M", "N", "P" };

        var objTerm = new ObjectTerm280(fact.DataTypeUse, fact.Decimals, IsTolerance, obj, sumValue, countValue, fact, false, filter);
        return objTerm;
    }

    private Dictionary<string, ObjectTerm280> ToOjectTerm280UsingFactValues(List<MTable> ruleTables, List<RuleTerm280> ruleTerms, string zetValue)
    {

        //check whether the term does not have a zet value (Z0001)
        //I changed the program, and in some cases I pass deliberately zetValue="" to avoid using the Zet 
        //this was necessary for rule 348. because open tables S.06.02.01.01 and S.07.01.01.01 have  zets with different values
        //var ruleTermsWithZet = ruleTerms.Select(rt => rt with { Z =   rt.Z.Contains("Z00") ? zetValue : "" });
        var openTables = ruleTables.Where(tbl => tbl.IsOpenTable).Select(tbl => tbl.TableCode);
        var ruleTermsWithUpdatedZetValue = ruleTerms.Select(rt => rt with { Z = (openTables.Contains(rt.T.Trim()) || !rt.Z.Contains("Z00")) ? "" : zetValue });
        Dictionary<string, ObjectTerm280> plainTermsOld = ruleTermsWithUpdatedZetValue
            .Select(rtm => new
            {
                rtm.Letter,
                Zet = rtm.Z,
                Fact = _SqlFunctions.SelectFactByRowColTableCode(DocumentId, rtm.T, rtm.Z, rtm.R, rtm.C),
                ObjectTerm = CreateObjectTerm280(_SqlFunctions.SelectFactByRowColTableCode(DocumentId, rtm.T, rtm.Z, rtm.R, rtm.C), rtm.Dv, 0, 0, rtm.IsTolerance, UpdateRuleTermFilter(rtm.Letter, rtm.Filter))
            })
            .ToDictionary(kd => kd.Letter, kv => kv.ObjectTerm);

        Dictionary<string, ObjectTerm280> plainTerms = ruleTermsWithUpdatedZetValue
            .Select(rtm =>
            {
                var fact = _SqlFunctions.SelectFactByRowColTableCode(DocumentId, rtm.T, rtm.Z, rtm.R, rtm.C);
                fact ??= CreateFactWithDefaultValue(ruleTables, rtm);
                var objectTerm = CreateObjectTerm280(fact, rtm.Dv, 0, 0, rtm.IsTolerance, UpdateRuleTermFilter(rtm.Letter, rtm.Filter));

                var objUpd = new
                {
                    rtm.Letter,
                    Zet = rtm.Z,
                    Fact = fact,
                    ObjectTerm = objectTerm
                };
                return objUpd;

            })
            .ToDictionary(objT => objT.Letter, objT => objT.ObjectTerm);


        return plainTerms;
        TemplateSheetFact? CreateFactWithDefaultValue(List<MTable> ruleTables, RuleTerm280 rtm)
        {
            var fact = new TemplateSheetFact();
            var tbl = ruleTables.FirstOrDefault(tbl => tbl.TableCode == rtm.T)!;
            var cell = _SqlFunctions.SelectTableCells(tbl.TableID)
                .FirstOrDefault(cell => cell.BusinessCode.Contains(rtm.C) && (tbl.IsOpenTable || cell.BusinessCode.Contains(rtm.R)));
            var rgxXbrl = new Regex(@"^MET\((.*?)\)");
            var match = rgxXbrl.Match(cell?.DatapointSignature ?? "xxx");
            var xbrlCode = match.Success ? match.Groups[1].Value : "";

            var metric = _SqlFunctions.SelectMMetric(xbrlCode);
            var hierarchyId = metric?.ReferencedHierarchyID ?? -1;

            var defaultValue = _SqlFunctions.SelectDefaultMemberFromHierarchy(hierarchyId)?.MemberXBRLCode;
            if (metric is not null && defaultValue is not null)
            {
                var dataTypeUse = metric is not null ? ConstantsAndUtils.SimpleDataTypes[metric.DataType] : "";
                fact = new TemplateSheetFact() { TextValue = defaultValue, DataTypeUse = dataTypeUse };
            }
            return fact;
        }
    }

    ObjectTerm280 ReplaceObjTerm(Dictionary<string, ObjectTerm280> objTerms, string objKey, object value, double sum, int count, int decimals)
    {
        //maybe I need to set obj to zero instead of sum
        var objTerm = objTerms[objKey];
        var newObjTerm = objTerm with { Obj = sum, sumValue = sum, countValue = count, DataType = "N", Decimals = decimals };
        objTerms.Remove(objKey);
        objTerms.Add(objKey, newObjTerm);
        return newObjTerm;
    }

    private string UpdateRuleTermFilter(string letter, string filter)
    {
        //not(isNull({t: SR.26.01.01.03, r: R0012; R0014; R0020; R0030; R0040, c: C0010, z: Z0001, dv: emptySequence(), filter: dim(this(), [s2c_dim:PO]) = [s2c_PU:x60] and not(isNull(dim(this(), [s2c_dim:FN]))), seq: True, id: v1, f: solvency, fv: solvency2}))
        //filter: dim(this(), [s2c_dim:PO]) = [s2c_PU:x60] and not(isNull(dim(this(), [s2c_dim:FN])))
        var res = filter.Replace("this()", $"this({letter})");
        return res;
    }

    private RuleStructure280 FillRuleStructureWithFactValues(RuleStructure280 ruleStructure)
    {
        //This procedure creates the object terms(which have fact values) for each component 
        //rule structure has components for if, then else, filter
        //A compoent has ruleTerms which have the specification of each cell, and the objectTerms which have the corresponding values
        //{t: S.23.01.02.02, r: R0700, c: C0060, z: Z0001, dv: 0, seq: False, id: v0, f: solvency, fv: solvency2} i= isum({t: S.23.01.02.02, r: R0710; R0720; R0730; R0740; R0760, c: C0060, z: Z0001, dv: emptySequence(), seq: True, id: v1, f: solvency, fv: solvency2})
        //objectTerm: an object which gets information from the fact and the the RuleTerm ({t:2000} such as sequence 

        //if we have only closed tables or mixed table make sure the zetValue is blank 

        var zetValue = ruleStructure.ZetValue;

        CreateComponentObjectTerms(ruleStructure.IfComponent, ruleStructure.RuleTables, zetValue);
        CreateComponentObjectTerms(ruleStructure.ThenComponent, ruleStructure.RuleTables, zetValue);
        CreateComponentObjectTerms(ruleStructure.ElseComponent, ruleStructure.RuleTables, zetValue);
        CreateComponentObjectTerms(ruleStructure.FilterComponent, ruleStructure.RuleTables, zetValue);



        return ruleStructure;

    }

    private void CreateComponentObjectTerms(RuleComponent280 ruleComponent, List<MTable> ruleTables, string zetValue)
    {
        Dictionary<string, ObjectTerm280> objectTerms = ToOjectTerm280UsingFactValues(ruleTables, ruleComponent.RuleTerms, zetValue);
        ruleComponent.ObjectTerms = objectTerms;
    }


    private (int count, double sum, int decimals) CalculateSumOfClosedTable(RuleTerm280 ruleTermRec, string zetValue)
    {
        //isum({t: S.23.01.02.01, r: R0300; R0310; R0320; R0330; R0340; R0350; R0360; R0370, z: Z0001, dv: emptySequence(), seq: True,.. )
        //scope({t: S.23.01.02.01, c:C0010;C0040, f: solvency, fv: solvency2})
        var (sumScopeType, rowCols) = GetScopeRowCols(ruleTermRec);
        var sum = 0.0;
        var count = 0;
        var decimals = 0;
        foreach (var rowCol in rowCols)
        {
            var row = sumScopeType == ScopeType.Rows ? rowCol : ruleTermRec.R;
            var col = sumScopeType == ScopeType.Cols ? rowCol : ruleTermRec.C;
            var zet = ruleTermRec.Z.Contains("Z000") ? zetValue : "";

            var res = _SqlFunctions.GetSumofTableCode(DocumentId, ruleTermRec.T, zet, row, col);
            //var fact = _SqlFunctions.SelectFactByRowColTableCode(DocumentId, ruleTermRec.T, zet, row, col);

            sum += res.sum;
            count += res.count;

            decimals = Math.Abs(decimals) > Math.Abs(res.decimals) ? decimals : res.decimals;

        }
        return (count, sum, decimals);

        (ScopeType scopeType, List<string> rowCol) GetScopeRowCols(RuleTerm280 ruleTerm)
        {
            var rows = string.IsNullOrEmpty(ruleTerm.R) ? new List<string>() : ruleTerm.R.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var cols = string.IsNullOrEmpty(ruleTerm.C) ? new List<string>() : ruleTerm.C.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            ScopeType sumScopeType = rows switch
            {
                _ when (!rows.Any() && !cols.Any()) => ScopeType.None,
                _ when (cols.Count() > rows.Count()) => ScopeType.Cols,
                _ => ScopeType.Rows
            };


            ScopeType sumScopeTypeOld = rows switch
            {
                _ when rows.Any() => ScopeType.Rows,
                _ when cols.Any() => ScopeType.Cols,
                _ => ScopeType.None
            };

            if (sumScopeType != sumScopeTypeOld && !(rows.Any() && cols.Any()))
            {
                throw new Exception($"sumScopeType DIFFERENT when there are rows AND cols");
            }

            var rowCols = sumScopeType switch
            {
                ScopeType.Rows => rows,
                ScopeType.Cols => cols,
                _ => new List<string>()
            };
            return (sumScopeType, rowCols);
        }

    }

    private (int count, double sum, int decimals) CalculateSumofOpenTable(int ruleId, List<MTable> ruleTables, RuleTerm280 seqTableTerm, RuleComponent280 filterComponent, string zetValue)
    {
        //the seqTableTerm is the term with the sum and it is considered the main table. 
        //We need to apply the filter for each row of the seqTableTerm
        //to evaluate the the filter for each row, need to find the related rows of all the terms
        var seqTableCode = seqTableTerm.T;
        var kyrTable = _SqlFunctions.SelectTableKyrKey(seqTableCode);
        var mainTableCol = kyrTable?.TableCol ?? "";
        var relatedTable = kyrTable?.FK_TableCode ?? "";
        var relatedTableCode = kyrTable?.FK_TableCode ?? "";
        var relatedTableCol = kyrTable?.FK_TableCol ?? "";


        var mainTable = ruleTables.FirstOrDefault(tbl => _SqlFunctions.SelectTableKyrKey(tbl.TableCode)?.FK_TableCode is not null);
        if (mainTable is null)
        {
            mainTable = ruleTables.FirstOrDefault(tb => tb.IsOpenTable);
        }

        var mainTableCode = mainTable?.TableCode?.Trim() ?? "";
        var kyrTables = _SqlFunctions.SelectTableKyrKeys(mainTableCode)
            .Where(kt => ruleTables.Any(table => table.TableCode.Trim() == (kt.FK_TableCode ?? "").Trim()))
            .ToList();

        if (!kyrTables.Any())
        {
            kyrTables.Add(new MTableKyrKeys() { TableCode = mainTableCode });
        }


        var seqSheet = _SqlFunctions.SelectTemplateSheets(_documentInstance.InstanceId).Where(sheet => sheet.TableCode == seqTableCode).FirstOrDefault();
        if (seqSheet is null)
        {
            return (0, 0, 0);
        }

        var facts = _SqlFunctions.SelectFactsInEveryRowForColumn(DocumentId, seqSheet.TemplateSheetId, seqTableTerm.C);
        double sum = 0;
        var count = 0;
        var decimals = 0;
        foreach (var fact in facts)
        {
            Console.Write("?");
            var row = fact.Row;
            var keyFactFromMain = _SqlFunctions.SelectFactByRowCol(DocumentId, seqSheet.TemplateSheetId, row, mainTableCol);

            foreach (var kyrTbl in kyrTables)
            {
                var relatedRowNew = _SqlFunctions.SelectFactsByColAndTextValue(DocumentId, relatedTableCode, relatedTableCol, keyFactFromMain?.TextValue ?? "").FirstOrDefault();
                var relatedRow = relatedRowNew?.Row?.Trim() ?? "";
                filterComponent.RuleTerms.ForEach(rt => rt.R = "");
                UpdateRuleTermsWithRowCol(filterComponent.RuleTerms, mainTableCode, relatedTableCode, row, relatedRow, ScopeType.Rows);
            }

            CreateComponentObjectTerms(filterComponent, ruleTables, "");
            var isFilterValid = filterComponent.IsEmpty
                                ? KleeneValue.True
                                : GeneralEvaluator.EvaluateBooleanExpression(ruleId, filterComponent.SymbolExpression, filterComponent.ObjectTerms);
            if (GeneralEvaluator.ToBoolean(isFilterValid))
            {
                sum += fact.NumericValue;
                count++;
                decimals = Math.Abs(decimals) > Math.Abs(fact.Decimals) ? decimals : fact.Decimals;
            }
        }
        return (count, sum, decimals);
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
            IsWarning = validationRule.Severity.Trim().ToUpper() == "WARNING",
            IsError = validationRule.Severity.Trim().ToUpper() == "ERROR",
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
            var val = $"{component.DislayRuleComponentTerms()}";
            return RegexUtils.TruncateString(val, 800);
        }

    }


    public static void UpdateRuleMessagesWithShortLabel(string inputFilePath, string outputFilePath)
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
                var rgxLine = new Regex(@"\${3}SHORT_LABEL\(en\).*-(.*)\${3}(.*)", RegexOptions.Compiled);

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




    private static void UpdateClosedTableTerm(RuleTerm280 term, ScopeType scopeType, string rowCol)
    {
        switch (scopeType)
        {
            case ScopeType.Rows:
                term.R = rowCol;
                break;
            case ScopeType.Cols:
                term.C = rowCol;
                break;
            case ScopeType.None:
            default:
                break;
        }

    }

    private List<RelatedRowRecord> GetDerivedRows(
    IEnumerable<RuleTerm280> ruleTerms,
    MTable mainTable,
    IEnumerable<MTable> tablesInValidation,
    IEnumerable<MTableKyrKeys> kyrTablesNew,
    int DocumentId,
    string sheetZDimVal,
    string row)
    {
        var derivedRows = new List<RelatedRowRecord>();


        foreach (var term in ruleTerms)
        {
            var termTableCode = term.T.Trim();

            // Skip if term table is the main table
            if (termTableCode == mainTable.TableCode)
                continue;

            // Find related table in validation
            var relatedTbl = tablesInValidation.FirstOrDefault(tv => tv.TableCode == termTableCode);
            if (relatedTbl is null)
            {
                _logger.Error($"Missing entry in TablesInValidation for table: {termTableCode}");
                continue;
            }

            // Skip if related table is not open
            if (!relatedTbl.IsOpenTable)
                continue;

            // Find KyrTable mapping
            var tblKyr = kyrTablesNew.FirstOrDefault(kt => kt.FK_TableCode.Trim() == termTableCode);
            if (tblKyr is null)
            {
                _logger.Error($"Missing entry in KyrTable for table: {mainTable.TableCode} fk_table: {termTableCode}");
                continue;
            }

            // Get main fact
            var factMain = _SqlFunctions.SelectFactByRowColTableCode(DocumentId, tblKyr.TableCode, sheetZDimVal, row, tblKyr.TableCol);
            var factMainValue = factMain?.TextValue.Trim() ?? "";

            // Get related row
            var relatedRowNew = _SqlFunctions.SelectFactsByColAndTextValue(DocumentId, tblKyr.FK_TableCode, tblKyr.FK_TableCol, factMainValue)
                .FirstOrDefault();
            var relatedRow = relatedRowNew?.Row?.Trim() ?? "";

            derivedRows.Add(new RelatedRowRecord(termTableCode, relatedRow));
        }

        return derivedRows;
    }

    private static bool LogUnmatchedForeignTables(
                    string mainTableCode,
                    IEnumerable<MTable> tablesInValidation,
                    IEnumerable<MTableKyrKeys> kyrTablesEntries,
                    ILogger _logger
        )
    {
        // Check for unmatched foreign open tables when there are two or more open tables
        //the first table would be the first open table in the rule
        bool isError = false;
        var foreignOpenTables = tablesInValidation.Where(ti => ti.IsOpenTable && ti.TableCode != mainTableCode).ToList();
        //var unmatchedForeign = foreignOpenTables.Where(fo => !kyrTablesEntries.Any(kt => kt.FK_TableCode == fo.TableCode));
        var unmatchedForeign = foreignOpenTables.Where(fo => !kyrTablesEntries.Any(kt => kt.FK_TableCode.Trim() == fo.TableCode.Trim()));

        foreach (var un in unmatchedForeign)
        {
            string message =
                $"Missing entry in KyrTable =>  main table:{mainTableCode}, foreign open table:{un.TableCode} ";
            _logger.Error(message);
            isError = true; // mark error if at least one exists
        }

        return isError;
    }

    private static bool HasManyOpenTables(IEnumerable<MTable> tablesInValidation)
    {
        var openTables = tablesInValidation.DistinctBy(tab => tab.TableID).Where(ti => ti.IsOpenTable).ToList();
        return openTables.Count() > 1;
    }

    private static MTable? GetMainOpenTable(RuleStructure280 ruleForScope, IEnumerable<RuleTerm280> ruleTerms, IEnumerable<MTable> tablesInValidation)
    {

        // check first in the scope
        var mainTableCode = ruleForScope.ScopeTable?.Trim() ?? "";
        MTable? mainTable = null;

        if (!string.IsNullOrEmpty(mainTableCode))
        {
            mainTable = tablesInValidation.FirstOrDefault(tb => tb.TableCode.Trim() == mainTableCode);
            return mainTable;
        }

        //if there are NO more than one open table, take the first table in the rule
        if (!HasManyOpenTables(tablesInValidation))
        {
            var firstTableTerm = ruleTerms.FirstOrDefault()?.T ?? "";
            mainTable = tablesInValidation.FirstOrDefault(tv => tv.TableCode.Equals(firstTableTerm));
            return mainTable;
        }

        var openValidationTables = tablesInValidation.Where(t => t.IsOpenTable);
        var firstMatch = ruleTerms.FirstOrDefault(rt => openValidationTables.Any(tv => tv.TableCode == rt.T));
        if (firstMatch == null)
        {
            return null;
        }
        mainTable = openValidationTables.FirstOrDefault(tb => tb.TableCode.Trim() == firstMatch.T);
        return mainTable;

    }


}
