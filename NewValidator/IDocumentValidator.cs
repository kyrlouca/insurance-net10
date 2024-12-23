namespace NewValidator
{
    public interface IDocumentValidator
    {
        int ValidateDocument();

        public int K_UpdateForeignKeysAllDocuments(int year);
        
        
    }
}