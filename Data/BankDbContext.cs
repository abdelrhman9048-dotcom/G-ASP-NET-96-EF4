using Microsoft.EntityFrameworkCore;
using NationalBank.Models;

namespace NationalBank.Data;

public class BankDbContext : DbContext
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Manager> Managers => Set<Manager>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<CustomerAccount> CustomerAccounts => Set<CustomerAccount>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(
            "Server=.;Database=NationalBankDb;Trusted_Connection=True;TrustServerCertificate=True");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- Manager ----------
        modelBuilder.Entity<Manager>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.FullName).IsRequired().HasMaxLength(100);
            e.Property(m => m.Email).IsRequired().HasMaxLength(150);
            e.Property(m => m.PhoneNumber).IsRequired().HasMaxLength(20);
            e.Property(m => m.HireDate).HasColumnType("date");
        });

        // ---------- Branch ----------
        modelBuilder.Entity<Branch>(e =>
        {
            e.HasKey(b => b.Code);
            e.Property(b => b.Code).HasMaxLength(10);
            e.Property(b => b.Name).IsRequired().HasMaxLength(100);
            e.Property(b => b.Address).IsRequired().HasMaxLength(200);
            e.Property(b => b.PhoneNumber).IsRequired().HasMaxLength(20);

            // 1:1 Branch - Manager (FK on Branch => unique index is created automatically)
            e.HasOne(b => b.Manager)
             .WithOne(m => m.Branch)
             .HasForeignKey<Branch>(b => b.ManagerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Customer ----------
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.FullName).IsRequired().HasMaxLength(100);
            e.Property(c => c.NationalId).IsRequired().HasMaxLength(14);
            e.HasIndex(c => c.NationalId).IsUnique();
            e.Property(c => c.DateOfBirth).HasColumnType("date");
            e.Property(c => c.Email).IsRequired().HasMaxLength(150);
            e.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(20);
            e.Property(c => c.Address).IsRequired().HasMaxLength(200);
            e.Property(c => c.CustomerType).HasConversion<string>().HasMaxLength(20);
        });

        // ---------- Account ----------
        modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(a => a.AccountNumber);
            e.Property(a => a.AccountNumber).HasMaxLength(20);
            e.Property(a => a.CurrentBalance).HasPrecision(18, 2);
            e.Property(a => a.AccountType).HasConversion<string>().HasMaxLength(20);

            // 1 Branch : M Accounts
            e.HasOne(a => a.Branch)
             .WithMany(b => b.Accounts)
             .HasForeignKey(a => a.BranchCode)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- CustomerAccount (M:M "Have") ----------
        modelBuilder.Entity<CustomerAccount>(e =>
        {
            e.HasKey(ca => new { ca.CustomerId, ca.AccountNumber });

            e.HasOne(ca => ca.Customer)
             .WithMany(c => c.CustomerAccounts)
             .HasForeignKey(ca => ca.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ca => ca.Account)
             .WithMany(a => a.CustomerAccounts)
             .HasForeignKey(ca => ca.AccountNumber)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(ca => ca.OwnershipType).HasConversion<string>().HasMaxLength(20);
            e.Property(ca => ca.AccountStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(ca => ca.OwnershipStartDate).HasColumnType("date");
        });

        // ---------- Transaction ----------
        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasKey(t => t.TransactionNumber);
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.TransactionType).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.Note).HasMaxLength(300);

            // 1 Account : M Transactions (history must never disappear silently)
            e.HasOne(t => t.Account)
             .WithMany(a => a.Transactions)
             .HasForeignKey(t => t.AccountNumber)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Seeding (Manager first, then Branch) ----------
        modelBuilder.Entity<Manager>().HasData(
            new Manager { Id = 1, FullName = "Khaled Mansour", Email = "khaled.mansour@nationalbank.com", PhoneNumber = "01001112233", HireDate = new DateTime(2015, 3, 1) },
            new Manager { Id = 2, FullName = "Sara Hassan",    Email = "sara.hassan@nationalbank.com",    PhoneNumber = "01002223344", HireDate = new DateTime(2018, 7, 15) },
            new Manager { Id = 3, FullName = "Omar Fathy",     Email = "omar.fathy@nationalbank.com",     PhoneNumber = "01003334455", HireDate = new DateTime(2020, 1, 10) }
        );

        modelBuilder.Entity<Branch>().HasData(
            new Branch { Code = "CAI-01", Name = "Cairo Main Branch", Address = "10 Tahrir Square, Cairo",      PhoneNumber = "0225750001", ManagerId = 1 },
            new Branch { Code = "ALX-01", Name = "Alexandria Branch", Address = "25 Corniche Road, Alexandria", PhoneNumber = "0334870002", ManagerId = 2 },
            new Branch { Code = "GIZ-01", Name = "Giza Branch",       Address = "5 Pyramids Street, Giza",      PhoneNumber = "0233850003", ManagerId = 3 }
        );
    }
}
