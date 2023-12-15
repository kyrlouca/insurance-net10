using System.Xml.Linq;

namespace XbrlReader
{
	public interface IFactsCreator
	{
		XElement RootNode { get; }

		(int,List<string>) CreateLooseFacts();
		public (bool success, string message) HandleExistingDocuments();

    }
}