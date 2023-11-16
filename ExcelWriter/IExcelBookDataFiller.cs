namespace ExcelWriter
{
	public interface IExcelBookDataFiller
	{
		bool PopulateExcelBook(int documentId, string filename);
	}
}