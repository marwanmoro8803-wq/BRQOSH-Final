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
    private void ConfigureCoreAndLedger(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("companies");
            entity.Property(c => c.NameAr).HasMaxLength(256).IsRequired();
            // ‏(accounting-core#6) ‏الافتراضُ في الكائن وفي DDL وحدَهما — لا `HasDefaultValue`: معه يُسقط EF عند الإدراج صفراً يُضبط قبل الحفظ الأوّل
            // ‏(صفرُ CLR) فتضع القاعدةُ ١٠٬٠٠٠ مكانه صامتة.
            entity.Property(c => c.ManualJournalApprovalThreshold).HasPrecision(18, 2);
            entity.Property(c => c.NameEn).HasMaxLength(256);
            entity.Property(c => c.AddressAr).HasMaxLength(512);
            entity.Property(c => c.LogoFilePath).HasMaxLength(512);
            entity.Property(c => c.PrintFontFamily).HasMaxLength(64).HasDefaultValue("Tahoma");
            entity.Property(c => c.VatRegistrationNumber).HasMaxLength(15);

            // بيانات البائع النظامية — كائنٌ مملوك يُسطَّح في نفس الجدول
            // فلا جدول جديد ولا انضمام: العنوان جزءٌ من هوية المنشأة لا كيانٌ مستقلّ بحياته.
            entity.Property(c => c.RegistrationName).HasMaxLength(256);
            entity.Property(c => c.CommercialRegistrationNumber).HasMaxLength(64);
            // العروض هنا تطابق أعمدة BrqoshSchemaUpdater حرفاً بحرف، فلا يختلف تركيبٌ جديد
            // (EnsureCreated من النموذج) عن تركيبٍ مُرقَّع. والتحقق الدلاليّ — رمز دولة بحرفين
            // مثلاً — موضعه النطاق (NationalAddress.Create) لا عرضُ العمود.
            entity.OwnsOne(c => c.NationalAddress, address =>
            {
                address.Property(a => a.BuildingNumber).HasColumnName("AddrBuildingNumber").HasMaxLength(16);
                address.Property(a => a.AdditionalNumber).HasColumnName("AddrAdditionalNumber").HasMaxLength(16);
                address.Property(a => a.StreetName).HasColumnName("AddrStreetName").HasMaxLength(128);
                address.Property(a => a.DistrictName).HasColumnName("AddrDistrictName").HasMaxLength(128);
                address.Property(a => a.CityName).HasColumnName("AddrCityName").HasMaxLength(128);
                address.Property(a => a.PostalZone).HasColumnName("AddrPostalZone").HasMaxLength(16);
                address.Property(a => a.CountrySubentity).HasColumnName("AddrCountrySubentity").HasMaxLength(128);
                address.Property(a => a.CountryCode).HasColumnName("AddrCountryCode").HasMaxLength(16);
            });
            entity.Navigation(c => c.NationalAddress).IsRequired();
            entity.Property(c => c.Profile).HasConversion<string>().HasMaxLength(32);
            entity.Property(c => c.CostingMethod).HasConversion<string>().HasMaxLength(32);
            entity.Property(c => c.FiscalYearStartMonth).HasDefaultValue(1);
            entity.Property(c => c.AuditorName).HasMaxLength(Company.AuditorNameMaxLength);
            entity.Property(c => c.AddressEn).HasMaxLength(Company.AddressEnMaxLength);
            entity.Property(c => c.BaseCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(c => c.FunctionalCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(c => c.TenantId).IsRequired();
            entity.Property(c => c.OperationalMode).HasConversion<string>().HasMaxLength(16);
            entity.Property(c => c.CompanyGroupId);
            entity.HasIndex(c => c.TenantId).IsUnique();
            entity.HasIndex(c => c.CompanyGroupId);
        });

        modelBuilder.Entity<CompanyGroup>(entity =>
        {
            entity.ToTable("company_groups");
            entity.Property(g => g.Code).HasMaxLength(16).IsRequired();
            entity.Property(g => g.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(g => g.ReportingCurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(g => g.Code).IsUnique();
        });

        modelBuilder.Entity<IntercompanyAccountMap>(entity =>
        {
            entity.ToTable("intercompany_account_maps");
            entity.Property(m => m.AccountCode).HasMaxLength(16).IsRequired();
            entity.HasIndex(m => new { m.CompanyGroupId, m.AccountCode }).IsUnique();
        });

        modelBuilder.Entity<BillingPlan>(entity =>
        {
            entity.ToTable("billing_plans");
            entity.Property(p => p.Code).HasMaxLength(32).IsRequired();
            entity.Property(p => p.NameAr).HasMaxLength(256).IsRequired();
            entity.HasIndex(p => p.Code).IsUnique();
        });

        // ‏(Phase 7) ‏رموزُ التفعيل المستهلكة — الـnonce فريد فلا يُعاد رمز.
        modelBuilder.Entity<Brqosh.Domain.Licensing.LicenseActivation>(entity =>
        {
            entity.ToTable("license_activations");
            entity.Property(a => a.Nonce).HasMaxLength(64).IsRequired();
            entity.Property(a => a.DeviceHash).HasMaxLength(64).IsRequired();
            entity.Property(a => a.Edition).HasMaxLength(32).IsRequired();
            entity.Property(a => a.CodeSha256).HasMaxLength(64).IsRequired();
            entity.HasIndex(a => a.Nonce).IsUnique();
        });

        // ‏(Phase 7) اتفاقيةُ الترخيص — مربوطةٌ بالمنشأة وبسجلّ التفعيل؛ لا تُعدَّل ولا تُحذف (حارسٌ في القاعدة).
        modelBuilder.Entity<Brqosh.Domain.Licensing.LicenseAgreement>(entity =>
        {
            entity.ToTable("license_agreements");
            entity.Property(a => a.AgreementNumber).HasMaxLength(64).IsRequired();
            entity.HasIndex(a => a.AgreementNumber).IsUnique();
            entity.Property(a => a.Edition).HasMaxLength(32).IsRequired();
            entity.Property(a => a.FirstPartyName).HasMaxLength(256).IsRequired();
            entity.Property(a => a.FirstPartyVatNumber).HasMaxLength(15);
            entity.Property(a => a.FirstPartyCommercialRegistration).HasMaxLength(20);
            entity.Property(a => a.FirstPartyAddress).HasMaxLength(512);
            entity.Property(a => a.SecondPartyName).HasMaxLength(256).IsRequired();
            entity.Property(a => a.SecondPartyVatNumber).HasMaxLength(15).IsRequired();
            entity.Property(a => a.SecondPartyCommercialRegistration).HasMaxLength(20);
            entity.Property(a => a.SecondPartyAddress).HasMaxLength(512);
            entity.Property(a => a.BodyText).IsRequired();
            entity.Property(a => a.BodySha256).HasMaxLength(64).IsRequired();
            entity.Property(a => a.PdfPath).HasMaxLength(1024);
            entity.Property(a => a.CreatedBy).HasMaxLength(128).IsRequired();
            entity.HasOne<Company>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Brqosh.Domain.Licensing.LicenseActivation>().WithMany().HasForeignKey(a => a.LicenseActivationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(a => a.CompanyId);
        });

        modelBuilder.Entity<TenantSubscription>(entity =>
        {
            entity.ToTable("tenant_subscriptions");
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(s => s.TenantId).IsUnique();
        });

        // ‏(ح-١٣٤) الذممُ المفتوحةُ عند بدء التشغيل — مستندٌ يقرؤه الدفترُ المساعد، لا فاتورة.
        modelBuilder.Entity<OpeningBalanceItem>(entity =>
        {
            entity.ToTable("opening_balance_items");
            entity.Property(i => i.Party).HasConversion<string>().HasMaxLength(16);
            entity.Property(i => i.DocumentNumber).HasMaxLength(50).IsRequired();
            entity.Property(i => i.Amount).HasPrecision(18, 2);
            entity.HasIndex(i => new { i.CompanyId, i.Party, i.PartyId, i.DocumentNumber }).IsUnique();
            entity.HasIndex(i => i.JournalEntryId);
        });

        modelBuilder.Entity<ExchangeRate>(entity =>
        {
            entity.ToTable("exchange_rates");
            entity.Property(r => r.FromCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(r => r.ToCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(r => r.Rate).HasPrecision(18, 6);
            entity.Property(r => r.Source).HasMaxLength(64);
            entity.HasIndex(r => new { r.CompanyId, r.FromCurrencyCode, r.ToCurrencyCode, r.EffectiveDate });
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("branches");
            entity.Property(b => b.Code).HasMaxLength(16).IsRequired();
            entity.Property(b => b.NameAr).HasMaxLength(256).IsRequired();
            entity.HasIndex(b => new { b.CompanyId, b.Code }).IsUnique();
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
            entity.HasQueryFilter(a => a.DeletedAtUtc == null);
        });

        modelBuilder.Entity<JournalEntry>(entity =>
        {
            entity.ToTable("journal_entries");
            entity.Property(e => e.Description).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.EntryNumber).HasMaxLength(32);
            entity.Property(e => e.SourceDocument).HasMaxLength(128);
            entity.Property(e => e.IsIntercompany).HasDefaultValue(false);
            entity.Property(e => e.CounterpartyCompanyId);
            entity.Property(e => e.WorkflowInstanceId);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.BranchId);
            // ‏قيدٌ على مستوى المنشأة — **والافتراضُ `false`**، فلا
            // ‏يتغيّر حرفٌ في قيدٍ قائم.
            entity.Property(e => e.IsEntityLevel).HasDefaultValue(false);
            // فهرس الاستعلام الأساسي: قيود شركة خلال فترة — جاهز للـ partitioning لاحقاً
            entity.HasIndex(e => new { e.CompanyId, e.EntryDate });
            entity.HasIndex(e => new { e.CompanyId, e.BranchId });
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
                // ‏**مذكرةُ العملة — والمبلغان أعلاه بالعملة الوظيفية دائماً.**
                // ‏فالدفترُ النظاميُّ بالريال، والمذكرةُ تحفظ أصلَ المعاملة وسعرَ ترجمتها.
                // ‏**وكلُّها لاغيةٌ للسطر الريالي** — فلا يتغيّر حرفٌ في قيدٍ قائم.
                lines.Property(l => l.CurrencyCode).HasMaxLength(3);
                lines.Property(l => l.ExchangeRate).HasPrecision(18, 6);
                lines.Property(l => l.ForeignAmount).HasPrecision(18, 2);
                lines.HasIndex(l => l.AccountId);
                // ‏فهرسٌ لمركز التكلفة — **وبلا مفتاحٍ أجنبيّ عمداً**:
                // ‏المركزُ يُوقَف حذفاً ناعماً، والمفتاحُ إمّا يمنع إيقافَه أو يترك المرجعَ
                // ‏معلّقاً بلا معنى. فالتحقّقُ في زمن الترحيل هو الطبقةُ الصحيحة،
                // ‏والفهرسُ لأجل تقريرٍ يُجمّع به.
                lines.HasIndex(l => l.CostCenterId);
                lines.HasOne<Account>().WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Restrict);
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
    }
}
