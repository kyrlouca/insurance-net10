using Shared.DataModels;

namespace Shared.CommonRoutines
{
	public interface ICommonRoutines
	{
		DocInstance GetDocInstance(int documentId);
		MModule GetModuleByCodeNew(string moduleCode);
	}
}