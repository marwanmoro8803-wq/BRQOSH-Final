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
    private void ConfigureSalesPurchasingInventory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.Property(c => c.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(c => c.VatRegistrationNumber).HasMaxLength(15);
            entity.Property(c => c.Phone).HasMaxLength(32);
            entity.Property(c => c.Address).HasMaxLength(512);
            entity.Property(c => c.ModifiedBy).HasMaxLength(128);
            // ‏ حدُّ الائتمان — وفارغُه بلا حدّ.
            entity.Property(c => c.CreditLimit).HasPrecision(18, 2);

            // هوية المشتري النظامية — العروضُ تطابق أعمدة BrqoshSchemaUpdater
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
            // ‏خصمُ المستند — يُوزَّع على الأوعية الضريبية عند الحساب.
            entity.Property(i => i.DocumentDiscountAmount).HasPrecision(18, 2);

            // ‏عملةُ المستند ومبالغُها — والريالُ يبقى في `Subtotal`
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
                // ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
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
                // فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
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
            // ‏ عملةُ الإشعار — نظيرُ أعمدة الفاتورة.
            entity.Property(n => n.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
            entity.Property(n => n.ExchangeRate).HasPrecision(18, 6);
            entity.Property(n => n.ForeignSubtotal).HasPrecision(18, 2);
            entity.Property(n => n.ForeignVatAmount).HasPrecision(18, 2);
            // ‏ خصمُ المستند على الإشعار — نظيرُ عمود الفاتورة.
            entity.Property(n => n.DocumentDiscountAmount).HasPrecision(18, 2);

            entity.OwnsMany(n => n.Lines, lines =>
            {
                lines.ToTable("credit_note_lines");
                // ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
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
                // فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
            });

            entity.Navigation(n => n.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ReceiptVoucher>(entity =>
        {
            entity.ToTable("receipt_vouchers");
            // ‏عملةُ السند وسعرُها يوم التحصيل — ومن فرقِه عن
            // ‏سعر يوم الفاتورة يقع الربحُ أو الخسارةُ المحقَّقة.
            entity.Property(v => v.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
            entity.Property(v => v.ExchangeRate).HasPrecision(18, 6).HasDefaultValue(1m);
            entity.Property(v => v.ReceivableRelieved).HasPrecision(18, 2);

            // ‏خصمُ السداد المبكر — يُطفئ من الذمّة ما لم يدخل الصندوق.
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
            // ‏عملةُ الفاتورة وسعرُها وأساسُها الأجنبيّ.
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
            // ‏ ‏معاملةُ الضريبة — «Domestic» لكلّ فاتورةٍ قائمة — وضريبةُ الاحتساب العكسيّ والقيمةُ الجمركيّة.
            // ‏والافتراضُ في الكائن وفي DDL وحدَهما — لا `HasDefaultValue` (انظر accounting-core#6 أعلاه).
            entity.Property(i => i.VatTreatment).HasConversion<string>().HasMaxLength(24);
            entity.Property(i => i.ReverseChargeVatAmount).HasPrecision(18, 2);
            entity.Property(i => i.ImportCustomsValue).HasPrecision(18, 2);

            entity.OwnsMany(i => i.Lines, lines =>
            {
                lines.ToTable("purchase_invoice_lines");
                // ‏سقفُ الطول في النموذج لا في القاعدة وحدَها —
                // ‏وإلا صار العمودُ `text` بلا حدّ، وكشفه مسبارُ المخطّط مخالفةَ نوع.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
                // ‏ ‏**انتهاءُ دفعة الشراء المباشر** — لفاتورة الشراء وحدَها.
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
                // فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم.
                lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16);
            });

            entity.Navigation(i => i.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PaymentVoucher>(entity =>
        {
            // ‏عملةُ السند وما حُرِّر من الذمّة بعملة الدفاتر.
            entity.Property(v => v.CurrencyCode).HasMaxLength(3);
            entity.Property(v => v.ExchangeRate).HasPrecision(18, 6);
            entity.Property(v => v.PayableRelieved).HasPrecision(18, 2);

            // ‏خصمُ السداد المكتسَب — يُطفئ من الذمّة ما لم يخرج من الصندوق.
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
            // ‏**رمزُ تزامنٍ — وهو صنفُ عطلِ `ع-D1` بعينه في موضعٍ آخر.**
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
            // ‏نصّاً لا رقماً — كبقيّة تعدادات هذا المخطّط، وليُقرأ في
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

        // ‏رصيدُ الدفعة — تفصيلٌ لرصيد المخزون تربطه به الهويّةُ هـ-٩.
        modelBuilder.Entity<StockBatch>(entity =>
        {
            // ‏**رمزُ تزامنٍ — وهو صنفُ عطلِ `ع-D1` بعينه في موضعٍ آخر.**
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

        // ‏وحداتُ القياس — قالبٌ مبذورٌ لكلّ منشأة **قابلٌ للتعديل**.
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
    }
}
