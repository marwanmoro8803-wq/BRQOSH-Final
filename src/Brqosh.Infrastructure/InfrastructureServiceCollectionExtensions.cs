using Brqosh.Application;
using Brqosh.Application.Accounting;
using Brqosh.Application.Intelligence;
using Brqosh.Application.MasterData;
using Brqosh.Application.Reporting;
using Brqosh.Application.Setup;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Application.Platform;
using Brqosh.Application.Treasury;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Crm;
using Brqosh.Infrastructure.Intelligence;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.MasterData;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Setup;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Webhooks;
using Brqosh.Infrastructure.Zatca;
using Brqosh.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBrqoshInfrastructure(
        this IServiceCollection services, string connectionString, bool usePostgres = false)
    {
        services.AddBrqoshApplication();
        services.AddSingleton<IConfigurationTemplateLoader, ConfigurationTemplateLoader>();
        services.AddDbContext<BrqoshDbContext>(options =>
        {
            if (usePostgres)
                // (CEEF-231 · ط5-٧) ‏**والمساران معاً** — ودرسُ `ط4-٣`: إصلاحُ أحد
                // ‏مسارَي شيءٍ واحدٍ يترك العطلَ حيّاً حيث يُستعمل الآخر.
                options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null));
            else
                options.UseSqlite(connectionString);
            // ‏(جاهزيّةُ الإطلاق · ح-٧٨) ‏وحارسُ الفترة المقفلة عند الحفظ — كما في المصنع.
            options.AddInterceptors(new AuditLogImmutabilityInterceptor(), new ClosedPeriodPostingInterceptor(),
                new ReadOnlyBooksInterceptor());
        });

        services.AddScoped<IPostingPolicy, SaudiPostingPolicy>();
        services.AddScoped<PostingOrchestrator>();

        // CEEF-122: خدمات الترحيل المالي — scoped لحل DesktopServiceFacade من DI بدل new XxxService(db)
        services.AddScoped<JournalPostingService>();
        services.AddScoped<SalesInvoicePostingService>();

        // (CEEF-230 · ط6-٣) ‏خدمةُ الفوترة الدورية — تُطلَب من الشاشة، فتُسجَّل.
        services.AddScoped<Sales.RecurringInvoiceService>();

        // (CEEF-230 · ط6-٤) ‏خدماتُ القطاعات — تُطلَب من شاشاتها، فتُسجَّل.
        services.AddScoped<RealEstate.LeaseService>();
        services.AddScoped<RealEstate.SecurityDepositService>();
        services.AddScoped<Healthcare.VisitBillingService>();
        services.AddScoped<Healthcare.ClaimSettlementService>();
        services.AddScoped<Hospitality.TipPoolService>();
        services.AddScoped<RealEstate.MaintenanceOrderService>();
        services.AddScoped<Logistics.DeliveryTripService>();
        services.AddScoped<Payroll.EmployeeAdvanceService>();
        services.AddScoped<Hospitality.FolioService>();
        services.AddScoped<Hospitality.KitchenIssueService>();

        // (CEEF-230 · ط6-٥) ‏تخصيصُ مكافأة نهاية الخدمة — **ولم يكن مسجَّلاً في الحقن
        // ‏أصلاً**: خدمةٌ مبنيّةٌ ومقيسةٌ في `ط2-٣` **لا يستطيع أحدٌ طلبَها**. فالمخصّصُ
        // ‏النظاميُّ لا يجري، والتزامٌ يتراكم لا يظهر في الميزانية.
        services.AddScoped<Payroll.EndOfServiceAccrualService>();
        services.AddScoped<Payroll.EndOfServiceSettlementService>();

        // (جاهزيّةُ الإطلاق · ح-٨٤) ‏صرفُ الرواتب وسدادُ التأمينات ودفعُ مستحقّ نهاية الخدمة — وكان `2108` و`2109` لا يُدينهما شيء.
        services.AddScoped<Payroll.PayrollPaymentService>();

        // (جاهزيّةُ الإطلاق · ح-٨٥) ‏**أربعٌ تطلبها الشاشاتُ بـ`GetService<T>` ولم تكن تُحَلّ** — ثلاثٌ تُبنى بـ`new` داخل محوِّلات
        // ‏حالات الاستخدام وحدَها، ورابعةٌ مسجَّلةٌ بواجهتها لا بصنفها (`IDataMigrationService`)؛ فكان استيرادُ كشف البنك
        // ‏ومطابقتُه الآليّةُ وشاشةُ مركز النقد وشاشةُ ترحيل البيانات ترمي «No service for type».
        // ‏والحارسُ `EveryServiceTheDesktopAsksForResolvesTests` يقرأ الطلبَ من مصدر الشاشات ويُحلّه من هذه الحاوية.
        services.AddScoped<Treasury.BankStatementImportService>();
        services.AddScoped<Treasury.BankStatementMatcherService>();
        services.AddScoped<Treasury.CashMovementReportService>();
        services.AddScoped<Setup.CsvDataMigrationService>();
        services.AddScoped<Payroll.LeaveRequestService>();
        services.AddScoped<Payroll.LeaveAccrualService>();
        services.AddScoped<Payroll.LeaveBalanceQuery>();

        // (CEEF-230 · ط4-٧) ‏إعادةُ تقييم الذمم بالعملات — إلزامُ المعيار 21.
        services.AddScoped<Accounting.MonetaryRevaluationService>();
        services.AddScoped<Logistics.CodSettlementService>();
        services.AddScoped<CreditNotePostingService>();
        services.AddScoped<PurchaseInvoicePostingService>();
        services.AddScoped<DebitNotePostingService>();
        services.AddScoped<GoodsReceiptPostingService>();
        services.AddScoped<PaymentVoucherPostingService>();
        services.AddScoped<ReceiptVoucherPostingService>();
        services.AddScoped<ChequePostingService>();
        services.AddScoped<BankStatementDifferencePostingService>();
        services.AddScoped<StockMovementPostingService>();
        services.AddScoped<PayrollPostingService>();
        services.AddScoped<FixedAssetAcquisitionPostingService>();
        services.AddScoped<FixedAssetDisposalPostingService>();
        services.AddScoped<FixedAssetCancellationService>();   // ‏(ح-١٥٧)
        services.AddScoped<FixedAssetDisposalReversalService>();   // ‏(ح-١٦١)
        services.AddScoped<ProgressBillingPostingService>();

        // CEEF-123: إلغاء/عكس/فترات — Tier P0 هجرة Desktop
        services.AddScoped<SalesInvoiceCancellationService>();
        services.AddScoped<RejectedInvoiceReissueService>();   // (tax-zatca#8)
        services.AddScoped<PurchaseInvoiceCancellationService>();
        services.AddScoped<CreditNoteCancellationService>();
        services.AddScoped<DebitNoteCancellationService>();
        services.AddScoped<VoucherReversalService>();
        services.AddScoped<PaymentAllocationService>();
        services.AddScoped<YearEndCloseService>();
        services.AddScoped<FiscalPeriodService>();

        // CEEF-123 Tier P1: خزينة · POS · تدقيق
        services.AddScoped<BankAccountService>();
        services.AddScoped<Operations.BackupService>();
        // (CEEF-231 · ط5-٤) ‏تكرارُ النسخة خارج الجهاز.
        services.AddScoped<Operations.OffsiteBackupService>();
        services.AddScoped<Operations.BackupScheduleService>();
        // (CEEF-231 · ط5-٥) ‏فحصُ قناة التحديث.
        services.AddScoped<Operations.UpdateCheckService>();
        // (CEEF-231 · ط5-١١) ‏تقليمُ ما يجوز — **ولا يمسّ سجلَّ التدقيق ولا المعلَّق في الطابور**.
        services.AddScoped<Operations.DataRetentionService>();
        services.AddScoped<BankTransferService>();
        services.AddScoped<Retail.PosSaleService>();
        services.AddScoped<Retail.PosShiftVariancePostingService>();
        services.AddScoped<Hospitality.ReservationDepositService>();
        services.AddScoped<Logistics.ShipmentBillingService>();
        services.AddScoped<Security.AuditService>();
        services.AddScoped<DocumentPrintService>();

        // CEEF-124: Tier P2/P3 — مخزون · مشاريع · تصنيع · مبيعات · أمن · ZATCA · bootstrap
        services.AddScoped<GoodsReceiptCancellationService>();
        services.AddScoped<JournalReversalService>();
        services.AddScoped<StockMovementCancellationService>();
        services.AddScoped<StockCountAdjustmentService>();
        services.AddScoped<CostingMethodService>();
        services.AddScoped<SoftDeletedRowService>();
        services.AddScoped<CodeAvailabilityQuery>();
        services.AddScoped<InventoryService>();
        services.AddScoped<LandedCostAllocationService>();
        services.AddScoped<PurchaseOrderService>();
        services.AddScoped<PayrollReversalService>();
        services.AddScoped<DepreciationService>();
        services.AddScoped<ProgressBillingCancellationService>();
        services.AddScoped<RetentionReleaseService>();
        services.AddScoped<SubcontractRetentionWithholdService>();
        services.AddScoped<ConstructionProjectService>();
        services.AddScoped<ProjectCostService>();
        services.AddScoped<WorkOrderService>();
        services.AddScoped<BillOfMaterialsService>();
        services.AddScoped<SalesOrderService>();
        services.AddScoped<SalesQuotationService>();
        services.AddScoped<CrmService>();
        services.AddScoped<ZatcaSubmissionService>();
        services.AddScoped<ZatcaCsidOnboardingService>();
        services.AddScoped<OrganizationProvisioningService>();
        services.AddScoped<IOrganizationProvisioningService>(sp => sp.GetRequiredService<OrganizationProvisioningService>());
        services.AddScoped<UserAuthService>();
        services.AddScoped<AdminRecoveryKey>();   // (security-licensing-tenancy#7)
        services.AddScoped<UserManagementService>();
        services.AddScoped<DatabaseBootstrapService>();

        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<PurchaseRequestWorkflowService>();
        services.AddScoped<PurchaseOrderWorkflowService>();
        services.AddScoped<PaymentVoucherWorkflowService>();
        services.AddScoped<ManualJournalEntryWorkflowService>();
        services.AddScoped<ManualJournalApprovalPolicy>();   // (accounting-core#6)
        services.AddScoped<Brqosh.Infrastructure.Companies.BusinessProfileChangeService>();   // (ui-ux-i18n-printing#7)
        services.AddScoped<IMasterDataGovernanceService, MasterDataGovernanceService>();
        services.AddScoped<ICashFlowForecastService, CashFlowForecastService>();
        services.AddScoped<IPaymentRunService, PaymentRunService>();

        services.AddScoped<ExchangeRateService>();
        services.AddScoped<IExchangeRateService>(sp => sp.GetRequiredService<ExchangeRateService>());
        services.AddSingleton<IFxRateProvider>(_ => new ChainedFxRateProvider(
            SamaFxRateProvider.FromEnvironment(),
            new FrankfurterFxRateProvider()));
        services.AddScoped<FxRateSyncService>();
        services.AddScoped<IFxRateSyncService>(sp => sp.GetRequiredService<FxRateSyncService>());
        services.AddScoped<IBillingPlanCatalog, BillingPlanCatalog>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<FxRevaluationService>();
        services.AddScoped<FxRevaluationBatchService>();
        services.AddScoped<FxClosingTranslationService>();
        services.AddScoped<ITrialBalanceQuery, TrialBalanceQueryAdapter>();
        services.AddScoped<IGeneralLedgerQuery, GeneralLedgerQueryAdapter>();
        services.AddScoped<IBalanceSheetQuery, BalanceSheetQueryAdapter>();
        services.AddScoped<IIncomeStatementQuery, IncomeStatementQueryAdapter>();
        services.AddScoped<ICashFlowStatementQuery, CashFlowStatementQueryAdapter>();
        services.AddScoped<IVatReturnQuery, VatReturnQueryAdapter>();
        services.AddScoped<ICustomerStatementQuery, CustomerStatementQueryAdapter>();
        services.AddScoped<ISupplierStatementQuery, SupplierStatementQueryAdapter>();
        services.AddScoped<IProjectProfitabilityQuery, ProjectProfitabilityQueryAdapter>();
        services.AddScoped<IConsolidatedTrialBalanceQuery, ConsolidatedTrialBalanceQueryAdapter>();
        services.AddScoped<IConsolidatedIncomeStatementQuery, ConsolidatedIncomeStatementQueryAdapter>();
        services.AddScoped<IConsolidatedBalanceSheetQuery, ConsolidatedBalanceSheetQueryAdapter>();
        services.AddScoped<IIntercompanyEliminationQuery, IntercompanyEliminationQueryAdapter>();
        services.AddScoped<IDataQualityService, DataQualityService>();
        services.AddScoped<IReportSnapshotService, ReportSnapshotService>();
        services.AddScoped<IEventTrackingQuery, EventTrackingQuery>();
        services.AddScoped<IFinancialIntelligenceService, FinancialIntelligenceService>();
        services.AddScoped<IFinancialAssistantService, FinancialAssistantService>();
        services.AddScoped<IProgramAssistantService, ProgramAssistantService>();
        services.AddScoped<IMonthEndCloseUseCase, MonthEndCloseUseCaseAdapter>();
        services.AddScoped<IBankStatementImportUseCase, BankStatementImportUseCaseAdapter>();
        services.AddScoped<IBankStatementMatchUseCase, BankStatementMatchUseCaseAdapter>();
        services.AddScoped<IBankStatementDifferencePostingUseCase, BankStatementDifferencePostingUseCaseAdapter>();
        services.AddScoped<ICashMovementReportUseCase, CashMovementReportUseCaseAdapter>();
        services.AddScoped<IDataMigrationService, CsvDataMigrationService>();

        services.AddHttpClient<IWebhookDispatcher, WebhookDispatcher>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // ‏(م-٣ · ن-٠٩) ‏**المصنعُ يبني ولا يَصِل — والوصلُ صار خطوةً صريحةً تُستدعى.**
        //
        // ‏كان هذا المصنعُ يضبط `DomainEventHub.Current` ويصل `OutboxDispatchHook` **من
        // ‏داخله**. و`AddSingleton<T>(factory)` كسولٌ: لا يعمل حتى يُطلب `IDomainEventPublisher`.
        // ‏**ولا يطلبه أحدٌ في الإنتاج** — مقيسٌ بمسحٍ على المستودع: الطالبُ الوحيد ملفُّ
        // ‏اختبار. فيبقى `DomainEventHub.Current` على `InMemoryDomainEventPublisher`،
        // ‏**والصندوقُ الصادر (outbox) والـwebhooks لا يعملان**، والشاشةُ تقول «لا شيء»
        // ‏فيُقرأ الصمتُ سلامة.
        //
        // ‏**وأثرٌ جانبيٌّ في مصنعِ حقنٍ هو الفخُّ نفسُه**: يعمل متى طُلب، ولا يعمل متى لم
        // ‏يُطلب، **ولا شيءَ في التوقيع يقول ذلك**. فصار المصنعُ بناءً محضاً، والوصلُ
        // ‏<see cref="WireDomainEventPublishing"/> — دالّةً باسمها يُستدعيها المُضيف.
        services.AddSingleton<IDomainEventPublisher>(sp =>
            new PersistingDomainEventPublisher(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IWebhookDispatcher>()));

        return services;
    }

    /// <summary>
    /// (م-٣ · ن-٠٩) <b>يصل نشرَ أحداث النطاق بالحاوية — ويجب أن يُستدعى مرّةً عند الإقلاع.</b>
    ///
    /// <para><c>DomainEventHub</c> و<c>OutboxDispatchHook</c> ساكنان: تنشر عليهما خدماتٌ
    /// تُنشأ بـ<c>new Service(db)</c> خارج الحقن — <b>وهي أغلبُ خدمات هذا النظام</b>. فلا
    /// بدّ من ربطهما بالحاوية صراحةً، ولا يجوز أن يقع ذلك أثراً جانبيّاً في مصنعٍ كسول.</para>
    ///
    /// <para><b>وبلا هذا النداء يعمل النظامُ كلُّه ولا يصل حدثٌ واحد</b> — ولا يشتكي شيء.</para>
    /// </summary>
    public static IServiceProvider WireDomainEventPublishing(this IServiceProvider services)
    {
        DomainEventHub.Current = services.GetRequiredService<IDomainEventPublisher>();

        // Outbox (ع-D2، CEEF-204): تفريغ الطابور بعد الالتزام حصراً.
        // ‏ويُوصَل هنا لأن `UnitOfWork` ساكنٌ بلا حاوية حقن — نفسُ نمط `DomainEventHub`.
        OutboxDispatchHook.Wire((db, ct) =>
            new OutboxDispatcher(services.GetRequiredService<IWebhookDispatcher>())
                .DispatchPendingAsync(db, ct));

        return services;
    }
}

file sealed class TrialBalanceQueryAdapter(BrqoshDbContext db) : ITrialBalanceQuery
{
    public async Task<IReadOnlyList<TrialBalanceDto>> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var rows = await new TrialBalanceQuery(db).ExecuteAsync(companyId, fromDate, toDate);
        return rows.Select(r => new TrialBalanceDto(
            r.AccountCode, r.AccountNameAr, r.Type, r.TotalDebit, r.TotalCredit)).ToList();
    }
}

file sealed class GeneralLedgerQueryAdapter(BrqoshDbContext db) : IGeneralLedgerQuery
{
    public async Task<IReadOnlyList<GeneralLedgerAccountDto>> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, string? accountCodeFilter = null,
        CancellationToken ct = default)
    {
        var accounts = await new GeneralLedgerQuery(db).ExecuteAsync(
            companyId, fromDate, toDate, accountCodeFilter);
        return accounts.Select(a => new GeneralLedgerAccountDto(
            a.AccountCode,
            a.AccountNameAr,
            a.Type,
            a.Lines.Select(l => new GeneralLedgerLineDto(
                l.EntryDate, l.EntryNumber, l.Description,
                l.Debit, l.Credit, l.RunningBalance)).ToList(),
            a.ClosingBalance)).ToList();
    }
}

file sealed class BalanceSheetQueryAdapter(BrqoshDbContext db) : IBalanceSheetQuery
{
    public async Task<BalanceSheetResultDto> ExecuteAsync(
        long companyId, DateOnly asOfDate, CancellationToken ct = default)
    {
        var result = await new BalanceSheetQuery(db).ExecuteAsync(companyId, asOfDate);
        return new BalanceSheetResultDto(
            result.AsOfDate,
            result.Assets.Select(MapRow).ToList(),
            result.Liabilities.Select(MapRow).ToList(),
            result.Equity.Select(MapRow).ToList());

        static BalanceSheetRowDto MapRow(BalanceSheetRow r) =>
            new(r.AccountCode, r.AccountNameAr, r.Type, r.Amount);
    }
}

file sealed class IncomeStatementQueryAdapter(BrqoshDbContext db) : IIncomeStatementQuery
{
    public async Task<IncomeStatementResultDto> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var result = await new IncomeStatementQuery(db).ExecuteAsync(companyId, fromDate, toDate);
        return new IncomeStatementResultDto(
            result.FromDate,
            result.ToDate,
            result.RevenueRows.Select(MapRow).ToList(),
            result.ExpenseRows.Select(MapRow).ToList());

        static IncomeStatementRowDto MapRow(IncomeStatementRow r) =>
            new(r.AccountCode, r.AccountNameAr, r.Type, r.Amount);
    }
}

file sealed class CashFlowStatementQueryAdapter(BrqoshDbContext db) : ICashFlowStatementQuery
{
    public async Task<CashFlowStatementResultDto> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var result = await new CashFlowStatementService(db).ExecuteAsync(companyId, fromDate, toDate);
        return new CashFlowStatementResultDto(
            result.FromDate,
            result.ToDate,
            MapSection(result.Operating),
            MapSection(result.Investing),
            MapSection(result.Financing),
            result.NetCashChange,
            result.OpeningCash,
            result.ClosingCash);

        static CashFlowSectionDto MapSection(CashFlowSection section) =>
            new(section.NameAr, section.Lines.Select(l => new CashFlowLineDto(l.Label, l.Amount)).ToList());
    }
}

file sealed class VatReturnQueryAdapter(BrqoshDbContext db) : IVatReturnQuery
{
    public async Task<VatReturnDto> ExecuteAsync(
        long companyId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var r = await new VatReturnQuery(db).ExecuteAsync(companyId, fromDate, toDate);
        // (CEEF-229 · م1-١٠) العبورُ في موضعٍ يُقاس — لا سطراً داخل مُحوِّلٍ لا يُرى.
        return VatReturnMapping.From(r);
    }
}

file sealed class CustomerStatementQueryAdapter(BrqoshDbContext db) : ICustomerStatementQuery
{
    public async Task<CustomerStatementResultDto> ExecuteAsync(
        long companyId, long customerId, CancellationToken ct = default)
    {
        var query = new CustomerBalanceQuery(db);
        var lines = await query.StatementAsync(companyId, customerId);
        var balance = await query.ForCustomerAsync(companyId, customerId);
        return new CustomerStatementResultDto(
            lines.Select(l => new CustomerStatementLineDto(
                l.Date, l.DocumentNumber, l.DocumentType,
                l.Debit, l.Credit, l.RunningBalance)).ToList(),
            balance.Balance);
    }
}

file sealed class SupplierStatementQueryAdapter(BrqoshDbContext db) : ISupplierStatementQuery
{
    public async Task<SupplierStatementResultDto> ExecuteAsync(
        long companyId, long supplierId, CancellationToken ct = default)
    {
        var query = new SupplierBalanceQuery(db);
        var lines = await query.StatementAsync(companyId, supplierId);
        var balance = await query.ForSupplierAsync(companyId, supplierId);
        return new SupplierStatementResultDto(
            lines.Select(l => new SupplierStatementLineDto(
                l.Date, l.DocumentNumber, l.DocumentType,
                l.Debit, l.Credit, l.RunningBalance)).ToList(),
            balance.Balance);
    }
}

file sealed class ProjectProfitabilityQueryAdapter(BrqoshDbContext db) : IProjectProfitabilityQuery
{
    public async Task<IReadOnlyList<ProjectProfitabilityDto>> ExecuteAsync(long companyId, CancellationToken ct = default)
    {
        var rows = await new ProjectProfitabilityQuery(db).ExecuteAsync(companyId, ct);
        return rows.Select(r => new ProjectProfitabilityDto(
            r.ProjectId, r.Code, r.NameAr, r.ContractValue, r.EstimatedTotalCost,
            r.IncurredCost, r.BilledWork, r.RecognizedRevenueEstimate, r.RetentionHeld,
            r.GrossMarginEstimate, r.CompletionMethod)).ToList();
    }
}

file sealed class ConsolidatedTrialBalanceQueryAdapter(
    BrqoshDbContext db, ExchangeRateService rateService) : IConsolidatedTrialBalanceQuery
{
    public async Task<IReadOnlyList<ConsolidatedTrialBalanceDto>> ExecuteAsync(
        long groupId, DateOnly fromDate, DateOnly toDate,
        IReadOnlyDictionary<long, decimal>? fxRatesFromFunctionalToReporting = null,
        CancellationToken ct = default)
    {
        var rows = await new ConsolidatedTrialBalanceQuery(db, rateService).ExecuteAsync(
            groupId, fromDate, toDate, fxRatesFromFunctionalToReporting, ct);
        return rows.Select(r => new ConsolidatedTrialBalanceDto(
            r.AccountCode, r.AccountNameAr, r.Type, r.TotalDebit, r.TotalCredit, r.CompanyCount)).ToList();
    }
}

file sealed class ConsolidatedIncomeStatementQueryAdapter(
    BrqoshDbContext db, ExchangeRateService rateService) : IConsolidatedIncomeStatementQuery
{
    public async Task<ConsolidatedIncomeStatementResultDto> ExecuteAsync(
        long groupId, DateOnly fromDate, DateOnly toDate,
        IReadOnlyDictionary<long, decimal>? fxRatesFromFunctionalToReporting = null,
        CancellationToken ct = default)
    {
        var result = await new ConsolidatedIncomeStatementQuery(db, rateService).ExecuteAsync(
            groupId, fromDate, toDate, fxRatesFromFunctionalToReporting, ct);
        return new ConsolidatedIncomeStatementResultDto(
            result.GroupId,
            result.ReportingCurrencyCode,
            result.FromDate,
            result.ToDate,
            result.RevenueRows.Select(r => new ConsolidatedIncomeStatementDto(
                r.AccountCode, r.AccountNameAr, r.Type, r.Amount, r.CompanyCount)).ToList(),
            result.ExpenseRows.Select(r => new ConsolidatedIncomeStatementDto(
                r.AccountCode, r.AccountNameAr, r.Type, r.Amount, r.CompanyCount)).ToList());
    }
}

file sealed class ConsolidatedBalanceSheetQueryAdapter(
    BrqoshDbContext db, ExchangeRateService rateService) : IConsolidatedBalanceSheetQuery
{
    public async Task<ConsolidatedBalanceSheetResultDto> ExecuteAsync(
        long groupId, DateOnly asOfDate,
        IReadOnlyDictionary<long, decimal>? fxRatesFromFunctionalToReporting = null,
        CancellationToken ct = default)
    {
        var result = await new ConsolidatedBalanceSheetQuery(db, rateService).ExecuteAsync(
            groupId, asOfDate, fxRatesFromFunctionalToReporting, ct);
        return new ConsolidatedBalanceSheetResultDto(
            result.GroupId,
            result.ReportingCurrencyCode,
            result.AsOfDate,
            result.Assets.Select(r => new ConsolidatedBalanceSheetDto(
                r.AccountCode, r.AccountNameAr, r.Type, r.Amount, r.CompanyCount)).ToList(),
            result.Liabilities.Select(r => new ConsolidatedBalanceSheetDto(
                r.AccountCode, r.AccountNameAr, r.Type, r.Amount, r.CompanyCount)).ToList(),
            result.Equity.Select(r => new ConsolidatedBalanceSheetDto(
                r.AccountCode, r.AccountNameAr, r.Type, r.Amount, r.CompanyCount)).ToList());
    }
}

file sealed class IntercompanyEliminationQueryAdapter(
    BrqoshDbContext db, ExchangeRateService rateService) : IIntercompanyEliminationQuery
{
    public async Task<IReadOnlyList<IntercompanyEliminationDto>> ExecuteAsync(
        long groupId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var rows = await new IntercompanyEliminationQuery(db, rateService).ExecuteAsync(
            groupId, fromDate, toDate, ct: ct);
        return rows.Select(r => new IntercompanyEliminationDto(
            r.AccountCode, r.EliminatedDebit, r.EliminatedCredit)).ToList();
    }
}

file sealed class MonthEndCloseUseCaseAdapter(BrqoshDbContext db, ITrialBalanceQuery trialBalance)
    : IMonthEndCloseUseCase
{
    public Task<MonthEndCloseResult> ClosePeriodAsync(
        long companyId, long periodId, string username, CancellationToken ct = default) =>
        new MonthEndCloseOrchestrator(db, trialBalance).ClosePeriodAsync(companyId, periodId, username, ct);
}

file sealed class BankStatementImportUseCaseAdapter(BrqoshDbContext db) : IBankStatementImportUseCase
{
    public async Task<BankStatementImportResult> ImportCsvAsync(
        long companyId, long bankAccountId, string sourceFileName, string csv, CancellationToken ct = default,
        decimal? openingBalance = null, decimal? closingBalance = null)
    {
        var statement = await new BankStatementImportService(db)
            .ImportCsvAsync(companyId, bankAccountId, sourceFileName, csv, ct, openingBalance, closingBalance);
        return new BankStatementImportResult(statement.Id, statement.Lines.Count);
    }
}

file sealed class BankStatementMatchUseCaseAdapter(BrqoshDbContext db) : IBankStatementMatchUseCase
{
    public async Task<BankStatementMatchResultDto> AutoMatchAsync(
        long companyId, long bankStatementId, CancellationToken ct = default)
    {
        var report = await new BankStatementMatcherService(db).AutoMatchAsync(companyId, bankStatementId, ct);
        return new BankStatementMatchResultDto(report.MatchedCount, report.UnmatchedAfter);
    }
}

file sealed class BankStatementDifferencePostingUseCaseAdapter(BrqoshDbContext db)
    : IBankStatementDifferencePostingUseCase
{
    public async Task<BankDifferencePostingResultDto> PostUnmatchedLineAsync(
        long companyId,
        long bankStatementId,
        int lineIndex,
        string? offsetAccountCode = null,
        CancellationToken ct = default)
    {
        var result = await new BankStatementDifferencePostingService(db)
            .PostUnmatchedLineAsync(companyId, bankStatementId, lineIndex, offsetAccountCode, ct);
        return new BankDifferencePostingResultDto(result.JournalEntryId, result.EntryNumber, result.LineIndex);
    }
}

file sealed class CashMovementReportUseCaseAdapter(BrqoshDbContext db) : ICashMovementReportUseCase
{
    public async Task<CashMovementSummaryDto> GetBankMovementAsync(
        long companyId, long bankAccountId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var summary = await new CashMovementReportService(db).GetBankMovementAsync(companyId, bankAccountId, from, to, ct);
        return new CashMovementSummaryDto(
            summary.TotalInflows, summary.TotalOutflows, summary.NetMovement, summary.TransactionCount);
    }
}
