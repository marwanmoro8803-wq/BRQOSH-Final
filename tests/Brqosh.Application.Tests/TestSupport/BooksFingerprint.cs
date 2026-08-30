using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (CEEF-229 · م3-٢ · ح-٦) <b>بصمةُ الدفاتر — ما يجب أن يعود كما كان بعد الاستعادة.</b>
///
/// <para><b>ولماذا لا يكفي «الملفّ أُنشئ»:</b> أشهرُ أعطال النسخ الاحتياطي ليس فشلَ النسخ
/// بل <b>نسخةٌ لا تُستعاد</b>: جدولٌ سقط، أو تسلسلٌ لم يُنقل فتكرّرت المفاتيح، أو نسخةٌ
/// أُخذت وسط معاملة. وكلُّها تمرّ على فحصٍ يسأل «هل وُجد الملفّ؟» — ولا تُكتشف إلا يوم
/// الحاجة، حين لا يبقى أصلٌ يُقارَن به.</para>
///
/// <para>فالبصمةُ تقيس <b>ما يراه المحاسب</b>: عددُ المستندات، ومجموعُ المدين والدائن،
/// وآخرُ رقمٍ في كلّ تسلسل. ومطابقتُها قبل الإتلاف وبعد الاستعادة هي الهوية <b>ح-٦</b>.</para>
///
/// <para><b>وآخرُ رقمٍ في التسلسل مقصودٌ خاصّة:</b> استعادةٌ تُعيد الصفوف وتنسى تسلسلات
/// القاعدة تُنتج نظاماً يعمل دقيقةً ثمّ يصطدم بمفتاحٍ مكرَّر عند أوّل مستندٍ جديد — وهو
/// عطلٌ لا تكشفه مقارنةُ الصفوف وحدها.</para>
/// </summary>
internal static class BooksFingerprint
{
    internal sealed record Snapshot(
        int Companies, int Accounts, int Customers, int JournalEntries, int JournalLines,
        int SalesInvoices, decimal TotalDebit, decimal TotalCredit,
        string? LastEntryNumber, string? LastInvoiceNumber)
    {
        internal string Describe() =>
            $"شركات {Companies} · حسابات {Accounts} · عملاء {Customers} · "
            + $"قيود {JournalEntries} ({JournalLines} سطراً) · فواتير {SalesInvoices} · "
            + $"مدين {TotalDebit:N2} · دائن {TotalCredit:N2} · "
            + $"آخر قيد «{LastEntryNumber ?? "—"}» · آخر فاتورة «{LastInvoiceNumber ?? "—"}»";
    }

    internal static async Task<Snapshot> TakeAsync(BrqoshDbContext db)
    {
        var lines = await db.JournalEntries.SelectMany(e => e.Lines)
            .Select(l => new { l.Debit, l.Credit }).ToListAsync();

        return new Snapshot(
            Companies: await db.Companies.IgnoreQueryFilters().CountAsync(),
            Accounts: await db.Accounts.IgnoreQueryFilters().CountAsync(),
            Customers: await db.Customers.IgnoreQueryFilters().CountAsync(),
            JournalEntries: await db.JournalEntries.IgnoreQueryFilters().CountAsync(),
            JournalLines: lines.Count,
            SalesInvoices: await db.SalesInvoices.IgnoreQueryFilters().CountAsync(),
            TotalDebit: lines.Sum(l => l.Debit),
            TotalCredit: lines.Sum(l => l.Credit),
            LastEntryNumber: await db.JournalEntries.IgnoreQueryFilters()
                .Where(e => e.EntryNumber != null)
                .OrderByDescending(e => e.EntryNumber).Select(e => e.EntryNumber).FirstOrDefaultAsync(),
            LastInvoiceNumber: await db.SalesInvoices.IgnoreQueryFilters()
                .Where(i => i.InvoiceNumber != null)
                .OrderByDescending(i => i.InvoiceNumber).Select(i => i.InvoiceNumber).FirstOrDefaultAsync());
    }
}
