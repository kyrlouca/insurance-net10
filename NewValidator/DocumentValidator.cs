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
        var validationRules = _SqlFunctions.SelectModuleValidationRules(_mModule.ModuleID);
        //729 simple >
        //743 simple isnull
        //4880 matches
        //787 equality of enumaratin
        validationRules = validationRules.Where(vr => vr.ValidationID == 787).ToList();
        foreach (var validationRule in validationRules)
        {            
            var tableId = validationRule.TableId;//108
            var rl = RuleStructure280.CreateRuleStructure(validationRule.Rule);

            //objectTerm: an object which gets information from the fact and the the RuleTerm ({t:2000} such as sequence 
            var ifComponent = rl.IfComponent;         
            Dictionary<string, ObjectTerm280> ifObjectTerms = UpdateRuleTermWithFactValues(ifComponent);            
            var isValidIf = ExpressionEvaluator.EvaluateExpression(ifComponent.SymbolExpression, ifObjectTerms);

            if (1 == 2)
            {
                var thenComponent = rl.ThenComponent;
                Dictionary<string, ObjectTerm280> thenObjectTerms = UpdateRuleTermWithFactValues(thenComponent);
                var isValidThen = ExpressionEvaluator.EvaluateExpression(thenComponent.SymbolExpression, thenObjectTerms);

                var elseComponent = rl.ElseComponent;
                Dictionary<string, ObjectTerm280> elseObjectTerms = UpdateRuleTermWithFactValues(elseComponent);
                var isValidElse = ExpressionEvaluator.EvaluateExpression(elseComponent.SymbolExpression, elseObjectTerms);

                var isPlainRule = ifComponent.IsValid && !elseComponent.IsValid && !thenComponent.IsValid;
                var isCompleteRule =
                    ifComponent.IsValid && elseComponent.IsValid && thenComponent.IsValid
                    || ifComponent.IsValid && !elseComponent.IsValid && !thenComponent.IsValid;
            }
            
        }


        return 1;
        


        Dictionary<string, ObjectTerm280> UpdateRuleTermWithFactValues(RuleComponent280 ruleComponent)
        {

            Dictionary<string, ObjectTerm280> plainTerms = ruleComponent.RuleTerms
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
    }

    private static ObjectTerm280 CreateObjectTerm280(TemplateSheetFact? fact,string defaultValue,bool IsTolerance)
    {
        if (fact == null)
        {
            return  new ObjectTerm280( "S", 0,  IsTolerance, defaultValue);
        }


        object obj = fact.DataTypeUse.Trim() switch {
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
        var objTerm= new ObjectTerm280(fact.DataTypeUse,fact.Decimals,IsTolerance, obj);
        return objTerm;
    }


}
