namespace CreateCombinedS61S62;
using Mapster;
using Microsoft.VisualBasic.FileIO;
using Serilog;
using Shared.DataModels;
using Shared.ExcelHelperRoutines;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class CreateSheetAndFacts
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;

    const int combinedTabelId = 100001;
    const string combinedTableCode = "S.06.02.01.99";



    public CreateSheetAndFacts(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
    }


    public int FindDocument()
    {
        var docs = _SqlFunctions.SelectDocInstances(_parameterData.FundId, _parameterData.ModuleCode, _parameterData.ApplicableYear, _parameterData.ApplicableQuarter);
        if (!docs.Any())
        {
            var message = $"No documents found for module:{_parameterData.ModuleCode}, Fund:{_parameterData.FundId} Year:{_parameterData.ApplicableYear} Quarter:{_parameterData.ApplicableQuarter}";
            _logger.Error(message);
            return 0;
        }
        var doc = docs.FirstOrDefault();
        return doc is null ? 0 : doc.InstanceId;
    }


    public int CreateCombinedSheet(int documentId)
    {
        //a new sheet will be created -- tableId=100001,tableCode="S.06.02.01.99"

        var sheet = _SqlFunctions.SelectTemplateSheetsByTableId(documentId, combinedTabelId).FirstOrDefault();
        if (sheet != null)
        {
            _SqlFunctions.DeleteFactsTemplateSheet(sheet.TemplateSheetId); 
            _SqlFunctions.DeleteTemplateSheet(sheet.TemplateSheetId);
        }

        var newSheet = new TemplateSheetInstanceDataModel()
        {
            InstanceId = documentId,
            TableID = combinedTabelId,
            TableCode = combinedTableCode,
            SheetCode = combinedTableCode,
            SheetCodeZet = combinedTableCode,
            DateCreated = DateTime.Now,
            IsOpenTable=true,
        };
       var sheetId = _SqlFunctions.CreateTemplateSheet(newSheet);
        Console.WriteLine($"Sheet Created:{sheetId} sheetcode:{combinedTableCode}");

        
        var moreRows = true;
        var totalFacts = 0;
        var count = 1800;
        var increment = 200;
        var testingCount = 0;
        while (moreRows)
        {
            var startRow = $"R{count+1:D4}"; 
            var endRow = $"R{count+increment:D4}";
            
            var facts61 = _SqlFunctions.CreateCombinedFactsForS61(documentId, sheetId, startRow, endRow);
            Console.Write("1");
            var facts62 = _SqlFunctions.CreateCombinedFactsForS62(documentId, sheetId, startRow, endRow);
            Console.Write("2");
            count +=increment;
            testingCount += 1;
            totalFacts=totalFacts+ facts61 + facts62;
            moreRows = facts61 > 0 ;
        }
                

        return totalFacts;
    }


    public int K_UpdateDocumentForeignKeys(int documentId)
    {
        Console.WriteLine($"---------- DocumentID:{documentId}");
        var kyrTables = _SqlFunctions.K_SelectKyrTables()
            .Where(k => k.TableCode.Trim() == "S.06.02.01.01")
            .ToList();
        var sheets = _SqlFunctions.SelectTemplateSheets(documentId);
        foreach (var kyrTable in kyrTables)
        {
            var mainSheet = sheets.FirstOrDefault(sheet => sheet.TableCode.Trim() == kyrTable.TableCode.Trim());
            var relatedSheet = sheets.FirstOrDefault(sheet => sheet.TableCode.Trim() == kyrTable.FK_TableCode.Trim());
            if (mainSheet is null || relatedSheet is null)
            {
                continue;
            }

            Console.WriteLine($"sheet:{mainSheet.SheetCode}");
            //find the fact in each row, with Column = mainCol
            var mainKeyRowFacts = _SqlFunctions.K_SelectFactsByCol(documentId, mainSheet.TableCode, kyrTable.TableCol.Trim());
            //find the fact in each row, with column =fk_Col
            var total = 0;
            var relatedRowFacts = _SqlFunctions.K_SelectFactsByCol(documentId, relatedSheet?.TableCode ?? "", kyrTable.FK_TableCol.Trim());
            foreach (var mainRowFact in mainKeyRowFacts)
            {

                var relatedFact = relatedRowFacts.FirstOrDefault(fact => fact.TextValue.Trim() == mainRowFact.TextValue.Trim());

                if (relatedFact is not null)
                {
                    //update all main facts in this row with FK_ROW
                    var relatedRow = relatedFact.Row;
                    if (relatedRow is not null)
                    {
                        Console.Write(".");
                        var count = _SqlFunctions.K_UpdateForeignKeys(mainRowFact.TemplateSheetId, mainRowFact.Row, relatedRow);
                        total += count;
                    }
                }

            }
            Console.WriteLine($"Updated Facts:{total}");


        }


        //272

        return 0;
    }


}
