using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper.Contrib.Extensions;

namespace Shared.DataModels;
[Table("ERROR_Document")]
public class ErrorDocumentModel
{
    public int ErrorDocumentId { get; set; }
    public bool IsDocumentValid { get; set; }
    public string UserId { get; set; }
    [Computed]
    public DateTime TimeSubmitted { get; set; }
    public bool ErrorCounter { get; set; }
    public bool WarningCounter { get; set; }
    public int OrganisationId { get; set; }
}
