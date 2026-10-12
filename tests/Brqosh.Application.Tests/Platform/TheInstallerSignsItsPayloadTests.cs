namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (م-٦ · ن-١٢) <b>المُنصِّبُ يوقّع حمولتَه قبل أن يحزمها — وكان يوقّع الحزمةَ وحدَها.</b>
///
/// <para><b>ما كان قبلها:</b> نداءُ <c>signtool</c> واحدٌ في <c>installer/build-msi.ps1</c>،
/// هدفُه ملفُّ <c>.msi</c> وحدَه. <b>وتوقيعُ الحزمة يحمي قناةَ التسليم لا المُنتَج</b>:
/// يُسكت SmartScreen ويكشف تحريفَ المُنصِّب أثناء النقل، <b>ولا يترك أثراً بعد التنصيب</b> —
/// فالحزمةُ تفكّ حمولتَها والحمولةُ تبقى بلا توقيع. فتطبيقٌ مُنصَّبٌ من حزمةٍ موقَّعةٍ تماماً
/// يقول UAC عنه «ناشرٌ غير معروف»، <b>ولا تستطيع سياسةُ قوائم السماح (WDAC/AppLocker) أن
/// تميّزه من ثنائيّةٍ مُستبدَلةٍ في مجلّد التنصيب</b> — وهو المسارُ الذي يُهاجَم فعلاً، لأنّ
/// المُنصِّبَ يُستعمل مرّةً والملفّاتُ تبقى سنين.</para>
///
/// <para><b>والترتيبُ هو ما لا يحرسه شيءٌ آخر:</b> التوقيعُ يُلحق كتلةً بملفّ PE فيتغيّر
/// محتواه. فلو وقع توقيعُ الحمولة <b>بعد</b> <c>wix build</c> لحملت الحزمةُ نسخاً غيرَ
/// موقَّعةٍ بينما القرصُ يحمل الموقَّعة — <b>وهو أسوأُ الحالَين</b>: من يقيس التوقيعَ على
/// القرص يقرأ نجاحاً، ومن يُنصّب يحصل على ثنائيّاتٍ عارية. <b>ونقلُ سطرٍ في إعادة هيكلةٍ
/// يفعلها، ولا بناءٌ يحمرّ ولا اختبارٌ يسقط.</b></para>
///
/// <para><b>وحدُّ هذه البوّابة مكتوبٌ فيها، ولا يُتجاوز:</b> تقيس <b>بنيةَ السكربت</b> —
/// وجودَ النداءَين وترتيبَهما ووحدانيّةَ منفذ التوقيع — <b>ولا تقيس توقيعاً مُنتَجاً</b>.
/// إنتاجُ توقيعٍ يلزمه <c>signtool</c> وشهادةُ توقيعِ كودٍ تكون سلسلتُها موثوقةً على آلة
/// البناء، <b>وكلاهما تبعيّةٌ خارجيّة</b>. والتحقّقُ من التوقيع الفعليّ يقع في السكربت نفسِه
/// (<c>Get-AuthenticodeSignature</c> على <b>كلّ</b> هدف، ويُسقط البناءَ إن لم تكن الحالةُ
/// <c>Valid</c>) — <b>فالبوّابةُ هنا تحرس أن يبقى ذلك المسارُ موصولاً، لا أن ينجح</b>.</para>
/// </summary>
public class TheInstallerSignsItsPayloadTests
{
    private const string Script = "installer/build-msi.ps1";

    /// <summary>نداءُ توقيع الحمولة — يُميَّز بمتغيّره لا بنصٍّ عامّ.</summary>
    private const string PayloadCall = "-Paths $PayloadBinaries";

    /// <summary>نداءُ توقيع الحزمة.</summary>
    private const string PackageCall = "-Paths @($MsiOut)";

    /// <summary>السطرُ الذي يحصد <c>$PublishDir</c> من القرص.</summary>
    private const string Harvest = "wix build ";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }

    private static string ScriptText()
    {
        var path = Path.Combine(RepoRoot(), Script.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"‏ملفٌّ مفقود: {Script}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ من الصنف نفسِه، ويجري أوّلاً:</b> القارئُ يقرأ سكربتاً حقيقيّاً.
    /// <b>وبلا هذا</b> كان ملفٌّ فارغٌ أو مسارٌ خاطئٌ يجعل كلَّ <c>DoesNotContain</c> أدناه
    /// ينجح بلا معنًى — <b>وهو عينُ ن-٥٣: شاهدٌ يولد كاذباً</b>.
    /// </summary>
    [Fact]
    public void TheReaderActuallyReadsTheBuildScript()
    {
        var text = ScriptText();

        Assert.Contains("BRQOSH MSI Build", text, StringComparison.Ordinal);
        Assert.Contains(Harvest, text, StringComparison.Ordinal);
        Assert.True(text.Length > 4000, $"‏السكربتُ أقصرُ من أن يكون هو: {text.Length} محرفاً");
    }

    [Fact]
    public void ThePayloadIsSigned_BeforeTheMsiIsBuilt()
    {
        var text = ScriptText();

        var payload = text.IndexOf(PayloadCall, StringComparison.Ordinal);
        var harvest = text.IndexOf(Harvest, StringComparison.Ordinal);

        Assert.True(payload >= 0, "‏لا توقيعَ للحمولة في build-msi.ps1 — الحزمةُ ستحمل ثنائيّاتٍ عارية.");
        Assert.True(harvest >= 0, "‏لم يُعثر على سطر wix build.");
        Assert.True(payload < harvest,
            "‏توقيعُ الحمولة يقع بعد wix build — فالحزمةُ تحمل نسخاً غيرَ موقَّعةٍ والقرصُ يحمل موقَّعة.");
    }

    [Fact]
    public void ThePackageIsSignedToo()
    {
        Assert.Contains(PackageCall, ScriptText(), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ومنفذُ التوقيع واحد.</b> نداءٌ ثانٍ لـ<c>signtool</c> خارج الدالّة يعني هدفاً
    /// يُوقَّع <b>بلا</b> فحصِ <c>Get-AuthenticodeSignature</c> بعده — <b>وتوقيعٌ غيرُ
    /// مُتحقَّقٍ منه يُقرأ نجاحاً</b>. وهو نمطُ «قرارٌ واحدٌ لكلّ سؤال» نفسُه الذي فرضه
    /// ن-٣٨ على «هل أنا إنتاج؟».
    /// </summary>
    [Fact]
    public void ThereIsExactlyOneSigningCall()
    {
        var text = ScriptText();

        var invocations = text.Split("signtool", StringSplitOptions.None).Length - 1;
        var actualSign = text.Split("$script:SignTool sign", StringSplitOptions.None).Length - 1;

        Assert.Equal(1, actualSign);
        // ‏وذكرُ الاسم يبقى محدوداً: البحثُ عن الأداة، ورسالةُ غيابها، والنداء.
        Assert.True(invocations <= 4, $"‏ذكرُ signtool تكرّر {invocations} مرّةً — هل ظهر منفذُ توقيعٍ ثانٍ؟");
    }

    /// <summary>
    /// <b>وكلُّ هدفٍ يُتحقَّق منه لا أوّلُه.</b> توقيعٌ جزئيٌّ — أربعةٌ من خمسٍ — يُقرأ
    /// نجاحاً إن فُحص الأوّلُ وحدَه.
    /// </summary>
    [Fact]
    public void EverySignedTargetIsVerified()
    {
        var text = ScriptText();

        Assert.Contains("foreach ($target in $targets)", text, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature $target", text, StringComparison.Ordinal);
        Assert.Contains("$sig.Status -ne 'Valid'", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وشهادةٌ حاضرةٌ وأداةٌ غائبةٌ تُسقط البناء</b> — ولا تُخرج حزمةً يظنّها بانيها
    /// موقَّعة. <b>وهذا مقيسٌ حيّاً</b> في م-٦: شُغّل السكربتُ بشهادةٍ محلّيّةٍ مؤقّتة على
    /// آلةٍ بلا <c>signtool</c>، فخرج بـ<c>1</c> عند خطوة الحمولة — <b>وبصمةُ الـMSI لم
    /// تتغيّر</b>، أي أنّ السقوطَ وقع قبل الحزم.
    /// </summary>
    [Fact]
    public void AMissingSignToolFailsTheBuild()
    {
        var text = ScriptText();

        Assert.Contains("ولا signtool — أُسقط البناء", text, StringComparison.Ordinal);

        // ‏**ويسقط، لا يمضي**: الرسالةُ وحدَها لا تكفي — لولا `exit 1` لطُبعت ومضى البناءُ
        // ‏فأخرج حزمةً غيرَ موقَّعةٍ بعد أن قال إنّ الشهادةَ مضبوطة.
        var message = text.IndexOf("ولا signtool — أُسقط البناء", StringComparison.Ordinal);
        var tail = text.Substring(message, Math.Min(200, text.Length - message));
        Assert.Contains("exit 1", tail, StringComparison.Ordinal);
    }
}
