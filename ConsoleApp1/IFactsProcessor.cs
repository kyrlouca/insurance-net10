using Shared.DataModels;

namespace XbrlReader
{
	public interface IFactsProcessor
	{		
		string DefaultCurrency { get; set; }		
		
		List<MTable> ModuleTablesFiled { get; }				
				

		List<TemplateSheetFact> FindFactsFromSignatureNewxx(int documentId, string cellSignature);
		List<TemplateSheetFact> FindFactsFromSignatureWild(int documentId, string cellSignature);
		List<TemplateSheetFact> FindMatchingFactsRegexOld(int documentId, string cellSignature);
		bool IsFactSignatureMatchingExpensive(string cellSignature, string factSignature);
		bool IsNewSignatureMatch(string cellSignature, string factSignature);
		int DecorateFactsAndAssignToSheets( int documentId, List<string> filings);
		void TestingCode();
		void UpdateCellsForeignRow(int documentId);
	}
}