using Syncfusion.XlsIO;

namespace Shared.ExcelHelperRoutines;

public interface ICustomPensionStyler
{
    IWorkbook? Workbook { get; }

    PensionStyles GetStyles(IWorkbook workbook);
}