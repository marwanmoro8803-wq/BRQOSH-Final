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
    private void ConfigureBranchScopes(ModelBuilder modelBuilder)
    {
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
}
