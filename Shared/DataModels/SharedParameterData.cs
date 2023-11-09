using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DataModels;


public class ParameterData
{
	public string environment { get; set; }
	public string SystemConnectionString { get; set; }
	public string EiopaConnectionString { get; set; }
	public string ExcelTemplateFile { get; set; }
	public string LoggerFile { get; set; }
	public string EiopaVersion { get; set; }
	public int UserId { get; set; }
	public int FundId { get; set; }
	public int CurrencyBatchId { get; set; }
	public int ApplicationYear { get; set; }
	public int ApplicationQuarter { get; set; }
	public string ModuleCode { get; set; }
	public string FileName { get; set; }

}
