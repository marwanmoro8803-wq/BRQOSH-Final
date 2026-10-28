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
    private void ConfigureSectors(ModelBuilder modelBuilder)
    {
        // ‏العقاراتُ والوحداتُ وعقودُ الإيجار.
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

        // ‏المرضى والوثائقُ والزيارات.
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

                // ‏دفعةُ الدواء المصروف — الطولُ نفسُه في كلّ مستند.
                lines.Property(l => l.BatchNumber).HasMaxLength(64);
            });

            entity.Navigation(v => v.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // ‏سُلَفُ الموظفين — أصلٌ يُستردّ بالاستقطاع.
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

        // ‏طلباتُ الإجازة.
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

        // ‏دفعاتُ المطالبات وقراراتُ الشركة عليها.
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

        // ‏أوامرُ الصيانة وسطورُ تكلفتها.
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

        // ‏أوعيةُ الإكراميات وتحصيلُها وتوزيعُها.
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

        // ‏رحلاتُ التوصيل وشحناتُها ومصاريفُها.
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

        // ‏قائمةُ الطعام ووصفاتُها.
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

        // ‏الغرفُ والحجوزُ وحساباتُ النزلاء.
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

        // ‏البوالص.
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
            // ‏أثرُ الشحنة — **سندُ التسليم لا زينةُ عرض**.
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
    }
}
