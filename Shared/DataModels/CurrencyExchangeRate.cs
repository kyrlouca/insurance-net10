namespace Shared.DataModels;
using Dapper.Contrib.Extensions;

[Table("CurrencyExchangeRate")]
public class CurrencyExchangeRate
{
    public int CurrencyBatchId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
}
