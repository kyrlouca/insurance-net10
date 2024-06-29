using Shared.DataModels;
using static Shared.SQLFunctions.SqlFunctions;

namespace Shared.SQLFunctions;
public enum ProgramCode { AG, DO, XB, VA, CX, RX }
public enum ProgramAction { DEL, INS, UPD }
public enum MessageType { ERROR, INFO, COMPLETE }

public enum MappingOrigin { Field, ColumnGeneral, Page, All };
public interface ISqlFunctions
{
    public void CreateTransactionLog(MessageType messageType, string message);
    DocInstance? SelectDocInstance(int documentId);
    public void UpdateDocumentStatus(int documentId, string status);
    public IEnumerable<DocInstance> SelectDocInstances(int fundId, string moduleCode, int ApplicableYear, int ApplicableQuarter);
    public DocInstance? SelectDocInstance(int fundId, string moduleCode, int ApplicableYear, int ApplicableQuarter);
    //**********sheets
    public TemplateSheetInstance? SelectTemplateSheetByZetValue(int documentId, string tableCode, string ZDimVal);
    public TemplateSheetInstance? SelectTemplateSheetBySheetCodeZet(int documentId,string tableCode, string sheetCodeZet);
    public List<TemplateSheetInstance> SelectTemplateSheetByTableCodeAllZets(int documentId, string tableCode);

    List<TemplateSheetInstance> SelectTemplateSheets(int documentId);
    public List<TemplateSheetInstance> SelectTemplateSheetsByTableId(int documentId, int tableId);
    public void UpdateTemplateSheetName(int templateSheetId, string sheetTabName);
    //*********facts
    public TemplateSheetFact? SelectFact(int factId);
    public List<TemplateSheetFact> SelectFactsForSheetId(int sheetId);
    public List<TemplateSheetFact> SelectFactsByColAndTextValue(int documentId, string tableCode, string col, string textValue);
    public List<TemplateSheetFact> SelectFactsBySignature(int documentId, string signature);
    public List<TemplateSheetFact> SelectFactsByCol(int documentId, string tableCode, string zet, string col);
    public TemplateSheetFact? SelectFactByRowColTableCode(int documentId, string tableCode, string zet, string row, string col);    
    public TemplateSheetFact? SelectFactByRowCol(int documentId, int sheetId, string row, string col);
    public List<TemplateSheetFact> SelectFactsInEveryRowForColumn(int documentId, string tableCode, string zet, string col);

    public List<TemplateSheetFact> SelectFactsInEveryRowForColumn(int documentId, int sheetId, string col);

    public (int count, double sum, int decimals) GetSumofTableCode(int documentId, string tableCode, string zet, string row, string col);
    public List<string> SelectDistinctRowsInSheet(int documentId, int sheetId);


    public List<TemplateSheetFactDim> SelectFactDims(int factId);
    MModule? SelectModuleByCode(string moduleCode);

    public MMetric? SelectMMetric(string xbrlCode);
    public MMember? SelectMMember(string domainString);    

    public List<MMember> SelectMMembersFromHierarchy(int hierarchyId);
    public MMember? SelectDefaultMemberFromHierarchy(int hierarchyId);
    public List<MAPPING> SelectMappings(int tableId, MappingOrigin mapping);
    public List<MAPPING> SelectRowColMappings(int tableId, string rowCol);

    public TemplateSheetInstance CreateTemplateSheet(int documentId, string sheetCode, string sheetCodeZet, string sheetTabName, string zDimVal, MTable table);

    public int CreateTemplateSheetFact(TemplateSheetFact fact, bool isLooseFact);
    
    public ContextModel? CreateContext(ContextModel context);

    public ContextLine? CreateContextLine(ContextLine contextLine);
    public ContextModel? SelectContextBySignature(int documentId, string contextSignature);
    public ContextModel? SelectContext(int documentId, string contextXbrlId);
    public List<ContextLine> SelectContextLines(int contextId);
    public FundModel? SelectFund(int fundId);

    //
    public MTable? SelectTable(string tableCode);

    public bool IsOpenTable(int tableId);
    public List<MTable> SelectTablesInModule280(int moduleId);
    public List<MTable> SelectTablesForValidationRule(int validationRuleId);
    //

    public List<MTableCell> SelectTableCells(int tableId);

    public List<MTableKyrKeys> SelectTableKyrKeys(string tableCode);
    public MTableKyrKeys? SelectTableKyrKey(string tableCode);
    

    public List<TableAxisOrdinateInfoModel> SelectTableAxisOrdinateInfo(int tableId);
    public MDimensionModel? SelectDimensionByCode(string DomainCode, string DimensionCode);
    public MDimensionModel? SelectDimensionByCode(string dimensionCode);
    public List<VValidationRuleExpressions> SelectValidationExpressionsWithTablesForModule(int ModuleId);

    public int CreateErrorRule(ERROR_Rule errorRule);

    public enum ErrorRuleTypes { Errors, Warnings, Both };
    public List<ERROR_Rule> SelectErrorRules(int documentId, ErrorRuleTypes errorType);
    public int CreateErrorDocument( ErrorDocumentModel errorDocument);
    public MTemplateOrTable? GetTableOrTemplate(string tableCode);
}