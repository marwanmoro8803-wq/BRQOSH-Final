using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
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
    }
}
