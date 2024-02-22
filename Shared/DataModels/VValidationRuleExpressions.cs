namespace Shared.DataModels;

public class VValidationRuleExpressions
{
    public int ValidationID { get; set; }
    public string ValidationCode { get; set; }
    public string ErrorMessage { get; set; }
    public string Rule { get; set; }
    public string Filter { get; set; }
    public string Prerequisites { get; set; }
    public string Join { get; set; }
    public string Scope { get; set; }
    public string SQL { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsPoint { get; set; }
    public bool AlwaysOn { get; set; }
    public bool IncludeInXBRL { get; set; }
    public string ToleranceMargin { get; set; }
    public string VariableNames { get; set; }
    public int ConceptID { get; set; }
    public int TableId { get; set; }
    public string TableCode { get; set; }
}