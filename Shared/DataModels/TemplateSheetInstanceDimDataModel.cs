namespace Shared.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper.Contrib.Extensions;

[Table("TemplateSheetInstanceDim")]

public class TemplateSheetInstanceDimDataModel
{
    public int TemplateSheetInstanceDimId { get; set; }
    public int TemplateSheetInstanceId { get; set; }
    public string Dim { get; set; }= string.Empty;
    public string Dom { get; set; }= string.Empty;
    public string DomValue { get; set; }= string.Empty;
    public bool IsExplicit { get; set; }= false;
    public string Signature { get; set; }= string.Empty;

}


