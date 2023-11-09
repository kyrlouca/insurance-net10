using Shared.DataModels;

namespace Shared.SharedHost
{
	public interface ISharedParameterHandler
	{
		ParameterData GetParameterData();
	}
}