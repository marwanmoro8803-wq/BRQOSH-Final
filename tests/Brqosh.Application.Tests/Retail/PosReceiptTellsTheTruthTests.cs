using Brqosh.Domain.Zatca;

namespace Brqosh.Application.Tests.Retail;

/// <summary>
/// (م-٢ · ن-٠٤) <b>إيصالُ نقطة البيع كان يكذب في الاتجاهين — وحُذف الجدولُ الذي يكذب.</b>
///
/// <para><b>العطل:</b> كان في <c>PosSaleService</c> جدولُ تسمياتٍ <b>رابع</b> يُطبع على إيصال
/// العميل:</para>
/// <list type="bullet">
///   <item><c>Reported</c> ⟸ «تم الإبلاغ للهيئة <b>(محاكاة)</b>» — <b>وهو النجاحُ الحقيقيّ،
///     يُوسَم محاكاةً</b>.</item>
///   <item><c>_</c> ⟸ «تم الإرسال» — <b>يبتلع <c>Rejected</c> و<c>SimulatedLocally</c> معاً</b>.
///     فالفاتورةُ التي <b>رفضتها الهيئة</b> تُطبع للعميل «تم الإرسال».</item>
/// </list>
///
/// <para><b>والاتجاهان كلاهما خطأٌ في مستندٍ ضريبيّ</b> — أحدُهما يُقلق بلا سبب، والآخر يُطمئن
/// على رفض. و<c>NameAr()</c> هو الجدولُ الصادقُ وكان موجوداً ولا يُستدعى هنا.</para>
///
/// <para><b>ولمَ يُقاس الجدولُ لا الإيصال:</b> لأنّ العلاجَ كان <b>حذفاً لا إضافة</b>. فما يُحرَس
/// أمران: أنّ الجدولَ الباقيَ صادقٌ في كلّ حالة، <b>وأنّه ما زال واحداً</b> — إذ لا يُعيد العطلَ
/// إلّا جدولٌ ثانٍ يُولد في مراجعةٍ عابرة.</para>
/// </summary>
public class PosReceiptTellsTheTruthTests
{
    private static readonly ZatcaComplianceStatus[] AllStatuses =
        Enum.GetValues<ZatcaComplianceStatus>();

    /// <summary>
    /// <b>لا حالتان تنهاران إلى اسمٍ واحد.</b> والجدولُ المحذوف انهار عنده ثلاثٌ إلى «تم الإرسال».
    /// </summary>
    [Fact]
    public void EveryStatus_HasItsOwnName_NoneCollapseIntoAnother()
    {
        var names = AllStatuses.Select(s => s.NameAr()).ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());

        // ‏وشاهدُ ضبطٍ موجب: لا اسمَ فارغٌ ولا اسمٌ لاتينيٌّ متسرّبٌ من `status.ToString()`.
        Assert.All(names, n => Assert.False(string.IsNullOrWhiteSpace(n)));
        Assert.All(names, n => Assert.DoesNotContain(n, AllStatuses.Select(s => s.ToString())));
    }

    /// <summary>
    /// <b>الرفضُ يُقال رفضاً.</b> وهذا هو الاتجاهُ الأخطر: عميلٌ يمضي بإيصالٍ يقول «تم الإرسال»
    /// وفاتورتُه مرفوضةٌ لدى الهيئة، فلا يُعاد شيءٌ ولا يُصحَّح.
    /// </summary>
    [Fact]
    public void ARejectedInvoice_IsNamedRejected_NotSent()
    {
        var name = ZatcaComplianceStatus.Rejected.NameAr();

        Assert.Contains("مرفوض", name);
        Assert.DoesNotContain("تم الإرسال", name);
    }

    /// <summary>
    /// <b>والمحاكاةُ تقول إنّها لم تصل.</b> فهي الحالةُ التي أُفردت أصلاً (CEEF-229 · م0-١٠)
    /// لئلّا تُعرَض قبولاً — وابتلاعُها في «تم الإرسال» يُعيد العطلَ الذي أُفردت لأجله.
    /// </summary>
    [Fact]
    public void ALocallySimulatedInvoice_SaysItNeverReachedTheAuthority()
    {
        var name = ZatcaComplianceStatus.SimulatedLocally.NameAr();

        Assert.Contains("لم يُرسل", name);
        Assert.False(ZatcaComplianceStatus.SimulatedLocally.IsAcknowledgedByAuthority());
    }

    /// <summary>
    /// <b>والاتجاهُ المعاكس:</b> النجاحُ الحقيقيُّ لا يُوسَم محاكاةً. وكان الجدولُ المحذوف
    /// يُلحق «(محاكاة)» بـ<c>Reported</c> و<c>Cleared</c> — <b>وهما إقرارُ الهيئة نفسُه</b>.
    /// </summary>
    [Theory]
    [InlineData(ZatcaComplianceStatus.Reported)]
    [InlineData(ZatcaComplianceStatus.Cleared)]
    public void ARealAcknowledgement_IsNotLabelledASimulation(ZatcaComplianceStatus status)
    {
        Assert.DoesNotContain("محاكاة", status.NameAr());
        Assert.True(status.IsAcknowledgedByAuthority());
    }

    /// <summary>
    /// <b>إثباتُ أكوادٍ ثابت — وهو الحارسُ الحقيقيُّ لهذا البند.</b>
    ///
    /// <para>فالتسمياتُ أعلاه تُثبت أنّ الجدولَ الباقيَ صادق، <b>ولا تمنع ولادةَ جدولٍ ثانٍ</b>.
    /// والعطلُ لم يكن في الجدول الصادق قطّ: كان في وجود <b>جدولٍ رابعٍ</b> إلى جانبه. فيُشترط
    /// هنا أنّ <c>PosSaleService</c> يستدعي <c>NameAr()</c> ولا يُطابق الحالاتِ بنفسه.</para>
    ///
    /// <para>ويتبع هذا سابقةَ <c>OrchestratorEventWiringEvidenceTests</c> في هذا المشروع.</para>
    /// </summary>
    [Fact]
    public void PosSaleService_UsesTheOneNamingTable_AndDeclaresNoSecond()
    {
        var source = ReadSource("Retail", "PosSaleService.cs");

        Assert.Contains("ComplianceStatus.NameAr()", source, StringComparison.Ordinal);

        // ‏شكلُ الجدول الثاني: ذراعُ `switch` تُطابق حالةً وتُنتج نصّاً. والتعليقاتُ تُستثنى،
        // ‏إذ يشرح تعليقُ الإصلاح الجدولَ المحذوفَ حرفيّاً — وشرحُ عطلٍ ليس إعادةً له.
        var code = string.Join('\n', source
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        foreach (var status in AllStatuses)
            Assert.DoesNotContain($"ZatcaComplianceStatus.{status} =>", code, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] relativeToInfrastructure)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            new[] { root, "src", "Brqosh.Infrastructure" }.Concat(relativeToInfrastructure).ToArray());

        Assert.True(File.Exists(path), $"‏ملفُّ المصدر غيرُ موجود: {path}");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
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
}
