using Shared.DataModels;

namespace XbrlReader
{
    public interface IFactsDecorator
    {
        string DefaultCurrency { get; set; }
        List<MTable> ModuleTablesFiled { get; }

        int DecorateFactsAndAssignToSheets(int documentId, List<string> filings);        
        
    }
}