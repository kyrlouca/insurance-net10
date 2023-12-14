namespace ExcelWriter
{
	public interface IExcelBookMerger
	{
		bool MergeTables(int documentId, string filename,string destFilename);
	}
}