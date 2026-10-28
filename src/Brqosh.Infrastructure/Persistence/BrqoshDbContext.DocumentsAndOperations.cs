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
    private void ConfigureDocumentsAndOperations(ModelBuilder modelBuilder)
    {
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
                // ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
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
                // فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
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
                // ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
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
                // ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
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
            // ‏ عملةُ الإشعار المدين — نظيرُ أعمدة فاتورة الشراء.
            entity.Property(n => n.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
            entity.Property(n => n.ExchangeRate).HasPrecision(18, 6);
            entity.Property(n => n.ForeignSubtotal).HasPrecision(18, 2);
            entity.Property(n => n.ForeignVatAmount).HasPrecision(18, 2);

            entity.OwnsMany(n => n.Lines, lines =>
            {
                lines.ToTable("debit_note_lines");
                // ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
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
                // فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
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
            // ‏ تكاليفُ التحويل — الأجرُ المباشرُ ونسبةُ التحميل.
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

            // ‏ ‏وصفةُ الأمر المجمَّدة عند إصداره — بدقّة قائمة الموادّ نفسِها.
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

            // سلسلة تجزئة كاشفة للعبث.
            entity.Property(a => a.PreviousHash).HasMaxLength(64).IsRequired();
            entity.Property(a => a.EntryHash).HasMaxLength(64).IsRequired();
            // فريد: يمنع تفرّع السلسلة حتى لو أخفق القفل الاستشاري (SQLite مثلاً).
            entity.HasIndex(a => new { a.CompanyId, a.Sequence }).IsUnique();
        });

        modelBuilder.Entity<FixedAsset>(entity =>
        {
            entity.ToTable("fixed_assets");
            // ‏ ‏طريقةُ الإهلاك — «StraightLine» لكلّ أصلٍ قائم، والافتراضُ في DDL وحدَه.
            entity.Property(a => a.Method).HasColumnName("DepreciationMethod").HasConversion<string>().HasMaxLength(24);
            entity.Property(a => a.Code).HasMaxLength(32).IsRequired();
            entity.Property(a => a.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(a => a.AcquisitionCost).HasPrecision(18, 2);
            entity.Property(a => a.SalvageValue).HasPrecision(18, 2);
            entity.Property(a => a.AccumulatedDepreciation).HasPrecision(18, 2);
            // بيعُ الأصل — المشتري ورقمُ فاتورته ومقابلُه وفئتُه.
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
                // ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
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
                // ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
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
                // فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
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
            // ‏**رمزُ تزامنٍ — وهو صنفُ عطلِ `ع-D1` بعينه في موضعٍ آخر.**
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

        // سجلُّ الاستهلاك — مفتاحُه المستند، فالترميمُ يبدأ منه.
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
            // طابور Outbox: استعلام «غير المُرسَل» يجري بعد كل ترحيل .
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
    }
}
