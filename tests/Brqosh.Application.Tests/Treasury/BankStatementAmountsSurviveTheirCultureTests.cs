using System.Globalization;
using System.Text.RegularExpressions;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Treasury;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// ‏(م-٥ · ن-٤٨) <b>كشفُ حسابٍ بنكيٌّ يدخل الدفترَ برقمٍ آخر — بلا خطأٍ ولا تحذير.</b>
///
/// <para><b>وثلاثةُ أسبابٍ تتراكب، والبندُ سمّى واحداً:</b>
/// <list type="number">
/// <item><c>NumberStyles.Any</c> يحمل <c>AllowThousands</c> و<c>AllowExponent</c> —
/// فـ<c>«1,5»</c> تُقرأ <b>15</b> و<c>«12,34»</c> تُقرأ <b>1234</b> و<c>«1e3»</c> تُقرأ <b>1000</b>.</item>
/// <item><c>Replace(",", "")</c> — وهو وحدَه من يصنع <c>«1.234,56»</c> ⟵ <b>1.23456</b>.</item>
/// <item><b>والقاسمةُ نفسُها</b>: فاصلةٌ غيرُ مُقتبَسةٍ داخل المبلغ تُشقّ الحقلَ عمودَين،
/// فـ<c>«1,234.56»</c> — <b>وهي الصيغةُ المحلّيّةُ لا الأجنبيّة</b> — تُقرأ <b>1</b>.</item>
/// </list></para>
///
/// <para><b>ولمَ يبلغ ذلك الدفتر:</b> المبلغُ الخاطئ لا يُطابق قيدَ الدفتر (المطابقةُ بالمبلغ
/// حرفيّاً)، <b>وعدمُ المطابقة هو بعينِه شرطُ «رحّل كفروق»</b> — فيُنشئ
/// <c>BankStatementDifferencePostingService</c> قيداً مُرحَّلاً بالرقم الخاطئ. <b>فالعطلُ
/// يُساق إلى الدفتر، ولا ينتظر فيه.</b></para>
///
/// <para><b>ولا يُستند إلى «الريال منزلتان»</b>: <c>BankAccount.SetCurrency</c> يقبل أيَّ
/// ثلاثةِ حروف، والمنتجُ يشحن ملفّاتِ ضريبةٍ لـ<b>البحرين والكويت وعُمان</b>
/// (<c>VatJurisdictionProfile.cs:19-21</c>) — <b>وعملاتُها ثلاثُ منازل</b>. فقاعدةُ هذا
/// الحارس لا تُحصي المنازل: <b>لا يُحوَّل إلى رقمٍ أيُّ حقلٍ فيه فاصلة</b>، وهي خاصّيّةٌ
/// تُرى بالنظر ولا تكسرها عملة.</para>
/// </summary>
public class BankStatementAmountsSurviveTheirCultureTests
{
    private const string GoodRow = "2026-07-02,سليم,100.00,REF-OK";
    private const string Header = "Date,Description,Amount,Reference";

    private static string FileWith(string amountCell) =>
        $"{Header}\n2026-07-01,محلُّ الشاهد,{amountCell},REF-1\n{GoodRow}";

    // ── الطبقة ١: ما يجب أن يُقرأ، ويبقى مقروءاً بعد الإصلاح ──────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب قبل كلِّ رفض.</b> حارسٌ يرفض كلَّ شيءٍ ليس حارساً، <b>وهذه
    /// الصيغُ تُستورد اليوم بصوابٍ ويجب ألّا يكسرها الإصلاح</b> — ومنها مقياسُ
    /// <c>decimal(18,4)</c> الذي تُصدّره أنظمةُ البنوك الأساسيّة.
    /// </summary>
    [Theory]
    [InlineData("1234.56", 1234.56)]
    [InlineData("-1234.56", -1234.56)]
    [InlineData("100", 100)]
    [InlineData("1234.5600", 1234.56)]   // ‏مقياسُ تصديرٍ بأربع منازل — يُقرأ ويُقرَّب
    [InlineData("100.0000", 100)]
    [InlineData("(500)", -500)]          // ‏السالبُ بالقوسين — معنًى واحدٌ في كلّ لغة
    [InlineData("+250.75", 250.75)]
    [InlineData("0.05", 0.05)]
    public void AnUnambiguousAmount_IsRead(string cell, decimal expected)
    {
        var lines = BankStatementCsvParser.Parse(FileWith(cell));

        Assert.Equal(2, lines.Count);
        Assert.Equal(expected, lines[0].SignedAmount);
        Assert.Equal(100m, lines[1].SignedAmount);
    }

    // ── الطبقة ٢: ما يجب أن يُرفَض — ومعه صفٌّ سليمٌ يُثبت أنّ الرفضَ للمبلغ ──

    /// <summary>
    /// <b>كلُّ حالةٍ هنا تحمل صفّاً سليماً ثانياً</b>، ورسالةُ الاستثناء تُطالَب بذكر
    /// <b>نصِّ الحقل المُخالف</b>. <b>وبدون هذين يمرّ «إصلاحٌ» يرمي على كلِّ ملفّ</b>:
    /// فرميةٌ لأنّ الملفَّ خلا من سطورٍ صالحةٍ تُرضي اختبارَ نوعٍ ولا تُرضي اختبارَ هويّة.
    /// </summary>
    [Theory]
    [InlineData("1.234,56")]   // ‏أوروبيّ — يُقرأ اليوم 1.23456
    [InlineData("1,5")]        // ‏عشريّةٌ بفاصلة — تُقرأ اليوم 15
    [InlineData("1,50")]       // ‏تُقرأ اليوم 150
    [InlineData("12,34")]      // ‏تُقرأ اليوم 1234
    [InlineData("1,5,5")]      // ‏تُقرأ اليوم 155
    [InlineData("1,,234")]     // ‏تُقرأ اليوم 1234
    [InlineData("0,123")]      // ‏لا قراءةَ أمريكيّةً لها أصلاً — تُقرأ اليوم 123
    [InlineData("1e3")]        // ‏أُسّيّ — يُقرأ اليوم 1000
    public void AnAmbiguousOrExoticAmount_IsRefusedByName(string cell)
    {
        // ‏الحقلُ مُقتبَسٌ ليصل كاملاً إلى المحلّل بدل أن تشقّه الفاصلة.
        var csv = $"{Header}\n2026-07-01,محلُّ الشاهد,\"{cell}\",REF-1\n{GoodRow}";

        var ex = Assert.Throws<AccountingException>(() => BankStatementCsvParser.Parse(csv));

        Assert.Contains(cell, ex.Message, StringComparison.Ordinal);
    }

    // ── الطبقة ٣: القاسمةُ نفسُها — وهي نصفُ العطل الذي لا تراه قواعدُ الحقل ──

    /// <summary>
    /// <b>الصيغةُ المحلّيّةُ تُفسَد قبل الأجنبيّة.</b> <c>«1,234.56»</c> بلا اقتباسٍ تُشقّ
    /// عمودَين، فيصير المبلغُ <b>1</b> ويهبط <c>«234.56»</c> في عمودٍ تالٍ.
    ///
    /// <para><b>وعددُ الأعمدة وحدَه لا يكشفها</b>: مع ترويسةٍ رباعيّةٍ — وهي ترويسةُ ثلاثٍ
    /// من أربع عيّناتٍ في هذا المستودع نفسِه — يصير عددُ الخلايا ٤ = ٤، <b>فيمرّ أيُّ
    /// حارسٍ يقيس العرض</b>. فالتقاطُها بأن يكون المبلغُ عدداً صحيحاً والخليّةُ التاليةُ
    /// كسراً عارياً — <b>وهي بصمةُ حقلٍ مشقوق</b>.</para>
    /// </summary>
    [Theory]
    [InlineData("Date,Description,Amount", "2026-07-01,حوالة,1,234.56")]
    [InlineData("Date,Description,Amount,Reference", "2026-07-01,حوالة,1,234.56")]
    [InlineData("Date,Description,Debit,Credit", "2026-07-01,حوالة,1,234.56,0")]
    public void AnUnquotedSeparatorThatSplitsTheAmount_IsRefused(string header, string row)
    {
        var csv = $"{header}\n{row}\n2026-07-02,سليم,100.00" + (header.Contains("Credit") ? ",0" : "");

        var ex = Assert.Throws<AccountingException>(() => BankStatementCsvParser.Parse(csv));

        // ‏**الصيغةُ المفصولةُ بـ«|» يُصدرها الحارسُ البنيويُّ وحدَه.** وأوّلُ صياغةٍ هنا
        // ‏كانت `Contains("234.56")` — **وكانت تمرّ بلا سبب**، لأنّ رسالةَ رفضِ الحقل
        // ‏تنتهي بمثالٍ فيه `1234.56` و«234.56» جزءٌ منه. فحارسٌ يُرضيه نصُّ المساعدة
        // ‏لا يقيس شيئاً.
        Assert.Contains("| 234.56", ex.Message, StringComparison.Ordinal);
    }

    // ── الطبقة ٤: التاريخ — النصفُ الآخرُ من التسرّب الثقافيّ ────────────────

    /// <summary>
    /// <b>وتاريخٌ يُقلب يوماً بشهرٍ يعبر حدَّ فترةٍ ماليّة.</b> المحلّلُ يقرأ بنمط
    /// <c>InvariantCulture</c> وهو <c>MM/dd/yyyy</c>، <b>فـ«05/01/2026» تصير ١ مايو</b>
    /// لا ٥ يناير — صامتةً لكلّ يومٍ ≤ ١٢، وصاخبةً لما فوقه. <b>وذلك أسوأُ خليط</b>:
    /// النصفُ الصاخبُ يُعلّم المُشغّلَ أنّ المحلّلَ يفحص التواريخ.
    /// </summary>
    [Fact]
    public void AnAmbiguousDate_IsNeverSilentlyTransposed()
    {
        var csv = $"{Header}\n05/01/2026,حوالة,100.00,REF-1\n{GoodRow}";

        var thrown = Record.Exception(() => BankStatementCsvParser.Parse(csv));

        if (thrown is null)
        {
            var lines = BankStatementCsvParser.Parse(csv);
            Assert.Fail(
                "‏«05/01/2026» قُبِلت صامتةً وقُرئت "
                + lines[0].TransactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + " — والملفُّ لا يقول أيُّهما اليومُ وأيُّهما الشهر. يُرفَض أو يُصرَّح بالصيغة.");
        }

        Assert.IsType<AccountingException>(thrown);
        Assert.Contains("05/01/2026", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>‏شاهدُ ضبطٍ موجب: التاريخُ الصريحُ يبقى مقروءاً — فالرفضُ ليس شاملاً.</summary>
    [Theory]
    [InlineData("2026-07-01")]
    [InlineData("2026/07/01")]
    public void AnIsoDate_IsStillRead(string date)
    {
        var lines = BankStatementCsvParser.Parse($"{Header}\n{date},حوالة,100.00,REF-1\n{GoodRow}");

        Assert.Equal(new DateOnly(2026, 7, 1), lines[0].TransactionDate);
    }

    // ── الطبقة ٥: اختبارٌ ذاتيٌّ للحارس — أيميّز أصلاً؟ ──────────────────────

    /// <summary>
    /// <b>وحارسٌ يقرأ كوداً صحيحاً لا يُثبت أنّه يرفض كوداً خاطئاً.</b> فتُعاد هنا
    /// <b>خوارزميّةُ ما قبل الإصلاح حرفيّاً</b> في الذاكرة، ويُطالَب جدولُ الرفض بأن
    /// <b>يسقط عليها</b>. فلو كُتبت التوقّعاتُ بحيث تُرضيها الخوارزميّةُ القديمة لكان
    /// الحارسُ زينةً تُوقَّع خضراء.
    /// </summary>
    [Theory]
    [InlineData("1.234,56", 1.23456)]
    [InlineData("1,5", 15)]
    [InlineData("1,50", 150)]
    [InlineData("12,34", 1234)]
    [InlineData("1,5,5", 155)]
    [InlineData("1,,234", 1234)]
    [InlineData("0,123", 123)]
    [InlineData("1e3", 1000)]
    public void ThePreFixAlgorithm_AcceptsExactlyWhatTheRejectTableForbids(string cell, decimal preFixValue)
    {
        Assert.True(PreFixParseDecimal(cell, out var got),
            $"‏الخوارزميّةُ القديمةُ كانت ترفض «{cell}» — فهذا الصفُّ ليس شاهداً على العطل.");

        Assert.Equal(preFixValue, got);
    }

    /// <summary>‏وشاهدُ ضبطٍ سالب: ما ترفضه القديمةُ أصلاً ليس من هذا العطل.</summary>
    [Theory]
    [InlineData("1 234,56")]
    [InlineData("١٢٣٤")]
    public void ThePreFixAlgorithm_AlreadyRefusedThese(string cell) =>
        Assert.False(PreFixParseDecimal(cell, out _));

    /// <summary>‏نسخةُ <c>ParseDecimal</c> كما كانت عند <c>369e5a0</c> — سطراً بسطر.</summary>
    private static bool PreFixParseDecimal(string value, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = value.Replace(",", "").Trim();
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            return true;
        return decimal.TryParse(normalized, out result);
    }

    // ── الطبقة ٦: أنّ الحارسَ يقيس المحلّلَ الحقيقيّ لا نسخةً منه ────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجبٌ على الأداة</b>: لولاه لمرَّت الطبقاتُ كلُّها لو صار
    /// <c>Parse</c> يرمي على كلّ مُدخَل. فملفٌّ سليمٌ تماماً يجب أن يُقرأ كاملاً.
    /// </summary>
    [Fact]
    public void TheParser_StillReadsAWhollyValidFile()
    {
        var csv = $"{Header}\n2026-07-01,إيداع,5000.00,REF-1\n2026-07-02,رسوم,-50.00,REF-2";

        var lines = BankStatementCsvParser.Parse(csv);

        Assert.Equal(2, lines.Count);
        Assert.Equal(5000m, lines[0].SignedAmount);
        Assert.Equal(-50m, lines[1].SignedAmount);
        Assert.Equal("REF-2", lines[1].Reference);
    }

    /// <summary>
    /// <b>وأنّ رسائلَ الرفض تُسمّي الحقل</b> — فبوّابةٌ تقول «فشل» ولا تقول «أين»
    /// تُكلّف قارئَها ما كلّفنا اكتشافُها، وهو العرفُ المكتوب في
    /// <c>NumericInputIsTheOnlyParserTests</c>.
    /// </summary>
    [Fact]
    public void TheRejectionMessage_NamesTheOffendingCellAndTheAcceptedShape()
    {
        var csv = $"{Header}\n2026-07-01,محلُّ الشاهد,\"1,5\",REF-1\n{GoodRow}";

        var ex = Assert.Throws<AccountingException>(() => BankStatementCsvParser.Parse(csv));

        Assert.Contains("1,5", ex.Message, StringComparison.Ordinal);
        Assert.Matches(new Regex("1234\\.56|١٢٣٤"), ex.Message);
    }
}
