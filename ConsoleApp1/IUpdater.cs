using Shared.DataModels;

namespace ConsoleApp1
{
	internal interface IUpdater
	{
		DocInstance GetDocument(int documentId);
	}
}