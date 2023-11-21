using Syncfusion.XlsIO;

namespace ExcelWriter
{
    public interface ICustomPensionStyles2
    {
        IWorkbook? Workbook { get; }

        PensionStyles GetStyles(IWorkbook workbook);
    }
}