using Microsoft.Extensions.Logging;
using Shared.CommonRoutines;
using Shared.DataModels;
using Shared.HostParameters;
using Shared.SharedHost;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Validator;

public class Validator : IValidator
{
    private readonly IParameterHandler _parameterHandler;
    private ParameterData _parameterData = new();
    private readonly ILogger _logger;
    private readonly ISqlFunctions _SqlFunctions;

    private MModule _mModule;
    public DocInstance _document { get; set; }

    public Validator(IParameterHandler getParameters, ILogger logger, ISqlFunctions sqlFunctions)
    {
        _parameterHandler = getParameters;
        _parameterData = getParameters.GetParameterData();
        _logger = logger;
        _SqlFunctions = sqlFunctions;
    }
    public int ValidateDocument()
    {
        _logger.LogInformation("Validator");
        return 0;
    }
}
