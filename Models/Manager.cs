namespace NationalBank.Models;

public class Manager
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public DateTime HireDate { get; set; }

    // 1 : 1 with Branch
    public Branch? Branch { get; set; }
}
