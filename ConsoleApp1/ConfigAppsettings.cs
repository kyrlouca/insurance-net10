using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1;
public class ConfigAppsettings
{

	//somehow I could not manage to use the internal modifier properly. It does not work for internal set from serializer
	public string LocalDatabaseConnectionString { get; internal set; } //Use this One because it can be either pension or Insurance
	public string EiopaDatabaseConnectionString { get; internal set; } //Use this One because it depends on solvency version

	public string OutputXbrlFolder { get; set; }
	public string ExcelTemplateFileGeneral { get; internal set; }
	
	public string LoggerXbrlReaderFile { get; set; }
	public string LoggerXbrlWriterFile { get; set; }
	public string LoggerXbrlValidatorFile { get; set; }
	public string LoggerExcelWriterFile { get; set; }
	public string LoggerExcelReaderFile { get; set; }
	
}
