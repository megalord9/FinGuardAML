
using System.ComponentModel.DataAnnotations;

namespace FinGuardAML.Models;

public sealed class TransactionRequest
{
    [Required]
    public string ClientId { get; set; } = "";

    [Range(typeof(decimal), "0.01", "999999999999",
        ParseLimitsInInvariantCulture = true)]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression("(?i)^[A-Z]{3}$")]
    public string Currency { get; set; } = "RUB";

    [Required]
    [RegularExpression("(?i)^(TRANSFER|EXCHANGE|WITHDRAWAL)$")]
    public string TransactionType { get; set; } = "TRANSFER";

    [Required]
    public string SenderCountry { get; set; } = "RU";

    [Required]
    public string ReceiverCountry { get; set; } = "RU";

    // Имя физического лица или название компании
    [Required]
    public string CounterpartyName { get; set; } = "";

    // Тип контрагента для OpenSanctions
    [Required]
    [RegularExpression("(?i)^(PERSON|COMPANY)$")]
    public string CounterpartyType { get; set; } = "PERSON";

    [Range(0, 1000000)]
    public int OperationsLast30Days { get; set; }

    [Range(typeof(decimal), "0", "999999999999999",
        ParseLimitsInInvariantCulture = true)]
    public decimal TotalAmountLast30Days { get; set; }
}
