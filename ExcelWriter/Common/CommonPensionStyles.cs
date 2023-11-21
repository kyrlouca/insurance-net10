using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelWriter;

public static class CommonPensionStyles
{
	public static IStyle? HeaderStyle(IWorkbook workbook)
	{
		if (workbook is null) { return null; }
		IStyle style = workbook.Styles.Add("HeaderStyle");
		style.Font.Bold = true;
		
		return style;
	}

	public static IStyle? BodyStyle(IWorkbook workbook)
	{
		if (workbook is null) { return null; }
		//IStyle bodyStyle = _destinationWorkbook.Styles.Add("BodyStyle");
		IStyle bodyStyle = workbook.Styles.Add("BodyStyle");

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

	public static IStyle? TableCodeStyle(IWorkbook workbook)
	{
		if (workbook is null) { return null; }
		IStyle style = workbook.Styles.Add("TableCodeStyle");
		//style.Color = Syncfusion.Drawing.Color.Red;
		style.Font.Color = ExcelKnownColors.Red;
		style.Font.Underline = ExcelUnderline.Single;
		//style.FillPattern = ExcelPattern.DarkUpwardDiagonal;
		style.Font.Bold = true;
		return style;
	}


	public static void ChangeDiagonalStyle(IWorkbook workbook)
	{
		var st = workbook.Styles["DPM_EmptyCell"];
		if (st is null)
		{
			return;
		}

		//st.FillPattern = ExcelPattern.Percent25Gray;
		st.Color = Syncfusion.Drawing.Color.LightGray;
		//st.Borders[ExcelBordersIndex.EdgeTop].LineStyle = ExcelLineStyle.None;
		st.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thin;
		st.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thin;
		st.IncludeBorder = true;
		//st.ColorIndex = ExcelKnownColors.Grey_50_percent;


	}
	public static IStyle DataSectionStyle(IWorkbook workbook)
	{
		if (workbook is null) { return null; }
		//IStyle bodyStyle = _destinationWorkbook.Styles.Add("BodyStyle");
		IStyle bodyStyle = workbook.Styles.Add("dataSection");

		bodyStyle.BeginUpdate();
		//bodyStyle.Color = Color.FromArgb(239, 243, 247);
		bodyStyle.Font.FontName = "Calibri";
		bodyStyle.Font.Size = 10;
		bodyStyle.Borders[ExcelBordersIndex.EdgeTop].LineStyle = ExcelLineStyle.Thick;
		bodyStyle.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thick;
		bodyStyle.Borders[ExcelBordersIndex.InsideHorizontal].LineStyle = ExcelLineStyle.Thin;
		bodyStyle.Borders[ExcelBordersIndex.InsideVertical].LineStyle = ExcelLineStyle.Thin;
		bodyStyle.EndUpdate();
		return bodyStyle;
	}

}
