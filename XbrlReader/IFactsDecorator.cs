using Shared.DataModels;

namespace XbrlReader
{
    public interface IFactsDecorator
    {
        string DefaultCurrency { get; set; }
        List<MTable> ModuleTables { get; }

        int DecorateFactsAndAssignToSheets(int documentId, List<string> filings);        
        
    }
}