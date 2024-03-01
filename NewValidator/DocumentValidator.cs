using NewValidator.ValidationClasses;
using Serilog;
using Shared.DataModels;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;
using System;
using System.Collections.Generic;
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

        var validationRules = _SqlFunctions.SelectValidationRulesForModule(_mModule.ModuleID);
        validationRules = validationRules.Where(vr => vr.ValidationID == 783).ToList();
        foreach (var validationRule in validationRules)
        {
            var tables = _SqlFunctions.SelectTablesForValidationRule(validationRule.ValidationID);
            var HasOpenTable = tables.Any(tbl => _SqlFunctions.IsOpenTable(tbl.TableID));
            //**check if all the tables exist for this rule??
            var rule = RuleStructure280.CreateRuleStructure(validationRule.Rule);
            if (!HasOpenTable)
            {             
                rule = FillRuleStructureWithFactValues(rule);
                var isValidRule = ExpressionEvaluator.ValidateRule(rule);
            }
            else if (HasOpenTable)
            {


                //if there is an open table involved and there is NO seq then start from the master
                //-- start creating a rule for each row of the master (so you have the row )
                //--- fill the row of the slave by using the key
                //if there is an open table and there is a seq:TRUE (SUM or COUNT) then  
                //--- for each row of the seq, check the filter using the row of the slave . 
                //--- the resulting object will have both the sum and the count because the function is not known  at the time 
                var seqTableTerm = rule.IfComponent.RuleTerms.FirstOrDefault(rt => rt.IsSequence);
                
                if (seqTableTerm != null)
                {
                    var seqTable = tables.FirstOrDefault(tb => tb.TableCode.Trim() == seqTableTerm.T.Trim());
                    var facts = _SqlFunctions.SelectFactForAllRowsSeq(DocumentId, seqTable!.TableCode ,seqTableTerm.Z, seqTableTerm.C);
                    foreach(var fact in facts)
                    {
                        var row = fact.Row;
                        var fkKeyValue= 
                        //now go through the filter 
                    }

                }
                

            }

        }


        return 1;



    }

    private static ObjectTerm280 CreateObjectTerm280(TemplateSheetFact? fact, string defaultValue, bool IsTolerance)
    {
        if (fact == null)
        {
            return new ObjectTerm280("E", 0, IsTolerance, defaultValue, true, new List<TemplateSheetFact>());
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
        var objTerm = new ObjectTerm280(fact.DataTypeUse, fact.Decimals, IsTolerance, obj, false, new List<TemplateSheetFact>());
        return objTerm;
    }

    Dictionary<string, ObjectTerm280> ToOjectTerm280UsingFactValues(List<RuleTerm280> ruleTerms)
    {
        Dictionary<string, ObjectTerm280> plainTerms = ruleTerms
            .Where(pt => !pt.IsSequence)
            .Select(ruleTerm => new
            {
                ruleTerm.Letter,
                Zet = ruleTerm.Z,
                Fact = _SqlFunctions.SelectFactByRowCol(DocumentId, ruleTerm.T, ruleTerm.Z, ruleTerm.R, ruleTerm.C),
                ObjectTerm = CreateObjectTerm280(_SqlFunctions.SelectFactByRowCol(DocumentId, ruleTerm.T, ruleTerm.Z, ruleTerm.R, ruleTerm.C), ruleTerm.Dv, ruleTerm.IsTolerance)
            })
            .ToDictionary(kd => kd.Letter, kv => kv.ObjectTerm);

        Dictionary<string, ObjectTerm280> sequenceTerms = ruleTerms
            .Where(pt => pt.IsSequence)
            .Select(ruleTerm => new
            {
                ruleTerm.Letter,
                Zet = ruleTerm.Z,
                Fact = _SqlFunctions.SelectFactByRowCol(DocumentId, ruleTerm.T, ruleTerm.Z, ruleTerm.R, ruleTerm.C),
                ObjectTerm = CreateObjectTerm280(_SqlFunctions.SelectFactByRowCol(DocumentId, ruleTerm.T, ruleTerm.Z, ruleTerm.R, ruleTerm.C), ruleTerm.Dv, ruleTerm.IsTolerance)
            })
            .ToDictionary(kd => kd.Letter, kv => kv.ObjectTerm);



        return plainTerms;
    }

    private RuleStructure280 FillRuleStructureWithFactValues(RuleStructure280 ruleStructure)
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
}
