namespace Shared.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper.Contrib.Extensions;

[Table("TemplateSheetInstance")]

public class TemplateSheetInstanceDataModel
{

    [Key]
    public int TemplateSheetId { get; set; }
    public int InstanceId { get; set; }
    public string SheetCode { get; set; }= string.Empty;
    public string SheetCodeZet { get; set; } = string.Empty;
    public string SheetTabName { get; set; } = string.Empty;
    public string TableCode { get; set; } = string.Empty;   
    public int TableID { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string YDimVal { get; set; }= string.Empty;
    public string ZDimVal { get; set; } = string.Empty;
    public string YDimValFromExcel { get; set; } = string.Empty;
    public string ZDimValFromExcel { get; set; } = string.Empty;
    public string YDimFilled { get; set; } = string.Empty;
    public string ZDimFilled { get; set; } = string.Empty;
    public string XbrlFilingIndicatorCode { get; set; }=string.Empty;
    public bool IsOpenTable { get; set; }
    [Computed]
    public int OpenRowCounter { get; set; }
    [Computed]
    public int FactsCounter { get; set; }

}

