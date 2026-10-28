using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Logistics;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Logistics;

/// <summary>ربحيّةُ مسارٍ — <b>وهي الرقمُ الذي بُني هذا الباب لإظهاره</b>.</summary>
public record RouteProfitability(
    string RouteName,
    int Trips,
    int Shipments,
    decimal FreightRevenue,
    decimal TripCost,
    decimal Margin);

/// <summary>
/// <b>عهدةُ الرحلة ومصاريفُها وربحيّةُ المسار.</b>
///
/// <para><b>والعهدةُ أصلٌ لا مصروف حتى تُسوَّى</b>: تسليمُها يُنقل النقدَ من الصندوق إلى
/// <c>1117</c> — <b>ولا يمسّ المصروفَ بريال</b>. فبغير ذلك تُصرف العهدةُ كلُّها مصروفاً
/// يومَ تسليمها، <b>فتُضخَّم تكلفةُ المسار بما سيعود</b>، ويختفي النقدُ العائد.</para>
///
/// <para><b>وهويّةُ هـ-٢٠ تُقاس على الحساب</b>: بعد التسوية <b>يعود رصيدُ <c>1117</c>
/// للرحلة إلى صفر</b> — ما أُنفق صار مصروفاً وما أُعيد عاد إلى الصندوق. <b>وبقاءُ رصيدٍ
/// فيه بعد التسوية هو الفرقُ الذي يمنعه النطاق</b>.</para>
///
/// <para><b>وربحيّةُ المسار تُشتقّ من المستندات</b>: أجورُ شحناتِ الرحلة من البوالص،
/// ومصاريفُها من سطورها. <b>ورقمٌ مخزَّنٌ كان سينحرف</b> عند أوّل إلغاءِ شحنةٍ أو تعديلِ
/// مصروف.</para>
/// </summary>
public class DeliveryTripService(BrqoshDbContext db)
{
    /// <summary>حسابُ مصاريف تشغيل الرحلات — <b>والتفصيلُ بالباب على سطر الرحلة</b>.</summary>
    public const string TripExpenseCode = "5206";

    /// <summary>
    /// يُسلّم العهدةَ للسائق — <b>نقلُ أصلٍ لا صرفُ مصروف</b>.
    /// </summary>
    public async Task<long> AdvanceFloatAsync(long companyId, long tripId, decimal amount,
        DateOnly on, string cashAccountCode = SaudiChartOfAccounts.SystemCodes.CashOnHand,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var trip = await LoadAsync(companyId, tripId, token);
            trip.AdvanceFloat(amount);

            var floats = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.TripCashFloat, token);
            var cash = await ResolveAsync(companyId, cashAccountCode, token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, on, PostingDocumentKind.JournalEntry,
                accountIds: [floats.Id, cash.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, on,
                $"عهدةُ رحلة {trip.TripNumber} — {trip.RouteName}",
                sourceDocument: $"TripFloat:{trip.TripNumber}");
            entry.AddLine(floats, trip.DriverFloat, 0m, $"عهدةُ السائق — {trip.VehiclePlate}");
            entry.AddLine(cash, 0m, trip.DriverFloat, $"تسليمُ عهدةِ رحلة {trip.TripNumber}");

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            return entry.Id;
        }, ct);
    }

    /// <summary>
    /// يُسوّي العهدةَ ويُقيّد المصاريف.
    /// </summary>
    /// <remarks>
    /// <para><b>والقيدُ يُفرغ <c>1117</c> بالضبط</b>: مدينٌ للمصروف بما أُنفق من العهدة،
    /// ومدينٌ للصندوق بما أُعيد، <b>ودائنٌ للعهدة بكاملها</b>. وما دفعته الشركةُ مباشرةً
    /// يُقيَّد مصروفاً في مقابل الصندوق <b>ولا يمرّ بالعهدة</b> — وإلا لَظهر فيها التزامٌ
    /// على السائق بما لم يأخذه.</para>
    ///
    /// <para><b>والنطاقُ يحرس هـ-٢٠ قبل أن يصل القيدُ أصلاً</b> — فلا يُقيَّد فرقٌ
    /// صامت.</para>
    /// </remarks>
    public async Task<long> SettleAsync(long companyId, long tripId, decimal returnedCash,
        DateOnly on, string cashAccountCode = SaudiChartOfAccounts.SystemCodes.CashOnHand,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var trip = await LoadAsync(companyId, tripId, token);
            trip.SettleFloat(returnedCash);

            var floats = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.TripCashFloat, token);
            var expense = await ResolveAsync(companyId, TripExpenseCode, token);
            var cash = await ResolveAsync(companyId, cashAccountCode, token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, on, PostingDocumentKind.JournalEntry,
                accountIds: [floats.Id, expense.Id, cash.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, on,
                $"تسويةُ رحلة {trip.TripNumber} — {trip.RouteName}",
                sourceDocument: $"TripSettle:{trip.TripNumber}");

            if (trip.SpentFromFloat > 0m)
                entry.AddLine(expense, trip.SpentFromFloat, 0m, "مصاريفُ رحلةٍ من العهدة");
            if (trip.ReturnedCash > 0m)
                entry.AddLine(cash, trip.ReturnedCash, 0m, "ما أُعيد من العهدة");
            if (trip.DriverFloat > 0m)
                entry.AddLine(floats, 0m, trip.DriverFloat, $"إقفالُ عهدةِ رحلة {trip.TripNumber}");

            // ‏**وما دفعته الشركةُ مباشرةً لا يمرّ بالعهدة** — مصروفٌ في مقابل الصندوق.
            var companyPaid = Math.Round(trip.TotalExpenses - trip.SpentFromFloat, 2,
                MidpointRounding.AwayFromZero);
            if (companyPaid > 0m)
            {
                entry.AddLine(expense, companyPaid, 0m, "مصاريفُ رحلةٍ على حساب الشركة");
                entry.AddLine(cash, 0m, companyPaid, "دفعُ مصاريفِ رحلةٍ مباشرةً");
            }

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            trip.LinkJournalEntry(entry.Id);
            await db.SaveChangesAsync(token);

            return entry.Id;
        }, ct);
    }

    /// <summary>
    /// ربحيّةُ المسارات في مدّة — <b>وهو الرقمُ الذي كان مستحيلاً قبل هذا الباب</b>.
    /// </summary>
    /// <remarks>
    /// <b>والرحلةُ الملغاةُ لا تدخل</b>: لم تُنفَّذ، <b>وإدخالُها يُنقص متوسّطَ ربح المسار
    /// بمصاريفَ لم تقع</b> — أو يرفعه بشحناتٍ لم تُنقل.
    /// </remarks>
    public async Task<IReadOnlyList<RouteProfitability>> RouteProfitabilityAsync(long companyId,
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var trips = await db.DeliveryTrips
            .Where(t => t.CompanyId == companyId
                        && t.Status != TripStatus.Cancelled
                        && t.DepartureDate >= from && t.DepartureDate <= to)
            .ToListAsync(ct);

        if (trips.Count == 0) return [];

        var shipmentIds = trips.SelectMany(t => t.Shipments.Select(s => s.ShipmentId))
            .Distinct().ToList();

        var freight = await db.Shipments
            .Where(s => s.CompanyId == companyId && shipmentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.FreightCharge })
            .ToDictionaryAsync(s => s.Id, s => s.FreightCharge, ct);

        return [.. trips
            .GroupBy(t => t.RouteName)
            .Select(g =>
            {
                var revenue = Math.Round(g.SelectMany(t => t.Shipments)
                    .Sum(s => freight.GetValueOrDefault(s.ShipmentId)), 2,
                    MidpointRounding.AwayFromZero);
                var cost = Math.Round(g.Sum(t => t.TotalExpenses), 2, MidpointRounding.AwayFromZero);

                return new RouteProfitability(g.Key, g.Count(),
                    g.Sum(t => t.Shipments.Count), revenue, cost,
                    Math.Round(revenue - cost, 2, MidpointRounding.AwayFromZero));
            })
            .OrderBy(r => r.Margin)];
    }

    private async Task<DeliveryTrip> LoadAsync(long companyId, long tripId, CancellationToken ct) =>
        await db.DeliveryTrips
            .SingleOrDefaultAsync(t => t.Id == tripId && t.CompanyId == companyId, ct)
        ?? throw new AccountingException("الرحلة غير موجودة.");

    private async Task<Account> ResolveAsync(long companyId, string code, CancellationToken ct) =>
        await db.Accounts.SingleOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct)
        ?? throw new AccountingException($"الحساب «{code}» غير موجود.");
}
