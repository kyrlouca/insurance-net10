namespace ExcelWriter
{
	public interface IExcelBookDataFiller
	{
		bool FillExcelBook(int documentId, string sourceFilename, string destFileName);
	}
}