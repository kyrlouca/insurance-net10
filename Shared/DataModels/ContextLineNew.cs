
namespace Shared.DataModels;

public record ContextLineNew
{
    public int ContextLineId { get; set; }
    public int ContextId { get; set; }
    public string Signature { get; set; } = "";
    public string Dimension { get; set; } = "";
    public string Domain { get; set; } = "";
    public string DomainValue { get; set; } = "";
    public string DomainAndValue { get; set; } = "";
    public bool IsExplicit { get; set; }
    public int InstanceId { get; set; }
}
