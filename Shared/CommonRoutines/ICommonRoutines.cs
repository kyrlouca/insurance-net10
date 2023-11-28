using Shared.DataModels;

namespace Shared.CommonRoutines;
public enum ProgramCode { AG, DO, XB, VA, CX, RX }
public enum ProgramAction { DEL, INS, UPD }
public enum MessageType { ERROR, INFO, COMPLETE }

public interface ICommonRoutines
{
	DocInstance? SelectDocInstance(int documentId);
	List<TemplateSheetInstance> SelectTempateSheets(int documentId);
	public DocInstance? SelectDocInstance(int fundId, string moduleCode, int ApplicableYear, int ApplicableQuarter);
	MModule? SelectModuleByCode(string moduleCode);
	public void CreateTransactionLog(int docInstanceId, MessageType messageType, string message);
	public void UpdateDocumentStatus(int documentId, string status);
	public MMember? SelectDomainMember(string domainString);
	public MTable? SelectTable(string tableCode);


}