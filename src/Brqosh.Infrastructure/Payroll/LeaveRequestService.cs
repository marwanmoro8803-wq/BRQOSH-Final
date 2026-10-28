using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>
/// (CEEF-233 · ز-٢) <b>تقديمُ طلبات الإجازة والبتُّ فيها.</b>
///
/// <para><b>والحارسان اللذان بغيرهما يصير السجلُّ زينةً:</b> <b>ألّا تتداخل إجازتان</b>،
/// <b>وألّا تُعتمد سنويّةٌ تتجاوز الرصيد</b>. وبغير الأوّل يُخصم يومٌ مرّتين أو يُستهلك
/// الرصيدُ ضِعفَ الغياب؛ <b>وبغير الثاني يُمنح ما لم يُستحقّ</b> — فيصير الرصيدُ سالباً،
/// <b>ويُطالَب الموظّفُ عند المغادرة بردّ أيّامٍ أُذن له فيها</b>.</para>
/// </summary>
public class LeaveRequestService(BrqoshDbContext db)
{
    /// <summary>
    /// يُقدّم طلباً — <b>ويُفحص التداخلُ عند التقديم وعند الاعتماد معاً</b>.
    /// </summary>
    /// <remarks>
    /// <b>وفحصٌ عند التقديم وحدَه لا يكفي</b>: طلبان يُقدَّمان معاً لا يرى أحدُهما الآخر
    /// (كلاهما معلَّق)، <b>ثمّ يُعتمدان فيتداخلان</b>. والاعتمادُ هو اللحظةُ الحاكمة.
    /// </remarks>
    public async Task<LeaveRequest> SubmitAsync(
        long companyId, long employeeId, LeaveType type,
        DateOnly fromDate, DateOnly toDate, string? reason = null,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var employee = await db.Employees
                .SingleOrDefaultAsync(e => e.CompanyId == companyId && e.Id == employeeId, token)
                ?? throw new AccountingException("الموظف غير موجود.");

            // ‏**ولا إجازةَ قبل التعيين ولا بعد انتهاء الخدمة** — وطلبٌ كهذا يُنتج
            // ‏استحقاقاً عن مدّةٍ لم يكن الموظّفُ فيها على رأس العمل.
            if (fromDate < employee.HireDate)
                throw new AccountingException("تاريخ الإجازة يسبق تاريخ التعيين.");
            if (employee.TerminationDate is DateOnly t && toDate > t)
                throw new AccountingException("تاريخ الإجازة يتجاوز تاريخ انتهاء الخدمة.");

            var number = await DocumentNumberGenerator.NextAsync(db,
                db.LeaveRequests.Where(r => r.CompanyId == companyId)
                    .Select(r => (string?)r.RequestNumber),
                "LV", fromDate, companyId);

            var request = LeaveRequest.Create(companyId, employeeId, number, type, fromDate, toDate, reason);

            await EnsureNoOverlapAsync(companyId, employeeId, request, token);
            await EnsureWithinStatutoryLimitAsync(companyId, employee, request, token);

            db.LeaveRequests.Add(request);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, SecurityContext.ResolveUsername(),
                "تقديم طلب إجازة", "LeaveRequest", request.RequestNumber,
                $"{type} · {fromDate:yyyy-MM-dd} ⟵ {toDate:yyyy-MM-dd} · {request.Days} يوماً", ct: token);

            return request;
        }, ct);
    }

    /// <summary>يعتمد الطلبَ — <b>بعد فحص التداخل والرصيد مرّةً أخرى</b>.</summary>
    public async Task<LeaveRequest> ApproveAsync(
        long companyId, long requestId, DateOnly on, string username,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var request = await db.LeaveRequests
                .SingleOrDefaultAsync(r => r.CompanyId == companyId && r.Id == requestId, token)
                ?? throw new AccountingException("طلب الإجازة غير موجود.");

            await EnsureNoOverlapAsync(companyId, request.EmployeeId, request, token);

            // ‏(ح-١١٧) ‏**والحدُّ النظاميُّ يُفحص عند الاعتماد أيضاً** — فطلبٌ قُدّم قبل الحارس لا يُعتمد بمدّته.
            var employee = await db.Employees
                .SingleAsync(e => e.CompanyId == companyId && e.Id == request.EmployeeId, token);
            await EnsureWithinStatutoryLimitAsync(companyId, employee, request, token);

            // ‏**والرصيدُ يُفحص للسنويّة وحدَها** — فالمرضيّةُ والوضعُ حقوقٌ لا تُقاس برصيد.
            if (LeaveEntitlementCalculator.ConsumesAnnualBalance(request.Type))
            {
                // ‏**ويُقاس الرصيدُ في يوم بداية الإجازة لا اليوم** — فاستحقاقٌ يتراكم
                // ‏حتى ذلك اليوم هو ما يملكه الموظّفُ حين يبدأها. **وقياسُه اليومَ يمنع
                // ‏إجازةً مستقبليّةً سيُستحقّ رصيدُها قبل بدئها.**
                var balance = await new LeaveBalanceQuery(db)
                    .ForEmployeeAsync(companyId, request.EmployeeId, request.FromDate, token);

                if (request.Days > balance.BalanceDays)
                    throw new AccountingException(
                        $"الرصيد لا يكفي — المتاح {balance.BalanceDays:N2} يوماً والمطلوب {request.Days}.");
            }

            request.Approve(on, username);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username,
                "اعتماد طلب إجازة", "LeaveRequest", request.RequestNumber,
                $"{request.Type} · {request.Days} يوماً", ct: token);

            return request;
        }, ct);
    }

    public async Task<LeaveRequest> RejectAsync(
        long companyId, long requestId, DateOnly on, string username,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var request = await db.LeaveRequests
                .SingleOrDefaultAsync(r => r.CompanyId == companyId && r.Id == requestId, token)
                ?? throw new AccountingException("طلب الإجازة غير موجود.");

            request.Reject(on, username);
            await db.SaveChangesAsync(token);
            return request;
        }, ct);
    }

    /// <summary>يُلغي الطلبَ — <b>ويُقبل بعد الاعتماد</b>، فإجازةٌ لم تُؤخذ تُردّ إلى الرصيد.</summary>
    public async Task<LeaveRequest> CancelAsync(
        long companyId, long requestId, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var request = await db.LeaveRequests
                .SingleOrDefaultAsync(r => r.CompanyId == companyId && r.Id == requestId, token)
                ?? throw new AccountingException("طلب الإجازة غير موجود.");

            request.Cancel();
            await db.SaveChangesAsync(token);
            return request;
        }, ct);
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١١٧ · ‏`treasury-payroll-assets#9`) <b>الإجازةُ المدفوعةُ بنصٍّ لا تتجاوز مدّتَها النظاميّة،
    /// والحجُّ مرّةً بعد سنتين.</b>
    /// </summary>
    /// <remarks>
    /// ‏هذه الأنواعُ تُصرف كاملةً — والدورةُ لا تخصم منها شيئاً — <b>فكانت «زواجٌ» بستّين يوماً أجرَ شهرين بلا عمل</b>.
    /// ‏والحجُّ المُحتسَبُ معلَّقٌ أو معتمَد، كالتداخل: المرفوضُ والملغى لم يُمنحا.
    /// </remarks>
    private async Task EnsureWithinStatutoryLimitAsync(
        long companyId, Employee employee, LeaveRequest request, CancellationToken ct)
    {
        if (LeaveEntitlementCalculator.StatutoryMaxDays(request.Type) is int max && request.Days > max)
            throw new AccountingException(
                $"المدّة النظاميّة لهذه الإجازة {max} يوماً والمطلوب {request.Days} — "
                + "ويُقدَّم الزائدُ طلباً بلا أجرٍ أو من الرصيد السنويّ.");

        if (request.Type != LeaveType.Hajj)
            return;

        if (request.FromDate < employee.HireDate.AddYears(LeaveEntitlementCalculator.HajjMinimumServiceYears))
            throw new AccountingException(
                $"إجازة الحجّ بعد سنتين متّصلتين من الخدمة — والتعيين في {employee.HireDate:yyyy-MM-dd}.");

        var earlier = await db.LeaveRequests
            .Where(r => r.CompanyId == companyId
                        && r.EmployeeId == employee.Id
                        && r.Id != request.Id
                        && r.Type == LeaveType.Hajj
                        && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved))
            .Select(r => r.RequestNumber)
            .FirstOrDefaultAsync(ct);
        if (earlier is not null)
            throw new AccountingException($"إجازة الحجّ مرّةً في الخدمة — وقد مُنحت بالطلب «{earlier}».");
    }

    /// <summary>
    /// <b>ولا تتداخل إجازتان مُحتسبتان لموظّفٍ واحد.</b>
    /// </summary>
    /// <remarks>
    /// <b>والمُحتسَبُ هو المعلَّقُ والمعتمَد معاً</b>: المرفوضُ والملغى لا يشغلان يوماً.
    /// <b>وإهمالُ المعلَّق يسمح بتقديم طلبين على المدّة نفسِها</b> ثمّ اعتمادِهما تباعاً —
    /// وكلٌّ منهما وحدَه سليم.
    /// </remarks>
    private async Task EnsureNoOverlapAsync(
        long companyId, long employeeId, LeaveRequest request, CancellationToken ct)
    {
        var others = await db.LeaveRequests
            .Where(r => r.CompanyId == companyId
                        && r.EmployeeId == employeeId
                        && r.Id != request.Id
                        && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved))
            .ToListAsync(ct);

        var clash = others.FirstOrDefault(r => r.OverlapsWith(request.FromDate, request.ToDate));
        if (clash is not null)
            throw new AccountingException(
                $"تتداخل مع الطلب «{clash.RequestNumber}» "
                + $"({clash.FromDate:yyyy-MM-dd} ⟵ {clash.ToDate:yyyy-MM-dd}).");
    }
}
