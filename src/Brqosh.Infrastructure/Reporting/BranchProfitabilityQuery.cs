using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>ربحيّةُ فرعٍ واحدٍ في الفترة.</summary>
public record BranchProfitRow(
    long? BranchId,
    string BranchNameAr,
    decimal Revenue,
    decimal Expenses)
{
    public decimal NetIncome => Revenue - Expenses;

    /// <summary>هامشُ الربح — <b>و<c>null</c> حين لا إيراد</b>.</summary>
    /// <remarks>
    /// <b>وصفرٌ مكانَ «لا يُحسب» كذبٌ صغير</b>: فرعٌ بمصروفاتٍ بلا إيرادٍ هامشُه ليس صفراً،
    /// <b>بل لا معنى له</b> — ويُقرأ الصفرُ «تعادلاً» وهو خسارةٌ كاملة.
    /// </remarks>
    public decimal? Margin => Revenue == 0m ? null
        : Math.Round(NetIncome * 100m / Revenue, 2, MidpointRounding.AwayFromZero);
}

/// <summary>حصيلةُ التقرير — <b>والمجموعُ يُطابق قائمةَ الدخل</b>.</summary>
public record BranchProfitabilityResult(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<BranchProfitRow> Rows)
{
    public decimal TotalRevenue => Rows.Sum(r => r.Revenue);
    public decimal TotalExpenses => Rows.Sum(r => r.Expenses);
    public decimal NetIncome => TotalRevenue - TotalExpenses;
}

/// <summary>
/// <b>ربحيّةُ الفروع — والبيانات كانت كلُّها موجودةً ولا يقرؤها تقرير.</b>
///
/// <para><b>ما كان:</b> كلُّ مستندٍ يحمل <c>BranchId</c>، <b>وكلُّ قيدٍ كذلك</b>، ومنتقي
/// الفرع في الجلسة مبنيٌّ منذ <c>CEEF-024</c>. <b>وصفرُ تقريرٍ في المستودع كلِّه يُرشِّح
/// بالفرع.</b> فمنشأةٌ بثلاثة فروعٍ تعرف ربحَها مجموعاً <b>ولا تعرف أيَّ فرعٍ يكسبه</b> —
/// وقد يكون فرعٌ واحدٌ يحمل الثلاثة، أو فرعٌ يخسر ويُخفيه مجموعُ إخوته.</para>
///
/// <para><b>والقرارُ المُعلَن — هذا تقريرٌ لا إقفال.</b> ورد البندُ في الخطة «الإقفالُ
/// متعدّد الفروع»، <b>والإقفالُ بطبيعته شركةٌ لا فرع</b>: الأرباحُ المرحَّلة حسابُ حقوق
/// ملكيةٍ للمنشأة، <b>وإقفالُ كلّ فرعٍ إلى أرباحه المرحَّلة يُنشئ ثلاثةَ كيانات محاسبية
/// حيث توجد واحدة</b> — وذلك دمجُ منشآتٍ لا إقفالُ فروع، وله مسارُه في <c>ط4-١</c>.
/// <b>فما تحتاجه المنشأةُ متعدّدةُ الفروع هو أن ترى أداءَ كلّ فرع</b>، لا أن تُقفل كلَّ
/// فرعٍ على حدة.</para>
///
/// <para><b>وهويّةُ هـ-٢٢:</b> مجموعُ صافي الفروع = صافي قائمة الدخل للفترة نفسِها،
/// <b>هللةً بهللة</b>. وكسرُها يعني أنّ قيداً ضاع من التوزيع — <b>وتقريرُ فروعٍ لا يجمع
/// إلى الكلّ يُقرأ خطأً في أحد الفرعين ولا يُعرف أيُّهما</b>.</para>
///
/// <para><b>وما لا فرعَ له يُعرض صفّاً باسمه</b> — لا يُطوى ولا يُوزَّع. فقيدٌ بلا فرعٍ
/// موجودٌ في كلّ قاعدةٍ رُقّيت من نسخةٍ سابقة لإسناد الفروع، <b>وطيُّه يكسر هـ-٢٢</b>،
/// <b>وتوزيعُه بالتناسب يخترع أرقاماً لا سندَ لها</b>.</para>
/// </summary>
public class BranchProfitabilityQuery(BrqoshDbContext db)
{
    public async Task<BranchProfitabilityResult> ExecuteAsync(
        long companyId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        // ‏**وقيودُ الإقفال السنويّ تُستبعَد** — كما تفعل قائمةُ الدخل: التقريرُ يعرض
        // ‏**أداءَ الفترة** لا أثرَ قيدِ إقفالها. وإدراجُه يُصفّر السنةَ المقفلة.
        var reportable = db.JournalEntries
            .WhereReportable(companyId, from, to)
            .ExcludeYearEndClosing();

        var branchIds = await reportable
            .Select(e => e.BranchId)
            .Distinct()
            .ToListAsync(ct);

        if (branchIds.Count == 0)
            return new BranchProfitabilityResult(from, to, []);

        var branches = await db.Branches.AsNoTracking().IgnoreQueryFilters()
            .Where(b => b.CompanyId == companyId)
            .ToDictionaryAsync(b => b.Id, b => b.NameAr, ct);

        var rows = new List<BranchProfitRow>();

        // ‏**واستعلامٌ لكلّ فرعٍ لا استعلامٌ واحدٌ بضمّ** — والفروعُ عددٌ صغير.
        //
        // ‏وإسنادُ `BranchId` داخل `SelectMany` يُترجَم إلى `APPLY`، **و SQLite لا
        // ‏يدعمها** — فيسقط التقريرُ على قاعدةٍ محلّية ويعمل على PostgreSQL.
        // ‏**وتقريرٌ يعمل على محرّكٍ دون آخر عطلٌ لا خيار**، وهذا الشكلُ هو عينُ ما
        // ‏يستعمله `TrialBalanceQuery` منذ أوّله.
        foreach (var branchId in branchIds)
        {
            var totals = await reportable
                .Where(e => e.BranchId == branchId)
                .SelectMany(e => e.Lines)
                .GroupBy(l => l.AccountId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    Debit = g.Sum(l => l.Debit),
                    Credit = g.Sum(l => l.Credit),
                })
                .ToListAsync(ct);

            if (totals.Count == 0) continue;

            var accountIds = totals.Select(t => t.AccountId).ToList();

            // ‏**والحساباتُ الموقوفةُ تُقرأ** — تقريرٌ تاريخيٌّ يرى ما أُوقف بعد حركته،
            // ‏وإسقاطُها يُسقط قيوداً مرحَّلةً فتنكسر هـ-٢٢.
            var accounts = await db.Accounts.IgnoreQueryFilters().AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Type, ct);

            decimal revenue = 0m, expenses = 0m;

            foreach (var total in totals)
            {
                if (!accounts.TryGetValue(total.AccountId, out var type)) continue;

                if (type == AccountType.Revenue)
                    revenue += total.Credit - total.Debit;
                else if (type == AccountType.Expense)
                    expenses += total.Debit - total.Credit;
            }

            if (revenue == 0m && expenses == 0m) continue;

            rows.Add(new BranchProfitRow(
                branchId,
                branchId is long id && branches.TryGetValue(id, out var name) ? name : "بلا فرع",
                Math.Round(revenue, 2, MidpointRounding.AwayFromZero),
                Math.Round(expenses, 2, MidpointRounding.AwayFromZero)));
        }

        rows = [.. rows.OrderByDescending(r => r.NetIncome)];

        return new BranchProfitabilityResult(from, to, rows);
    }
}
