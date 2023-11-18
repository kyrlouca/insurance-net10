namespace ExcelWriter
{
	public interface ITemplateMerger
	{
		bool MergeTemplates(int documentId, string filename,string destFilename);
	}
}