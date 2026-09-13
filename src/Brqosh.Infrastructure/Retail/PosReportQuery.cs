using System.Globalization;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Retail;

/// <summary>
/// (CEEF-230 · ط6-٨) <b>ملخّصُ نقطة البيع — وكان يعرض ما لم يمرّ بالدرج.</b>
///
/// <para><b>والشاشةُ تَعِد بنصّها</b>: «ملخص يومي لمعاملات نقاط البيع». <b>والاستعلامُ
/// كان يقرأ كلَّ فواتير المبيعات المرحّلة</b> — فتدخل مبيعاتُ الآجل للتجّار في ملخّص
/// الدرج.</para>
///
/// <para><b>وأثرُه أنّ مطابقةَ الصندوق لا تنجح أبداً</b>: أمينُ الصندوق يعدّ ما في الدرج
/// ويقارنه بالتقرير، <b>والفرقُ فاتورةُ عميلٍ آجلةٍ لم تمرّ بالدرج قطّ</b> — فيُطارَد
/// فرقٌ لا وجودَ له، <b>أو يُتَّهم أمينُ الصندوق بعجزٍ ليس منه</b>.</para>
///
/// <para><b>وتمييزُها موجودٌ منذ البداية</b>: <c>PosShiftId</c> على الفاتورة — <b>ولم
/// يكن يُقرأ</b>. وكشفُ الحساب يستعمله منذ <c>م0-٩</c>، فالاصطلاحُ قائمٌ وهذا خرقُه.</para>
/// </summary>
public class PosReportQuery(BrqoshDbContext db)
{
    public record DailySaleRow(string Date, int Transactions, decimal TotalSales, decimal VatAmount);

    public async Task<IReadOnlyList<DailySaleRow>> DailySummaryAsync(long companyId, int days = 30,
        CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-days));
        var invoices = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.InvoiceDate >= from
                        && i.Status == SalesInvoiceStatus.Posted
                        && i.PosShiftId != null)
            .ToListAsync(ct);

        return invoices
            .GroupBy(i => i.InvoiceDate)
            .OrderByDescending(g => g.Key)
            // ‏(م-٥ · ن-٤٧) **بثقافةٍ صريحة.** هذا النصُّ يُطبع في PDF كما هو
            // ‏(`PosReportPdfGenerator`) ويُعرض في شبكة نقاط البيع — **فالاستعلامُ هو من
            // ‏يقرّر التقويم**. وبلا ثقافةٍ يتبع إعدادَ الجهاز: على ثقافةٍ تقويمُها أمُّ
            // ‏القرى يصير «1447-07-16»، وعلى البوذيّ «2569-01-05».
            //
            // ‏**والأصحُّ معماريّاً ألّا يُنسّق الاستعلامُ أصلاً** بل يحمل `DateOnly`
            // ‏ويُنسّق كلُّ مُستهلِكٍ لنفسه. **ولم يُفعَل هنا عمداً**: الشبكةُ تربط
            // ‏`{Binding Date}` بلا `StringFormat`، فتحويلُه إلى `DateOnly` **ينقل العطلَ
            // ‏إلى الشاشة** بدل أن يرفعه. فيُثبَّت النصُّ هنا، ويبقى تصحيحُ الطبقات بنداً
            // ‏مستقلّاً مع بقيّة سطح المكتب.
            .Select(g => new DailySaleRow(
                g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.Count(),
                g.Sum(i => i.GrandTotal()),
                g.Sum(i => i.VatAmount)))
            .ToList();
    }
}
