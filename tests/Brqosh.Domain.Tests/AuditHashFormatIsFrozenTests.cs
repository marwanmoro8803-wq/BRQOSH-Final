using Brqosh.Domain.Security;

namespace Brqosh.Domain.Tests;

/// <summary>
/// ‏<b>صيغةُ تجزئة سجلّ التدقيق مُجمَّدة — وكانت محروسةً بلا شيء.</b>
///
/// <para>‏كلُّ اختبارٍ في <c>AuditChainTests</c> يحسب التجزئةَ ثمّ يقارنها بتجزئةٍ حسبها الكودُ
/// نفسُه في العمليّة نفسِها. <b>فلو تغيّر الفاصلُ أو ترتيبُ الحقول أو صيغةُ الوقت لبقيت كلُّها
/// خضراء — وسقط التحقّقُ من كلّ سلسلةٍ قائمةٍ في قواعد العملاء</b>، فيُقرأ سجلٌّ سليمٌ عبثاً،
/// ولا يُعرف السببُ لأنّ لا حقلَ تغيّر.</para>
///
/// <para>‏<b>ولم يكن الخطرُ نظريّاً:</b> الفاصلُ <c>U+001F</c> كان مكتوباً في المصدر <b>محرفاً خاماً
/// لا يُرى</b> داخل علامتَي المحرف. فمحرّرٌ يُطهّر محارفَ التحكّم، أو نسخٌ ولصقٌ عبر أداةٍ
/// تُسقطها، يُغيّر صيغةَ كلّ تجزئةٍ في المنتَج <b>بلا فرقٍ مرئيٍّ في الملفّ</b>.</para>
///
/// <para>‏<b>والقيمُ أدناه حُسبت خارج الكود</b> — بـ<c>hashlib</c> على الصيغة الموثّقة في
/// <see cref="AuditLogEntry.ComputeHash"/> — لا نسخاً لمخرَجه. فهي تقيس الكودَ لا تصفه.</para>
/// </summary>
public class AuditHashFormatIsFrozenTests
{
    private static readonly DateTime When = new(2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc);
    private static readonly string Previous = string.Concat(Enumerable.Repeat("AB", 32));

    [Fact]
    public void AnEntryWithAReason_HashesToTheFrozenValue()
    {
        var entry = At(AuditLogEntry.Record(7, "مدير", "إقفال سنوي", "FiscalYear", "2025",
            "قيد JV-2025-000001", "تصحيح"));
        entry.ChainTo(3, Previous);

        Assert.Equal("989095C74074E72B53504068BBFF09E3F9663E869F67124D9DB197A308FB0D87", entry.EntryHash);
        Assert.True(entry.ContentMatchesHash());
    }

    /// <summary>‏وسببٌ غائبٌ يُجزَّأ نصّاً فارغاً — لا يُسقَط حقلُه فتنزاح الحدود.</summary>
    [Fact]
    public void AnEntryWithoutAReason_HashesItAsEmpty()
    {
        var entry = At(AuditLogEntry.Record(7, "مدير", "إقفال سنوي", "FiscalYear", "2025",
            "قيد JV-2025-000001"));
        entry.ChainTo(3, Previous);

        Assert.Equal("25CC0137E945A6657C0593DA1E90AA48CB615FA36AA74B57438E61A8ABB278CF", entry.EntryHash);
    }

    [Fact]
    public void TheFirstEntry_ChainsToTheGenesis()
    {
        var entry = At(AuditLogEntry.Record(7, "admin", "Login", "User", "admin"));
        entry.ChainTo(1, "");

        Assert.Equal(AuditLogEntry.GenesisHash, entry.PreviousHash);
        Assert.Equal("EE31F631C4D85F2C709C4275E3AA6196884330E0246DEBF097FE24ADAB390B67", entry.EntryHash);
    }

    /// <summary>
    /// ‏الوقتُ يُكتب <c>UtcNow</c> داخل <see cref="AuditLogEntry.Record"/> ولا مدخلَ له — فيُثبَّت
    /// هنا بالانعكاس <b>قبل الربط</b>، لأنّ <c>ChainTo</c> هو من يحسب التجزئة.
    /// </summary>
    private static AuditLogEntry At(AuditLogEntry entry)
    {
        typeof(AuditLogEntry).GetProperty(nameof(AuditLogEntry.OccurredAtUtc))!.SetValue(entry, When);
        return entry;
    }
}
