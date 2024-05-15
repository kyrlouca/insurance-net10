namespace Shared.SQLFunctions;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.SqlClient;
using Serilog;
using Shared.DataModels;
using Shared.GeneralUtils;
using Shared.HostParameters;
using Shared.SharedHost;
using Syncfusion.XlsIO.Implementation.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlTypes;
using System.Reflection.Metadata;
using static Shared.SQLFunctions.ISqlFunctions;

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



    public void CreateTransactionLog(MessageType messageType, string message)
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
            InstanceId = _parameterData.DocumentId,
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
                      *
                    FROM dbo.DocInstance doc
                    WHERE doc.InstanceId = @documentId
                    ";
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var doc = connectionInsurance.QuerySingleOrDefault<DocInstance>(sqlGetDocument, new { documentId });
        return doc;
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


    public void UpdateDocumentStatus(int documentId, string status)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlUpdate = @"update DocInstance  set status= @status where  InstanceId= @documentId;";
        var doc = connectionInsurance.Execute(sqlUpdate, new { documentId, status });
    }



    public TemplateSheetInstance? SelectTemplateSheetByZetValue(int documentId, string tableCode, string ZDimVal)
    {
        //*** do NOT use this . the sheetCodeZet is the same with ZdimVal
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSheets = @"
            SELECT 
              sheet.*
            FROM TemplateSheetInstance sheet    
            WHERE 
                sheet.InstanceId = @documentId        
                and sheet.TableCode = @tableCode        
                and ZDimVal = @ZDimVal
             ";
        var sheet = connectionLocal.QuerySingleOrDefault<TemplateSheetInstance>(sqlSheets, new { documentId, tableCode, ZDimVal });
        return sheet;
    }

    public TemplateSheetInstance? SelectTemplateSheetBySheetCodeZet(int documentId, string tableCode, string sheetCodeZet)
    {
        //*** do NOT use this . the sheetCodeZet is the same with ZdimVal
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSheets = @"
            SELECT 
              sheet.*
            FROM TemplateSheetInstance sheet    
            WHERE 
                sheet.InstanceId = @documentId        
                and sheet.TableCode = @tableCode        
                and SheetCodeZet = @sheetCodeZet
             ";
        var sheet = connectionLocal.QuerySingleOrDefault<TemplateSheetInstance>(sqlSheets, new { documentId, tableCode, sheetCodeZet });
        return sheet;
    }

    public List<TemplateSheetInstance> SelectTemplateSheetByTableCodeAllZets(int documentId, string tableCode)    
    {
        //
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSheets = @"
            SELECT 
              sheet.*
            FROM TemplateSheetInstance sheet    
            WHERE 
                sheet.InstanceId = @documentId        
                and sheet.TableCode = @tableCode                        
             ";
        var sheet = connectionLocal.Query<TemplateSheetInstance>(sqlSheets, new { documentId, tableCode }).ToList();
        return sheet;
    }
    public List<TemplateSheetInstance> SelectTemplateSheets(int documentId)
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

    public List<TemplateSheetInstance> SelectTemplateSheetsByTableId(int documentId, int tableId)
    {
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSheets = @"Select * from TemplateSheetInstance sheet where sheet.InstanceId= @documentId and sheet.TableID= @tableId";
        var sheets = connectionLocal.Query<TemplateSheetInstance>(sqlSheets, new { documentId, tableId });
        return sheets.ToList();
    }

    public void UpdateTemplateSheetName(int templateSheetId, string sheetTabName)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlUpdate = @"update TemplateSheetInstance set SheetTabName = @SheetTabName where TemplateSheetId= @TemplateSheetId";
        var res = connectionInsurance.Execute(sqlUpdate, new { templateSheetId, sheetTabName });
    }

    public TemplateSheetFact? SelectFact(int factId)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelect = "select * from TemplateSheetFact fact where fact.FactId= @FactId";
        var res = connectionInsurance?.QuerySingleOrDefault<TemplateSheetFact>(sqlSelect, new { factId });
        return res;
    }

    public List<TemplateSheetFact> SelectFactsForSheetId(int sheetId)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelect = "select * from  TemplateSheetFact where TemplateSheetId= @sheetId;";
        var res = connectionInsurance.Query<TemplateSheetFact>(sqlSelect, new { sheetId}).ToList();
        return res;
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


    public MMember? SelectMMember(string xbrlCode)
    {
        //memberXbrlCode= s2c_AM:x2 => find mMember
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlMem = @"select * from mMember mem where MemberXBRLCode = @xbrlCode";
        xbrlCode = xbrlCode.Trim();
        var val = connectionEiopa.QuerySingleOrDefault<MMember>(sqlMem, new { xbrlCode });
        return val;
    }

    public List<MMember> SelectMMembersFromHierarchy(int hierarchyId)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlMem = @"select mem.* from mHierarchyNode hn	join mMember mem on mem.MemberID=hn.MemberID where HierarchyID = @HierarchyID";
        var res = connectionEiopa.Query<MMember>(sqlMem, new { hierarchyId })?.ToList() ?? new List<MMember>();
        return res;
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




    public TemplateSheetInstance CreateTemplateSheet(int documentId, string sheetCode, string sheetCodeZet, string sheetTabName, string zDimVal, MTable table)
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
            YDimVal = "xx",
            ZDimVal = zDimVal,
            Status = "LD",
            Description = table.TableLabel.TruncateString(199),
            XbrlFilingIndicatorCode = table.XbrlFilingIndicatorCode,
            IsOpenTable = table?.IsOpenTable ?? false,
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

    public int CreateTemplateSheetFact(TemplateSheetFact fact,bool isLooseFact)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        if (fact is null)
        {
            return 0;
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
        var sqlInsert = isLooseFact ? sqlInsertLooseFact:sqlInsertSheetFact ;
        try
        {
            factId = connectionInsurance.QuerySingle<int>(sqlInsert, fact);
        }
        catch (Exception ex)
        {
            Log.Error($"error creating Fact :{fact.Row} col:{fact.Col} - {ex.Message}");
            return 0;
        }        
        return factId;
    }


    public ContextLine? CreateContextLine(ContextLine contextLine)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlInsert = @"
        INSERT INTO[ContextLine]([ContextId],signature, [Dimension], [Domain], [DomainValue], [DomainAndValue], [IsExplicit], [InstanceId] , [isNil])
                    VALUES(@ContextId,@signature, @Dimension, @Domain, @DomainValue, @DomainAndValue, @IsExplicit, @InstanceID, @isNil);
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
            var res = context with { ContextId = ctxId };
            return res;
        }
        catch (Exception ex)
        {
            Log.Error($"error creating Context :{context.Signature} - {ex.Message}");
            return null;
        }

    }

    public ContextModel? SelectContextBySignature(int documentId, string contextSignature)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelectContext = @"select * from ContextLine ctx where ctx.InstanceId= @documentId and ctx.Signature=@contextSignature";

        var ctx = connectionInsurance.QuerySingleOrDefault<ContextModel>(sqlSelectContext, new { documentId, contextSignature });
        return ctx;

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

        var ctx = connectionInsurance.Query<ContextLine>(sqlSelectContext, new { contextId, }).ToList();
        return ctx;
    }


    public FundModel? SelectFund(int fundId)
    {
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);

        var sqlFund = "Select * from fund fnd where fnd.FundId= @FundId";
        var fund = connectionLocal.QuerySingleOrDefault<FundModel>(sqlFund, new { fundId });
        return fund;
    }


    public MTable? SelectTable(string tableCode)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlTable = @"SELECT * from mTable mtab   where mtab.TableCode= @tableCode";


        var result = connectionEiopa.QueryFirstOrDefault<MTable>(sqlTable, new { tableCode });
        return result;
    }

    public bool IsOpenTable(int tableId)
    {
        var yOrdinatesForKeys = SelectTableAxisOrdinateInfo(tableId)
            .Where(ord => ord.AxisOrientation == "Y" && ord.IsRowKey && ord.IsOpenAxis);
        return yOrdinatesForKeys.Any();
    }

    public List<MTable> SelectTablesInModule280(int moduleId)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlSelect = @"
            SELECT tab.*
            FROM
              mTemplateOrTable child
              JOIN mTemplateOrTable par ON par.TemplateOrTableID = child.ParentTemplateOrTableID
              JOIN mModuleBusinessTemplate mb ON mb.BusinessTemplateID=par.TemplateOrTableID
              JOIN mTaxonomyTable taxo ON taxo.AnnotatedTableID= child.TemplateOrTableID
              JOIN mTable tab ON tab.TableID=taxo.TableID
            WHERE
              mb.ModuleID= @ModuleID
            ORDER BY par.TemplateOrTableCode
            ";


        var ctx = connectionInsurance.Query<MTable>(sqlSelect, new { moduleId }).ToList();
        return ctx;
    }

    public List<MTable> SelectTablesForValidationRule(int validationRuleId)
    {
        //a rule may apply to more than one table (or to no table), therefore rule may appear twice
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

        var sqlSelect = @"
          select 
            tab.* from mTable tab 
		    join vValidationRuleTables vrt on vrt.TableID=tab.TableID
			where vrt.ValidationID= @validationRuleId;
        ";

        var tables = connectionEiopa.Query<MTable>(sqlSelect, new { validationRuleId }).ToList();
        foreach(MTable table in tables)
        {
            table.IsOpenTable = IsOpenTable(table.TableID);
        }
        return tables;
    }

    public List<MTableCell> SelectTableCells(int tableId)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlSelectContext = @"select * from mTableCell cell where cell.TableID =@TableID";

        var ctx = connectionInsurance.Query<MTableCell>(sqlSelectContext, new { tableId, }).ToList();
        return ctx;
    }


    public List<MTableKyrKeys> SelectTableKyrKeys(string tableCode)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlTable = @"select * from mTableKyrKeys tk where tk.TableCode= @tableCode";

        var result = connectionEiopa.Query<MTableKyrKeys>(sqlTable, new { tableCode }).ToList();
        return result;
    }

    public MTableKyrKeys? SelectTableKyrKey(string tableCode)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlTable = @"select * from mTableKyrKeys tk where tk.TableCode= @tableCode";

        var result = connectionEiopa.QueryFirstOrDefault<MTableKyrKeys>(sqlTable, new { tableCode });
        return result;
    }
    public List<TemplateSheetFact> SelectFactsBySignature(int documentId, string signature)
    {
        using var connectionInsurance = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelectContext = @"select * from TemplateSheetFact fact where InstanceId=@DocumentId and fact.DataPointSignature = @signature;	";

        var ctx = connectionInsurance.Query<TemplateSheetFact>(sqlSelectContext, new { documentId, signature }).ToList();
        return ctx;
    }

    public List<TemplateSheetFact> SelectFactsByColAndTextValue(int documentId,string tableCode,string col,string textValue)
    {
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelect = @"
            select * from TemplateSheetFact fact 
            join TemplateSheetInstance sheet on sheet.TemplateSheetId=fact.TemplateSheetId
            where 1=1
                and fact.InstanceId= @documentId
                and sheet.TableCode= @tableCode
                and fact.Col=@col
                and fact.TextValue= @textValue
";
        var facts = connectionLocal.Query<TemplateSheetFact>(sqlSelect, new { documentId, tableCode, col,textValue }).ToList();
        return facts;
    }


    public (int count, double sum, int decimals) GetSumofTableCode(int documentId, string tableCode, string zet, string row, string col)
    {
        //ZET: sometimes is not used :  when  the closed table has an empty zet and the open tables have an actual zet
        //for example rule 647 : 
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelectZet = @"
                 SELECT count(*), sum(fact.NumericValue),max(fact.decimals)
                FROM
                  TemplateSheetFact fact
                  JOIN TemplateSheetInstance sheet ON sheet.TemplateSheetId=fact.TemplateSheetId
                WHERE
                  1=1
                  AND sheet.InstanceId= @documentId
                  and sheet.TableCode= @TableCode
                  AND fact.ZetValues= @Zet
                  AND fact.Row= @Row
                  AND fact.Col= @Col                
               "
        ;               
        row = (row ?? "").Trim();
        col = (col ?? "").Trim();
        tableCode = (tableCode ?? "").Trim();
        //var sqlSelect = string.IsNullOrEmpty(zet) ? sqlSelectWithoutZet : sqlSelectZet;
        var sqlSelect =  sqlSelectZet;



        var res = connectionLocal.QuerySingleOrDefault<(int count, double sum, int decimals)>(sqlSelect, new { documentId, tableCode, zet, row, col });        
        return res;
    }

    public TemplateSheetFact? SelectFactByRowColTableCode(int documentId, string tableCode, string zet, string row, string col)
    {
        //ZET: sometimes is not used :  when  the closed table has an empty zet and the open tables have an actual zet
        //for example rule 647 : 
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelectZet = @"
                SELECT  fact.* 
                FROM
                  TemplateSheetFact fact
                  JOIN TemplateSheetInstance sheet ON sheet.TemplateSheetId=fact.TemplateSheetId
                WHERE
                  1=1
                  AND sheet.InstanceId= @documentId
                  and sheet.TableCode= @TableCode
                  AND fact.ZetValues= @Zet
                  AND fact.Row= @Row
                  AND fact.Col= @Col
                  
                ORDER BY fact.Row, fact.Col;
                "
        ;
        var sqlSelectWithoutZet = @"
                SELECT  fact.* 
                FROM
                  TemplateSheetFact fact
                  JOIN TemplateSheetInstance sheet ON sheet.TemplateSheetId=fact.TemplateSheetId
                WHERE
                  1=1
                  AND sheet.InstanceId= @documentId
                  and sheet.TableCode= @TableCode
                  --AND fact.ZetValues= @Zet
                  AND fact.Row= @Row
                  AND fact.Col= @Col                  
                ORDER BY fact.Row, fact.Col;
                "
        ;
        row = (row ?? "").Trim();
        col=(col??"").Trim();
        tableCode = (tableCode ?? "").Trim();   
        var sqlSelect = string.IsNullOrEmpty(zet) ? sqlSelectWithoutZet : sqlSelectZet;
              
        

        var facts = connectionLocal.Query<TemplateSheetFact>(sqlSelect, new { documentId, tableCode, zet, row, col });
        
        if (facts.Count() > 1)
        {
            //multiple facts for each row/col because of either currency or multiple country f            
            var sum = facts.Sum(fact=>fact.NumericValue);
            var firstFact = facts.First();
            firstFact.NumericValue = sum;
            _logger.Error($"MULTIPLE!! Facts! documentId:{documentId}, tableCode:{tableCode}, row:{row}, col:{col}, Zet:{zet}");
            return firstFact;            
        }

        return facts.FirstOrDefault();
    }   
    public TemplateSheetFact? SelectFactByRowCol(int documentId, int sheetId,  string row, string col)
    {
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelect = @"
                SELECT sheet.SheetCode, fact.TextValue, fact.DataType, fact.NumericValue, fact.* 
                FROM
                  TemplateSheetFact fact
                  JOIN TemplateSheetInstance sheet ON sheet.TemplateSheetId=fact.TemplateSheetId
                WHERE
                  1=1
                  AND sheet.InstanceId= @documentId
                  and sheet.TemplateSheetId=@sheetId
                  AND fact.Row= @Row
                  AND fact.Col= @Col
                "
        ;

        var fact = connectionLocal.QuerySingleOrDefault<TemplateSheetFact>(sqlSelect, new { documentId, sheetId, row, col });
        return fact;
    }

    public List<TemplateSheetFact> SelectFactsByCol(int documentId, string tableCode, string zet,  string col)
    {
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);

        var sqlSelectWithZet = @"
                SELECT sheet.SheetCode, fact.TextValue, fact.DataType, fact.NumericValue, fact.* 
                FROM
                  TemplateSheetFact fact
                  JOIN TemplateSheetInstance sheet ON sheet.TemplateSheetId=fact.TemplateSheetId
                WHERE
                  1=1
                  AND sheet.InstanceId= @documentId
                  and sheet.TableCode= @TableCode
                  AND fact.ZetValues= @Zet                  
                  AND fact.Col= @Col
                  AND (fact.FieldOrigin is null or fact.FieldOrigin='')
                ORDER BY fact.Row, fact.Col;
                "
        ;
        

        var sqlSelectWithoutZet = @"
                SELECT sheet.SheetCode, fact.TextValue, fact.DataType, fact.NumericValue, fact.* 
                FROM
                  TemplateSheetFact fact
                  JOIN TemplateSheetInstance sheet ON sheet.TemplateSheetId=fact.TemplateSheetId
                WHERE
                  1=1
                  AND sheet.InstanceId= @documentId
                  and sheet.TableCode= @TableCode                  
                  AND fact.Col= @Col
                  AND (fact.FieldOrigin is null or fact.FieldOrigin='')
                ORDER BY fact.Row, fact.Col;
                "
        ;

        var sqlSelect = string.IsNullOrEmpty(zet) ? sqlSelectWithoutZet : sqlSelectWithZet;
        var facts = connectionLocal.Query<TemplateSheetFact>(sqlSelect, new { documentId, tableCode, zet, col }).ToList();
        return facts;
    }
   
    public List<TemplateSheetFact> SelectFactsInEveryRowForColumn(int documentId, string tableCode, string zet, string col)
    {
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelect = @"
                SELECT  fact.* 
                FROM
                  TemplateSheetFact fact
                  JOIN TemplateSheetInstance sheet ON sheet.TemplateSheetId=fact.TemplateSheetId
                WHERE
                  1=1
                  AND sheet.InstanceId= @documentId
                  and sheet.TableCode= @tableCode
                  --AND fact.ZetValues= @Zet                  
                  AND fact.Col= @col
                ORDER BY fact.Row, fact.Col;
                "
        ;
        var facts = connectionLocal.Query<TemplateSheetFact>(sqlSelect, new { documentId, tableCode, zet, col }).ToList();
        return facts;
    }

    public List<string> SelectDistinctRowsInSheet(int documentId, int sheetId)
    {
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelect = @"
                SELECT  distinct fact.Row
                FROM
                  TemplateSheetFact fact
                  JOIN TemplateSheetInstance sheet ON sheet.TemplateSheetId=fact.TemplateSheetId
                WHERE
                  1=1
                  AND sheet.InstanceId= @documentId
                  and sheet.TemplateSheetId=@sheetId    
                ORDER BY fact.Row
                ";
        var facts = connectionLocal.Query<string>(sqlSelect, new { documentId, sheetId }).ToList();
        return facts;
    }



    public List<TableAxisOrdinateInfoModel> SelectTableAxisOrdinateInfo(int tableId)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlSelect = @"
            SELECT ao.OrdinateID, ao.OrdinateCode as Col, ao.IsRowKey, mmm.AxisOrientation, mmm.IsOpenAxis, mmm.OptionalKey, mmm.AxisLabel, oc.DimensionMemberSignature as Signature
            FROM
              mAxisOrdinate ao
              JOIN mTableAxis ta ON ta.AxisID= ao.AxisID
              JOIN MAXIS mmm ON mmm.AxisID=ta.AxisID
              JOIN mOrdinateCategorisation oc ON oc.OrdinateID= ao.OrdinateID
            WHERE
              1=1
              AND ta.TableID= @TableID              
            ORDER BY ao.OrdinateCode;
        ";
        var ctx = connectionEiopa.Query<TableAxisOrdinateInfoModel>(sqlSelect, new { tableId }).ToList();
        return ctx;
    }

    public MDimensionModel? SelectDimensionByCode(string DomainCode, string DimensionCode)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlSelect = @"
            select 
	            dim.*
            from 
	            mDimension dim	
	            join mDomain dom  on dim.DomainID=dom.DomainID
            where 
                dom.DomainCode= @DomainCode
	            and dim.DimensionCode= @DimensionCode
        ";
        var res = connectionEiopa.QuerySingleOrDefault<MDimensionModel>(sqlSelect, new { DomainCode, DimensionCode });
        return res;
    }

    public MDimensionModel? SelectDimensionByCode(string dimensionCode)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlSelect = @"
            select 
	            dim.*
            from 
	            mDimension dim	
	        where 
                dim.DimensionCode= @DimensionCode
        ";
        var res = connectionEiopa.QuerySingleOrDefault<MDimensionModel>(sqlSelect, new { dimensionCode });
        return res;
    }



    public List<VValidationRuleExpressions> SelectValidationExpressionsWithTablesForModule(int ModuleId)
    {
        //in  vValidationRuleTables you may find validationId (correspond to expression) without tables        

        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);

        

        var sqlSelect = @"
            with validationDis as (
	            select distinct ValidationID,vrt.Severity from  vValidationRuleTables vrt where ModuleID=@ModuleID and vrt.TableID is not null
            ) 
            select 
	            severity,vre.*
            from  vValidationRuleExpressions vre 
            join validationDis vds on vds.ValidationID= vre.ValidationID
            where vre.IsEnabled=1

";

        var ctx = connectionEiopa.Query<VValidationRuleExpressions>(sqlSelect, new { ModuleId }).ToList();
        return ctx;
    }


    public  int CreateErrorRule(ERROR_Rule errorRule)
    {
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
                

        //var dataRecord = record.Adapt<RecordDataModel>();
        //dataRecord.IsActive = true;
        try
        {
            int res = (int)connectionLocal.Insert<ERROR_Rule>(errorRule);
            return res ;

        }
        catch (Exception e)
        {
            Console.Write(e.Message);
            return 0;
        }
    }

    
    public List<ERROR_Rule> SelectErrorRules(int errorDocumentId, ErrorRuleTypes errorType )
    {
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);
        var sqlSelect = @"select * from ERROR_Rule er where er.ErrorDocumentId = @ErrorDocumentId and er.IsError = @IsError and er.isWarning=@isWarning";
        var isError= errorType == ErrorRuleTypes.Errors;
        var isWarning = errorType == ErrorRuleTypes.Warnings;
        var errors = connectionLocal.Query<ERROR_Rule>(sqlSelect, new { errorDocumentId, isError,isWarning }).ToList();
        return errors;

    }


    public int CreateErrorDocument(ErrorDocumentModel errorDocument)
    {        
        using var connectionLocal = new SqlConnection(_parameterData.SystemConnectionString);

        var sqlDelete = @"delete from ERROR_Document where ErrorDocumentId = @ErrorDocumentId";
        connectionLocal.Execute(sqlDelete, new {  errorDocument.ErrorDocumentId });
        try
        {
            int res = (int)connectionLocal.Insert<ErrorDocumentModel>(errorDocument);
            return res;

        }
        catch (Exception e)
        {
            Console.Write(e.Message);
            return 0;
        }
        
    }


    public MTemplateOrTable? GetTableOrTemplate(string tableCode)
    {
        using var connectionEiopa = new SqlConnection(_parameterData.EiopaConnectionString);
        var sqlTemplate = @"
				SELECT * 
				FROM mTemplateOrTable tt
				WHERE 
				  1=1				  
				  AND tt.TemplateOrTableCode = @tableCode
                    AND TemplateOrTableType = 'BusinessTable' 
				";
        var template = connectionEiopa.QueryFirstOrDefault<MTemplateOrTable>(sqlTemplate, new { tableCode });
        return template;

    }
}

