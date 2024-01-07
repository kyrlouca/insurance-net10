namespace ExcelWriter;

using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.CommonRoutines;
using Shared.SQLFunctions;

public class WriterMainApp : IWriterMainApp
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private readonly IExcelBookWriter _excelBookWriter;
    private readonly IExcelBookDataFiller _excelBookDataFiller;
    private readonly IExcelBookMerger _templateMerger;


    public int id = 12;
    public WriterMainApp(IParameterHandler getParameters, ILogger logger, ICustomPensionStyler customPensionStyles, ISqlFunctions sqlFunctions, IExcelBookWriter excelBookWriter, IExcelBookDataFiller excelBookDataFiller, IExcelBookMerger templateMerger)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _excelBookWriter = excelBookWriter;
        _excelBookDataFiller = excelBookDataFiller;
        _templateMerger = templateMerger;
    }
    public int Run()
    {
        Console.WriteLine("started Excel Writer");

        var doc = _SqlFunctions.SelectDocInstance(_parameterData.DocumentId);        
        
        if (doc is null)
        {
            var message = $"Cannot Find DocInstance for fund:{_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
            return 1;
        }

        if (doc.Status.Trim() == "P")
        {
            var message = $"Document currently being Processed by another User. Document Id:{doc.InstanceId}";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
            return 1;
        }

        if (doc.EiopaVersion.Trim() != _parameterData.EiopaVersion)
        {
            var message = $"Eiopa Version Submitted :{_parameterData.EiopaVersion} different than Document eiopa version: {_parameterData.EiopaVersion} ";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
            return 1;
        }

        var fileName = _parameterData.FileName.Trim();
        var fileNoExtension = Path.GetFileNameWithoutExtension(fileName);
        var dir = Path.GetDirectoryName(fileName);
        if (dir is null)
        {
            var message = $"Cannot find Directory for path {fileName} :FundId: {_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
            return 1;
        }

        var EmptyFilename = Path.Combine(dir, $"{fileNoExtension}_empty.xlsx");
        var filledFilename = Path.Combine(dir, $"{fileNoExtension}_filled.xlsx");
        var mergedFilename = Path.Combine(dir, $"{fileNoExtension}_merged.xlsx");


        if (1 == 1)
        {
            Console.WriteLine($"\n Create excel Fil3e : {filledFilename}");
            //****************************************************************************************************
            var savedFile = _excelBookWriter.CreateExcelBook(doc.InstanceId, EmptyFilename);
            if (string.IsNullOrEmpty(savedFile))
            {
                var message = $"Can NOT create file: {EmptyFilename} ";
                _logger.Error(message);
                _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
                return 1;
            }
        }
        if (1 == 1)
        {
            Console.WriteLine($"\n Fill Excel : {filledFilename}");
            //****************************************************************************************************
            var isFilled = _excelBookDataFiller.FillExcelBook(doc.InstanceId, EmptyFilename, filledFilename);
            if (!isFilled)
            {
                var message = $"Can NOT Fill file: Empty:{EmptyFilename}  - filled:{filledFilename}";
                _logger.Error(message);
                _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
                return 1;
            }

        }
        if (1 == 1)
        {
            Console.WriteLine($"\n Merge TabSheets  : {mergedFilename}");
            //****************************************************************************************************
            var isMerged = _templateMerger.MergeTables(doc.InstanceId, filledFilename, mergedFilename);
            if (!isMerged)
            {
                {
                    var message = $"Can NOT Merge file: Filled:{filledFilename}  - filled:{mergedFilename}";
                    _logger.Error(message);
                    _SqlFunctions.CreateTransactionLog(MessageType.ERROR, message);
                    return 1;
                }
            }
        }
        if (!_parameterData.IsDevelop )
        {
            var (isSuccess, errorMessage) = FileUtilsKyr.DeleteFile(EmptyFilename);
            if (!isSuccess)
            {
                _logger.Error(errorMessage);
            }
            var (isFsuccess, sErrorMessage) = FileUtilsKyr.DeleteFile(filledFilename);
            if (!isFsuccess)
            {
                _logger.Error(sErrorMessage);
            }
            var (isRsuccess, rMessage) = FileUtilsKyr.MoveFile(mergedFilename, fileName);
            if (!isRsuccess)
            {
                _logger.Error(rMessage);
            }
        }
        return 0;

    }
}





