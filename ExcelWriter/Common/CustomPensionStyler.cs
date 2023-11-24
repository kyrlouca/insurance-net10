using ExcelWriter.DataModels;
using Microsoft.Identity.Client;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelWriter;

public record struct PensionStyles(
    IStyle Normal,
    IStyle HeaderStyle,
    IStyle TableCodeStyle,
    IStyle DiagonalStyle,
    IStyle LeftLabelStyle,
    IStyle DataSectionStyle,
    IStyle LeftRowNumbersSectionStyle,
    IStyle TopColumnNumbersStyle
    );
public class CustomPensionStyler : ICustomPensionStyler
{
    public IWorkbook? Workbook { get => _workbook; }
    internal IWorkbook? _workbook;

    public PensionStyles GetStyles(IWorkbook workbook)
    {
        _workbook = workbook;

        return new PensionStyles(NormalStyle(), HeaderStyle(), TableCodeStyle(), DiagonalStyle(), LeftLabelStyle(), DataSectionStyle(), LeftRowNumbersSectionStyle(), TopColumnNumbersStyle());
    }
    private IStyle LeftLabelStyle()
    {

        //IStyle bodyStyle = _destinationWorkbook.Styles.Add("BodyStyle");        
        IStyle bodyStyle = Workbook.Styles.Add("LeftLabelStyle");

        bodyStyle.BeginUpdate();
        bodyStyle.Font.FontName = "Calibri";
        bodyStyle.WrapText = false;
        bodyStyle.EndUpdate();
        return bodyStyle;
    }
    private IStyle HeaderStyle()
    {
        IStyle style = Workbook.Styles.Add("HeaderStyle");
        style.Font.Bold = true;

        return style;
    }
    private IStyle TableCodeStyle()
    {
        IStyle style = Workbook.Styles.Add("TableCodeStyle");
        //style.Color = Syncfusion.Drawing.Color.Red;
        style.Font.Color = ExcelKnownColors.Red;
        style.Font.Underline = ExcelUnderline.Single;
        //style.FillPattern = ExcelPattern.DarkUpwardDiagonal;
        style.Font.Bold = true;
        return style;
    }
    private IStyle DataSectionStyle()
    {
        IStyle style = Workbook.Styles.Add("dataSection");

        style.BeginUpdate();
        //bodyStyle.Color = Color.FromArgb(239, 243, 247);
        style.Font.FontName = "Calibri";
        //style.Font.Size = 10;
        style.Borders[ExcelBordersIndex.EdgeTop].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeLeft].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.InsideHorizontal].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.InsideVertical].LineStyle = ExcelLineStyle.Thin;
        style.EndUpdate();
        return style;
    }
    private IStyle LeftRowNumbersSectionStyle()
    {
        IStyle style = Workbook.Styles.Add("leftRow");

        style.BeginUpdate();
        //bodyStyle.Color = Color.FromArgb(239, 243, 247);
        style.Font.FontName = "Calibri";
        style.Font.Size = 10;
        style.WrapText = false;
        style.Borders[ExcelBordersIndex.EdgeTop].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeLeft].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thick;
        style.Borders[ExcelBordersIndex.InsideHorizontal].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.InsideVertical].LineStyle = ExcelLineStyle.Thin;

        style.EndUpdate();
        return style;
    }
    private IStyle TopColumnNumbersStyle()
    {
        IStyle style = Workbook.Styles.Add("ColumnNumber");

        style.BeginUpdate();
        //bodyStyle.Color = Color.FromArgb(239, 243, 247);
        style.Font.FontName = "Calibri";
        //style.Font.Size = 10;
        style.WrapText = false;
        style.Borders[ExcelBordersIndex.EdgeTop].LineStyle = ExcelLineStyle.Thick;        
        style.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thick;
        style.Borders[ExcelBordersIndex.EdgeLeft].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.InsideHorizontal].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.InsideVertical].LineStyle = ExcelLineStyle.Thin;
        

        style.EndUpdate();
        return style;
    }
    private IStyle DiagonalStyle()
    {
        IStyle st;
        try
        {
            st = Workbook.Styles["DPM_EmptyCell"];
        }
        catch (Exception ex)
        {
            st = Workbook.Styles.Add("DPM_EmptyCell");
        }

        //st.FillPattern = ExcelPattern.Percent25Gray;
        st.Color = Syncfusion.Drawing.Color.LightGray;
        //st.Borders[ExcelBordersIndex.EdgeTop].LineStyle = ExcelLineStyle.None;
        st.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thin;
        st.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thin;
        st.IncludeBorder = true;
        //st.ColorIndex = ExcelKnownColors.Grey_50_percent;
        return st;

    }
    private IStyle NormalStyle()
    {
        IStyle st;
        try
        {
            st = Workbook.Styles["Normal"];
        }
        catch (Exception ex)
        {
            st = Workbook.Styles.Add("normal");
        }

        st.Font.FontName = "Calibri";
        st.Font.Size = 11;
        return st;

    }
}

