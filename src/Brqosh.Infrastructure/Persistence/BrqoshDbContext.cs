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
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Zatca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brqosh.Infrastructure.Persistence;

public class BrqoshDbContext : DbContext
{
    public BrqoshDbContext(DbContextOptions<BrqoshDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyGroup> CompanyGroups => Set<CompanyGroup>();
    public DbSet<IntercompanyAccountMap> IntercompanyAccountMaps => Set<IntercompanyAccountMap>();
    public DbSet<BillingPlan> BillingPlans => Set<BillingPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();
    public DbSet<ReceiptVoucher> ReceiptVouchers => Set<ReceiptVoucher>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<DebitNote> DebitNotes => Set<DebitNote>();
    public DbSet<PaymentVoucher> PaymentVouchers => Set<PaymentVoucher>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<ZatcaSettings> ZatcaSettings => Set<ZatcaSettings>();
    public DbSet<ZatcaEInvoiceDocument> ZatcaEInvoiceDocuments => Set<ZatcaEInvoiceDocument>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<Cheque> Cheques => Set<Cheque>();
    public DbSet<ReconciledBankEntry> ReconciledBankEntries => Set<ReconciledBankEntry>();
    public DbSet<BankStatement> BankStatements => Set<BankStatement>();
    public DbSet<ConstructionProject> ConstructionProjects => Set<ConstructionProject>();
    public DbSet<ConstructionContract> ConstructionContracts => Set<ConstructionContract>();
    public DbSet<ProgressBilling> ProgressBillings => Set<ProgressBilling>();
    public DbSet<SubcontractAgreement> SubcontractAgreements => Set<SubcontractAgreement>();
    public DbSet<BillOfMaterials> BillsOfMaterials => Set<BillOfMaterials>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<PosShift> PosShifts => Set<PosShift>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<InventoryCostLayer> InventoryCostLayers => Set<InventoryCostLayer>();
    public DbSet<StockBatch> StockBatches => Set<StockBatch>();
    public DbSet<InventoryCostConsumption> InventoryCostConsumptions => Set<InventoryCostConsumption>();
    public DbSet<PaymentAllocationLine> PaymentAllocationLines => Set<PaymentAllocationLine>();
    public DbSet<ProjectCostEntry> ProjectCostEntries => Set<ProjectCostEntry>();
    public DbSet<StoredDomainEvent> StoredDomainEvents => Set<StoredDomainEvent>();
    public DbSet<ReportSnapshot> ReportSnapshots => Set<ReportSnapshot>();
    public DbSet<DocumentVersionHistory> DocumentVersionHistories => Set<DocumentVersionHistory>();
    public DbSet<LoginAuditEntry> LoginAuditEntries => Set<LoginAuditEntry>();
    public DbSet<SalesQuotation> SalesQuotations => Set<SalesQuotation>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<CrmLead> CrmLeads => Set<CrmLead>();
    public DbSet<CrmOpportunity> CrmOpportunities => Set<CrmOpportunity>();
    public DbSet<CrmActivity> CrmActivities => Set<CrmActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("companies");
            entity.Property(c => c.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(c => c.NameEn).HasMaxLength(256);
            entity.Property(c => c.AddressAr).HasMaxLength(512);
            entity.Property(c => c.LogoFilePath).HasMaxLength(512);
            entity.Property(c => c.PrintFontFamily).HasMaxLength(64).HasDefaultValue("Tahoma");
            entity.Property(c => c.VatRegistrationNumber).HasMaxLength(15);

            // (CEEF-229 · Z-10) بيانات البائع النظامية — كائنٌ مملوك يُسطَّح في نفس الجدول
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

        modelBuilder.Entity<TenantSubscription>(entity =>
        {
            entity.ToTable("tenant_subscriptions");
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(s => s.TenantId).IsUnique();
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
                lines.HasIndex(l => l.AccountId);
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

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.Property(c => c.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(c => c.VatRegistrationNumber).HasMaxLength(15);
            entity.Property(c => c.Phone).HasMaxLength(32);
            entity.Property(c => c.Address).HasMaxLength(512);
            entity.Property(c => c.ModifiedBy).HasMaxLength(128);

            // (CEEF-229 · م1-١ب) هوية المشتري النظامية — العروضُ تطابق أعمدة BrqoshSchemaUpdater
            // حرفاً بحرف، فلا يختلف تركيبٌ جديد (EnsureCreated من النموذج) عن تركيبٍ مُرقَّع.
            // والنمطُ نفسه المُتَّبع في Company (Z-10): كائنٌ مملوك يُسطَّح في الجدول نفسه.
            entity.Property(c => c.RegistrationName).HasMaxLength(256);
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

            entity.HasIndex(c => new { c.CompanyId, c.NameAr });
            entity.HasIndex(c => new { c.CompanyId, c.VatRegistrationNumber })
                .IsUnique()
                .HasFilter("\"VatRegistrationNumber\" <> ''");
            entity.HasQueryFilter(c => c.DeletedAtUtc == null);
        });

        modelBuilder.Entity<SalesInvoice>(entity =>
        {
            entity.ToTable("sales_invoices");
            entity.Property(i => i.InvoiceNumber).HasMaxLength(32);
            entity.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(i => i.PosPaymentMethod).HasConversion<string>().HasMaxLength(16);
            entity.Property(i => i.BranchId);
            entity.HasIndex(i => new { i.CompanyId, i.InvoiceDate });
            entity.HasIndex(i => new { i.CompanyId, i.BranchId });
            entity.HasIndex(i => new { i.CompanyId, i.InvoiceNumber }).IsUnique();
            entity.HasIndex(i => i.CustomerId);
            entity.HasOne<Customer>().WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(i => i.PosShiftId);

            entity.Property(i => i.Subtotal).HasPrecision(18, 2);
            entity.Property(i => i.VatAmount).HasPrecision(18, 2);

            entity.OwnsMany(i => i.Lines, lines =>
            {
                lines.ToTable("sales_invoice_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.WithOwner().HasForeignKey("sales_invoice_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.ProductId);
                // (CEEF-207) فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
            });

            entity.Navigation(i => i.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CreditNote>(entity =>
        {
            entity.ToTable("credit_notes");
            entity.Property(n => n.NoteNumber).HasMaxLength(32);
            entity.Property(n => n.Reason).HasMaxLength(512).IsRequired();
            entity.Property(n => n.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(n => new { n.CompanyId, n.NoteNumber }).IsUnique();
            entity.HasIndex(n => n.OriginalInvoiceId);
            entity.HasIndex(n => n.CustomerId);

            entity.Property(n => n.Subtotal).HasPrecision(18, 2);
            entity.Property(n => n.VatAmount).HasPrecision(18, 2);

            entity.OwnsMany(n => n.Lines, lines =>
            {
                lines.ToTable("credit_note_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.WithOwner().HasForeignKey("credit_note_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.ProductId);
                // (CEEF-207) فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
            });

            entity.Navigation(n => n.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ReceiptVoucher>(entity =>
        {
            entity.ToTable("receipt_vouchers");
            entity.Property(v => v.VoucherNumber).HasMaxLength(32);
            entity.Property(v => v.Description).HasMaxLength(512).IsRequired();
            entity.Property(v => v.Reference).HasMaxLength(128);
            entity.Property(v => v.Amount).HasPrecision(18, 2);
            entity.Property(v => v.Method).HasConversion<string>().HasMaxLength(16);
            entity.Property(v => v.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(v => v.BranchId);
            entity.HasIndex(v => new { v.CompanyId, v.VoucherNumber }).IsUnique();
            entity.HasIndex(v => new { v.CompanyId, v.BranchId });
            entity.HasIndex(v => v.CustomerId);
            entity.HasIndex(v => v.BankAccountId);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.Property(s => s.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(s => s.VatRegistrationNumber).HasMaxLength(15);
            entity.Property(s => s.Phone).HasMaxLength(32);
            entity.Property(s => s.Address).HasMaxLength(512);
            entity.Property(s => s.ModifiedBy).HasMaxLength(128);
            entity.HasIndex(s => new { s.CompanyId, s.NameAr });
            entity.HasIndex(s => new { s.CompanyId, s.VatRegistrationNumber })
                .IsUnique()
                .HasFilter("\"VatRegistrationNumber\" <> ''");
            entity.HasQueryFilter(s => s.DeletedAtUtc == null);
        });

        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.ToTable("purchase_invoices");
            entity.Property(i => i.InvoiceNumber).HasMaxLength(32);
            entity.Property(i => i.SupplierInvoiceReference).HasMaxLength(64).IsRequired();
            entity.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(i => i.BranchId);
            entity.HasIndex(i => new { i.CompanyId, i.InvoiceNumber }).IsUnique();
            entity.HasIndex(i => new { i.CompanyId, i.BranchId });
            entity.HasIndex(i => new { i.CompanyId, i.SupplierId, i.SupplierInvoiceReference });
            entity.HasIndex(i => i.SupplierId);
            entity.HasOne<Supplier>().WithMany().HasForeignKey(i => i.SupplierId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(i => i.PurchaseOrderId);

            entity.Property(i => i.Subtotal).HasPrecision(18, 2);
            entity.Property(i => i.VatAmount).HasPrecision(18, 2);

            entity.OwnsMany(i => i.Lines, lines =>
            {
                lines.ToTable("purchase_invoice_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.WithOwner().HasForeignKey("purchase_invoice_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.ProductId);
                // (CEEF-207) فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
            });

            entity.Navigation(i => i.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PaymentVoucher>(entity =>
        {
            entity.ToTable("payment_vouchers");
            entity.Property(v => v.VoucherNumber).HasMaxLength(32);
            entity.Property(v => v.Description).HasMaxLength(512).IsRequired();
            entity.Property(v => v.Reference).HasMaxLength(128);
            entity.Property(v => v.Amount).HasPrecision(18, 2);
            entity.Property(v => v.Method).HasConversion<string>().HasMaxLength(16);
            entity.Property(v => v.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(v => v.BranchId);
            entity.HasIndex(v => new { v.CompanyId, v.VoucherNumber }).IsUnique();
            entity.HasIndex(v => new { v.CompanyId, v.BranchId });
            entity.HasIndex(v => v.SupplierId);
            entity.HasIndex(v => v.BankAccountId);
            entity.HasIndex(v => v.WorkflowInstanceId);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.Property(p => p.Code).HasMaxLength(32).IsRequired();
            entity.Property(p => p.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(p => p.Unit).HasMaxLength(32).IsRequired();
            entity.Property(p => p.Type).HasConversion<string>().HasMaxLength(16);
            // (CEEF-230 · ط1-٣) ‏نصّاً لا رقماً — كبقيّة تعدادات هذا المخطّط، وليُقرأ في
            // ‏القاعدة بلا جدولِ ترجمة. والافتراضُ `None` فلا يتغيّر صنفٌ قائم.
            entity.Property(p => p.Tracking).HasConversion<string>().HasMaxLength(16)
                .HasDefaultValue(InventoryTracking.None);
            entity.Property(p => p.SalePrice).HasPrecision(18, 2);
            entity.Property(p => p.WeightedAverageCost).HasPrecision(18, 4);
            entity.Property(p => p.Barcode).HasMaxLength(64);
            entity.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();
            entity.HasQueryFilter(p => p.DeletedAtUtc == null);
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("warehouses");
            entity.Property(w => w.Code).HasMaxLength(16).IsRequired();
            entity.Property(w => w.NameAr).HasMaxLength(256).IsRequired();
            entity.HasIndex(w => new { w.CompanyId, w.Code }).IsUnique();
            entity.HasQueryFilter(w => w.DeletedAtUtc == null);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements");
            entity.Property(m => m.MovementNumber).HasMaxLength(32);
            entity.Property(m => m.Type).HasConversion<string>().HasMaxLength(24);
            entity.Property(m => m.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(m => m.Description).HasMaxLength(512).IsRequired();
            entity.Property(m => m.SourceDocument).HasMaxLength(128);
            entity.Property(m => m.BranchId);
            entity.HasIndex(m => new { m.CompanyId, m.MovementNumber }).IsUnique();
            entity.HasIndex(m => new { m.CompanyId, m.MovementDate });
            entity.HasIndex(m => new { m.CompanyId, m.BranchId });

            entity.OwnsMany(m => m.Lines, lines =>
            {
                lines.ToTable("stock_movement_lines");
                lines.WithOwner().HasForeignKey("stock_movement_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitCost).HasPrecision(18, 4);
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.Property(l => l.ExpiryDate);
            });

            entity.Navigation(m => m.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // (CEEF-230 · ط1-٣) ‏رصيدُ الدفعة — تفصيلٌ لرصيد المخزون تربطه به الهويّةُ هـ-٩.
        modelBuilder.Entity<StockBatch>(entity =>
        {
            entity.ToTable("stock_batches");
            entity.Property(b => b.BatchNumber).HasMaxLength(64).IsRequired();
            entity.Property(b => b.Quantity).HasPrecision(18, 4);
            entity.Property(b => b.BranchId);

            // ‏**الدفعةُ هويّةٌ في مستودعها**: الرقمُ نفسُه في مستودعين صفّان لا صفّ،
            // ‏فالكمّيةُ تُحفظ حيث هي فعلاً. والتفرّدُ يمنع صفَّين للدفعة الواحدة —
            // ‏وهو ما يجعل هـ-٩ قابلةً للقياس أصلاً.
            entity.HasIndex(b => new { b.CompanyId, b.WarehouseId, b.ProductId, b.BatchNumber })
                .IsUnique();
            entity.HasIndex(b => new { b.CompanyId, b.ProductId, b.ExpiryDate });
            entity.HasIndex(b => new { b.CompanyId, b.BranchId });
        });

        modelBuilder.Entity<InventoryBalance>(entity =>
        {
            entity.ToTable("inventory_balances");
            entity.Property(b => b.QuantityOnHand).HasPrecision(18, 4);
            entity.HasIndex(b => new { b.CompanyId, b.WarehouseId, b.ProductId }).IsUnique();
            entity.HasIndex(b => b.ProductId);

            // رمز تزامن (ع-D1، CEEF-205): تحديث الرصيد قراءة-ثم-كتابة بقيمة مطلقة،
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

            // (CEEF-229 · Z-4) شبكة الأمان في القاعدة لسلسلة العدّاد التسلسليّ.
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
            entity.HasIndex(r => new { r.CompanyId, r.BankAccountId, r.JournalEntryId }).IsUnique();
        });

        modelBuilder.Entity<BankStatement>(entity =>
        {
            entity.ToTable("bank_statements");
            entity.Property(s => s.SourceFileName).HasMaxLength(256).IsRequired();
            entity.HasIndex(s => new { s.CompanyId, s.BankAccountId, s.ImportedAtUtc });

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

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("purchase_orders");
            entity.Property(o => o.OrderNumber).HasMaxLength(32);
            entity.Property(o => o.Notes).HasMaxLength(1024);
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(o => new { o.CompanyId, o.OrderNumber }).IsUnique();
            entity.HasIndex(o => o.SupplierId);
            entity.HasIndex(o => o.WorkflowInstanceId);

            entity.Property(o => o.Subtotal).HasPrecision(18, 2);
            entity.Property(o => o.VatAmount).HasPrecision(18, 2);

            entity.OwnsMany(o => o.Lines, lines =>
            {
                lines.ToTable("purchase_order_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.WithOwner().HasForeignKey("purchase_order_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.ProductId);
                // (CEEF-207) فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
            });

            entity.Navigation(o => o.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PurchaseRequest>(entity =>
        {
            entity.ToTable("purchase_requests");
            entity.Property(r => r.RequestNumber).HasMaxLength(32);
            entity.Property(r => r.Justification).HasMaxLength(1024);
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(r => new { r.CompanyId, r.RequestNumber }).IsUnique();
            entity.HasIndex(r => r.SupplierId);
            entity.HasIndex(r => r.WorkflowInstanceId);

            entity.OwnsMany(r => r.Lines, lines =>
            {
                lines.ToTable("purchase_request_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.WithOwner().HasForeignKey("purchase_request_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.ProductId);
            });

            entity.Navigation(r => r.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.ToTable("workflow_instances");
            entity.Property(w => w.DocumentKind).HasConversion<string>().HasMaxLength(32);
            entity.Property(w => w.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(w => w.SubmittedBy).HasMaxLength(128).IsRequired();
            entity.Property(w => w.RejectedBy).HasMaxLength(128);
            entity.Property(w => w.RejectionReason).HasMaxLength(1024);
            entity.HasIndex(w => new { w.CompanyId, w.DocumentKind, w.DocumentId }).IsUnique();

            entity.OwnsMany(w => w.Approvals, approvals =>
            {
                approvals.ToTable("workflow_approval_records");
                approvals.WithOwner().HasForeignKey("workflow_instance_id");
                approvals.Property<long>("id");
                approvals.HasKey("id");
                approvals.Property(a => a.Slot).HasConversion<string>().HasMaxLength(16);
                approvals.Property(a => a.ApprovedBy).HasMaxLength(128).IsRequired();
                approvals.Property(a => a.Comment).HasMaxLength(512);
            });

            entity.Navigation(w => w.Approvals)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<GoodsReceipt>(entity =>
        {
            entity.ToTable("goods_receipts");
            entity.Property(g => g.ReceiptNumber).HasMaxLength(32);
            entity.Property(g => g.Notes).HasMaxLength(1024);
            entity.Property(g => g.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(g => g.BranchId);
            entity.HasIndex(g => new { g.CompanyId, g.ReceiptNumber }).IsUnique();
            entity.HasIndex(g => new { g.CompanyId, g.BranchId });
            entity.HasIndex(g => g.PurchaseOrderId);
            entity.HasIndex(g => g.SupplierId);

            entity.OwnsMany(g => g.Lines, lines =>
            {
                lines.ToTable("goods_receipt_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.WithOwner().HasForeignKey("goods_receipt_id");
                lines.Property<long>("Id");
                lines.HasKey("Id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.OrderedQuantity).HasPrecision(18, 4);
                lines.Property(l => l.ReceivedQuantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitCost).HasPrecision(18, 4);
                lines.Property(l => l.ProductId);
            });
            entity.Navigation(g => g.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<DebitNote>(entity =>
        {
            entity.ToTable("debit_notes");
            entity.Property(n => n.NoteNumber).HasMaxLength(32);
            entity.Property(n => n.Reason).HasMaxLength(512).IsRequired();
            entity.Property(n => n.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(n => new { n.CompanyId, n.NoteNumber }).IsUnique();
            entity.HasIndex(n => n.OriginalPurchaseInvoiceId);
            entity.HasIndex(n => n.SupplierId);

            entity.Property(n => n.Subtotal).HasPrecision(18, 2);
            entity.Property(n => n.VatAmount).HasPrecision(18, 2);

            entity.OwnsMany(n => n.Lines, lines =>
            {
                lines.ToTable("debit_note_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.WithOwner().HasForeignKey("debit_note_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.ProductId);
                // (CEEF-207) فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
            });
            entity.Navigation(n => n.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ConstructionProject>(entity =>
        {
            entity.ToTable("construction_projects");
            entity.Property(p => p.Code).HasMaxLength(32).IsRequired();
            entity.Property(p => p.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(p => p.Location).HasMaxLength(512);
            entity.Property(p => p.ContractValue).HasPrecision(18, 2);
            entity.Property(p => p.EstimatedTotalCost).HasPrecision(18, 2);
            entity.Property(p => p.RetentionPercent).HasPrecision(5, 2);
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();
            entity.HasIndex(p => p.CustomerId);

            entity.OwnsMany(p => p.BoqItems, items =>
            {
                items.ToTable("construction_boq_items");
                items.WithOwner().HasForeignKey("construction_project_id");
                items.Property<long>("id");
                items.HasKey("id");
                items.Property(i => i.ItemNumber).HasMaxLength(32).IsRequired();
                items.Property(i => i.Description).HasMaxLength(512).IsRequired();
                items.Property(i => i.Unit).HasMaxLength(32).IsRequired();
                items.Property(i => i.Quantity).HasPrecision(18, 4);
                items.Property(i => i.UnitPrice).HasPrecision(18, 2);
            });

            entity.Navigation(p => p.BoqItems)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasQueryFilter(p => p.DeletedAtUtc == null);
        });

        modelBuilder.Entity<ConstructionContract>(entity =>
        {
            entity.ToTable("construction_contracts");
            entity.Property(c => c.ContractNumber).HasMaxLength(32).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(1024);
            entity.Property(c => c.ContractValue).HasPrecision(18, 2);
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(c => new { c.CompanyId, c.ContractNumber }).IsUnique();
            entity.HasIndex(c => c.ProjectId);
        });

        modelBuilder.Entity<ProgressBilling>(entity =>
        {
            entity.ToTable("progress_billings");
            entity.Property(b => b.BillingNumber).HasMaxLength(32);
            entity.Property(b => b.PeriodDescription).HasMaxLength(512).IsRequired();
            entity.Property(b => b.CurrentWorkAmount).HasPrecision(18, 2);
            entity.Property(b => b.RetentionPercent).HasPrecision(5, 2);
            entity.Property(b => b.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(b => new { b.CompanyId, b.BillingNumber }).IsUnique();
            entity.HasIndex(b => b.ProjectId);
            entity.HasIndex(b => b.CustomerId);
        });

        modelBuilder.Entity<SubcontractAgreement>(entity =>
        {
            entity.ToTable("subcontract_agreements");
            entity.Property(a => a.AgreementNumber).HasMaxLength(32).IsRequired();
            entity.Property(a => a.ScopeDescription).HasMaxLength(1024);
            entity.Property(a => a.ContractValue).HasPrecision(18, 2);
            entity.Property(a => a.RetentionPercent).HasPrecision(5, 2);
            entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(a => new { a.CompanyId, a.AgreementNumber }).IsUnique();
            entity.HasIndex(a => a.ProjectId);
            entity.HasIndex(a => a.SupplierId);
        });

        modelBuilder.Entity<BillOfMaterials>(entity =>
        {
            entity.ToTable("bills_of_materials");
            entity.Property(b => b.Code).HasMaxLength(32).IsRequired();
            entity.Property(b => b.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(b => b.OutputQuantity).HasPrecision(18, 4);
            entity.HasIndex(b => new { b.CompanyId, b.Code }).IsUnique();
            entity.HasIndex(b => b.FinishedProductId);

            entity.OwnsMany(b => b.Lines, lines =>
            {
                lines.ToTable("bom_lines");
                lines.WithOwner().HasForeignKey("bill_of_materials_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
            });
            entity.Navigation(b => b.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasQueryFilter(b => b.DeletedAtUtc == null);
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.ToTable("work_orders");
            entity.Property(w => w.OrderNumber).HasMaxLength(32);
            entity.Property(w => w.Notes).HasMaxLength(1024);
            entity.Property(w => w.PlannedQuantity).HasPrecision(18, 4);
            entity.Property(w => w.ProducedQuantity).HasPrecision(18, 4);
            entity.Property(w => w.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(w => new { w.CompanyId, w.OrderNumber });
            entity.HasIndex(w => w.BomId);
        });

        modelBuilder.Entity<PosShift>(entity =>
        {
            entity.ToTable("pos_shifts");
            entity.Property(s => s.CashierName).HasMaxLength(128).IsRequired();
            entity.Property(s => s.OpeningCash).HasPrecision(18, 2);
            entity.Property(s => s.ClosingCash).HasPrecision(18, 2);
            entity.Property(s => s.TotalSales).HasPrecision(18, 2);
            entity.Property(s => s.CashSales).HasPrecision(18, 2);
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(s => new { s.CompanyId, s.Status });
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("app_users");
            entity.Property(u => u.Username).HasMaxLength(64).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(128).IsRequired();
            entity.Property(u => u.DisplayNameAr).HasMaxLength(128).IsRequired();
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
            entity.Property(u => u.MfaSecret).HasMaxLength(64);
            entity.Property(u => u.MobilePhoneE164).HasMaxLength(16);
            entity.HasIndex(u => new { u.CompanyId, u.Username }).IsUnique();
            entity.HasQueryFilter(u => u.DeletedAtUtc == null);
        });

        modelBuilder.Entity<OtpChallenge>(entity =>
        {
            entity.ToTable("otp_challenges");
            entity.Property(c => c.Username).HasMaxLength(64).IsRequired();
            entity.Property(c => c.PhoneE164).HasMaxLength(16).IsRequired();
            entity.Property(c => c.CodeHash).HasMaxLength(64).IsRequired();
            entity.Property(c => c.Purpose).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(c => new { c.CompanyId, c.Username, c.Purpose, c.IsConsumed });
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("audit_log");
            entity.Property(a => a.Username).HasMaxLength(64).IsRequired();
            entity.Property(a => a.Action).HasMaxLength(64).IsRequired();
            entity.Property(a => a.EntityType).HasMaxLength(64).IsRequired();
            entity.Property(a => a.EntityKey).HasMaxLength(128).IsRequired();
            entity.Property(a => a.Details).HasMaxLength(2048);
            entity.Property(a => a.Reason).HasMaxLength(512);
            entity.HasIndex(a => new { a.CompanyId, a.OccurredAtUtc });

            // (O-19، CEEF-212) سلسلة تجزئة كاشفة للعبث.
            entity.Property(a => a.PreviousHash).HasMaxLength(64).IsRequired();
            entity.Property(a => a.EntryHash).HasMaxLength(64).IsRequired();
            // فريد: يمنع تفرّع السلسلة حتى لو أخفق القفل الاستشاري (SQLite مثلاً).
            entity.HasIndex(a => new { a.CompanyId, a.Sequence }).IsUnique();
        });

        modelBuilder.Entity<FixedAsset>(entity =>
        {
            entity.ToTable("fixed_assets");
            entity.Property(a => a.Code).HasMaxLength(32).IsRequired();
            entity.Property(a => a.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(a => a.AcquisitionCost).HasPrecision(18, 2);
            entity.Property(a => a.SalvageValue).HasPrecision(18, 2);
            entity.Property(a => a.AccumulatedDepreciation).HasPrecision(18, 2);
            entity.HasIndex(a => new { a.CompanyId, a.Code }).IsUnique();
        });

        modelBuilder.Entity<DocumentVersionHistory>(entity =>
        {
            entity.ToTable("document_version_history");
            entity.Property(v => v.DocumentType).HasMaxLength(64).IsRequired();
            entity.Property(v => v.DocumentKey).HasMaxLength(128).IsRequired();
            entity.Property(v => v.Action).HasMaxLength(32).IsRequired();
            entity.Property(v => v.Username).HasMaxLength(64).IsRequired();
            entity.HasIndex(v => new { v.CompanyId, v.DocumentType, v.DocumentKey, v.Version }).IsUnique();
        });

        modelBuilder.Entity<LoginAuditEntry>(entity =>
        {
            entity.ToTable("login_audit");
            entity.Property(l => l.Username).HasMaxLength(64).IsRequired();
            entity.Property(l => l.FailureReason).HasMaxLength(256);
            entity.HasIndex(l => new { l.CompanyId, l.CreatedAtUtc });
        });

        modelBuilder.Entity<SalesQuotation>(entity =>
        {
            entity.ToTable("sales_quotations");
            entity.Property(q => q.QuotationNumber).HasMaxLength(32);
            entity.Property(q => q.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(q => q.Subtotal).HasPrecision(18, 2);
            entity.Property(q => q.VatAmount).HasPrecision(18, 2);
            entity.HasIndex(q => new { q.CompanyId, q.QuotationNumber }).IsUnique();
            entity.OwnsMany(q => q.Lines, lines =>
            {
                lines.ToTable("sales_quotation_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.WithOwner().HasForeignKey("sales_quotation_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.ProductId);
            });
            entity.Navigation(q => q.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.ToTable("sales_orders");
            entity.Property(o => o.OrderNumber).HasMaxLength(32);
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(o => o.Subtotal).HasPrecision(18, 2);
            entity.Property(o => o.VatAmount).HasPrecision(18, 2);
            entity.HasIndex(o => new { o.CompanyId, o.OrderNumber }).IsUnique();
            entity.HasIndex(o => o.CustomerId);
            entity.HasIndex(o => o.SalesQuotationId);
            entity.OwnsMany(o => o.Lines, lines =>
            {
                lines.ToTable("sales_order_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.WithOwner().HasForeignKey("sales_order_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.ProductId);
                // (CEEF-207) فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
            });
            entity.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CrmLead>(entity =>
        {
            entity.ToTable("crm_leads");
            entity.Property(l => l.Name).HasMaxLength(256).IsRequired();
            entity.Property(l => l.Phone).HasMaxLength(32);
            entity.Property(l => l.Email).HasMaxLength(256);
            entity.Property(l => l.Source).HasMaxLength(64);
            entity.Property(l => l.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(l => new { l.CompanyId, l.Status });
        });

        modelBuilder.Entity<CrmOpportunity>(entity =>
        {
            entity.ToTable("crm_opportunities");
            entity.Property(o => o.Title).HasMaxLength(256).IsRequired();
            entity.Property(o => o.Stage).HasConversion<string>().HasMaxLength(16);
            entity.Property(o => o.Amount).HasPrecision(18, 2);
            entity.HasIndex(o => new { o.CompanyId, o.Stage });
        });

        modelBuilder.Entity<CrmActivity>(entity =>
        {
            entity.ToTable("crm_activities");
            entity.Property(a => a.Subject).HasMaxLength(256).IsRequired();
            entity.Property(a => a.Notes).HasMaxLength(2048);
            entity.Property(a => a.ActivityType).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(a => new { a.CompanyId, a.DueAtUtc });
        });

        modelBuilder.Entity<PayrollRun>(entity =>
        {
            entity.ToTable("payroll_runs");
            entity.Property(p => p.PeriodCode).HasMaxLength(7).IsRequired();
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(p => new { p.CompanyId, p.PeriodCode }).IsUnique();

            entity.OwnsMany(p => p.Lines, lines =>
            {
                lines.ToTable("payroll_lines");
                lines.WithOwner().HasForeignKey("payroll_run_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.EmployeeName).HasMaxLength(256).IsRequired();
                lines.Property(l => l.BasicSalary).HasPrecision(18, 2);
                lines.Property(l => l.HousingAllowance).HasPrecision(18, 2);
                lines.Property(l => l.TransportAllowance).HasPrecision(18, 2);
                lines.Property(l => l.GosiEmployee).HasPrecision(18, 2);
                lines.Property(l => l.GosiEmployer).HasPrecision(18, 2);
            });
            entity.Navigation(p => p.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<InventoryCostLayer>(entity =>
        {
            entity.ToTable("inventory_cost_layers");
            entity.Property(l => l.OriginalQuantity).HasPrecision(18, 4);
            entity.Property(l => l.RemainingQuantity).HasPrecision(18, 4);
            entity.Property(l => l.UnitCost).HasPrecision(18, 4);
            entity.Property(l => l.SourceDocument).HasMaxLength(128).IsRequired();
            entity.HasIndex(l => new { l.CompanyId, l.ProductId, l.WarehouseId, l.ReceivedDate });
            entity.HasIndex(l => new { l.CompanyId, l.ProductId, l.RemainingQuantity });
        });

        // (CEEF-229 · م0-٣) سجلُّ الاستهلاك — مفتاحُه المستند، فالترميمُ يبدأ منه.
        modelBuilder.Entity<InventoryCostConsumption>(entity =>
        {
            entity.ToTable("inventory_cost_consumptions");
            entity.Property(c => c.Quantity).HasPrecision(18, 4);
            entity.Property(c => c.UnitCost).HasPrecision(18, 4);
            entity.Property(c => c.SourceDocument).HasMaxLength(128).IsRequired();
            entity.HasIndex(c => new { c.CompanyId, c.SourceDocument });
            entity.HasIndex(c => c.CostLayerId);
        });

        modelBuilder.Entity<PaymentAllocationLine>(entity =>
        {
            entity.ToTable("payment_allocation_lines");
            entity.Property(a => a.DocumentNumber).HasMaxLength(64).IsRequired();
            entity.Property(a => a.Amount).HasPrecision(18, 2);
            entity.Property(a => a.DocumentType).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(a => new { a.CompanyId, a.DocumentType, a.DocumentId });
            entity.HasIndex(a => a.ReceiptVoucherId);
            entity.HasIndex(a => a.PaymentVoucherId);
        });

        modelBuilder.Entity<ProjectCostEntry>(entity =>
        {
            entity.ToTable("project_cost_entries");
            entity.Property(c => c.Description).HasMaxLength(512).IsRequired();
            entity.Property(c => c.SourceDocument).HasMaxLength(128);
            entity.Property(c => c.Amount).HasPrecision(18, 2);
            entity.HasIndex(c => new { c.CompanyId, c.ProjectId, c.CostDate });
        });

        modelBuilder.Entity<StoredDomainEvent>(entity =>
        {
            entity.ToTable("domain_events");
            entity.Property(e => e.EventType).HasMaxLength(128).IsRequired();
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.Property(e => e.LastDispatchError).HasMaxLength(512);
            entity.HasIndex(e => e.EventId).IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.OccurredAtUtc });
            // طابور Outbox: استعلام «غير المُرسَل» يجري بعد كل ترحيل (CEEF-204).
            entity.HasIndex(e => e.DispatchedAtUtc);
        });

        modelBuilder.Entity<ReportSnapshot>(entity =>
        {
            entity.ToTable("report_snapshots");
            entity.Property(e => e.ReportType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.Property(e => e.Hash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.CompanyId, e.ReportType, e.CapturedAtUtc });
        });

        MapBranchScope(modelBuilder.Entity<CreditNote>());
        MapBranchScope(modelBuilder.Entity<DebitNote>());
        MapBranchScope(modelBuilder.Entity<PurchaseOrder>());
        MapBranchScope(modelBuilder.Entity<PurchaseRequest>());
        MapBranchScope(modelBuilder.Entity<Cheque>());
        MapBranchScope(modelBuilder.Entity<BankAccount>());
        MapBranchScope(modelBuilder.Entity<ReconciledBankEntry>());
        MapBranchScope(modelBuilder.Entity<ZatcaEInvoiceDocument>());
        MapBranchScope(modelBuilder.Entity<Warehouse>());
        MapBranchScope(modelBuilder.Entity<InventoryBalance>());
        MapBranchScope(modelBuilder.Entity<InventoryCostLayer>());
        MapBranchScope(modelBuilder.Entity<StockBatch>());
        MapBranchScope(modelBuilder.Entity<FixedAsset>());
        MapBranchScope(modelBuilder.Entity<ProgressBilling>());
        MapBranchScope(modelBuilder.Entity<ConstructionProject>());
        MapBranchScope(modelBuilder.Entity<ConstructionContract>());
        MapBranchScope(modelBuilder.Entity<SubcontractAgreement>());
        MapBranchScope(modelBuilder.Entity<ProjectCostEntry>());
        MapBranchScope(modelBuilder.Entity<WorkOrder>());
        MapBranchScope(modelBuilder.Entity<BillOfMaterials>());
        MapBranchScope(modelBuilder.Entity<PayrollRun>());
        MapBranchScope(modelBuilder.Entity<PosShift>());
        MapBranchScope(modelBuilder.Entity<PaymentAllocationLine>());
    }

    private static void MapBranchScope<T>(EntityTypeBuilder<T> entity) where T : class, IBranchScoped
    {
        entity.Property(e => e.BranchId);
        entity.HasIndex(e => new { e.CompanyId, e.BranchId });
    }
}
