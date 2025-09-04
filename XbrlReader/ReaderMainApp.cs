namespace XbrlReader;
using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;
using System.Reflection.Metadata;

public class ReaderMainApp : IReaderMainApp
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    private readonly IFactsCreator _factsCreator;
    private readonly IFactsDecorator _factsDecorator;
    private readonly ICombinedS62Services _combinedS62Services;

    public ReaderMainApp(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions,
        IFactsCreator factsCreator,
        IFactsDecorator factsDecorator,
        ICombinedS62Services combinedS62Services
        )
    {
        _parameterHandler = getParameters;
        _logger = logger;
        _SqlFunctions = sqlFunctions;
        _factsCreator = factsCreator;
        _factsDecorator = factsDecorator;
        _combinedS62Services = combinedS62Services;
    }



    public async Task<int> Run()
    {
        _parameterData = _parameterHandler.GetParameterData();



        Console.WriteLine($"Xbrl Reading and Loading file:{_parameterData.FileName}");

        var isEiopaVersionValid = IsValidEiopaVersion();
        if (!isEiopaVersionValid)
        {
            var errorEiopaMessage = $"Invalid Eiopa Version:{_parameterData.EiopaVersion} for year:{_parameterData.ApplicableYear}, quarter:{_parameterData.ApplicableQuarter}";
            _logger.Error(errorEiopaMessage);
            _SqlFunctions.CreateTransactionLog(MessageType.ERROR, errorEiopaMessage);
            return 1;
        }

        //*****************************************************************************
        var _documentId = 298; //set this when debugging. when you avoid to CreateLooseFacts
        //*****************************************************************************
        var filingsSubmitted = new List<string>();
        if (_parameterData.IsDevelop && 1 != 2)
        {
            //only if debugging and not creating loose facts
            filingsSubmitted = new List<string>() {
            "S.01.01",
            "S.01.02",
            "S.02.01",
            "S.05.01",
            "S.06.02",
            "S.06.04",
            "S.09.01",
            "S.14.02",
            "S.17.01",
            "S.17.03",
            "S.18.01",
            "S.19.01",
            "S.20.01",
            "S.21.01",
            "S.21.02",
            "S.21.03",
            "S.23.01",
            "S.23.02",
            "S.23.03",
            "S.23.04",
            "S.25.01",
            "S.26.01",
            "S.26.02",
            "S.26.04",
            "S.26.05",
            "S.26.06",
            "S.27.01",
            "S.28.01",
            "S.29.01",
            "S.29.02",
            "S.29.03",
            "S.29.04",
            "S.30.01",
            "S.30.02",
            "S.30.03",
            "S.30.04",
            "S.31.01",

        };
        }


        //delete existing documents
        if (!_parameterData.IsDevelop || 1 == 2)
        {
            var (isHandleSuccess, handleMessage) = _factsCreator.HandleExistingDocuments();
            if (!isHandleSuccess)
            {
                _logger.Information(handleMessage);
                _SqlFunctions.CreateTransactionLog(MessageType.COMPLETE, handleMessage);
                return 1;
            }
        }

        //create loose facts
        if (!_parameterData.IsDevelop || 1 == 2)
        {
            (_documentId, filingsSubmitted) = _factsCreator.CreateLooseFacts();
            if (_documentId == 0)
            {
                return 1;
            }
        }

        //decorate facts and assign to sheets
        if (!_parameterData.IsDevelop || 1 == 1)
        {
            var res = _factsDecorator.DecorateFactsAndAssignToSheets(_documentId, filingsSubmitted);
            if (res != 0)
            {
                return res;
            }
        }

        if (!_parameterData.IsDevelop || 1 == 2)
        {
            var cnt = _combinedS62Services.K_UpdateDocumentForeignKeys(_documentId);
            Console.WriteLine("Create Sheet S.06.02.01.99");
            var combinedSheetId = _combinedS62Services.CreateCombinedSheetOnly(_documentId);
            if (combinedSheetId == 0)
            {
                Console.WriteLine($"Sheet NOT created");
                _logger.Error($"Document: {_documentId}. Combined Sheet NOT created");
                return 1;
            }


            var facts = await _combinedS62Services.CreateCombinedFacts(_documentId, combinedSheetId);
            Console.WriteLine($"\nFacts created:{facts} for S.06.02.01.99");

        }


        _SqlFunctions.UpdateDocumentStatus(_documentId, "L");
        var message = $"Xbrl Document Loaded Successfully:DocumentId= {_documentId}";
        _logger.Information(message);
        _SqlFunctions.CreateTransactionLog(MessageType.COMPLETE, message);
        return 0;
    }

    public record ValidEiopaVersion(string EiopaVersion, int ValidYear, List<int> ValidQuarters);
    private bool IsValidEiopaVersion()
    {
        List<ValidEiopaVersion> versions =
                [
                    new("IU260", 2021, [4, 0]),
                new("IU260", 2022, [1, 2, 3]),

                new("IU270", 2022, [4,0]),
                new("IU270", 2023, [1, 2, 3]),

                new("IU280", 2023, [4,0]),
                new("IU280", 2024, [1, 2, 3]),

                new("IU282", 2024, [4,0]),
                new("IU282", 2025, [1, 2, 3]),

            ];

        var versionData = versions.Where(x => x.EiopaVersion == _parameterData.EiopaVersion);

        foreach (var version in versionData)
        {
            if (version.ValidYear == _parameterData.ApplicableYear && version.ValidQuarters.Contains(_parameterData.ApplicableQuarter))
            {
                return true;
            }
        }

        return false;
    }

}
