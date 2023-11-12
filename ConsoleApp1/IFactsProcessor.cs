using Shared.DataModels;

namespace XbrlReader
{
	public interface IFactsProcessor
	{
		string _ModuleCode { get; }
		int ApplicableQuarter { get; }
		int ApplicableYear { get; }
		string DefaultCurrency { get; set; }
		int FileName { get; }
		int ModuleId { get; }
		List<MTable> ModuleTablesFiled { get; }
		int PensionFundId { get; }
		DateTime StartTime { get; }
		int TestingTableId { get; set; }
		int UserId { get; }

		List<TemplateSheetFact> FindFactsFromSignatureNewxx(int documentId, string cellSignature);
		List<TemplateSheetFact> FindFactsFromSignatureWild(int documentId, string cellSignature);
		List<TemplateSheetFact> FindMatchingFactsRegexOld(int documentId, string cellSignature);
		bool IsFactSignatureMatchingExpensive(string cellSignature, string factSignature);
		bool IsNewSignatureMatch(string cellSignature, string factSignature);
		void ProcessFactsAndAssignToSheets(List<string> filings, int documentId);
		void TestingCode();
		void UpdateCellsForeignRow(int documentId);
	}
}