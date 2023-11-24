using ExcelWriter.DataModels;
using Microsoft.Identity.Client;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelWriter;

public record struct PensionStyles(IStyle HeaderStyle, IStyle BodyStyle, IStyle TableCodeStyle, IStyle DataSectionStyle,IStyle DiagonalStyle);
public class CustomPensionStyles2 : ICustomPensionStyles2
{
    public IWorkbook? Workbook { get => _workbook; }
    internal IWorkbook? _workbook;

    public PensionStyles GetStyles(IWorkbook workbook)
    {
        _workbook = workbook;
        
        return new PensionStyles(HeaderStyle(), BodyStyle(), TableCodeStyle(), DataSectionStyle(), DiagonalStyle());
    }
    private IStyle BodyStyle()
    {

        //IStyle bodyStyle = _destinationWorkbook.Styles.Add("BodyStyle");        
        IStyle bodyStyle = Workbook.Styles.Add("BodyStyle");

        bodyStyle.BeginUpdate();
        //bodyStyle.Color = Color.FromArgb(239, 243, 247);
        bodyStyle.Font.FontName = "Calibri";
        bodyStyle.Font.Size = 10;
        bodyStyle.WrapText = false;
        //bodyStyle.Borders[ExcelBordersIndex.EdgeLeft].LineStyle = ExcelLineStyle.Thin;
        //bodyStyle.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thin;
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
        style.Font.Size = 10;
        style.Borders[ExcelBordersIndex.EdgeTop].LineStyle = ExcelLineStyle.Thick;
        style.Borders[ExcelBordersIndex.EdgeLeft].LineStyle = ExcelLineStyle.Thick;
        style.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thin;
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
}

