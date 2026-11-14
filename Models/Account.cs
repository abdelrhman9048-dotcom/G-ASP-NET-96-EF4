using NationalBank.Enums;

namespace NationalBank.Models;

public class Account
{
    public string AccountNumber { get; set; } = null!;  // PK
    public decimal CurrentBalance { get; set; }
    public AccountType AccountType { get; set; }
    public DateTime OpeningDate { get; set; }

    // M : 1 with Branch
    public string BranchCode { get; set; } = null!;
    public Branch Branch { get; set; } = null!;

    // M : M with Customer
    public ICollection<CustomerAccount> CustomerAccounts { get; set; } = new List<CustomerAccount>();

    // 1 : M with Transaction
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
