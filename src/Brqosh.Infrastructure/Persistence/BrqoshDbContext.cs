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
    public DbSet<OpeningBalanceItem> OpeningBalanceItems => Set<OpeningBalanceItem>();
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
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyUnit> PropertyUnits => Set<PropertyUnit>();
    public DbSet<LeaseContract> LeaseContracts => Set<LeaseContract>();

    // (CEEF-230 · ط3-٢) ‏الصحّة.
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<InsurancePayer> InsurancePayers => Set<InsurancePayer>();
    public DbSet<InsurancePolicy> InsurancePolicies => Set<InsurancePolicy>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Brqosh.Domain.Payroll.EmployeeAdvance> EmployeeAdvances =>
        Set<Brqosh.Domain.Payroll.EmployeeAdvance>();
    // (CEEF-233 · ز-٢) ‏طلباتُ الإجازة والغياب — **والمستندُ الذي يُشتقّ منه الرصيدُ والخصم**.
    public DbSet<Brqosh.Domain.Payroll.LeaveRequest> LeaveRequests =>
        Set<Brqosh.Domain.Payroll.LeaveRequest>();
    public DbSet<InsuranceClaim> InsuranceClaims => Set<InsuranceClaim>();
    public DbSet<Domain.RealEstate.MaintenanceOrder> MaintenanceOrders =>
        Set<Domain.RealEstate.MaintenanceOrder>();
    public DbSet<TipPool> TipPools => Set<TipPool>();
    public DbSet<Domain.Logistics.DeliveryTrip> DeliveryTrips =>
        Set<Domain.Logistics.DeliveryTrip>();

    // (CEEF-230 · ط3-٣) ‏الضيافة.
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<GuestFolio> GuestFolios => Set<GuestFolio>();

    // (CEEF-230 · ط3-٤) ‏اللوجستيات.
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<GosiPolicy> GosiPolicies => Set<GosiPolicy>();
    public DbSet<RecurringInvoicePlan> RecurringInvoicePlans => Set<RecurringInvoicePlan>();
    public DbSet<RecurringInvoiceRun> RecurringInvoiceRuns => Set<RecurringInvoiceRun>();
    public DbSet<InventoryCostConsumption> InventoryCostConsumptions => Set<InventoryCostConsumption>();
    public DbSet<PaymentAllocationLine> PaymentAllocationLines => Set<PaymentAllocationLine>();
    public DbSet<ProjectCostEntry> ProjectCostEntries => Set<ProjectCostEntry>();
    public DbSet<StoredDomainEvent> StoredDomainEvents => Set<StoredDomainEvent>();
    public DbSet<ReportSnapshot> ReportSnapshots => Set<ReportSnapshot>();
    public DbSet<DocumentVersionHistory> DocumentVersionHistories => Set<DocumentVersionHistory>();
    public DbSet<LoginAuditEntry> LoginAuditEntries => Set<LoginAuditEntry>();
    public DbSet<SalesQuotation> SalesQuotations => Set<SalesQuotation>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderInvoiceLine> SalesOrderInvoiceLines => Set<SalesOrderInvoiceLine>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<Brqosh.Domain.Accounting.PostingRoleAccount> PostingRoleAccounts => Set<Brqosh.Domain.Accounting.PostingRoleAccount>();
    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();
    public DbSet<CrmLead> CrmLeads => Set<CrmLead>();
    public DbSet<CrmOpportunity> CrmOpportunities => Set<CrmOpportunity>();
    public DbSet<CrmActivity> CrmActivities => Set<CrmActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
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
            // (CEEF-231 · O-32) ‏قيدٌ على مستوى المنشأة — **والافتراضُ `false`**، فلا
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
                // (CEEF-231 · O-34) ‏**مذكرةُ العملة — والمبلغان أعلاه بالعملة الوظيفية دائماً.**
                // ‏فالدفترُ النظاميُّ بالريال، والمذكرةُ تحفظ أصلَ المعاملة وسعرَ ترجمتها.
                // ‏**وكلُّها لاغيةٌ للسطر الريالي** — فلا يتغيّر حرفٌ في قيدٍ قائم.
                lines.Property(l => l.CurrencyCode).HasMaxLength(3);
                lines.Property(l => l.ExchangeRate).HasPrecision(18, 6);
                lines.Property(l => l.ForeignAmount).HasPrecision(18, 2);
                lines.HasIndex(l => l.AccountId);
                // (CEEF-230 · ط1-٧) ‏فهرسٌ لمركز التكلفة — **وبلا مفتاحٍ أجنبيّ عمداً**:
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

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.Property(c => c.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(c => c.VatRegistrationNumber).HasMaxLength(15);
            entity.Property(c => c.Phone).HasMaxLength(32);
            entity.Property(c => c.Address).HasMaxLength(512);
            entity.Property(c => c.ModifiedBy).HasMaxLength(128);
            // ‏(جاهزيّةُ الإطلاق · ح-٩٥) حدُّ الائتمان — وفارغُه بلا حدّ.
            entity.Property(c => c.CreditLimit).HasPrecision(18, 2);

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
            // (CEEF-230 · ط1-٢) ‏خصمُ المستند — يُوزَّع على الأوعية الضريبية عند الحساب.
            entity.Property(i => i.DocumentDiscountAmount).HasPrecision(18, 2);

            // (CEEF-230 · ط1-٤) ‏عملةُ المستند ومبالغُها — والريالُ يبقى في `Subtotal`
            // ‏و`VatAmount` فلا يتغيّر شيءٌ ممّا بُني عليهما.
            entity.Property(i => i.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
            entity.Property(i => i.ExchangeRate).HasPrecision(18, 6).HasDefaultValue(1m);
            entity.Property(i => i.ForeignSubtotal).HasPrecision(18, 2);
            entity.Property(i => i.ForeignVatAmount).HasPrecision(18, 2);
            entity.Property(i => i.InvoiceNumber).HasMaxLength(32);
            entity.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(i => i.PosPaymentMethod).HasConversion<string>().HasMaxLength(16);
            entity.Property(i => i.PosCardAmount).HasPrecision(18, 2);
            entity.Property(i => i.BranchId);
            entity.Property(i => i.WarehouseId);   // ‏(ح-١١٠) مستودعُ الصرف — فارغُه الافتراضيّ
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
                lines.Ignore(l => l.ExpiryDate); // (جاهزيّةُ الإطلاق · ح-١٠٨) ‏الانتهاءُ لفاتورة الشراء وحدَها
                lines.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                lines.Property(l => l.UnitCode).HasMaxLength(16);
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
            // ‏(جاهزيّةُ الإطلاق · ح-٣٥) عملةُ الإشعار — نظيرُ أعمدة الفاتورة.
            entity.Property(n => n.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
            entity.Property(n => n.ExchangeRate).HasPrecision(18, 6);
            entity.Property(n => n.ForeignSubtotal).HasPrecision(18, 2);
            entity.Property(n => n.ForeignVatAmount).HasPrecision(18, 2);
            // ‏(جاهزيّةُ الإطلاق · ح-٦) خصمُ المستند على الإشعار — نظيرُ عمود الفاتورة.
            entity.Property(n => n.DocumentDiscountAmount).HasPrecision(18, 2);

            entity.OwnsMany(n => n.Lines, lines =>
            {
                lines.ToTable("credit_note_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.Ignore(l => l.ExpiryDate); // (جاهزيّةُ الإطلاق · ح-١٠٨) ‏الانتهاءُ لفاتورة الشراء وحدَها
                lines.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                lines.Property(l => l.UnitCode).HasMaxLength(16);
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
            // (CEEF-230 · ط1-٤) ‏عملةُ السند وسعرُها يوم التحصيل — ومن فرقِه عن
            // ‏سعر يوم الفاتورة يقع الربحُ أو الخسارةُ المحقَّقة.
            entity.Property(v => v.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
            entity.Property(v => v.ExchangeRate).HasPrecision(18, 6).HasDefaultValue(1m);
            entity.Property(v => v.ReceivableRelieved).HasPrecision(18, 2);

            // (CEEF-231 · ط1-٢ج) ‏خصمُ السداد المبكر — يُطفئ من الذمّة ما لم يدخل الصندوق.
            entity.Property(v => v.SettlementDiscount).HasPrecision(18, 2).HasDefaultValue(0m);
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
            // (CEEF-230 · ط4-٥) ‏عملةُ الفاتورة وسعرُها وأساسُها الأجنبيّ.
            entity.Property(i => i.CurrencyCode).HasMaxLength(3);
            entity.Property(i => i.ExchangeRate).HasPrecision(18, 6);
            entity.Property(i => i.ForeignSubtotal).HasPrecision(18, 2);
            entity.Property(i => i.ForeignVatAmount).HasPrecision(18, 2);

            entity.ToTable("purchase_invoices");
            entity.Property(i => i.WarehouseId);   // ‏(ح-١١١) مستودعُ الاستلام — فارغُه الافتراضيّ
            entity.Property(i => i.ProjectId);     // ‏(ح-١٤٤) المشروعُ المُحمَّلُ عليه — فارغُه الافتراضيّ
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
            // ‏(جاهزيّةُ الإطلاق · tax-zatca#12) ‏معاملةُ الضريبة — «Domestic» لكلّ فاتورةٍ قائمة — وضريبةُ الاحتساب العكسيّ والقيمةُ الجمركيّة.
            // ‏والافتراضُ في الكائن وفي DDL وحدَهما — لا `HasDefaultValue` (انظر accounting-core#6 أعلاه).
            entity.Property(i => i.VatTreatment).HasConversion<string>().HasMaxLength(24);
            entity.Property(i => i.ReverseChargeVatAmount).HasPrecision(18, 2);
            entity.Property(i => i.ImportCustomsValue).HasPrecision(18, 2);

            entity.OwnsMany(i => i.Lines, lines =>
            {
                lines.ToTable("purchase_invoice_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                // ‏(جاهزيّةُ الإطلاق · ح-١٠٨ · ‏`order-to-cash-procure-to-pay-inventory#7`) ‏**انتهاءُ دفعة الشراء المباشر** — لفاتورة الشراء وحدَها.
                lines.Property(l => l.ExpiryDate);
                lines.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                lines.Property(l => l.UnitCode).HasMaxLength(16);
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
            // (CEEF-230 · ط4-٥) ‏عملةُ السند وما حُرِّر من الذمّة بعملة الدفاتر.
            entity.Property(v => v.CurrencyCode).HasMaxLength(3);
            entity.Property(v => v.ExchangeRate).HasPrecision(18, 6);
            entity.Property(v => v.PayableRelieved).HasPrecision(18, 2);

            // (CEEF-231 · ط1-٢ج) ‏خصمُ السداد المكتسَب — يُطفئ من الذمّة ما لم يخرج من الصندوق.
            entity.Property(v => v.SettlementDiscount).HasPrecision(18, 2).HasDefaultValue(0m);

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
            // (CEEF-231 · ط5-٨) ‏**رمزُ تزامنٍ — وهو صنفُ عطلِ `ع-D1` بعينه في موضعٍ آخر.**
            //
            // ‏‏متوسّطُ التكلفة المرجّح يُقرأ ثمّ يُكتب في خمسة مسارات ترحيل. ‏**فترحيلان متزامنان يقرآن المتوسطَ نفسَه ويكتبان قيمتين**، والثاني يمحو ‏الأوّل — **فتُقيَّد تكلفةُ مبيعاتٍ بمتوسّطٍ لم يشهد شراءً وقع فعلاً**، ‏وينفصل الدفترُ المساعد عن الأستاذ فرقاً دائماً.
            //
            // ‏و`xmin` عمودٌ نظاميٌّ في PostgreSQL يتغيّر مع كلّ تحديثٍ للصف، **فيُحوّل
            // ‏الفسادَ الصامتَ إلى `DbUpdateConcurrencyException` صريح** — و`UnitOfWork`
            // ‏يردّه رسالةً مفهومةً بعد ارتدادٍ كامل. **ولا يُعاد تلقائياً**: السياقُ يحمل
            // ‏كياناتٍ بائتة، وإعادةُ ترحيلٍ بحالةٍ بائتةٍ أخطرُ من خطأٍ يُعيده المستخدم.
            if (Database.IsNpgsql())
                entity.Property<uint>("xmin")
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();

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
            // (CEEF-231 · ط5-٨) ‏**رمزُ تزامنٍ — وهو صنفُ عطلِ `ع-D1` بعينه في موضعٍ آخر.**
            //
            // ‏‏`ApplyDelta` يقرأ الكمّيةَ ثمّ يكتبها. **فبيعان متزامنان من دفعةٍ ‏واحدة يُنقصانها مرّةً لا مرّتين** — فيبقى في الدفاتر دواءٌ خرج، ‏**ويُصرف من دفعةٍ فرغت**.
            //
            // ‏و`xmin` عمودٌ نظاميٌّ في PostgreSQL يتغيّر مع كلّ تحديثٍ للصف، **فيُحوّل
            // ‏الفسادَ الصامتَ إلى `DbUpdateConcurrencyException` صريح** — و`UnitOfWork`
            // ‏يردّه رسالةً مفهومةً بعد ارتدادٍ كامل. **ولا يُعاد تلقائياً**: السياقُ يحمل
            // ‏كياناتٍ بائتة، وإعادةُ ترحيلٍ بحالةٍ بائتةٍ أخطرُ من خطأٍ يُعيده المستخدم.
            if (Database.IsNpgsql())
                entity.Property<uint>("xmin")
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();

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

        // (CEEF-230 · ط1-١) ‏وحداتُ القياس — قالبٌ مبذورٌ لكلّ منشأة **قابلٌ للتعديل**.
        // ‏فمعاملُ «العلبة» ليس ثابتاً كونياً: علبةُ هذا التاجر اثنتا عشرة وعلبةُ ذاك أربعٌ
        // ‏وعشرون. **وثابتٌ في الشيفرة يكذب على أحدهما**.
        modelBuilder.Entity<UnitOfMeasure>(entity =>
        {
            entity.ToTable("units_of_measure");
            entity.Property(u => u.Code).HasMaxLength(16).IsRequired();
            entity.Property(u => u.NameAr).HasMaxLength(64).IsRequired();
            entity.Property(u => u.UnCode).HasMaxLength(8).IsRequired();
            entity.Property(u => u.BaseCode).HasMaxLength(16).IsRequired();
            entity.Property(u => u.Factor).HasPrecision(18, 6);
            entity.HasIndex(u => new { u.CompanyId, u.Code }).IsUnique();
        });

        // ‏فئاتُ الأصناف وحساباتُها (الإيراد/المخزون/تكلفة المبيعات).
        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.ToTable("product_categories");
            entity.Property(c => c.Code).HasMaxLength(32).IsRequired();
            entity.Property(c => c.NameAr).HasMaxLength(128).IsRequired();
            entity.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique();
        });

        // (CEEF-230 · ط3-١) ‏العقاراتُ والوحداتُ وعقودُ الإيجار.
        modelBuilder.Entity<Property>(entity =>
        {
            entity.ToTable("properties");
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.City).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(512).IsRequired();
            entity.Property(x => x.DeedNumber).HasMaxLength(64);
            entity.Property(x => x.BranchId);
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.BranchId });
        });

        modelBuilder.Entity<PropertyUnit>(entity =>
        {
            entity.ToTable("property_units");
            entity.Property(x => x.UnitNumber).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.AreaSquareMeters).HasPrecision(18, 2);
            entity.Property(x => x.ListedAnnualRent).HasPrecision(18, 2);

            // ‏**ورقمُ الوحدة لا يتكرّر في عقارها**: «شقّة ٣» مرّتين في مبنىً واحدٍ
            // ‏يجعل العقدَ يُشير إلى إحداهما بلا تمييز.
            entity.HasIndex(x => new { x.PropertyId, x.UnitNumber }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.Status });
        });

        modelBuilder.Entity<LeaseContract>(entity =>
        {
            entity.ToTable("lease_contracts");
            entity.Property(x => x.ContractNumber).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.RentPerPeriod).HasPrecision(18, 2);
            entity.Property(x => x.SecurityDeposit).HasPrecision(18, 2);
            entity.Property(x => x.DepositForfeited).HasPrecision(18, 2);
            entity.Property(x => x.BranchId);
            entity.HasIndex(x => new { x.CompanyId, x.ContractNumber }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.PropertyUnitId, x.Status });
            entity.HasIndex(x => new { x.CompanyId, x.BranchId });
        });

        // (CEEF-230 · ط3-٢) ‏المرضى والوثائقُ والزيارات.
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("patients");
            entity.Property(p => p.MedicalRecordNumber).HasMaxLength(32).IsRequired();
            entity.Property(p => p.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(p => p.NationalId).HasMaxLength(32).IsRequired();
            entity.Property(p => p.Phone).HasMaxLength(32).IsRequired();
            entity.Property(p => p.Gender).HasConversion<string>().HasMaxLength(16);
            entity.Property(p => p.BranchId);

            // ‏**ملفّان لشخصٍ واحد يُفرّقان تاريخَه المرضيّ نصفين** — وهو خطرٌ على البدن
            // ‏قبل أن يكون خطأً في الدفاتر.
            entity.HasIndex(p => new { p.CompanyId, p.MedicalRecordNumber }).IsUnique();
            entity.HasIndex(p => new { p.CompanyId, p.CustomerId });
            entity.HasIndex(p => new { p.CompanyId, p.BranchId });
        });

        modelBuilder.Entity<InsurancePayer>(entity =>
        {
            entity.ToTable("insurance_payers");
            entity.Property(p => p.Code).HasMaxLength(32).IsRequired();
            entity.Property(p => p.NameAr).HasMaxLength(256).IsRequired();
            entity.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();
        });

        modelBuilder.Entity<InsurancePolicy>(entity =>
        {
            entity.ToTable("insurance_policies");
            entity.Property(p => p.PolicyNumber).HasMaxLength(64).IsRequired();
            entity.Property(p => p.MemberNumber).HasMaxLength(64).IsRequired();
            entity.Property(p => p.CoveragePercent).HasPrecision(9, 4);
            entity.Property(p => p.CopayFixed).HasPrecision(18, 2);
            entity.Property(p => p.AnnualLimit).HasPrecision(18, 2);
            entity.HasIndex(p => new { p.CompanyId, p.PatientId, p.IsActive });
            entity.HasIndex(p => new { p.CompanyId, p.PayerId });
        });

        modelBuilder.Entity<Visit>(entity =>
        {
            entity.ToTable("visits");
            entity.Property(v => v.VisitNumber).HasMaxLength(32).IsRequired();
            entity.Property(v => v.DoctorName).HasMaxLength(128).IsRequired();
            entity.Property(v => v.Diagnosis).HasMaxLength(512);
            entity.Property(v => v.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(v => v.BranchId);
            entity.HasIndex(v => new { v.CompanyId, v.VisitNumber }).IsUnique();
            entity.HasIndex(v => new { v.CompanyId, v.PatientId, v.VisitDate });

            // ‏**والسقفُ يُشتقّ من هذا الفهرس**: زياراتُ وثيقةٍ في سنتها.
            entity.HasIndex(v => new { v.CompanyId, v.PolicyId, v.VisitDate });
            entity.HasIndex(v => new { v.CompanyId, v.BranchId });

            entity.OwnsMany(v => v.Lines, lines =>
            {
                lines.ToTable("visit_lines");
                lines.WithOwner().HasForeignKey("visit_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
                lines.Property(l => l.TaxExemptionReasonCode).HasMaxLength(32);

                // (CEEF-231 · ط3-٢ج) ‏دفعةُ الدواء المصروف — الطولُ نفسُه في كلّ مستند.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
            });

            entity.Navigation(v => v.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // (CEEF-230 · ط2-٥) ‏سُلَفُ الموظفين — أصلٌ يُستردّ بالاستقطاع.
        modelBuilder.Entity<Brqosh.Domain.Payroll.EmployeeAdvance>(entity =>
        {
            entity.ToTable("employee_advances");
            entity.Property(a => a.AdvanceNumber).HasMaxLength(32).IsRequired();
            entity.Property(a => a.Reason).HasMaxLength(256);
            entity.Property(a => a.Amount).HasPrecision(18, 2);
            entity.Property(a => a.InstallmentAmount).HasPrecision(18, 2);
            entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(a => a.BranchId);
            entity.HasIndex(a => new { a.CompanyId, a.AdvanceNumber }).IsUnique();
            entity.HasIndex(a => new { a.CompanyId, a.EmployeeId, a.Status });
        });

        // (CEEF-233 · ز-٢) ‏طلباتُ الإجازة.
        modelBuilder.Entity<Brqosh.Domain.Payroll.LeaveRequest>(entity =>
        {
            entity.ToTable("leave_requests");
            entity.Property(r => r.RequestNumber).HasMaxLength(32).IsRequired();
            entity.Property(r => r.Reason).HasMaxLength(256);
            entity.Property(r => r.DecidedBy).HasMaxLength(64);
            entity.Property(r => r.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(r => r.BranchId);
            entity.HasIndex(r => new { r.CompanyId, r.RequestNumber }).IsUnique();
            entity.HasIndex(r => new { r.CompanyId, r.EmployeeId, r.Status });
        });

        // (CEEF-230 · ط3-٢ب) ‏دفعاتُ المطالبات وقراراتُ الشركة عليها.
        modelBuilder.Entity<InsuranceClaim>(entity =>
        {
            entity.ToTable("insurance_claims");
            entity.Property(c => c.ClaimNumber).HasMaxLength(64).IsRequired();
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(c => c.BranchId);
            entity.HasIndex(c => new { c.CompanyId, c.PayerId, c.ClaimNumber }).IsUnique();
            entity.HasIndex(c => new { c.CompanyId, c.Status });

            entity.OwnsMany(c => c.Lines, lines =>
            {
                lines.ToTable("insurance_claim_lines");
                lines.WithOwner().HasForeignKey("claim_id");
                lines.HasKey(l => l.Id);
                lines.Property(l => l.ClaimedAmount).HasPrecision(18, 2);
                lines.Property(l => l.ApprovedAmount).HasPrecision(18, 2);
                lines.Property(l => l.RejectedAmount).HasPrecision(18, 2);
                lines.Property(l => l.RejectionReason).HasMaxLength(512);
                lines.Property(l => l.Disposition).HasConversion<string>().HasMaxLength(16);

                // ‏**وفاتورةُ مطالبةٍ واحدةٌ في دفعةٍ واحدة — عبر الدفعات كلِّها.**
                // ‏فحارسُ النطاق يمنع التكرارَ داخل الدفعة، **ودفعتان تحملان الفاتورةَ
                // ‏نفسَها تُصدران إشعارين دائنين لدينٍ واحد** فيصير رصيدُ الشركة سالباً.
                lines.HasIndex(l => l.PayerInvoiceId).IsUnique();
                lines.HasIndex(l => l.VisitId);
            });

            entity.Navigation(c => c.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // (CEEF-230 · ط3-١ب) ‏أوامرُ الصيانة وسطورُ تكلفتها.
        modelBuilder.Entity<Domain.RealEstate.MaintenanceOrder>(entity =>
        {
            entity.ToTable("maintenance_orders");
            entity.Property(m => m.OrderNumber).HasMaxLength(64).IsRequired();
            entity.Property(m => m.Description).HasMaxLength(512).IsRequired();
            entity.Property(m => m.VendorName).HasMaxLength(256);
            entity.Property(m => m.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(m => m.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(m => m.TenantChargeAmount).HasPrecision(18, 2);
            entity.Property(m => m.BranchId);
            entity.HasIndex(m => new { m.CompanyId, m.OrderNumber }).IsUnique();
            entity.HasIndex(m => new { m.CompanyId, m.PropertyId, m.UnitId });
            entity.HasIndex(m => new { m.CompanyId, m.Status });

            entity.OwnsMany(m => m.Lines, lines =>
            {
                lines.ToTable("maintenance_cost_lines");
                lines.WithOwner().HasForeignKey("order_id");
                lines.HasKey(l => l.Id);
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Amount).HasPrecision(18, 2);
            });

            entity.Navigation(m => m.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // (CEEF-230 · ط3-٣د) ‏أوعيةُ الإكراميات وتحصيلُها وتوزيعُها.
        modelBuilder.Entity<TipPool>(entity =>
        {
            entity.ToTable("tip_pools");
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(t => t.BranchId);
            entity.HasIndex(t => new { t.CompanyId, t.PoolDate });
            entity.HasIndex(t => new { t.CompanyId, t.Status });

            entity.OwnsMany(t => t.Collections, lines =>
            {
                lines.ToTable("tip_collections");
                lines.WithOwner().HasForeignKey("pool_id");
                lines.HasKey(l => l.Id);
                lines.Property(l => l.Source).HasConversion<string>().HasMaxLength(8);
                lines.Property(l => l.Amount).HasPrecision(18, 2);
                lines.Property(l => l.Note).HasMaxLength(256);
            });

            entity.OwnsMany(t => t.Distributions, lines =>
            {
                lines.ToTable("tip_distributions");
                lines.WithOwner().HasForeignKey("pool_id");
                lines.HasKey(l => l.Id);
                lines.Property(l => l.Amount).HasPrecision(18, 2);

                // ‏**وحصّةُ الموظّف الواحد صفٌّ واحد** — والجمعُ في النطاق، والقيدُ هنا
                // ‏يمنع صفَّين يلتقيان في تقريرٍ فيُظهران نادلاً أخذ ضِعفَ ما أخذ.
                lines.HasIndex("pool_id", "EmployeeId").IsUnique();
            });

            entity.Navigation(t => t.Collections).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(t => t.Distributions).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // (CEEF-230 · ط3-٤ب) ‏رحلاتُ التوصيل وشحناتُها ومصاريفُها.
        modelBuilder.Entity<Domain.Logistics.DeliveryTrip>(entity =>
        {
            entity.ToTable("delivery_trips");
            entity.Property(t => t.TripNumber).HasMaxLength(64).IsRequired();
            entity.Property(t => t.RouteName).HasMaxLength(256).IsRequired();
            entity.Property(t => t.VehiclePlate).HasMaxLength(32).IsRequired();
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(t => t.DriverFloat).HasPrecision(18, 2);
            entity.Property(t => t.ReturnedCash).HasPrecision(18, 2);
            entity.Property(t => t.BranchId);
            entity.HasIndex(t => new { t.CompanyId, t.TripNumber }).IsUnique();

            // ‏**والمسارُ مفهرسٌ لأنّه مقامُ الربحيّة** — وهو ما يُجمَّع عليه.
            entity.HasIndex(t => new { t.CompanyId, t.RouteName, t.DepartureDate });

            entity.OwnsMany(t => t.Shipments, lines =>
            {
                lines.ToTable("trip_shipments");
                lines.WithOwner().HasForeignKey("trip_id");
                lines.HasKey(l => l.Id);

                // ‏**والشحنةُ الواحدة على رحلةٍ واحدة** — عبر الرحلات كلِّها. فحارسُ
                // ‏النطاق يمنع التكرارَ داخل الرحلة، **ورحلتان تحملان الشحنةَ نفسَها
                // ‏تحسبان أجرتَها مرّتين** في ربحيّة مسارين.
                lines.HasIndex(l => l.ShipmentId).IsUnique();
            });

            entity.OwnsMany(t => t.Expenses, lines =>
            {
                lines.ToTable("trip_expenses");
                lines.WithOwner().HasForeignKey("trip_id");
                lines.HasKey(l => l.Id);
                lines.Property(l => l.Kind).HasConversion<string>().HasMaxLength(20);
                lines.Property(l => l.Amount).HasPrecision(18, 2);
                lines.Property(l => l.Note).HasMaxLength(256);
            });

            entity.Navigation(t => t.Shipments).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(t => t.Expenses).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // (CEEF-230 · ط3-٣) ‏قائمةُ الطعام ووصفاتُها.
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.ToTable("menu_items");
            entity.Property(m => m.Code).HasMaxLength(32).IsRequired();
            entity.Property(m => m.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(m => m.Section).HasConversion<string>().HasMaxLength(16);
            entity.Property(m => m.SalePrice).HasPrecision(18, 2);
            entity.HasIndex(m => new { m.CompanyId, m.Code }).IsUnique();

            // ‏**وصنفُ بيعٍ واحدٌ لطبقٍ واحد**: ربطُ صنفين بوصفتين يُفجّر وصفتين
            // ‏لبيعةٍ واحدة، **فيُصرف ضِعفُ المخزون**.
            entity.HasIndex(m => new { m.CompanyId, m.SaleProductId }).IsUnique()
                .HasFilter(null);

            entity.OwnsMany(m => m.Components, components =>
            {
                components.ToTable("menu_item_components");
                components.WithOwner().HasForeignKey("menu_item_id");
                components.Property<long>("id");
                components.HasKey("id");
                components.Property(c => c.QuantityPerServing).HasPrecision(18, 4);

                // ‏**ومكوّنٌ واحدٌ لا يتكرّر في وصفةٍ واحدة** — والجمعُ في النطاق يمنعه
                // ‏قبل القاعدة، وهذا حارسُه الأخير.
                components.HasIndex("menu_item_id", "ProductId").IsUnique();
            });

            entity.Navigation(m => m.Components).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // (CEEF-230 · ط3-٣ب) ‏الغرفُ والحجوزُ وحساباتُ النزلاء.
        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("rooms");
            entity.Property(r => r.RoomNumber).HasMaxLength(16).IsRequired();
            entity.Property(r => r.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(r => r.NightlyRate).HasPrecision(18, 2);
            entity.HasIndex(r => new { r.CompanyId, r.RoomNumber }).IsUnique();
            entity.HasIndex(r => new { r.CompanyId, r.Status });
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("reservations");
            entity.Property(r => r.ReservationNumber).HasMaxLength(32).IsRequired();
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(r => r.RatePerNight).HasPrecision(18, 2);
            entity.Property(r => r.BranchId);
            entity.HasIndex(r => new { r.CompanyId, r.ReservationNumber }).IsUnique();
            entity.HasIndex(r => new { r.CompanyId, r.RoomId, r.Status });
            entity.HasIndex(r => new { r.CompanyId, r.CheckInDate });
            entity.HasIndex(r => new { r.CompanyId, r.BranchId });
        });

        modelBuilder.Entity<GuestFolio>(entity =>
        {
            entity.ToTable("guest_folios");
            entity.HasIndex(f => new { f.CompanyId, f.ReservationId, f.IsSettled });

            entity.OwnsMany(f => f.Charges, charges =>
            {
                charges.ToTable("folio_charges");
                charges.WithOwner().HasForeignKey("guest_folio_id");
                charges.Property<long>("id");
                charges.HasKey("id");
                charges.Property(c => c.Description).HasMaxLength(512).IsRequired();
                charges.Property(c => c.Amount).HasPrecision(18, 2);
                charges.Property(c => c.Kind).HasConversion<string>().HasMaxLength(16);
                charges.Property(c => c.VatCategory).HasConversion<string>().HasMaxLength(16);

                // ‏**هذا القيدُ هو حارسُ الازدواج الحقيقيّ** — لا فحصٌ في الذاكرة يسبقه
                // ‏تشغيلٌ آخر. **والفراغُ لا يتصادم**، فرسومُ المطعم والغسيل تتعدّد بحرّية.
                charges.HasIndex("guest_folio_id", "RoomNightDate").IsUnique();
            });

            entity.Navigation(f => f.Charges).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // (CEEF-230 · ط3-٤) ‏البوالص.
        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.ToTable("shipments");
            entity.Property(s => s.WaybillNumber).HasMaxLength(32).IsRequired();
            entity.Property(s => s.ConsigneeName).HasMaxLength(256).IsRequired();
            entity.Property(s => s.ConsigneePhone).HasMaxLength(32).IsRequired();
            entity.Property(s => s.DestinationCity).HasMaxLength(64).IsRequired();
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(s => s.WeightKg).HasPrecision(18, 3);
            entity.Property(s => s.FreightCharge).HasPrecision(18, 2);
            entity.Property(s => s.CodAmount).HasPrecision(18, 2);
            entity.Property(s => s.BranchId);

            entity.HasIndex(s => new { s.CompanyId, s.WaybillNumber }).IsUnique();

            // ‏**وهذا الفهرسُ هو ما يُجيب سؤالَ التاجر**: ما الذي حُصّل لي ولم يُورَّد؟
            entity.HasIndex(s => new { s.CompanyId, s.ShipperCustomerId, s.CodCollected, s.CodRemitted });
            entity.HasIndex(s => new { s.CompanyId, s.Status });
            entity.HasIndex(s => new { s.CompanyId, s.BranchId });
            // (CEEF-230 · ط3-٤ج) ‏أثرُ الشحنة — **سندُ التسليم لا زينةُ عرض**.
            entity.OwnsMany(sh => sh.Events, ev =>
            {
                ev.ToTable("shipment_events");
                ev.WithOwner().HasForeignKey("shipment_id");
                ev.HasKey(e => e.Id);
                ev.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
                ev.Property(e => e.Location).HasMaxLength(128);
                ev.Property(e => e.Note).HasMaxLength(256);
                ev.Property(e => e.ReceivedBy).HasMaxLength(128);
                ev.HasIndex("shipment_id", "OccurredOn");
            });

            entity.Navigation(sh => sh.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // (CEEF-230 · ط2-١) ‏سجلُّ الموظفين — وكان الاسمُ نصّاً حرّاً يُعاد كتابتُه شهرياً.
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

        // (CEEF-230 · ط2-٢) ‏نِسَبُ التأمينات — لكلّ منشأةٍ نسختُها، فالمُنظِّم يُعدّلها.
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

        // (CEEF-230 · ط1-٦) ‏الموازنة — حبّتُها شهريّةٌ بمفتاح الفترة المالية نفسِه.
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

        // (CEEF-230 · ط1-٥) ‏خططُ الفوترة الدورية وأثرُ تكراراتها.
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
                lines.Ignore(l => l.ExpiryDate); // (جاهزيّةُ الإطلاق · ح-١٠٨) ‏الانتهاءُ لفاتورة الشراء وحدَها
                lines.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                lines.Property(l => l.UnitCode).HasMaxLength(16);
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
                lines.Ignore(l => l.ExpiryDate); // (جاهزيّةُ الإطلاق · ح-١٠٨) ‏الانتهاءُ لفاتورة الشراء وحدَها
                lines.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                lines.Property(l => l.UnitCode).HasMaxLength(16);
                lines.WithOwner().HasForeignKey("purchase_request_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.ProductId);
                // ‏(م-٥ · ن-٥١) **نصّاً كإخوته الثمانية** — وكان عدداً صحيحاً هنا وحدَه
                // ‏وفي سطور عرض السعر. و`SalesInvoiceLine` نوعٌ واحدٌ يملكه تسعةُ مستندات،
                // ‏فاستعلامٌ يُكتب لأحدهم يُرمى على الآخر بـ22P02 لا بنتيجةٍ فارغة.
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
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
                lines.Property(l => l.UnitCode).HasMaxLength(16);
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
            // ‏(جاهزيّةُ الإطلاق · ح-٣٩) عملةُ الإشعار المدين — نظيرُ أعمدة فاتورة الشراء.
            entity.Property(n => n.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
            entity.Property(n => n.ExchangeRate).HasPrecision(18, 6);
            entity.Property(n => n.ForeignSubtotal).HasPrecision(18, 2);
            entity.Property(n => n.ForeignVatAmount).HasPrecision(18, 2);

            entity.OwnsMany(n => n.Lines, lines =>
            {
                lines.ToTable("debit_note_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.Ignore(l => l.ExpiryDate); // (جاهزيّةُ الإطلاق · ح-١٠٨) ‏الانتهاءُ لفاتورة الشراء وحدَها
                lines.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                lines.Property(l => l.UnitCode).HasMaxLength(16);
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
            // ‏(جاهزيّةُ الإطلاق · ح-١٣) تكاليفُ التحويل — الأجرُ المباشرُ ونسبةُ التحميل.
            entity.Property(b => b.LabourCostPerUnit).HasPrecision(18, 2);
            entity.Property(b => b.OverheadRatePercent).HasPrecision(9, 4);
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
            // ‏(ح-١٤٧) ‏الصرفُ الفعليّ والتالف — صفرٌ افتراضاً: الأمرُ القائمُ يُكمَل بالوصفة كما كان.
            entity.Property(w => w.IssuedMaterialCost).HasPrecision(18, 2);
            entity.Property(w => w.ScrapQuantity).HasPrecision(18, 4);
            entity.Property(w => w.MaterialCostRelieved).HasPrecision(18, 2);   // ‏(ح-١٤٨) دفعاتُ الإكمال
            entity.Property(w => w.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(w => new { w.CompanyId, w.OrderNumber });
            entity.HasIndex(w => w.BomId);

            // ‏(جاهزيّةُ الإطلاق · ح-٧٧) ‏وصفةُ الأمر المجمَّدة عند إصداره — بدقّة قائمة الموادّ نفسِها.
            entity.Property(w => w.RecipeLabourCostPerUnit).HasPrecision(18, 2);
            entity.Property(w => w.RecipeOverheadRatePercent).HasPrecision(9, 4);
            entity.OwnsMany(w => w.Materials, materials =>
            {
                materials.ToTable("work_order_materials");
                materials.WithOwner().HasForeignKey("work_order_id");
                materials.Property<long>("id");
                materials.HasKey("id");
                materials.Property(m => m.Quantity).HasPrecision(18, 4);
            });
            entity.Navigation(w => w.Materials).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PosShift>(entity =>
        {
            entity.ToTable("pos_shifts");
            entity.Property(s => s.WarehouseId);   // ‏(ح-١١٤) مستودعُ البيع — فارغُه الافتراضيّ
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
            entity.Property(u => u.RecoveryKeyHash).HasMaxLength(128);
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
            // ‏(جاهزيّةُ الإطلاق · treasury-payroll-assets#11) ‏طريقةُ الإهلاك — «StraightLine» لكلّ أصلٍ قائم، والافتراضُ في DDL وحدَه.
            entity.Property(a => a.Method).HasColumnName("DepreciationMethod").HasConversion<string>().HasMaxLength(24);
            entity.Property(a => a.Code).HasMaxLength(32).IsRequired();
            entity.Property(a => a.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(a => a.AcquisitionCost).HasPrecision(18, 2);
            entity.Property(a => a.SalvageValue).HasPrecision(18, 2);
            entity.Property(a => a.AccumulatedDepreciation).HasPrecision(18, 2);
            // (جاهزيّةُ الإطلاق · ح-١٦) بيعُ الأصل — المشتري ورقمُ فاتورته ومقابلُه وفئتُه.
            // ‏والأطوالُ هي أطوالُ قائمة السماح في `SchemaSqlGuard` نفسِها — فالنموذجُ
            // ‏والـDDL يلتقيان، ولو اختلفا أسقطا مسبارَ تغطية المخطّط بفارق نوع.
            entity.Property(a => a.DisposalInvoiceNumber).HasMaxLength(64);
            entity.Property(a => a.DisposalProceeds).HasPrecision(18, 2);
            entity.Property(a => a.DisposalVatCategory).HasConversion<string>().HasMaxLength(16);
            entity.Property(a => a.DisposalVatExemptionCode).HasMaxLength(64);
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
                lines.Ignore(l => l.ExpiryDate); // (جاهزيّةُ الإطلاق · ح-١٠٨) ‏الانتهاءُ لفاتورة الشراء وحدَها
                lines.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                lines.Property(l => l.UnitCode).HasMaxLength(16);
                lines.WithOwner().HasForeignKey("sales_quotation_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.Description).HasMaxLength(512).IsRequired();
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
                lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
                lines.Property(l => l.ProductId);
                // ‏(م-٥ · ن-٥١) **نصّاً كإخوته الثمانية** — انظر تعليق سطور طلب الشراء.
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
            });
            entity.Navigation(q => q.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // ‏أذونُ التسليم من أوامر البيع.
        modelBuilder.Entity<DeliveryNote>(entity =>
        {
            entity.ToTable("delivery_notes");
            entity.Property(n => n.Number).HasMaxLength(32).IsRequired();
            entity.Property(n => n.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(n => new { n.CompanyId, n.Number }).IsUnique();
            entity.HasIndex(n => n.SalesOrderId);
            entity.HasMany(n => n.Lines).WithOne().HasForeignKey(l => l.DeliveryNoteId);
        });
        modelBuilder.Entity<DeliveryNoteLine>(entity =>
        {
            entity.ToTable("delivery_note_lines");
            entity.Property(l => l.Quantity).HasPrecision(18, 4);
            entity.Property(l => l.UnitCost).HasPrecision(18, 4);
        });

        // ‏حجزُ المخزون لأوامر البيع المؤكَّدة.
        modelBuilder.Entity<Brqosh.Domain.Accounting.PostingRoleAccount>(entity =>
        {
            entity.ToTable("posting_role_accounts");
            entity.Property(r => r.Role).HasMaxLength(64).IsRequired();
            entity.Property(r => r.AccountCode).HasMaxLength(16).IsRequired();
            entity.HasIndex(r => new { r.CompanyId, r.Role }).IsUnique();
        });

        modelBuilder.Entity<StockReservation>(entity =>
        {
            entity.ToTable("stock_reservations");
            entity.Property(r => r.Quantity).HasPrecision(18, 4);
            entity.Property(r => r.ReleasedQuantity).HasPrecision(18, 4);
            entity.Ignore(r => r.OpenQuantity);
            entity.HasIndex(r => new { r.CompanyId, r.ProductId });
            entity.HasIndex(r => r.SalesOrderId);
        });

        // ‏ما فُوتر من كلّ سطر أمر بيع (الفوترة الجزئية).
        modelBuilder.Entity<SalesOrderInvoiceLine>(entity =>
        {
            entity.ToTable("sales_order_invoice_lines");
            entity.Property(l => l.Quantity).HasPrecision(18, 4);
            entity.HasIndex(l => new { l.CompanyId, l.SalesOrderId });
            entity.HasIndex(l => l.SalesInvoiceId);
        });

        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.ToTable("sales_orders");
            entity.Property(o => o.OrderNumber).HasMaxLength(32);
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(o => o.Subtotal).HasPrecision(18, 2);
            entity.Property(o => o.VatAmount).HasPrecision(18, 2);
            entity.Property(o => o.DocumentDiscountAmount).HasPrecision(18, 2);
            entity.Property(o => o.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
            entity.Property(o => o.ExchangeRate).HasPrecision(18, 6).HasDefaultValue(1m);
            entity.HasIndex(o => new { o.CompanyId, o.OrderNumber }).IsUnique();
            entity.HasIndex(o => o.CustomerId);
            entity.HasIndex(o => o.SalesQuotationId);
            entity.OwnsMany(o => o.Lines, lines =>
            {
                lines.ToTable("sales_order_lines");
                // (CEEF-230 · ط1-٣) ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                lines.Ignore(l => l.ExpiryDate); // (جاهزيّةُ الإطلاق · ح-١٠٨) ‏الانتهاءُ لفاتورة الشراء وحدَها
                lines.Property(l => l.DiscountPercent).HasPrecision(9, 4);
                lines.Property(l => l.UnitCode).HasMaxLength(16);
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
                lines.Property(l => l.AdvanceDeduction).HasPrecision(18, 2);
                lines.Property(l => l.AbsenceDeduction).HasPrecision(18, 2);
                lines.WithOwner().HasForeignKey("payroll_run_id");
                lines.Property<long>("id");
                lines.HasKey("id");
                lines.Property(l => l.EmployeeName).HasMaxLength(256).IsRequired();
                lines.Property(l => l.BasicSalary).HasPrecision(18, 2);
                lines.Property(l => l.HousingAllowance).HasPrecision(18, 2);
                lines.Property(l => l.TransportAllowance).HasPrecision(18, 2);
                lines.Property(l => l.GosiEmployee).HasPrecision(18, 2);
                lines.Property(l => l.GosiEmployer).HasPrecision(18, 2);
                            lines.Property(l => l.OtherAllowance).HasPrecision(18, 2);
                lines.Property(l => l.EmployeeId);
            });
            entity.Navigation(p => p.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<InventoryCostLayer>(entity =>
        {
            // (CEEF-231 · ط5-٨) ‏**رمزُ تزامنٍ — وهو صنفُ عطلِ `ع-D1` بعينه في موضعٍ آخر.**
            //
            // ‏‏`Consume` يقرأ `RemainingQuantity` ثمّ يكتبها. **فصرفان ‏متزامنان من الطبقة نفسِها يمرّان كلاهما** — يُستهلك أكثرُ ممّا فيها، ‏**وتخرج بضاعةٌ بتكلفةٍ لا وجودَ لها**.
            //
            // ‏و`xmin` عمودٌ نظاميٌّ في PostgreSQL يتغيّر مع كلّ تحديثٍ للصف، **فيُحوّل
            // ‏الفسادَ الصامتَ إلى `DbUpdateConcurrencyException` صريح** — و`UnitOfWork`
            // ‏يردّه رسالةً مفهومةً بعد ارتدادٍ كامل. **ولا يُعاد تلقائياً**: السياقُ يحمل
            // ‏كياناتٍ بائتة، وإعادةُ ترحيلٍ بحالةٍ بائتةٍ أخطرُ من خطأٍ يُعيده المستخدم.
            if (Database.IsNpgsql())
                entity.Property<uint>("xmin")
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();

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
            entity.Property(a => a.IsReversed).HasDefaultValue(false);
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
        MapBranchScope(modelBuilder.Entity<RecurringInvoicePlan>());
        MapBranchScope(modelBuilder.Entity<Employee>());
        MapBranchScope(modelBuilder.Entity<Property>());
        MapBranchScope(modelBuilder.Entity<LeaseContract>());
        MapBranchScope(modelBuilder.Entity<Patient>());
        MapBranchScope(modelBuilder.Entity<Visit>());
        MapBranchScope(modelBuilder.Entity<Reservation>());
        MapBranchScope(modelBuilder.Entity<Shipment>());
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

    /// <summary>
    /// (CEEF-231 · ط4-٢) <b>المَخنَقُ الوحيد لختم الفرع على القيود.</b>
    ///
    /// <para><b>ما كان:</b> <c>JournalEntry.BranchId</c> يُسنَد في <c>JournalPostingService</c>
    /// وثلاثِ خدماتِ خزينةٍ فقط — <b>وسبعٌ وثلاثون خدمةً تُضيف قيوداً في المستودع</b>.
    /// فقيدُ فاتورةِ مبيعاتٍ يخرج بلا فرع، <b>والفاتورةُ نفسُها تحمله</b>. فمنشأةٌ بثلاثة
    /// فروعٍ يقع كلُّ أستاذها في «بلا فرع»، <b>ولا يُعرف أيُّ فرعٍ يكسب</b>.</para>
    ///
    /// <para><b>ولماذا هنا لا في كلّ خدمة:</b> سبعٌ وثلاثون موضعاً تُصلَح اليوم،
    /// <b>والثامنةُ والثلاثون تُكتب غداً بلا ختم</b> — وهو صنفُ العطل نفسُه عائداً.
    /// <b>والمَخنَقُ يُغلق الصنفَ لا الحالة</b>، وهو اصطلاحُ هذا المستودع في
    /// <c>ApplyBalanceDeltaAsync</c> وفي <c>PostCoreAsync</c>.</para>
    ///
    /// <para><b>وما لا فرعَ له يبقى بلا فرعٍ صامتاً</b>: منشأةٌ بلا فروعٍ نشطة — وهي حالُ
    /// كلّ قاعدةٍ في أوّل يومها — <b>ورميٌ هنا يمنع أوّلَ قيدٍ فيها</b>. والختمُ راحةٌ
    /// لا حراسة، <b>والحراسةُ في <c>BranchAssignmentHelper</c></b> حيث تُطلب صراحة.</para>
    /// </summary>
    private async Task StampJournalEntryBranchesAsync(CancellationToken ct)
    {
        // (CEEF-231 · O-32) ‏**والمُعلَنُ على مستوى المنشأة يُتخطّى** — الإقفالُ السنويُّ
        // ‏إجراءٌ على الشخصية الاعتبارية، **وفرعُ الجلسة صدفةٌ لا معنى**. فوسمُه يُسند
        // ‏حقوقَ ملكيةِ المنشأة كلِّها إلى الفرع الذي صادف أن يكون مختاراً حين ضُغط الزرّ.
        var unstamped = ChangeTracker.Entries<Brqosh.Domain.Accounting.JournalEntry>()
            .Where(e => e.State == EntityState.Added
                        && e.Entity.BranchId is null
                        && !e.Entity.IsEntityLevel)
            .Select(e => e.Entity)
            .ToList();

        if (unstamped.Count == 0) return;

        foreach (var companyId in unstamped.Select(e => e.CompanyId).Distinct())
        {
            // ‏**وفرعُ الجلسة أوّلاً ثمّ المقرّ** — وهو ترتيبُ `BranchAssignmentService`
            // ‏نفسِه. ولا يُستدعى ذلك النوعُ من هنا لأنّ السياقَ لا يعرف طبقتَه.
            long? resolved = null;

            if (BranchSessionContext.PreferredBranchId is long preferred && preferred > 0)
            {
                var ok = await Branches.AsNoTracking().AnyAsync(
                    b => b.CompanyId == companyId && b.Id == preferred && b.IsActive, ct);
                if (ok) resolved = preferred;
            }

            resolved ??= (await Branches.AsNoTracking()
                .Where(b => b.CompanyId == companyId && b.IsActive)
                .OrderByDescending(b => b.IsHeadquarters)
                .ThenBy(b => b.Id)
                .FirstOrDefaultAsync(ct))?.Id;

            if (resolved is not long branchId) continue;

            foreach (var entry in unstamped.Where(e => e.CompanyId == companyId))
                entry.StampBranchIfUnset(branchId);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        await StampJournalEntryBranchesAsync(ct);
        return await base.SaveChangesAsync(ct);
    }
}
