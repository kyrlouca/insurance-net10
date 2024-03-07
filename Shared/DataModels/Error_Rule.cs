
namespace Shared.DataModels;
using Dapper.Contrib.Extensions;

[Table("ERROR_Rule")]
public class ERROR_Rule
{
    [Key]
    public int ErrorId { get; set; }
    public int RuleId { get; set; }
    public int ErrorDocumentId { get; set; }
    public int SheetId { get; set; }
    public string SheetCode { get; set; }
    public string RowCol { get; set; }
    public string RuleMessage { get; set; }
    public bool IsError { get; set; }
    public bool IsWarning { get; set; }
    public bool IsDataError { get; set; }
    public string Row { get; set; }
    public string Col { get; set; }
    public string DataValue { get; set; }
    public string DataType { get; set; }
    public string TableBaseFormula { get; set; }
    public string Filter { get; set; }
    public string Scope { get; set; }
    public string FormulaForIf { get; set; }
    public string FormulaForThen { get; set; }
    public string FormulaForElse { get; set; }
    public string RuleTerms { get; set; }
    
}
