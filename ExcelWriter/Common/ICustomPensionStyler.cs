using Syncfusion.XlsIO;

namespace ExcelWriter
{
    public interface ICustomPensionStyler
    {
        IWorkbook? Workbook { get; }

        PensionStyles GetStyles(IWorkbook workbook);
    }
}