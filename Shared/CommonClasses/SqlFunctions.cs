namespace Shared.CommonRoutines;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.DataModels;
using Shared.GeneralUtils;
using Shared.HostRoutines;
using Shared.SharedHost;

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

    public List<TemplateSheetFactDim> SelectFactDims(int factId)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);

        var sqlSelect = @"select * from TemplateSheetFactDim fd where fd.FactId= @factId;";
        var res = connectionInsurance?.Query<TemplateSheetFactDim>(sqlSelect, new { factId })?.ToList() ?? new List<TemplateSheetFactDim>();
        return res;
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

    public MMember? SelectMMember(string xbrlCode)
    {
        //memberXbrlCode= s2c_AM:x2 => find mMember
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);        
        var sqlMem = @"select * from mMember mem where MemberXBRLCode = @xbrlCode";
        xbrlCode=xbrlCode.Trim();
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



    public List<MAPPING> SelectMappings(int tableId, MappingOrigin mappingOrigin)
    {

        
        //-- and ORIGIN = 'C' and IS_IN_TABLE = 1 and map.IS_PAGE_COLUMN_KEY = 1 (used for selecting a dim)
        //-- and ORIGIN = 'C' and IS_IN_TABLE = 1 and map.IS_PAGE_COLUMN_KEY = 0 (used for listing the values of the DIM)

        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var andOriginSQL = "";

        switch (mappingOrigin)
        {
            case MappingOrigin.Field:
                andOriginSQL = " AND ORIGIN = 'F' ";
                break;
            case MappingOrigin.Page:
                andOriginSQL = "AND ORIGIN = 'C' and IS_IN_TABLE=1  AND IS_PAGE_COLUMN_KEY=1";
                break;
            case MappingOrigin.ColumnGeneral:
                andOriginSQL = " AND ORIGIN = 'C' ";
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

        var result = connectionEiopa!.Query<MAPPING>(sqlTable, new { tableId })?.ToList();


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




        var result = connectionEiopa!.Query<MAPPING>(sqlTable, new { tableId, rowCol })?.ToList();
        return result ?? new List<MAPPING>();
    }




    public TemplateSheetInstance CreateTemplateSheet(int documentId, string sheetCode, string sheetCodeZet, string sheetTabName, MTable table)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var SqlInsertTemplateSheet = @"
                         INSERT INTO TemplateSheetInstance
                           (
                            [InstanceId]
                           ,[UserId]         
                           ,[TableId]
                           ,[DateCreated]
                           ,[SheetCode]                 
                           ,[SheetCodeZet]                 
                           ,[sheetTabName]                 
                           ,[TableCode]                 
                           ,[YDimVal]
                           ,[ZDimVal]
                           ,[status]
                           ,[Description]
                           ,[XbrlFilingIndicatorCode]
                            ,[IsOpenTable]

                            )
                        VALUES
                           (
                            @InstanceId
                           ,@UserId  
                           ,@TableId
                           ,@DateCreated
                           ,@SheetCode
                           ,@SheetCodeZet
                           ,@sheetTabName
                           ,@TableCode
                           ,@YDimVal
                           ,@ZDimVal
                           ,@status           
                           ,@Description
                           ,@XbrlFilingIndicatorCode
                            ,@IsOpenTable
                            );        
                            SELECT CAST(SCOPE_IDENTITY() as int);
                        ";
        Console.Write(',');

        var sheet = new TemplateSheetInstance()
        {
            InstanceId = documentId,
            UserId = "KL",
            TableID = table.TableID,
            TableCode = table.TableCode,
            DateCreated = DateTime.Now,
            SheetCode = sheetCode,
            SheetCodeZet = sheetCodeZet,
            SheetTabName = sheetTabName,
            YDimVal = table.YDimVal ?? "",
            ZDimVal = table.ZDimVal ??"",
            Status = "LD",
            Description = RegexUtils.TruncateString(table.TableLabel, 199),
            XbrlFilingIndicatorCode = table.XbrlFilingIndicatorCode,
            IsOpenTable = table?.IsOpenTable ??false,
            OpenRowCounter = 0
        };

        var sheetId = connectionInsurance.QuerySingle<int>(SqlInsertTemplateSheet, sheet);
        sheet.TemplateSheetId = sheetId;
        
        return sheet;
    }

    public TemplateSheetFact? CreateTemplateSheetFact(TemplateSheetFact fact)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        if(fact is null )
        {
            return null;
        }
        var sqlInsertFact = @"
             INSERT INTO TemplateSheetFact 
                (InstanceId ,templateSheetId, Row, Col, Zet, CellID, FieldOrigin, TableID, DataPointSignature, Unit, Decimals, NumericValue, BooleanValue, DateTimeValue, TextValue, DPS, IsRowKey, IsShaded, XBRLCode, DataType, DataPointSignatureFilled,  InternalRow, internalCol, DataTypeUse, IsEmpty, IsConversionError, ZetValues, OpenRowSignature, CurrencyDim,  metricId, contextId,  RowSignature  )
             VALUES 
                (@InstanceId,@templateSheetId,  @Row, @Col, @Zet, @CellID, @FieldOrigin, @TableID, @DataPointSignature, @Unit, @Decimals, @NumericValue, @BooleanValue, @DateTimeValue, @TextValue, @DPS, @IsRowKey, @IsShaded, @XBRLCode, @DataType, @DataPointSignatureFilled,  @InternalRow, @internalCol, @DataTypeUse, @IsEmpty, @IsConversionError, @ZetValues, @OpenRowSignature, @CurrencyDim,  @metricId,  @contextId,  @RowSignature );
             SELECT CAST(SCOPE_IDENTITY() as int);            
            ";
        int factId = 0;
        try
        {
            factId = connectionInsurance.QuerySingle<int>(sqlInsertFact,fact);
        }
        catch( Exception ex ) {
            Log.Error($"error creating Fact :{fact.Row} col:{fact.Col} - {ex.Message}");
            return null;
        }
        fact.FactId = factId;
        return fact;
    }

}
