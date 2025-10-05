namespace Shared.DataModels;
using Dapper.Contrib.Extensions;

[Table("TemplateSheetInstanceDim")]
public class TemplateSheetInstanceDimDataModel
{
    [Key]
    public int TemplateSheetInstanceDimId { get; set; }

    public int TemplateSheetId { get; set; }

    public int MemberId { get; set; }

    public string MemberXBRLCode { get; set; } = string.Empty;

    public string MemberLabel { get; set; } = string.Empty;

    public string DomainCode { get; set; } = string.Empty;

    public string DomainLabel { get; set; } = string.Empty;


    public string DimensionCode { get; set; } = string.Empty;

    public string DimensionLabel { get; set; } = string.Empty;
    

}


