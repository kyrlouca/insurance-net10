using Shared.DataModels;

namespace Validator
{
    public interface IValidator
    {
        DocInstance _document { get; set; }

        int ValidateDocument();
    }
}