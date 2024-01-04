namespace Shared.CommonRoutines;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.DataModels;
using Shared.GeneralUtils;
using Shared.HostParameters;
using Shared.SharedHost;
using System.Collections.Generic;

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



    public void CreateTransactionLog(int docInstanceId, MessageType messageType, string message)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var tl = new LogTransactionModel()
        {
            ExternalId = _parameterData.ExternalId,
            FileName = _parameterData.FileName,
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


    public IEnumerable<DocInstance> SelectDocInstances(int fundId, string moduleCode, int ApplicableYear, int ApplicableQuarter)
    {
        var sqlGetDocument = @"
            SELECT * 
			FROM 
				DocInstance doc
			WHERE
			  doc.PensionFundId =@fundId AND doc.moduleCode=@moduleCode and doc.ApplicableYear=@ApplicableYear AND doc.ApplicableQuarter=@ApplicableQuarter
			ORDER BY doc.InstanceId DESC
        ";
        using var connectionSystem = new SqlConnection(_parameterData.SystemConnectionString);
        var docs = connectionSystem.Query<DocInstance>(sqlGetDocument, new { fundId, moduleCode, ApplicableYear, ApplicableQuarter });
        return docs ?? Enumerable.Empty<DocInstance>();
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

        try
        {
            var sheetId = connectionInsurance.QuerySingle<int>(SqlInsertTemplateSheet, sheet);
            sheet.TemplateSheetId = sheetId;

            return sheet;
        }
        catch (Exception ex)
        {
            Log.Error($"error creating Sheet :{sheet.SheetCode}");
            throw;
        }
        
    }

    public TemplateSheetFact? CreateTemplateSheetFact(TemplateSheetFact fact)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        if(fact is null )
        {
            return null;
        }
        var sqlInsertLooseFact = @"
             INSERT INTO TemplateSheetFact 
                (InstanceId , Row, Col, Zet, CellID, FieldOrigin, TableID, DataPointSignature, Unit, Decimals, NumericValue, BooleanValue, DateTimeValue, TextValue, DPS, IsRowKey, IsShaded, XBRLCode, DataType, DataPointSignatureFilled,  InternalRow, internalCol, DataTypeUse, IsEmpty, IsConversionError, ZetValues, OpenRowSignature, CurrencyDim,  metricId, contextId,ContextNumberId,  RowSignature  )
             VALUES 
                (@InstanceId,  @Row, @Col, @Zet, @CellID, @FieldOrigin, @TableID, @DataPointSignature, @Unit, @Decimals, @NumericValue, @BooleanValue, @DateTimeValue, @TextValue, @DPS, @IsRowKey, @IsShaded, @XBRLCode, @DataType, @DataPointSignatureFilled,  @InternalRow, @internalCol, @DataTypeUse, @IsEmpty, @IsConversionError, @ZetValues, @OpenRowSignature, @CurrencyDim,  @metricId,  @contextId,@ContextNumberId,  @RowSignature );
             SELECT CAST(SCOPE_IDENTITY() as int);            
            ";

        var sqlInsertSheetFact = @"
             INSERT INTO TemplateSheetFact 
                (InstanceId ,templateSheetId, Row, Col, Zet, CellID, FieldOrigin, TableID, DataPointSignature, Unit, Decimals, NumericValue, BooleanValue, DateTimeValue, TextValue, DPS, IsRowKey, IsShaded, XBRLCode, DataType, DataPointSignatureFilled,  InternalRow, internalCol, DataTypeUse, IsEmpty, IsConversionError, ZetValues, OpenRowSignature, CurrencyDim,  metricId, contextId,ContextNumberId,  RowSignature  )
             VALUES 
                (@InstanceId,@templateSheetId,  @Row, @Col, @Zet, @CellID, @FieldOrigin, @TableID, @DataPointSignature, @Unit, @Decimals, @NumericValue, @BooleanValue, @DateTimeValue, @TextValue, @DPS, @IsRowKey, @IsShaded, @XBRLCode, @DataType, @DataPointSignatureFilled,  @InternalRow, @internalCol, @DataTypeUse, @IsEmpty, @IsConversionError, @ZetValues, @OpenRowSignature, @CurrencyDim,  @metricId,  @contextId,@ContextNumberId,  @RowSignature );
             SELECT CAST(SCOPE_IDENTITY() as int);            
            ";
        int factId = 0;
        var sqlInsert= fact.TemplateSheetId>0 ? sqlInsertSheetFact : sqlInsertLooseFact;
        try
        {
            factId = connectionInsurance.QuerySingle<int>(sqlInsert,fact);
        }
        catch( Exception ex ) {
            Log.Error($"error creating Fact :{fact.Row} col:{fact.Col} - {ex.Message}");
            return null;
        }
        fact.FactId = factId;
        return fact;
    }


    public ContextLine? CreateContextLine(ContextLine contextLine)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlInsert = @"
        INSERT INTO[ContextLine]([ContextId],signature, [Dimension], [Domain], [DomainValue], [DomainAndValue], [IsExplicit], [InstanceId])
                    VALUES(@ContextId,@signature, @Dimension, @Domain, @DomainValue, @DomainAndValue, @IsExplicit, @InstanceID);
        SELECT CAST(SCOPE_IDENTITY() as int);            
        ";

        var ctxId = 0;
        try
        {
            ctxId = connectionInsurance.QuerySingleOrDefault<int>(sqlInsert, contextLine);
            contextLine.ContextId = ctxId;
            return contextLine;
        }
        catch (Exception ex)
        {
            Log.Error($"error creating ContextLine :{contextLine.ContextId} col:{contextLine.DomainAndValue} - {ex.Message}");
            return null;
        }                
        
    }


    public ContextModel? CreateContext(ContextModel context)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlInsertContext = @"INSERT INTO Context (InstanceId, ContextXbrlId, Signature, TableId) 
                    VALUES (@InstanceId,@ContextXbrlId, @Signature, @TableId)
                    SELECT CAST(SCOPE_IDENTITY() as int);
                ";

        var ctxId = 0;
        try
        {
            ctxId = connectionInsurance.QuerySingleOrDefault<int>(sqlInsertContext, context);
            var res= context with { ContextId=ctxId };
            return res;
        }
        catch (Exception ex)
        {
            Log.Error($"error creating Context :{context.Signature} - {ex.Message}");
            return null;
        }

    }

    public ContextModel? SelectContext(int documentId, string contextXbrlId)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelectContext = @"select * from Context ctx where ctx.InstanceId=@documentId and  ctx.ContextXbrlId=@ContextXbrlId";

        var ctx = connectionInsurance.QuerySingleOrDefault<ContextModel>(sqlSelectContext, new { documentId, contextXbrlId });
        return ctx;        

    }

    public List<ContextLine> SelectContextLines(int contextId)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelectContext = @"select * from ContextLine cl where cl.ContextId = @ContextId";

        var ctx = connectionInsurance.Query<ContextLine>(sqlSelectContext, new { contextId,  }).ToList();
        return ctx;
    }


}

