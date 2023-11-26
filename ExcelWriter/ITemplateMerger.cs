namespace ExcelWriter
{
	public interface ITemplateMerger
	{
		bool MergeTables(int documentId, string filename,string destFilename);
	}
}