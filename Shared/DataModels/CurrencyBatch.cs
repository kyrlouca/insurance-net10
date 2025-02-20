using System.Collections.Generic;
namespace Shared.DataModels;
using Dapper.Contrib.Extensions;

[Table("CurrencyBatch")]

public class CurrencyBatch
{
    [Key]
    public int CurrencyBatchId { get; set; }
    public DateTime DateCreated { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }
    public int Wave { get; set; }
    public string Status { get; set; } = string.Empty;
}
