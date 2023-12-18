
namespace Shared.DataModels;
public record ContextModel
{

    public int InstanceId { get; set; }
    public int ContextId { get; set; }
    public string ContextXbrlId { get; set; }
    public string Signature { get;  set; }
    public int TableId { get; set; }
}

