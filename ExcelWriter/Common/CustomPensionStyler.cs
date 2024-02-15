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

        var styleName = "LeftLabelStyle";
        IStyle style = GetOrCreateStyle(styleName);
        
        style.BeginUpdate();
        style.IncludeAlignment = false;
        style.IncludeBorder = false;        
        style.WrapText = true;

        style.Font.FontName = "Calibri";
        style.Font.Size = 12;
        
        
        style.WrapText = true;        
        //style.Borders.LineStyle = ExcelLineStyle.Thin;
        //style.Borders[ExcelBordersIndex.DiagonalUp].LineStyle = ExcelLineStyle.None;
        //style.Borders[ExcelBordersIndex.DiagonalDown].LineStyle = ExcelLineStyle.None;

        style.ColorIndex = ExcelKnownColors.Custom33;
        style.EndUpdate();
        return style;
    }
    private IStyle HeaderStyle()
    {
        
        var styleName = "HeaderStyle";
        IStyle style = GetOrCreateStyle(styleName);
        style.Font.Size = 15;
        style.Font.Bold = true;
        style.WrapText = false;

        return style;
    }
    private IStyle TableCodeStyle()
    {        
        
        var styleName = "TableCodeStyle";
        IStyle style = GetOrCreateStyle(styleName);


        //style.Color = Syncfusion.Drawing.Color.Red;
        style.Font.Color = ExcelKnownColors.Red;
        style.Font.Underline = ExcelUnderline.Single;
        style.Font.Size = 12;
        //style.FillPattern = ExcelPattern.DarkUpwardDiagonal;
        style.Font.Bold = true;
        return style;
    }
    private IStyle DataSectionStyle()
    {
        
        var styleName = "dataSection";
        IStyle style = GetOrCreateStyle(styleName);


        style.BeginUpdate();
        //bodyStyle.Color = Color.FromArgb(239, 243, 247);
        style.Font.FontName = "Calibri";
        
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
     
        var styleName = "leftRow";
        IStyle style = GetOrCreateStyle(styleName);


        style.BeginUpdate();
        //bodyStyle.Color = Color.FromArgb(239, 243, 247);
        style.Font.FontName = "Calibri";
        style.Font.Size = 12;
        style.WrapText = false;
        style.Borders[ExcelBordersIndex.EdgeTop].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeLeft].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thick;
        style.Borders[ExcelBordersIndex.InsideHorizontal].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.InsideVertical].LineStyle = ExcelLineStyle.Thin;
        style.ColorIndex = ExcelKnownColors.Custom34;
        //style.ColorIndex = ExcelKnownColors.Custom44;
        style.EndUpdate();
        return style;
    }
    private IStyle TopColumnNumbersStyle()
    {
     
        var styleName = "ColumnNumber";
        IStyle style = GetOrCreateStyle(styleName);


        style.BeginUpdate();
        //bodyStyle.Color = Color.FromArgb(239, 243, 247);
        style.Font.FontName = "Calibri";        
        style.WrapText = false;
        style.Borders[ExcelBordersIndex.EdgeTop].LineStyle = ExcelLineStyle.Thick;        
        style.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thick;
        style.Borders[ExcelBordersIndex.EdgeLeft].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.InsideHorizontal].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.InsideVertical].LineStyle = ExcelLineStyle.Thin;
        style.ColorIndex = ExcelKnownColors.Custom34;
        

        style.EndUpdate();
        return style;
    }
    private IStyle DiagonalStyle()
    {
        var styleName = "DPM_EmptyCell";
        IStyle style = GetOrCreateStyle(styleName);
        
        //st.FillPattern = ExcelPattern.Percent25Gray;
        style.Color = Syncfusion.Drawing.Color.LightGray;
        //st.Borders[ExcelBordersIndex.EdgeTop].LineStyle = ExcelLineStyle.None;
        style.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thin;
        style.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thin;
        style.IncludeBorder = true;
        //st.ColorIndex = ExcelKnownColors.Grey_50_percent;
        return style;

    }

    
    private IStyle NormalStyle()
    {
        var styleName = "Normal";
        IStyle style=GetOrCreateStyle(styleName);
        
        style.Font.FontName = "Calibri";
        style.Font.Size = 12;
        return style;

    }
    private IStyle GetOrCreateStyle(string styleName)
    {
        IStyle style;
        try
        {
            style = Workbook.Styles[styleName];
        }
        catch (Exception ex)
        {
            style = Workbook.Styles.Add(styleName);
            return style;
        }
        return style;
    }
}

