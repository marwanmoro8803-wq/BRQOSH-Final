using Brqosh.Domain.Accounting;
using Brqosh.Domain.RealEstate;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.RealEstate;

/// <summary>
/// (CEEF-230 · ط3-١) <b>تفعيلُ عقد الإيجار — والربطُ بالفوترة الدورية.</b>
///
/// <para><b>وهذا هو ما يجعل القطاعَ عاملاً</b>: العقدُ يُوقَّع مرّةً، و<c>ط1-٥</c> تُولّد
/// فواتيرَه شهراً بشهر بقيدِ تفرّدٍ يمنع فوترةَ الشهر مرّتين. <b>وبغير الربط يبقى العقدُ
/// ورقةً في جدول</b>.</para>
/// </summary>
public class LeaseService(BrqoshDbContext db)
{
    /// <summary>
    /// يُفعّل عقداً: يفحص التداخل، ويُنشئ خطةَ الفوترة، ويشغل الوحدة.
    ///
    /// <para><b>والثلاثةُ في معاملةٍ واحدة</b>: عقدٌ يُفعَّل بلا خطةٍ يُنتج وحدةً مؤجَّرةً
    /// لا تُفوتَر — <b>إيرادٌ يضيع بلا أثر</b>. وخطةٌ تُنشأ بلا عقدٍ مفعَّلٍ تُفوتِر
    /// مستأجراً لم يستلم.</para>
    /// </summary>
    public async Task<LeaseContract> ActivateAsync(
        LeaseContract lease, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var unit = await db.PropertyUnits
                .SingleOrDefaultAsync(u => u.Id == lease.PropertyUnitId
                                           && u.CompanyId == lease.CompanyId, token)
                ?? throw new AccountingException("الوحدة غير موجودة.");

            if (unit.Status == UnitStatus.Maintenance)
                throw new AccountingException("الوحدة في الصيانة — أخرِجْها قبل التأجير.");

            // ‏**والتداخلُ يُفحص بالتواريخ لا بحالة الوحدة**: عقدٌ ينتهي في ديسمبر لا يمنع
            // ‏عقداً يبدأ في يناير، **وحالةُ الوحدة لا تعرف التواريخ**. فالفحصُ على مدّة
            // ‏كلّ عقدٍ قائم — وتأجيرُ المؤجَّر يُنتج عقدين على شيءٍ واحدٍ ونزاعاً لا يحسمه
            // ‏النظام.
            var existing = await db.LeaseContracts
                .Where(l => l.CompanyId == lease.CompanyId
                            && l.PropertyUnitId == lease.PropertyUnitId
                            && l.Id != lease.Id
                            && l.Status == LeaseStatus.Active)
                .ToListAsync(token);

            var clash = existing.FirstOrDefault(l => l.OverlapsWith(lease.StartDate, lease.EndDate));
            if (clash is not null)
                throw new AccountingException(
                    $"الوحدة مؤجَّرةٌ بالعقد «{clash.ContractNumber}» "
                    + $"من {clash.StartDate:yyyy-MM-dd} إلى {clash.EndDate:yyyy-MM-dd} — "
                    + "ولا عقدان على وحدةٍ في مدّةٍ متداخلة.");

            var plan = RecurringInvoicePlan.Create(
                lease.CompanyId, lease.TenantCustomerId,
                $"LEASE-{lease.ContractNumber}",
                $"إيجار العقد {lease.ContractNumber}",
                lease.Frequency, lease.StartDate, lease.EndDate);

            plan.AddLine($"إيجار الوحدة {unit.UnitNumber}", 1m, lease.RentPerPeriod);

            db.RecurringInvoicePlans.Add(plan);
            if (lease.Id == 0) db.LeaseContracts.Add(lease);
            await db.SaveChangesAsync(token);

            lease.Activate(plan.Id, unit);
            await db.SaveChangesAsync(token);

            return lease;
        }, ct);
    }

    /// <summary>
    /// يُنهي عقداً ويُوقف خطةَ فوترته ويُخلي الوحدة.
    ///
    /// <para><b>وإيقافُ الخطة أهمُّ من إنهاء العقد</b>: عقدٌ يُنهى وخطتُه تعمل <b>يُفوتِر
    /// مستأجراً غادر</b> — ويُطالَب بمالٍ لا يستحقّ عليه.</para>
    /// </summary>
    public async Task TerminateAsync(long companyId, long leaseId, DateOnly on,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var lease = await db.LeaseContracts
                .SingleOrDefaultAsync(l => l.Id == leaseId && l.CompanyId == companyId, token)
                ?? throw new AccountingException("العقد غير موجود.");

            var unit = await db.PropertyUnits
                .SingleOrDefaultAsync(u => u.Id == lease.PropertyUnitId, token)
                ?? throw new AccountingException("وحدة العقد غير موجودة.");

            lease.Terminate(on, unit);

            if (lease.RecurringPlanId is long planId)
            {
                var plan = await db.RecurringInvoicePlans
                    .SingleOrDefaultAsync(p => p.Id == planId, token);
                plan?.End();
            }

            await db.SaveChangesAsync(token);
        }, ct);
    }
}
