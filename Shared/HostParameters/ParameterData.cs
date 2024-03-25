using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.HostParameters;


public class ParameterData
{
    public int ExternalId { get; set; }
    public string Environment { get; set; }
    public string SystemConnectionString { get; set; }
    public string EiopaConnectionString { get; set; }
    public string ExcelTemplateFile { get; set; }
    public string LoggerFile { get; set; }
    public string EiopaVersion { get; set; }
    public int UserId { get; set; }
    public int FundId { get; set; }
    public int CurrencyBatchId { get; set; }
    public int ApplicableYear { get; set; }
    public int ApplicableQuarter { get; set; }
    public string ModuleCode { get; set; }
    public string FileName { get; set; }
    public string FileNameError { get; set; }
    public string FileNameWarning { get; set; }
    public int DocumentId { get; set; }

    public bool IsDevelop { get; set; }
}
