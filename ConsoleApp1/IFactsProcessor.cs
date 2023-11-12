using Shared.DataModels;

namespace XbrlReader
{
	public interface IFactsProcessor
	{
		string _ModuleCode { get; }
		int ApplicableQuarterxx { get; }
		int ApplicableYearxx { get; }
		string DefaultCurrency { get; set; }
		int FileNamexx { get; }
		int ModuleId { get; }
		List<MTable> ModuleTablesFiled { get; }
		int PensionFundIdxx { get; }
		DateTime StartTime { get; }
		int TestingTableId { get; set; }
		int UserIdxx { get; }

		List<TemplateSheetFact> FindFactsFromSignatureNewxx(int documentId, string cellSignature);
		List<TemplateSheetFact> FindFactsFromSignatureWild(int documentId, string cellSignature);
		List<TemplateSheetFact> FindMatchingFactsRegexOld(int documentId, string cellSignature);
		bool IsFactSignatureMatchingExpensive(string cellSignature, string factSignature);
		bool IsNewSignatureMatch(string cellSignature, string factSignature);
		int ProcessFactsAndAssignToSheets(List<string> filings, int documentId);
		void TestingCode();
		void UpdateCellsForeignRow(int documentId);
	}
}