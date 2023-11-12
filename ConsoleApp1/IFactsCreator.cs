using System.Xml.Linq;

namespace XbrlReader
{
	public interface IFactsCreator
	{
		XElement RootNode { get; }

		int CreateLooseFacts();
	}
}