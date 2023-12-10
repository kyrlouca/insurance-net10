namespace ExcelWriter;
using Serilog;
using Shared.CommonRoutines;
using Shared.HostRoutines;
using Shared.SharedHost;

public class ExcelWriterMainApp : IExcelWriterMainApp
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private readonly IExcelBookWriter _excelBookWriter;
    private readonly IExcelBookDataFiller _excelBookDataFiller;
    private readonly ITemplateMerger _templateMerger;


    public int id = 12;
    public ExcelWriterMainApp(IParameterHandler getParameters, ILogger logger, ICustomPensionStyler customPensionStyles, ISqlFunctions sqlFunctions, IExcelBookWriter excelBookWriter, IExcelBookDataFiller excelBookDataFiller, ITemplateMerger templateMerger)
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
        Console.WriteLine("started Excle");

        var doc = _SqlFunctions.SelectDocInstance(_parameterData.FundId, _parameterData.ModuleCode, _parameterData.ApplicableYear, _parameterData.ApplicableQuarter);

        if (doc is null)
        {
            var message = $"Cannot Find DocInstance for fund:{_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(0, MessageType.ERROR, message);
            return 1;
        }

        if (doc.Status == "P")
        {
            var message = $"Document currently being Processed by another User. Document Id:{doc.InstanceId}";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(0, MessageType.ERROR, message);
            return 1;
        }

        if (doc.EiopaVersion.Trim() != _parameterData.EiopaVersion)
        {
            var message = $"Eiopa Version Submitted :{_parameterData.EiopaVersion} different than Document eiopa version: {_parameterData.EiopaVersion} ";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(0, MessageType.ERROR, message);
            return 1;
        }

        var fileName = _parameterData.FileName.Trim();
        var fileNoExtension = Path.GetFileNameWithoutExtension(fileName);
        var dir = Path.GetDirectoryName(fileName);
        if (dir is null)
        {
            var message = $"Cannot find Directory for path {fileName} :FundId: {_parameterData.FundId} year:{_parameterData.ApplicableYear} quarter:{_parameterData.ApplicableQuarter} ";
            _logger.Error(message);
            _SqlFunctions.CreateTransactionLog(0, MessageType.ERROR, message);
            return 1;
        }
       
        var EmptyFilename = Path.Combine(dir, $"{fileNoExtension}_empty.xlsx");
        var filledFilename = Path.Combine(dir, $"{fileNoExtension}_filled.xlsx");
        var mergedFilename = Path.Combine(dir, $"{fileNoExtension}_merged.xlsx");


        if (1 == 1)
        {
            Console.WriteLine($"\n Create Empty File : {EmptyFilename}");
            _excelBookWriter.CreateExcelBook(doc.InstanceId, EmptyFilename);
            //return 0;
            if (string.IsNullOrEmpty(EmptyFilename))
            {
                return 1;
            }
            Console.WriteLine($"\n Fill excel File : {filledFilename}");
            var y = _excelBookDataFiller.PopulateExcelBook(doc.InstanceId, EmptyFilename, filledFilename);
        }
        Console.WriteLine($"\n Merge to File : {mergedFilename}");
        var x = _templateMerger.MergeTables(doc.InstanceId, filledFilename, mergedFilename);


        return 0;

    }



}
