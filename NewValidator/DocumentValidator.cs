using Serilog;
using Shared.HostParameters;
using Shared.SharedHost;
using Shared.SQLFunctions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewValidator;

internal enum ValidStatus { Valid, Error, Waring };
public class DocumentValidator : IDocumentValidator
{
    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;


    public DocumentValidator(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
    }
    public int ValidateDocument()
    {
        return 1;
    }
}
