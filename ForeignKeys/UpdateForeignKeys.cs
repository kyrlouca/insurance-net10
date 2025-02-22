namespace ForeignKeys;
using Mapster;
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


public class UpdateForeignKeys : IUpdateForeignKeys
{

    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;

    public UpdateForeignKeys(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
    }

    public int UpdateForeignKeysForYear(int year)
    {

        var docs = _SqlFunctions.SelectDocInstances(_parameterData.FundId, _parameterData.ModuleCode, _parameterData.ApplicableYear, _parameterData.ApplicableQuarter);

        var documents = _SqlFunctions.K_documentsForYear(year);
        if (_parameterData.IsDevelop)
        {
            documents = documents.Where(id => id == 278).ToList();

        }
        foreach (var document in documents)
        {
            K_UpdateDocumentForeignKeys(document);
        }

        return 0;
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
                        var count = _SqlFunctions.K_UpdateForeignKeys(mainRowFact.TemplateSheetId, mainRowFact.Row, relatedRow);
                        total += count;
                    }
                }

            }
            Console.WriteLine($"Updated Facts:{total}");


        }

        return 0;
    }


}
