namespace ExcelWriter
{
	public interface IExcelBookWriter
	{		
		string CreateExcelBook(int documentId, string filename);
	}
}