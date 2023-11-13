using Shared.DataModels;

namespace Shared.CommonRoutines;
public enum ProgramCode { AG, DO, XB, VA, CX, RX }
public enum ProgramAction { DEL, INS, UPD }
public enum MessageType { ERROR, INFO, COMPLETE }

public interface ICommonRoutines
{
	DocInstance? GetDocInstance(int documentId);
	public DocInstance? GetDocInstance(int fundId, string moduleCode, int ApplicableYear, int ApplicableQuarter);
	MModule GetModuleByCodeNew(string moduleCode);
	public void CreateTransactionLog(int docInstanceId, MessageType messageType, string message);
	public void UpdateDocumentStatus(int documentId, string status);
	
}