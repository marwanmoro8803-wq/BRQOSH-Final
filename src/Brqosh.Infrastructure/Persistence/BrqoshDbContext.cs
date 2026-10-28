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

public partial class BrqoshDbContext : DbContext
{
    public BrqoshDbContext(DbContextOptions<BrqoshDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyGroup> CompanyGroups => Set<CompanyGroup>();
    public DbSet<IntercompanyAccountMap> IntercompanyAccountMaps => Set<IntercompanyAccountMap>();
    public DbSet<BillingPlan> BillingPlans => Set<BillingPlan>();
    public DbSet<Brqosh.Domain.Licensing.LicenseActivation> LicenseActivations => Set<Brqosh.Domain.Licensing.LicenseActivation>();
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

    // ‏الصحّة.
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<InsurancePayer> InsurancePayers => Set<InsurancePayer>();
    public DbSet<InsurancePolicy> InsurancePolicies => Set<InsurancePolicy>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Brqosh.Domain.Payroll.EmployeeAdvance> EmployeeAdvances =>
        Set<Brqosh.Domain.Payroll.EmployeeAdvance>();
    // ‏طلباتُ الإجازة والغياب — **والمستندُ الذي يُشتقّ منه الرصيدُ والخصم**.
    public DbSet<Brqosh.Domain.Payroll.LeaveRequest> LeaveRequests =>
        Set<Brqosh.Domain.Payroll.LeaveRequest>();
    public DbSet<InsuranceClaim> InsuranceClaims => Set<InsuranceClaim>();
    public DbSet<Domain.RealEstate.MaintenanceOrder> MaintenanceOrders =>
        Set<Domain.RealEstate.MaintenanceOrder>();
    public DbSet<TipPool> TipPools => Set<TipPool>();
    public DbSet<Domain.Logistics.DeliveryTrip> DeliveryTrips =>
        Set<Domain.Logistics.DeliveryTrip>();

    // ‏الضيافة.
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<GuestFolio> GuestFolios => Set<GuestFolio>();

    // ‏اللوجستيات.
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
        ConfigureCoreAndLedger(modelBuilder);
        ConfigureSalesPurchasingInventory(modelBuilder);
        ConfigureSectors(modelBuilder);
        ConfigurePayrollTreasuryZatca(modelBuilder);
        ConfigureDocumentsAndOperations(modelBuilder);
        ConfigureBranchScopes(modelBuilder);
    }

    private static void MapBranchScope<T>(EntityTypeBuilder<T> entity) where T : class, IBranchScoped
    {
        entity.Property(e => e.BranchId);
        entity.HasIndex(e => new { e.CompanyId, e.BranchId });
    }

    /// <summary>
    /// <b>المَخنَقُ الوحيد لختم الفرع على القيود.</b>
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
        // ‏**والمُعلَنُ على مستوى المنشأة يُتخطّى** — الإقفالُ السنويُّ
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
