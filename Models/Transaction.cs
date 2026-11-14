using NationalBank.Enums;

namespace NationalBank.Models;

public class Transaction
{
    public int TransactionNumber { get; set; }          // PK (identity)
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public TransactionType TransactionType { get; set; }
    public string? Note { get; set; }

    // M : 1 with Account
    public string AccountNumber { get; set; } = null!;
    public Account Account { get; set; } = null!;
}
