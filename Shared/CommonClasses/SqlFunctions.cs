namespace Shared.CommonRoutines;
using Dapper;
using Microsoft.Data.SqlClient;
using Shared.DataModels;
using Shared.HostRoutines;
using Shared.SharedHost;
using Shared.SpecialRoutines;
using Shared.GeneralUtils;
using System.Reflection;
using Serilog;
public class SqlFunctions : ISqlFunctions
{
    readonly ParameterData _parameterData;
    readonly IParameterHandler? _parameterHandler;
    readonly ILogger _logger;
    public SqlFunctions(IParameterHandler parameterHandler, ILogger logger)
    {
        _parameterHandler = parameterHandler;
        _parameterData = _parameterHandler?.GetParameterData() ?? new();
        _logger = logger;
    }

    public DocInstance? SelectDocInstance(int documentId)
    {
        var sqlGetDocument = @"
                    SELECT
                      doc.InstanceId
                     ,doc.PensionFundId
                     ,doc.ModuleId
                     ,doc.Status
                     ,doc.ModuleCode
                     ,doc.ApplicableYear
                     ,doc.ApplicableQuarter
                     ,doc.EntityCurrency
                     ,doc.UserId
                    FROM dbo.DocInstance doc
                    WHERE doc.InstanceId = @documentId
                    ";
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var doc = connectionInsurance.QuerySingleOrDefault<DocInstance>(sqlGetDocument, new { documentId });
        return doc;
    }



    public List<TemplateSheetInstance> SelectTempateSheets(int documentId)
    {

        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSheets = @"
			SELECT *, (SELECT COUNT(*) FROM TemplateSheetFact fact WHERE fact.TemplateSheetId= sheet.TemplateSheetId) AS FactsCounter
			FROM TemplateSheetInstance sheet
			WHERE
			  sheet.InstanceId = @documentID
			ORDER BY sheet.SheetTabName   			";
        var sheets = connectionLocal.Query<TemplateSheetInstance>(sqlSheets, new { documentId });
        //	.Where(sheet => sheet.FactsCounter > 0);

        return sheets.ToList();

    }

    public DocInstance? SelectDocInstance(int fundId, string moduleCode, int ApplicableYear, int ApplicableQuarter)
    {
        var sqlGetDocument = @"
            SELECT * 
			FROM 
				InsuranceDatabase.dbo.DocInstance doc
			WHERE
			  doc.PensionFundId =@fundId AND doc.ApplicableYear=@ApplicableYear AND doc.ApplicableQuarter=@ApplicableQuarter
			ORDER BY doc.InstanceId DESC
        ";
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var doc = connectionInsurance.QueryFirstOrDefault<DocInstance>(sqlGetDocument, new { fundId, moduleCode, ApplicableYear, ApplicableQuarter });
        return doc;
    }



    public MModule? SelectModuleByCode(string moduleCode)
    {
        using var connectionPension = new SqlConnection(_parameterData.SystemConnectionString);
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

        //module code : {ari, qri, ara, ...}
        var sqlModule = "select ModuleCode, ModuleId, ModuleLabel from mModule mm where mm.ModuleCode = @ModuleCode";
        var module = connectionEiopa.QuerySingleOrDefault<MModule>(sqlModule, new { moduleCode = moduleCode.ToLower().Trim() });
        return module;

    }


    public void CreateTransactionLog(int docInstanceId, MessageType messageType, string message)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var tl = new LogTransactionModel()
        {
            ExternalId = -1,
            PensionFundId = _parameterData.FundId,
            ModuleCode = _parameterData.ModuleCode,
            ApplicableYear = _parameterData.ApplicableYear,
            ApplicableQuarter = _parameterData.ApplicableQuarter,
            Message = message,
            UserId = _parameterData.UserId,
            ProgramCode = "EX",
            ProgramAction = ProgramAction.INS.ToString(),
            InstanceId = docInstanceId,
            MessageType = messageType.ToString()

        };
        var sqlInsert = @"
                INSERT INTO TransactionLog(ExternalId,PensionFundId, ModuleCode, ApplicableYear, ApplicableQuarter, Message, UserId, ProgramCode, ProgramAction,InstanceId,MessageType,FileName)
                VALUES(@externalId,@PensionFundId, @ModuleCode, @ApplicableYear, @ApplicableQuarter, @Message,  @UserId, @ProgramCode, @ProgramAction,@InstanceId,@MessageType,@FileName);
            ";
        var x = connectionInsurance.Execute(sqlInsert, tl);
    }


    public void UpdateDocumentStatus(int documentId, string status)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlUpdate = @"update DocInstance  set status= @status where  InstanceId= @documentId;";
        var doc = connectionInsurance.Execute(sqlUpdate, new { documentId, status });
    }

    public MMember? SelectDomainMember(string domainString)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var xbrlCode = SpecialRoutines.DimDom.GetParts(domainString).DomAndValRaw;
        var sqlMem = @"select * from mMember mem where MemberXBRLCode = @xbrlCode";
        var val = connectionEiopa.QuerySingleOrDefault<MMember>(sqlMem, new { xbrlCode });
        return val;
    }
    public MTable? SelectTable(string tableCode)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlTable = @"SELECT * from mTable mtab   where mtab.TableCode= @tableCode";


        var result = connectionEiopa.QueryFirstOrDefault<MTable>(sqlTable, new { tableCode });
        return result;
    }


   
    public List<MAPPING> SelectTableMappings(int tableId, MappingOrigin mappingOrigin)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var andOriginSQL = "";

        switch (mappingOrigin)
        {
            case MappingOrigin.Field:
                andOriginSQL = "AND ORIGIN = 'F' ";
                break;
            case MappingOrigin.Page:
                andOriginSQL = "AND DYN_TABLE_NAME LIKE 'PAGE%' ";
                break;
            case MappingOrigin.Column:
                andOriginSQL = "AND ORIGIN = 'C' ";
                break;
            default:
                andOriginSQL = "";
                break;
        }

        var sqlTable = @$"
			SELECT map.* 
			FROM MAPPING map
			WHERE  
			  map.TABLE_VERSION_ID=@tableId
			  {andOriginSQL}
			ORDER BY DYN_TABLE_NAME, DYN_TAB_COLUMN_NAME
			";

        var result = connectionEiopa.Query<MAPPING>(sqlTable, new { tableId })?.ToList();
        return result ?? new List<MAPPING>();
    }

    public List<MAPPING> SelectRowColMappings(int tableId, string rowCol)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlTable = @"
			SELECT map.* 
			FROM MAPPING map
			WHERE  
			  map.TABLE_VERSION_ID=@tableId
			  AND ORIGIN='F'
              AND DYN_TAB_COLUMN_NAME= @rowcol
			ORDER BY DYN_TABLE_NAME, DYN_TAB_COLUMN_NAME
			";

        var result = connectionEiopa.Query<MAPPING>(sqlTable, new { tableId, rowCol })?.ToList();
        return result ?? new List<MAPPING>();
    }

}
