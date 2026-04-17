using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.DataModels;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace XbrlReader;

public class CombinedS62Services : ICombinedS62Services
{

    ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;
    const int combinedTabelId = 100001;
    const string combinedTableCode = "S.06.02.01.99";


    public CombinedS62Services(IParameterHandler getParameters, ISqlFunctions sqlFunctions, ILogger logger)
    {
        _logger = logger;
        _parameterData = getParameters.GetParameterData();
        _SqlFunctions = sqlFunctions;

    }




    public int CreateCombinedSheetOnly(int documentId)
    {
        //a new sheet will be created -- tableId=100001,tableCode="S.06.02.01.99"

        var sheet = _SqlFunctions.SelectTemplateSheetsByTableId(documentId, combinedTabelId).FirstOrDefault();
        if (sheet != null)
        {
            _SqlFunctions.DeleteFactsTemplateSheet(sheet.TemplateSheetId);
            var y = _SqlFunctions.DeleteTemplateSheet(sheet.TemplateSheetId);
        }

        var newSheet = new TemplateSheetInstanceDataModel()
        {
            InstanceId = documentId,
            TableID = combinedTabelId,
            TableCode = combinedTableCode,
            SheetCode = combinedTableCode,
            SheetCodeZet = combinedTableCode,
            DateCreated = DateTime.Now,
            IsOpenTable = true,
        };
        var sheetId = _SqlFunctions.CreateTemplateSheet(newSheet);
        Console.WriteLine($"Sheet Created:{sheetId} sheetcode:{combinedTableCode}");
        return sheetId;
    }

    public int CreateCombinedFacts(int documentId, int sheetId)
    {

        //create facts in batches of 200 rows
        //do not open and close connection in the loop, very slow
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        connectionLocal.Open();

        var moreRows = true;
        var totalFacts = 0;
        var count = 0;
        var increment = 200;
        var testingCount = 0;
        while (moreRows)
        {
            var startRow = $"R{count + 1:D4}";
            var endRow = $"R{count + increment:D4}";

            var facts61 =  OpenConnection_CreateCombinedFactsForS61(connectionLocal, documentId, sheetId, startRow, endRow);
            Console.Write("1");

            var facts62 =  OpenConnection_CreateCombinedFactsForS62(connectionLocal, documentId, sheetId, startRow, endRow);
            Console.Write("2");
            count += increment;
            testingCount += 1;
            totalFacts = totalFacts + facts61 + facts62;
            moreRows = (facts61 + facts62) > 0;
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


    private int OpenConnection_CreateCombinedFactsForS61(SqlConnection connectionLocal, int documentId, int sheetId, string startRow, string endRow)
    {

        var sqlInsert = @"
INSERT INTO Dbo.Templatesheetfact (Instanceid, Templatesheetid, Row, rowForeign, Col, Textvalue, Numericvalue, Datetimevalue, CurrencyDim)
SELECT 
   Factt1.Instanceid, 
   @sheetId,
   Factt1.Row, 
    Factt1.RowFOreign, 
   Factt1.Col,
   Factt1.Textvalue, 
   Factt1.Numericvalue, 
   Factt1.Datetimevalue, 
   '' -- Setting CurrencyDim to an empty string
FROM Dbo.Templatesheetfact AS Factt1
INNER JOIN Dbo.Templatesheetinstance AS Sheett1 
   ON Sheett1.Templatesheetid = Factt1.Templatesheetid
WHERE 
   Sheett1.InstanceId = @DocumentId  
   AND Sheett1.Tablecode = 'S.06.02.01.01'
   AND (Factt1.Row between @startRow and @endRow)
   
";

        try
        {

            var facts =  connectionLocal.Execute(sqlInsert, new { documentId, sheetId, startRow, endRow });
            return facts;

        }
        catch (Exception e)
        {
            _logger.Error(e.Message);

            Console.Write(e.Message);
            throw (e);
        }



    }


    private  int OpenConnection_CreateCombinedFactsForS62(SqlConnection connectionLocal, int documentId, int sheetId, string startRow, string endRow)
    {
        var sqlInsert = @"
WITH S61c40 AS
(
   SELECT
      Factt1.Instanceid,
      Factt1.Templatesheetid,
      Factt1.Row,
      Factt1.Rowforeign
   FROM
      Dbo.Templatesheetfact AS Factt1
   INNER JOIN Dbo.Templatesheetinstance AS Sheett1 
      ON Sheett1.Templatesheetid = Factt1.Templatesheetid
      AND Sheett1.Instanceid = Factt1.Instanceid
   WHERE
      Sheett1.Tablecode = 'S.06.02.01.01'
      AND Factt1.Col = 'C0001'
      AND (Factt1.Row between @startRow and @endRow)
      AND Sheett1.InstanceId = @DocumentId
),
S62 AS
(
   SELECT
      Factt1.Instanceid,
      Factt1.Templatesheetid,
      Factt1.Row,
      Factt1.Col,
      Factt1.Textvalue,
      Factt1.Numericvalue,
      Factt1.Datetimevalue
   FROM
      Dbo.Templatesheetfact AS Factt1
   INNER JOIN Dbo.Templatesheetinstance AS Sheett1 
      ON Sheett1.Templatesheetid = Factt1.Templatesheetid
      AND Sheett1.Instanceid = Factt1.Instanceid
   INNER JOIN S61c40 
      ON S61c40.Instanceid = Sheett1.Instanceid
      AND S61c40.Rowforeign = Factt1.Row      
   WHERE
      Sheett1.InstanceId = @DocumentId      
      AND Sheett1.Tablecode = 'S.06.02.01.02'
      AND Factt1.Instanceid = @DocumentId
      
)

INSERT INTO Dbo.Templatesheetfact (Instanceid, Templatesheetid, Row, Col, Textvalue, Numericvalue, Datetimevalue, CurrencyDim)
SELECT 
   S61c40.Instanceid, 
   @sheetId,
   S61c40.Row, 
   Coalesce(S62.Col,'CXXXX'),
   S62.Textvalue, 
   S62.Numericvalue, 
   S62.Datetimevalue, 
   '' -- Setting CurrencyDim to an empty string
FROM S61c40
LEFT JOIN S62 
   ON S62.Row = S61c40.Rowforeign
  AND S62.Instanceid = S61c40.Instanceid
   

";
        try
        {
            var facts = connectionLocal.Execute(sqlInsert, new { documentId, sheetId, startRow, endRow }, commandTimeout: 120);
            return facts;
        }
        catch (Exception e)
        {

            _logger.Error(e.Message);
            Console.Write(e.Message);
            throw (e);
        }

        return 0;
    }


    private int OpenConnection_DeleteFactsTemplateSheet(SqlConnection connectionLocal, int templateSheetId)
    {


        var sqlDelete = @"delete from TemplateSheetFact where TemplateSheetId=@TemplateSheetId;";
        var count = connectionLocal.Execute(sqlDelete, new { templateSheetId });
        return count;

    }

}
