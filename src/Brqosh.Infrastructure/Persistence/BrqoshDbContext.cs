using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

public class BrqoshDbContext : DbContext
{
    public BrqoshDbContext(DbContextOptions<BrqoshDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("companies");
            entity.Property(c => c.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(c => c.VatRegistrationNumber).HasMaxLength(15);
            entity.Property(c => c.Profile).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.Property(a => a.Code).HasMaxLength(20).IsRequired();
            entity.Property(a => a.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(a => a.NameEn).HasMaxLength(256);
            entity.Property(a => a.Type).HasConversion<string>().HasMaxLength(16);
            // كود الحساب فريد داخل الشركة الواحدة
            entity.HasIndex(a => new { a.CompanyId, a.Code }).IsUnique();
            entity.HasIndex(a => a.ParentId);
        });

        modelBuilder.Entity<JournalEntry>(entity =>
        {
            entity.ToTable("journal_entries");
            entity.Property(e => e.Description).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.EntryNumber).HasMaxLength(32);
            entity.Property(e => e.SourceDocument).HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
            // فهرس الاستعلام الأساسي: قيود شركة خلال فترة — جاهز للـ partitioning لاحقاً
            entity.HasIndex(e => new { e.CompanyId, e.EntryDate });
            entity.HasIndex(e => new { e.CompanyId, e.EntryNumber }).IsUnique();

            entity.OwnsMany(e => e.Lines, lines =>
            {
                lines.ToTable("journal_entry_lines");
                lines.WithOwner().HasForeignKey("journal_entry_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.AccountCode).HasMaxLength(20).IsRequired();
                lines.Property(l => l.Description).HasMaxLength(512);
                lines.Property(l => l.Debit).HasPrecision(18, 2);
                lines.Property(l => l.Credit).HasPrecision(18, 2);
                lines.HasIndex(l => l.AccountId);
            });

            entity.Navigation(e => e.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<FiscalPeriod>(entity =>
        {
            entity.ToTable("fiscal_periods");
            entity.Property(p => p.Code).HasMaxLength(7).IsRequired();
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.Property(c => c.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(c => c.VatRegistrationNumber).HasMaxLength(15);
            entity.Property(c => c.Phone).HasMaxLength(32);
            entity.Property(c => c.Address).HasMaxLength(512);
            entity.HasIndex(c => new { c.CompanyId, c.NameAr });
        });

        modelBuilder.Entity<SalesInvoice>(entity =>
        {
            entity.ToTable("sales_invoices");
            entity.Property(i => i.InvoiceNumber).HasMaxLength(32);
            entity.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(i => new { i.CompanyId, i.InvoiceDate });
            entity.HasIndex(i => new { i.CompanyId, i.InvoiceNumber }).IsUnique();
            entity.HasIndex(i => i.CustomerId);

            entity.OwnsMany(i => i.Lines, lines =>
            {
                lines.ToTable("sales_invoice_lines");
                lines.WithOwner().HasForeignKey("sales_invoice_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
            });

            entity.Navigation(i => i.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
