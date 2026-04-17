
namespace XbrlReader
{
    public interface ICombinedS62Services
    {
        int CreateCombinedSheetOnly(int documentId);
        int CreateCombinedFacts(int documentId,int sheetId);        
        int K_UpdateDocumentForeignKeys(int documentId);
    }
}