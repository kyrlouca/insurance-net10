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
        validationRules = validationRules.Where(vr => vr.ValidationID == 729).ToList();
        foreach (var validationRule in validationRules)
        {
            var tableId = validationRule.TableId;//108
            var rl = RuleStructure280.CreateRuleStructure(validationRule.Rule);
            var ifRule = rl.IfComponent;

            Dictionary<string, ObjectTerm280> plainTerms = new();
            foreach (var ruleTerm in ifRule.RuleTerms)
            {
                var zet = ruleTerm.Z;//todo need to figure out how to add zet to the fact
                var fact = _SqlFunctions.SelectFactByRowCol(DocumentId, ruleTerm.T, zet, ruleTerm.R, ruleTerm.C);                
                var obj= CreateObjectTerm280(fact,ruleTerm.Dv);
                plainTerms.Add(ruleTerm.Letter, obj);
            }
            ExpressionEvaluator.EvaluateExpression(ifRule.SymbolExpression, plainTerms);
        }
        

        return 1;
    }

    private static ObjectTerm280 CreateObjectTerm280(TemplateSheetFact? fact,string defaultValue)
    {
        if (fact == null)
        {
            return  new ObjectTerm280( "S", 0, defaultValue);
        }


        object obj = fact.DataTypeUse.Trim() switch {
            "E" => fact.TextValue,
            "S" => fact.NumericValue,
            "I" => fact.NumericValue,
            "M" => fact.NumericValue,
            "N" => fact.NumericValue,
            "P" => fact.NumericValue,
            "B" => fact.BooleanValue, 
            "D" => fact.DateTimeValue,
            _ => throw new NotImplementedException() 
        };
        var objTerm= new ObjectTerm280(fact.DataTypeUse,fact.Decimals,obj);
        return objTerm;
    }


}
