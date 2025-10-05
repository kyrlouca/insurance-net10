namespace Shared.DataModels;
using Dapper.Contrib.Extensions;

[Table("mDomain")]
public class MDomain
{
    [Key]
    public int DomainID { get; set; }

    public string DomainCode { get; set; } = string.Empty;

    public string DomainLabel { get; set; } = string.Empty;

    public string DomainDescription { get; set; } = string.Empty;

    public string DomainXBRLCode { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public bool? IsTypedDomain { get; set; }

    public int? ConceptID { get; set; }
}