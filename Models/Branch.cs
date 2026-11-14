namespace NationalBank.Models;

public class Branch
{
    public string Code { get; set; } = null!;          // PK  e.g. CAI-01
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;

    // 1 : 1 with Manager (FK lives here)
    public int ManagerId { get; set; }
    public Manager Manager { get; set; } = null!;

    // 1 : M with Account
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
