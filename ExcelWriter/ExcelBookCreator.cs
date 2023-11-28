namespace ExcelWriter;
using Shared.CommonRoutines;
using Shared.HostRoutines;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.SharedHost;
using Shared.DataModels;
using System.Reflection.Metadata;
using Syncfusion.XlsIO.Implementation;
using Syncfusion.XlsIO;
using Syncfusion.XlsIO.Implementation.Collections;
using System;
using System.Drawing;
using ExcelWriter.Common;

public class ExcelBookCreator : IExcelBookWriter
{
    private readonly IParameterHandler _parameterHandler;
    ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ICommonRoutines _commonRoutines;
    private IWorkbook? _destinationWorkbook;
    private IWorkbook? _originWorkbook; //template workbook
    int _documentId = 0;
    string debugTableCode = "";

    private readonly ICustomPensionStyler _customPensionStyler;
    PensionStyles _pensionStyles;

    public ExcelBookCreator(IParameterHandler parametersHandler, ILogger logger, ICommonRoutines commonRoutines, ICustomPensionStyler customPensionStyles)
    {
        _parameterHandler = parametersHandler;
        _logger = logger;
        _commonRoutines = commonRoutines;
        _customPensionStyler = customPensionStyles;

    }



    public string CreateExcelBook(int documentId, string filename)
    {
        _documentId = documentId;
        _parameterData = _parameterHandler.GetParameterData();
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdgWH5fc3RdRWFfU0B0W0o=");

        //TestDebug();
        //return true;

        using var excelEngine = new ExcelEngine();



        (_originWorkbook, var originMessage) = HelperRoutines.OpenExistingExcelWorkbook(excelEngine, _parameterData.ExcelTemplateFile);
        if (_originWorkbook is null)
        {
            _logger.Error(originMessage);
            _commonRoutines.CreateTransactionLog(0, MessageType.ERROR, originMessage);
            return "";
        }

        (_destinationWorkbook, var xMessage) = HelperRoutines.CreateExcelWorkbook(excelEngine);
        var errorMessage = "Cannot create excel Workbook syncfusion file";
        if (_destinationWorkbook is null)
        {
            _logger.Error(errorMessage);
            _commonRoutines.CreateTransactionLog(0, MessageType.ERROR, errorMessage + "--" + xMessage);
            return "";
        }

        var originpensionStyles = _customPensionStyler.GetStyles(_originWorkbook);
        _pensionStyles = _customPensionStyler.GetStyles(_destinationWorkbook);


        //////////////////////////////////////////////////////////////////
        //Start processing

        //      var tableCodeStyle = _pensionStyles.ta TableCodeStyle();
        //var bodyStyle = CustomPensionStyles.BodyStyle(_destinationWorkbook);
        //var headerStyle = CustomPensionStyles.HeaderStyle(_destinationWorkbook);
        //var dataSectionStyle = CustomPensionStyles.DataSectionStyle(_destinationWorkbook);



        var sheets = SelectTempateSheetInstances().OrderBy(sh => sh.TableCode);


        int START_ROW = 1;
        int START_COL = 1;
        int DATA_ROW_POSITION = 14;
        foreach (var sheet in sheets)
        {
            Console.WriteLine("process" + sheet?.SheetTabName + "-" + sheet?.TableCode + sheet?.SheetTabName);

            var template = GetTableOrTemplate(sheet.TableCode, true);
            if (template is null)
                continue;


            //the template has only 4 parts (S.04.01.01 )
            var filingSheetCode = string.Join(".", sheet.TableCode.Split(".").ToList().GetRange(0, 4));
            var originSheet = _originWorkbook.Worksheets[filingSheetCode];
            if (originSheet is null) continue;

            var sheetName = sheet.SheetTabName.Trim();
            //var sheetName = sheet.SheetTabName.Replace("#", "__").Trim();
            var destSheet = _destinationWorkbook.Worksheets.Create(sheetName);
            destSheet.Zoom = 80;

            //destSheet.UsedRange.CellStyle = bodyStyle;


            /////Table code
            var tableCode = destSheet.Range["A1"];
            tableCode.Text = sheet.TableCode;
            tableCode.CellStyle = _pensionStyles.TableCodeStyle;

            //template code
            var parentTemplate = GetTableOrTemplate(filingSheetCode, false);
            var tblLabel = destSheet.Range["A2"];
            tblLabel.Text = parentTemplate?.TemplateOrTableLabel;
            tblLabel.CellStyle = _pensionStyles.HeaderStyle;



            ///////////LABELs ab
            var _TC = template.TC;
            var descRange = CopyRangeToFixedPosition(START_ROW + 2, START_COL, originSheet, destSheet, _TC);


            //////////DATA RANGE 			
            var _TD = template.TD;
            var originDataRange = originSheet.Range[_TD];
            if (sheet.IsOpenTable)
            {
                //for open tables template.TD does not include the key columns, so the starting column should extend to the left.//set the left column position to the description 
                var originDataOriginalRange = originSheet.Range[_TD];
                originDataRange = originSheet.Range[originDataOriginalRange.Row, descRange?.Column ?? 0, originDataOriginalRange.LastRow, originDataOriginalRange.LastColumn];
            }

            //Row and Col position for Destination Data Range must be fixed for both open and closed table			
            var dataRowPos = DATA_ROW_POSITION;
            var dataColPos = sheet.IsOpenTable
                ? START_COL
                : START_COL + 2;//make room for left label and row number

            var dataRange = CopyRangeToFixedPosition(dataRowPos, dataColPos, originSheet, destSheet, originDataRange.AddressLocal);
            if (dataRange == null)
            {
                continue;
            }
            var dataNamed = $"{destSheet.Name.Trim()}_data";
            _destinationWorkbook.Names.Remove(dataNamed);
            var dataNamedRange = _destinationWorkbook.Names.Add(dataNamed);
            dataNamedRange.RefersToRange = dataRange;

            dataRange.ColumnWidth = 30;
            dataRange.WrapText = false;
            if (!sheet.IsOpenTable)
            {
                var bor = dataRange.Borders;
                //dataRange.BorderAround(ExcelLineStyle.Thick);
            }



            /////////////LEFT Labels 
            var _TL = template.TL;
            IRange? originleftLabelsRange;

            if (!sheet.IsOpenTable)
            {
                try
                {
                    originleftLabelsRange = originSheet.Range[_TL];
                }
                catch (Exception ex)
                {
                    throw (new Exception($"Cannot find Range:{_TL}"));
                }


                var leftLabelRange = CopyRangeToFixedPosition(dataRowPos, dataColPos - 2, originSheet, destSheet, _TL);


                leftLabelRange.ColumnWidth = 50;
                leftLabelRange.WrapText = false;
            }

            //////////// LEFT ROW Numbers
            if (!sheet.IsOpenTable)
            {

                var lfr = originSheet.Range[_TL].Offset(0, 1) ?? throw (new Exception($"Cannot find the Column Numbers in range :{_TL}"));
                var orignColumnNames = lfr.Columns[0];
                var leftRowNumRange = CopyRangeToFixedPosition(dataRowPos, dataColPos - 1, originSheet, destSheet, orignColumnNames.Address);

                leftRowNumRange.CellStyle = _pensionStyles.LeftRowNumbersSectionStyle;                

                var leftNamed = $"{destSheet.Name.Trim()}_left";
                _destinationWorkbook.Names.Remove(leftNamed);
                var tlNamedLeftRow = _destinationWorkbook.Names.Add(leftNamed);
                tlNamedLeftRow.RefersToRange = leftRowNumRange;

            }


            ////////////TOP LABELS(above columns)
            //Top labels must be above the destination data range
            var _TT = template.TT;
            if (_TT is not null)
            {
                IRange expandedTopLabel;

                var otr = originSheet.Range[_TT];
                try
                {
                    expandedTopLabel = originSheet.Range[otr.Row, originDataRange.Column, otr.LastRow, otr.LastColumn];
                }
                catch (Exception ex)
                {
                    var message = $"Fail to read top label TT for {sheet.TableCode}";
                    Console.WriteLine(message);
                    Log.Error(message + ex.Message);
                    throw ex;
                }


                var upperRowPosition = dataRowPos - (otr.LastRow - otr.Row) - 2;
                var topLabelsRange = CopyRangeToFixedPosition(upperRowPosition, dataColPos, originSheet, destSheet, expandedTopLabel.Address);

            }


            //////////// TOP COLUMN Numbers

            var tcn = originDataRange.Offset(-1, 0);
            var orignColumnNumbers = tcn.Rows[0];
            var topColumnsRange = CopyRangeToFixedPosition(dataRowPos - 1, dataColPos, originSheet, destSheet, orignColumnNumbers.Address);
            if (topColumnsRange is not null)
            {
                topColumnsRange.CellStyle = _pensionStyles.TopColumnNumbersStyle;
            }


            var topNamed = $"{destSheet.Name.Trim()}_top";
            _destinationWorkbook.Names.Remove(topNamed);
            var tlNamedRange = _destinationWorkbook.Names.Add(topNamed);
            tlNamedRange.RefersToRange = topColumnsRange;



        }



        //////////////////////////////////////////////////////////////////

        //CustomPensionStyles.ChangeDiagonalStyle(_destinationWorkbook);

        //var savedFile = _parameterData.FileName;
        var savedFile = filename;
        var (isSaveValid, saveMessage) = HelperRoutines.SaveWorkbook(_destinationWorkbook, savedFile);
        if (!isSaveValid)
        {
            _logger.Error(saveMessage);
            _commonRoutines.CreateTransactionLog(0, MessageType.ERROR, saveMessage);
            return "";
        }

        return savedFile;


        static IRange CopyRangeToFixedPosition(int UpperLeftRow, int UpperLeftCol, IWorksheet? originSheet, IWorksheet destSheet, string rangeStr)
        {
            try
            {

                var cOriginRange = originSheet.Range[rangeStr];
                var cOffset = HelperRoutines.OffsetRange(cOriginRange, UpperLeftRow, UpperLeftCol);
                IRange destRange = destSheet.Range[cOffset.StartRow, cOffset.StartCol, cOffset.EndRow, cOffset.EndCol];
                cOriginRange.CopyTo(destRange, ExcelCopyRangeOptions.All);
                return destRange;
            }
            catch (Exception ex)
            {
                //just go on
                throw new Exception($"Cannot find Left Labels for {rangeStr}--- {ex.Message}");
            }
        }
    }

    public bool PopulateExcelBool()
    {
        return false;
    }

    private bool UpdateClosedTableValues(IRange dataRange, IRange RowRange, IRange colRange, List<string> factZetList)
    {
        var dataRows = dataRange.Rows;
        foreach (var row in dataRows)
        {
            foreach (var cell in row)
            {
                var xx = cell.Text;
                Console.WriteLine(xx);
            }
        }

        return true;
    }



    private List<TemplateSheetInstance> SelectTempateSheetInstances()
    {

        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSheets = @"
			SELECT *, (SELECT COUNT(*) FROM TemplateSheetFact fact WHERE fact.TemplateSheetId= sheet.TemplateSheetId) AS FactsCounter
			FROM TemplateSheetInstance sheet
			WHERE
			  sheet.InstanceId = @_documentID
			ORDER BY sheet.SheetTabName   			";
        var sheets = connectionLocal.Query<TemplateSheetInstance>(sqlSheets, new { _documentId })
            .Where(sheet => sheet.FactsCounter > 0);

        if (!string.IsNullOrEmpty(debugTableCode))
        {
            sheets = sheets.Where(sheet => sheet.TableCode.Trim() == debugTableCode).ToList();
            Console.WriteLine($"**** Debugging-- Create ONLY the sheet: {debugTableCode} ");
        }
        return sheets.ToList();

    }

    private MTemplateOrTable? GetTableOrTemplate(string tableCode, bool isAnnotaed)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlTemplate = @"
				SELECT * 
				FROM mTemplateOrTable tt
				WHERE 
				  1=1				  
				  AND tt.TemplateOrTableCode = @tableCode
				";
        if (isAnnotaed)
        {
            sqlTemplate += @" AND TemplateOrTableType = 'AnnotatedTable' ";
        }
        var template = connectionEiopa.QueryFirstOrDefault<MTemplateOrTable>(sqlTemplate, new { tableCode });
        return template;

    }
    private void TestDebug()
    {
        using (ExcelEngine excelEngine = new ExcelEngine())
        {
            IApplication application = excelEngine.Excel;
            application.DefaultVersion = ExcelVersion.Xlsx;
            IWorkbook workbook = application.Workbooks.Create(1);
            IWorksheet worksheet = workbook.Worksheets[0];
            IRange range = worksheet[1, 4];

            //Hiding the range ‘D1’
            worksheet.ShowRange(range, false);
            IRange firstRange = worksheet[1, 1, 3, 3];
            IRange secondRange = worksheet[5, 5, 7, 7];
            RangesCollection rangeCollection = new RangesCollection(application, worksheet);
            rangeCollection.Add(firstRange);
            rangeCollection.Add(secondRange);

            //Hiding a collection of ranges
            worksheet.ShowRange(rangeCollection, false);

            //Saving the workbook as stream
            var filename = "C:\\Users\\kyrlo\\soft\\dotnet\\insurance-project\\TestingXbrl270\\axa.xlsx";
            FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite);
            workbook.SaveAs(stream);
            stream.Dispose();
        }
    }
}
