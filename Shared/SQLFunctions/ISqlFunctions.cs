using Shared.DataModels;

namespace Shared.SQLFunctions;
public enum ProgramCode { AG, DO, XB, VA, CX, RX }
public enum ProgramAction { DEL, INS, UPD }
public enum MessageType { ERROR, INFO, COMPLETE }

public enum MappingOrigin { Field, ColumnGeneral, Page, All };
public interface ISqlFunctions
{
    public void CreateTransactionLog( MessageType messageType, string message);
    DocInstance? SelectDocInstance(int documentId);
    public IEnumerable<DocInstance> SelectDocInstances(int fundId, string moduleCode, int ApplicableYear, int ApplicableQuarter);

    List<TemplateSheetInstance> SelectTempateSheets(int documentId);
    public DocInstance? SelectDocInstance(int fundId, string moduleCode, int ApplicableYear, int ApplicableQuarter);
    public List<TemplateSheetFactDim> SelectFactDims(int factId);
    MModule? SelectModuleByCode(string moduleCode);
    
    public void UpdateDocumentStatus(int documentId, string status);
    public MMember? SelectMMember(string domainString);
    public MTable? SelectTable(string tableCode);
    public List<MAPPING> SelectMappings(int tableId, MappingOrigin mapping);
    public List<MAPPING> SelectRowColMappings(int tableId, string rowCol);

    public TemplateSheetInstance CreateTemplateSheet(int documentId, string sheetCode, string sheetCodeZet, string sheetTabName, MTable table);
    public TemplateSheetFact? CreateTemplateSheetFact(TemplateSheetFact fact);
    public ContextModel? CreateContext(ContextModel context);

    public ContextLine? CreateContextLine(ContextLine contextLine);
    public ContextModel? SelectContextBySignature(int documentId, string contextSignature);
    public ContextModel? SelectContext(int documentId, string contextXbrlId);
    public List<ContextLine> SelectContextLines(int contextId);
    public FundModel? SelectFund(int fundId);

    public List<MTableCell> SelectTableCells(int tableId);
    public List<TemplateSheetFact> SelectFactsBySignature(int documentId, string signature);
    public List<MTable> SelectTablesInModule280(int moduleId);
}