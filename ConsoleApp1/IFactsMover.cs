using Shared.DataModels;

namespace XbrlReader
{
    public interface IFactsMover
    {
        string DefaultCurrency { get; set; }
        List<MTable> ModuleTablesFiled { get; }

        int DecorateFactsAndAssignToSheets(int documentId, List<string> filings);
        List<TemplateSheetFact> FindFactsFromSignatureNewxx(int documentId, string cellSignature);
        List<TemplateSheetFact> FindFactsFromSignatureWild(int documentId, string cellSignature);
                
        bool IsNewSignatureMatch(string cellSignature, string factSignature);        
        void UpdateCellsForeignRow(int documentId);
    }
}