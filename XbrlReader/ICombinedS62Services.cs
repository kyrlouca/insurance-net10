
namespace XbrlReader
{
    public interface ICombinedS62Services
    {
        Task<int> CreateCombinedSheet(int documentId);
        int FindDocument();
        int K_UpdateDocumentForeignKeys(int documentId);
    }
}