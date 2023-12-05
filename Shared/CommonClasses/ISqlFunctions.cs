using Shared.DataModels;

namespace Shared.CommonRoutines;
public enum ProgramCode { AG, DO, XB, VA, CX, RX }
public enum ProgramAction { DEL, INS, UPD }
public enum MessageType { ERROR, INFO, COMPLETE }

public enum MappingOrigin { Field, Column, Page , All };
public interface ISqlFunctions
{
	DocInstance? SelectDocInstance(int documentId);
	List<TemplateSheetInstance> SelectTempateSheets(int documentId);
	public DocInstance? SelectDocInstance(int fundId, string moduleCode, int ApplicableYear, int ApplicableQuarter);
    public List<TemplateSheetFactDim> SelectFactDims(int factId);
    MModule? SelectModuleByCode(string moduleCode);
	public void CreateTransactionLog(int docInstanceId, MessageType messageType, string message);
	public void UpdateDocumentStatus(int documentId, string status);
	public MMember? SelectDomainMember(string domainString);
	public MTable? SelectTable(string tableCode);
	public List<MAPPING> SelectMappings(int tableId,MappingOrigin mapping);
	public List<MAPPING> SelectRowColMappings(int tableId, string rowCol);

	public TemplateSheetInstance CreateTemplateSheet(int documentId, string sheetCode, string sheetTabName, MTable table);

}