using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NationalBank.Data;
using NationalBank.Enums;
using NationalBank.Models;

Console.OutputEncoding = Encoding.UTF8;

// The connection is opened/used by this context and closed when it is disposed (on Exit)
using var db = new BankDbContext();

bool running = true;
while (running)
{
    Console.Clear();
    ShowMenu();
    string? choice = Console.ReadLine()?.Trim();

    try
    {
        switch (choice)
        {
            case "1": AddCustomer(db); break;
            case "2": OpenAccount(db); break;
            case "3": UpdateAccountStatus(db); break;
            case "4": RemoveAccountFromCustomer(db); break;
            case "5": ListCustomers(db); break;
            case "0":
                running = false;
                continue;
            default:
                Error("Invalid choice. Please enter a number from the menu.");
                break;
        }
    }
    catch (DbUpdateException ex)
    {
        Error($"Database error: {ex.InnerException?.Message ?? ex.Message}");
    }
    catch (Exception ex)
    {
        Error($"Unexpected error: {ex.Message}");
    }

    Pause();
}

db.Dispose(); // close the connection explicitly
Console.WriteLine("Goodbye!");

// =====================================================================
// Menu
// =====================================================================
static void ShowMenu()
{
    Console.WriteLine("========================================");
    Console.WriteLine("    National Bank — Management");
    Console.WriteLine("========================================");
    Console.WriteLine("  1) Add a new Customer");
    Console.WriteLine("  2) Open a new Account for a Customer");
    Console.WriteLine("  3) Update Account Status (Active / Closed)");
    Console.WriteLine("  4) Remove an Account from a Customer");
    Console.WriteLine("  5) List all Customers (with accounts)");
    Console.WriteLine("  0) Exit");
    Console.WriteLine("----------------------------------------");
    Console.Write("  Enter choice: ");
}

// =====================================================================
// 1) Add Customer
// =====================================================================
static void AddCustomer(BankDbContext db)
{
    Console.WriteLine("--- Add New Customer ---");

    string fullName = ReadRequired("Full Name      : ", 3, 100);
    string nationalId = ReadNationalId(db);
    DateTime dob = ReadDateOfBirth();
    string email = ReadEmail();
    string phone = ReadPhone();
    string address = ReadRequired("Address        : ", 5, 200);
    CustomerType type = ReadEnumChoice<CustomerType>("Customer Type:");

    var customer = new Customer
    {
        FullName = fullName,
        NationalId = nationalId,
        DateOfBirth = dob,
        Email = email,
        PhoneNumber = phone,
        Address = address,
        CustomerType = type
    };

    db.Customers.Add(customer);
    db.SaveChanges();

    Console.WriteLine();
    Success($"Customer created successfully. CustomerId = {customer.Id}");
}

// =====================================================================
// 2) Open Account
// =====================================================================
static void OpenAccount(BankDbContext db)
{
    Console.WriteLine("--- Open New Account ---");

    string accountNumber = ReadAccountNumber("Account Number : ");
    AccountType accountType = ReadEnumChoice<AccountType>("Account Type:");
    string branchCode = ReadRequired("Branch Code    : ", 1, 10);
    int customerId = ReadInt("Customer Id    : ");
    OwnershipType role = ReadEnumChoice<OwnershipType>("Ownership Role:");

    Info($"Validating branch '{branchCode}' and customer #{customerId}...");

    var branch = db.Branches.FirstOrDefault(b => b.Code == branchCode);
    if (branch is null)
    {
        Error($"Branch '{branchCode}' does not exist.");
        return;
    }

    var customer = db.Customers.Find(customerId);
    if (customer is null)
    {
        Error($"Customer #{customerId} does not exist.");
        return;
    }

    var account = db.Accounts
        .Include(a => a.CustomerAccounts)
        .FirstOrDefault(a => a.AccountNumber == accountNumber);

    if (account is null)
    {
        // Brand-new account: it must start with a Primary owner
        if (role != OwnershipType.Primary)
        {
            Error("A new account must be opened with a Primary owner first.");
            return;
        }

        account = new Account
        {
            AccountNumber = accountNumber,
            AccountType = accountType,
            BranchCode = branch.Code,
            CurrentBalance = 0m,
            OpeningDate = DateTime.Now
        };
        db.Accounts.Add(account);
    }
    else
    {
        // Existing account => joint account scenario (add another owner)
        if (account.BranchCode != branch.Code || account.AccountType != accountType)
        {
            Error($"Account '{accountNumber}' already exists as {account.AccountType} at branch '{account.BranchCode}'.");
            return;
        }
        if (account.CustomerAccounts.Any(ca => ca.CustomerId == customerId))
        {
            Error($"Customer #{customerId} is already linked to account '{accountNumber}'.");
            return;
        }
        if (role == OwnershipType.Primary &&
            account.CustomerAccounts.Any(ca => ca.OwnershipType == OwnershipType.Primary))
        {
            Error("This account already has a Primary owner. Use CoHolder instead.");
            return;
        }
    }

    db.CustomerAccounts.Add(new CustomerAccount
    {
        CustomerId = customerId,
        AccountNumber = account.AccountNumber,
        OwnershipType = role,
        OwnershipStartDate = DateTime.Now,
        AccountStatus = AccountStatus.Active
    });

    db.SaveChanges();
    Success($"Account '{account.AccountNumber}' created and linked to customer {customerId} as {role} owner.");
}

// =====================================================================
// 3) Update Account Status
// =====================================================================
static void UpdateAccountStatus(BankDbContext db)
{
    Console.WriteLine("--- Update Account Status ---");

    string accountNumber = ReadAccountNumber("Account Number : ");
    int customerId = ReadInt("Customer Id    : ");

    var link = db.CustomerAccounts
        .FirstOrDefault(ca => ca.AccountNumber == accountNumber && ca.CustomerId == customerId);

    if (link is null)
    {
        Error("No link found between this account and this customer.");
        return;
    }

    AccountStatus newStatus = ReadEnumChoice<AccountStatus>("New Status:");

    if (link.AccountStatus == newStatus)
    {
        Info($"Status is already {newStatus}. Nothing to change.");
        return;
    }

    link.AccountStatus = newStatus;
    db.SaveChanges();
    Success($"Status updated to {newStatus}.");
}

// =====================================================================
// 4) Remove Account from Customer
// =====================================================================
static void RemoveAccountFromCustomer(BankDbContext db)
{
    Console.WriteLine("--- Remove Account From Customer ---");

    string accountNumber = ReadAccountNumber("Account Number : ");
    int customerId = ReadInt("Customer Id    : ");

    var link = db.CustomerAccounts
        .FirstOrDefault(ca => ca.AccountNumber == accountNumber && ca.CustomerId == customerId);

    if (link is null)
    {
        Error("No link found between this account and this customer.");
        return;
    }

    string realAccountNumber = link.AccountNumber;

    db.CustomerAccounts.Remove(link);
    db.SaveChanges();

    Console.WriteLine();
    Success("  Ownership link deleted.");

    // If nobody owns the account anymore, remove the account too
    bool hasOtherOwners = db.CustomerAccounts.Any(ca => ca.AccountNumber == realAccountNumber);
    if (!hasOtherOwners)
    {
        bool hasTransactions = db.Transactions.Any(t => t.AccountNumber == realAccountNumber);
        if (hasTransactions)
        {
            Info($"      That was the last owner, but account '{realAccountNumber}' has transaction history, so it was kept.");
        }
        else
        {
            var account = db.Accounts.Find(realAccountNumber);
            if (account is not null)
            {
                db.Accounts.Remove(account);
                db.SaveChanges();
            }
            Info($"      That was the last owner — account '{realAccountNumber}' was also removed.");
        }
    }
}

// =====================================================================
// 5) List all Customers (Eager Loading)
// =====================================================================
static void ListCustomers(BankDbContext db)
{
    Console.WriteLine("--- All Customers ---");
    Console.WriteLine();

    var customers = db.Customers
        .AsNoTracking()
        .Include(c => c.CustomerAccounts)
            .ThenInclude(ca => ca.Account)
                .ThenInclude(a => a.Branch)
        .OrderBy(c => c.Id)
        .ToList();

    if (customers.Count == 0)
    {
        Console.WriteLine("  (no customers yet)");
        return;
    }

    foreach (var c in customers)
    {
        Console.WriteLine($"  #{c.Id} {c.FullName} ({c.CustomerType})");

        if (c.CustomerAccounts.Count == 0)
        {
            Console.WriteLine("      (no accounts)");
            continue;
        }

        foreach (var ca in c.CustomerAccounts)
        {
            var a = ca.Account;
            Console.WriteLine(
                $"      {a.AccountNumber,-10} {a.AccountType,-9} Balance: {a.CurrentBalance,12:N2}   " +
                $"{ca.OwnershipType,-8} {ca.AccountStatus,-7} @ {a.Branch.Name}");
        }
    }
}

// =====================================================================
// Input helpers (never crash on bad input)
// =====================================================================
static string ReadRequired(string prompt, int minLen, int maxLen)
{
    while (true)
    {
        Console.Write(prompt);
        string input = (Console.ReadLine() ?? "").Trim();

        if (input.Length < minLen || input.Length > maxLen)
        {
            Error($"Value is required and must be between {minLen} and {maxLen} characters.");
            continue;
        }
        return input;
    }
}

static int ReadInt(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        if (int.TryParse(Console.ReadLine()?.Trim(), out int value) && value > 0)
            return value;

        Error("Please enter a valid positive number.");
    }
}

static string ReadAccountNumber(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        string input = (Console.ReadLine() ?? "").Trim();

        if (Regex.IsMatch(input, @"^[A-Za-z0-9\-]{4,20}$"))
            return input;

        Error("Account number must be 4-20 characters (letters, digits, or '-').");
    }
}

static string ReadNationalId(BankDbContext db)
{
    while (true)
    {
        Console.Write("National ID    : ");
        string input = (Console.ReadLine() ?? "").Trim();

        if (!Regex.IsMatch(input, @"^\d{14}$"))
        {
            Error("National ID must be exactly 14 digits.");
            continue;
        }
        if (db.Customers.Any(c => c.NationalId == input))
        {
            Error("A customer with this National ID already exists.");
            continue;
        }
        return input;
    }
}

static DateTime ReadDateOfBirth()
{
    while (true)
    {
        Console.Write("Date of Birth  : (yyyy-MM-dd) ");
        string input = (Console.ReadLine() ?? "").Trim();

        if (!DateTime.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime dob))
        {
            Error("Invalid date. Use the format yyyy-MM-dd (e.g. 2000-01-25).");
            continue;
        }
        if (dob < new DateTime(1900, 1, 1) || dob > DateTime.Today.AddYears(-18))
        {
            Error("Customer must be at least 18 years old (and born after 1900).");
            continue;
        }
        return dob;
    }
}

static string ReadEmail()
{
    while (true)
    {
        Console.Write("Email          : ");
        string input = (Console.ReadLine() ?? "").Trim();

        try
        {
            var addr = new MailAddress(input);
            if (addr.Address == input && input.Length <= 150 && input.Contains('.'))
                return input;
        }
        catch (FormatException) { }

        Error("Invalid email address.");
    }
}

static string ReadPhone()
{
    while (true)
    {
        Console.Write("Phone          : ");
        string input = (Console.ReadLine() ?? "").Trim();

        if (Regex.IsMatch(input, @"^\+?\d{8,15}$"))
            return input;

        Error("Phone must contain 8-15 digits (optional leading +).");
    }
}

static T ReadEnumChoice<T>(string title) where T : struct, Enum
{
    var values = Enum.GetValues<T>();

    while (true)
    {
        Console.WriteLine(title);
        for (int i = 0; i < values.Length; i++)
            Console.WriteLine($"    {i + 1}) {values[i]}");
        Console.Write("  Choice: ");

        if (int.TryParse(Console.ReadLine()?.Trim(), out int choice) &&
            choice >= 1 && choice <= values.Length)
            return values[choice - 1];

        Error($"Please choose a number between 1 and {values.Length}.");
    }
}

// =====================================================================
// Console helpers
// =====================================================================
static void Success(string msg) => WriteColored(msg, ConsoleColor.Green);
static void Error(string msg) => WriteColored(msg, ConsoleColor.Red);
static void Info(string msg) => WriteColored(msg, ConsoleColor.Cyan);

static void WriteColored(string msg, ConsoleColor color)
{
    var old = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(msg);
    Console.ForegroundColor = old;
}

static void Pause()
{
    Console.WriteLine();
    Console.WriteLine("Press any key to return to the menu...");
    Console.ReadKey(true);
}
