using Brqosh.Domain.Crm;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Reporting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Treasury;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Workflow;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Logistics;
using Brqosh.Domain.RealEstate;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brqosh.Infrastructure.Persistence;

// ‏(Phase 7) جزءٌ من نموذج BrqoshDbContext — يُستدعى من OnModelCreating بترتيبه الأصليّ؛ نُقل كما هو.
public partial class BrqoshDbContext
{
    private void ConfigurePayrollTreasuryZatca(ModelBuilder modelBuilder)
    {
        // ‏سجلُّ الموظفين — وكان الاسمُ نصّاً حرّاً يُعاد كتابتُه شهرياً.
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("employees");
            entity.Property(e => e.Code).HasMaxLength(32).IsRequired();
            entity.Property(e => e.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(e => e.NationalId).HasMaxLength(10).IsRequired();
            entity.Property(e => e.JobTitle).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Nationality).HasConversion<string>().HasMaxLength(16);
            // ‏(ح-١٣٨) فئةُ الاشتراك — نصّيّةُ التخزين كأخواتها، و«قديم» افتراضاً للصفوف القائمة.
            entity.Property(e => e.GosiRegime).HasConversion<string>().HasMaxLength(16)
                .HasDefaultValue(GosiRegime.Legacy).HasSentinel(GosiRegime.Legacy);
            entity.Property(e => e.ProjectId);   // ‏(ح-١٤٦) المشروعُ الذي يعمل عليه — فارغُه الافتراضيّ
            entity.Property(e => e.Iban).HasMaxLength(24);
            entity.Property(e => e.BasicSalary).HasPrecision(18, 2);
            entity.Property(e => e.HousingAllowance).HasPrecision(18, 2);
            entity.Property(e => e.TransportAllowance).HasPrecision(18, 2);
            entity.Property(e => e.OtherAllowance).HasPrecision(18, 2);
            entity.Property(e => e.BranchId);

            entity.HasIndex(e => new { e.CompanyId, e.Code }).IsUnique();

            // ‏**والهويّةُ لا تتكرّر**: موظّفان برقمٍ واحدٍ يعنيان راتباً مزدوجاً لشخص،
            // ‏أو ملفَّ أجورٍ ترفضه الجهةُ المستقبِلة.
            entity.HasIndex(e => new { e.CompanyId, e.NationalId }).IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.IsActive });
            entity.HasIndex(e => new { e.CompanyId, e.BranchId });
        });

        // ‏نِسَبُ التأمينات — لكلّ منشأةٍ نسختُها، فالمُنظِّم يُعدّلها.
        modelBuilder.Entity<GosiPolicy>(entity =>
        {
            entity.ToTable("gosi_policies");
            entity.Property(g => g.SaudiEmployeePercent).HasPrecision(9, 4);
            entity.Property(g => g.SaudiEmployerPercent).HasPrecision(9, 4);
            entity.Property(g => g.NonSaudiEmployeePercent).HasPrecision(9, 4);
            entity.Property(g => g.NonSaudiEmployerPercent).HasPrecision(9, 4);
            entity.Property(g => g.ContributoryWageCap).HasPrecision(18, 2);
            entity.Property(g => g.ContributoryWageFloor).HasPrecision(18, 2);
            // ‏(ح-١٣٨) جدولُ النظام الجديد.
            entity.Property(g => g.NewLawEmployeePercent).HasPrecision(9, 4);
            entity.Property(g => g.NewLawEmployerPercent).HasPrecision(9, 4);
            entity.Property(g => g.NewLawAnnualStepPercent).HasPrecision(9, 4);
            entity.HasIndex(g => g.CompanyId).IsUnique();
        });

        // ‏الموازنة — حبّتُها شهريّةٌ بمفتاح الفترة المالية نفسِه.
        modelBuilder.Entity<Budget>(entity =>
        {
            entity.ToTable("budgets");
            entity.Property(b => b.Code).HasMaxLength(32).IsRequired();
            entity.Property(b => b.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(b => b.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(b => new { b.CompanyId, b.Code }).IsUnique();
            entity.HasIndex(b => new { b.CompanyId, b.Year });

            entity.OwnsMany(b => b.Lines, lines =>
            {
                lines.ToTable("budget_lines");
                lines.WithOwner().HasForeignKey("budget_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.AccountCode).HasMaxLength(20).IsRequired();
                lines.Property(l => l.PeriodCode).HasMaxLength(7).IsRequired();
                lines.Property(l => l.Amount).HasPrecision(18, 2);
                lines.HasIndex(l => new { l.PeriodCode, l.AccountCode });
            });

            entity.Navigation(b => b.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // ‏خططُ الفوترة الدورية وأثرُ تكراراتها.
        modelBuilder.Entity<RecurringInvoicePlan>(entity =>
        {
            entity.ToTable("recurring_invoice_plans");
            entity.Property(p => p.WarehouseId);   // ‏(ح-١١٤) مستودعُ الصرف — فارغُه الافتراضيّ
            entity.Property(p => p.Code).HasMaxLength(32).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(512).IsRequired();
            entity.Property(p => p.Frequency).HasConversion<string>().HasMaxLength(16);
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(p => p.BranchId);
            entity.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();
            entity.HasIndex(p => new { p.CompanyId, p.Status });
            entity.HasIndex(p => new { p.CompanyId, p.BranchId });

            entity.OwnsMany(p => p.Lines, lines =>
            {
                lines.ToTable("recurring_invoice_plan_lines");
                lines.WithOwner().HasForeignKey("recurring_invoice_plan_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
                // ‏(م-٥ · ن-٥١) **ولا سقفَ هنا وحدَه.** كان `HasMaxLength(32)` على هذا المالك
                // ‏دون الثمانية الآخرين، فصار العمودُ `character varying(32)` هنا و`text` هناك
                // ‏— **نوعُ CLR واحدٌ ونوعُ التخزين اثنان**، وهو الشقُّ الذي لا تراه مقارنةُ
                // ‏نوعِ CLR وحدَها. وأثرُه مقيسٌ صغير: رمزُ إعفاءٍ أطولُ من ٣٢ يُقبل على ثمانية
                // ‏مستنداتٍ ويُرفض بـ22001 هنا. **والتوسيعُ هو الاتّجاه الآمن** — فلا بيانات
                // ‏تُرفض بعده — أمّا فرضُ السقف على التسعة فثمانيةُ تضييقاتٍ لمكسبٍ أصغر،
                // ‏وتُترك بنداً مستقلّاً إن أُريد سقفٌ موحّد.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.Ignore(l => l.ExpiryDate); // (جاهزيّةُ الإطلاق · ح-١٠٨) ‏الانتهاءُ لفاتورة الشراء وحدَها
                lines.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                lines.Property(l => l.UnitCode).HasMaxLength(16);
            });

            entity.Navigation(p => p.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<RecurringInvoiceRun>(entity =>
        {
            entity.ToTable("recurring_invoice_runs");

            // ‏**هذا القيدُ هو حارسُ الازدواج الحقيقيّ** — لا فحصٌ في الذاكرة يسبقه تشغيلٌ
            // ‏آخر. وفوترةُ العميل مرّتين عن شهرٍ واحد عطلٌ يراه العميلُ قبلنا.
            entity.HasIndex(r => new { r.PlanId, r.OccurrenceDate }).IsUnique();
            entity.HasIndex(r => new { r.CompanyId, r.OccurrenceDate });
        });

        modelBuilder.Entity<InventoryBalance>(entity =>
        {
            entity.ToTable("inventory_balances");
            entity.Property(b => b.QuantityOnHand).HasPrecision(18, 4);
            entity.HasIndex(b => new { b.CompanyId, b.WarehouseId, b.ProductId }).IsUnique();
            entity.HasIndex(b => b.ProductId);

            // رمز تزامن : تحديث الرصيد قراءة-ثم-كتابة بقيمة مطلقة،
            // فترحيلان متزامنان لنفس الصنف يقرآن نفس الرصيد ويكتبان نفس القيمة —
            // آخر كاتب يمحو أثر الأول (ضياع تحديث صامت يفسد المخزون وتكلفة المبيعات).
            // xmin عمود نظام في PostgreSQL يتغيّر مع كل تحديث للصف، فيكشف التعارض
            // ويحوّل الفساد الصامت إلى DbUpdateConcurrencyException صريح يُعاد معه.
            if (Database.IsNpgsql())
                entity.Property<uint>("xmin")
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
        });

        modelBuilder.Entity<ZatcaSettings>(entity =>
        {
            entity.ToTable("zatca_settings");
            entity.Property(s => s.ApiBaseUrl).HasMaxLength(512);
            entity.Property(s => s.CertificateFilePath).HasMaxLength(1024);
            entity.Property(s => s.PrivateKeyFilePath).HasMaxLength(1024);
            entity.Property(s => s.ApiToken).HasMaxLength(2048);
            entity.Property(s => s.ComplianceRequestId).HasMaxLength(64);
            entity.Property(s => s.CsidSecret).HasMaxLength(512);
            entity.Property(s => s.SigningMachineId).HasMaxLength(128);   // ‏(tax-zatca#16)
            entity.Property(s => s.Environment).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(s => s.CompanyId).IsUnique();
        });

        modelBuilder.Entity<ZatcaEInvoiceDocument>(entity =>
        {
            entity.ToTable("zatca_einvoice_documents");
            entity.Property(d => d.InvoiceNumber).HasMaxLength(32).IsRequired();
            entity.Property(d => d.PreviousInvoiceHash).HasMaxLength(128).IsRequired();
            entity.Property(d => d.InvoiceHash).HasMaxLength(128).IsRequired();
            entity.Property(d => d.QrTlvBase64).HasMaxLength(2048).IsRequired();
            entity.Property(d => d.XmlFilePath).HasMaxLength(1024).IsRequired();
            entity.Property(d => d.SignedXmlFilePath).HasMaxLength(1024);
            entity.Property(d => d.DocumentType).HasConversion<string>().HasMaxLength(16);
            entity.Property(d => d.TransmissionMode).HasConversion<string>().HasMaxLength(16);
            entity.Property(d => d.ComplianceStatus).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(d => new { d.CompanyId, d.DocumentType, d.SourceDocumentId }).IsUnique();
            entity.HasIndex(d => new { d.CompanyId, d.InvoiceNumber });

            // شبكة الأمان في القاعدة لسلسلة العدّاد التسلسليّ.
            //
            // القفل الاستشاريّ يمنع السباق على PostgreSQL، والفهرس يمنعه على أيّ مسار آخر —
            // كتابةٌ من أداة، أو مسارٌ ينسى القفل مستقبلاً. سلسلة سجلّ التدقيق أخذت الاثنين
            // منذ البداية؛ سلسلة ZATCA لم تأخذ أياً منهما، وتفرّعُها لا يُصلَح بأثر رجعيّ.
            entity.HasIndex(d => new { d.CompanyId, d.Icv }).IsUnique();
        });

        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.ToTable("bank_accounts");
            entity.Property(b => b.Code).HasMaxLength(16).IsRequired();
            entity.Property(b => b.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(b => b.BankName).HasMaxLength(256).IsRequired();
            entity.Property(b => b.AccountNumber).HasMaxLength(64);
            entity.Property(b => b.Iban).HasMaxLength(34);
            entity.Property(b => b.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(b => new { b.CompanyId, b.Code }).IsUnique();
            entity.HasIndex(b => b.GlAccountId);
            entity.HasQueryFilter(b => b.DeletedAtUtc == null);
        });

        modelBuilder.Entity<CostCenter>(entity =>
        {
            entity.ToTable("cost_centers");
            entity.Property(c => c.Code).HasMaxLength(16).IsRequired();
            entity.Property(c => c.NameAr).HasMaxLength(256).IsRequired();
            entity.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique();
            entity.HasQueryFilter(c => c.DeletedAtUtc == null);
        });

        modelBuilder.Entity<Cheque>(entity =>
        {
            entity.ToTable("cheques");
            entity.Property(c => c.ChequeNumber).HasMaxLength(32).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(512).IsRequired();
            entity.Property(c => c.Amount).HasPrecision(18, 2);
            entity.Property(c => c.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(c => new { c.CompanyId, c.ChequeNumber, c.Type });
            entity.HasIndex(c => c.BankAccountId);
            entity.HasIndex(c => c.CustomerId);
            entity.HasIndex(c => c.SupplierId);
        });

        modelBuilder.Entity<ReconciledBankEntry>(entity =>
        {
            entity.ToTable("reconciled_bank_entries");
            // ‏(ح-١٥٥) ‏**التسويةُ لكلّ سطر** — فالفريدُ يشمل السطرَ (والصفرُ للقيد كلِّه)؛ والاسمُ صريحٌ لأنّ اسمَ EF المشتقَّ
            // ‏يتجاوز ٦٣ حرفاً فيُقصّ في PostgreSQL إلى اسم الفهرس القديم نفسِه.
            entity.Property(r => r.JournalLineId).HasDefaultValue(0L);
            entity.HasIndex(r => new { r.CompanyId, r.BankAccountId, r.JournalEntryId, r.JournalLineId })
                .IsUnique()
                .HasDatabaseName("IX_reconciled_bank_entries_line");
        });

        modelBuilder.Entity<BankStatement>(entity =>
        {
            entity.ToTable("bank_statements");
            entity.Property(s => s.SourceFileName).HasMaxLength(256).IsRequired();
            entity.HasIndex(s => new { s.CompanyId, s.BankAccountId, s.ImportedAtUtc });
            // ‏(ح-١٥٤) ‏رصيدا الكشف كما يقولهما البنك — فارغان للكشوف القائمة.
            entity.Property(s => s.OpeningBalance).HasPrecision(18, 2);
            entity.Property(s => s.ClosingBalance).HasPrecision(18, 2);

            entity.OwnsMany(s => s.Lines, lines =>
            {
                lines.ToTable("bank_statement_lines");
                lines.WithOwner().HasForeignKey("bank_statement_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Reference).HasMaxLength(128);
                lines.Property(l => l.SignedAmount).HasPrecision(18, 2);
                lines.Property(l => l.MatchedJournalEntryId);
            });

            entity.Navigation(s => s.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
