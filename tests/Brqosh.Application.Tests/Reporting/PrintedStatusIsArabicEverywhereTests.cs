using System.Reflection;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-٥٤) <b>حالةُ المستند لدى الهيئة تُطبع بالعربيّة في كلّ ورقة — وكان المستخلصُ
/// يطبع اسمَها البرمجيّ.</b>
///
/// <para>‏CEEF-229 (م0-١٠) أصلح سطرَ الامتثال في الفاتورة: «Cleared» اسمٌ برمجيٌّ على ورقةٍ عربيّة،
/// <b>والمحاكاةُ تُعلَن «محاكاة محلية — لم يُرسل للهيئة»</b> لا «معتمَدة». <b>ثمّ بقي المستخلصُ يبني
/// السطرَ بيده</b> — قارئٌ ثانٍ يشتقّ ما تملكه الشجرةُ (<c>NameAr</c>) — فيطبع في تذييله
/// «SimulatedLocally» و«ReadyToClear» كما هما. <b>وإيصالُ نقطة البيع كذلك</b> حين لا رسالةَ معه.</para>
/// </summary>
public class PrintedStatusIsArabicEverywhereTests
{
    /// <summary>‏<b>القياسُ الحاكم:</b> المستخلصُ المُحاكى يقول «محاكاة» بالعربيّة، لا اسمَ الحالة.</summary>
    [Fact]
    public void AProgressBilling_PrintsTheSimulationInArabic()
    {
        var line = BillingLine(ZatcaComplianceStatus.SimulatedLocally);

        Assert.Contains("محاكاة محلية — لم يُرسل للهيئة", line, StringComparison.Ordinal);
        Assert.DoesNotContain("SimulatedLocally", line, StringComparison.Ordinal);
        Assert.Contains("ICV: 7", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ZatcaComplianceStatus.Cleared, "معتمَد من الهيئة")]
    [InlineData(ZatcaComplianceStatus.ReadyToClear, "جاهز للاعتماد")]
    [InlineData(ZatcaComplianceStatus.Rejected, "مرفوض")]
    public void AProgressBilling_PrintsEveryStatusInArabic(ZatcaComplianceStatus status, string arabic)
    {
        var line = BillingLine(status);

        Assert.Contains(arabic, line, StringComparison.Ordinal);
        Assert.DoesNotContain(status.ToString(), line, StringComparison.Ordinal);
    }

    /// <summary>‏<b>والمستخلصُ والفاتورةُ سطرٌ واحد</b> — فلا يفترقان بتعديلٍ في أحدهما.</summary>
    [Theory]
    [InlineData(ZatcaComplianceStatus.SimulatedLocally)]
    [InlineData(ZatcaComplianceStatus.Cleared)]
    public void TheBillingAndTheInvoice_PrintTheSameLine(ZatcaComplianceStatus status)
    {
        var invoice = Invoke(typeof(SalesInvoicePdfGenerator), "BuildComplianceLine",
            new SalesInvoicePdfGenerator.InvoicePrintOptions(Icv: 7, ComplianceStatus: status.ToString()));

        Assert.Equal(invoice, BillingLine(status));
    }

    /// <summary>‏<b>شاهدُ ضبط</b> — بلا عدّادٍ ولا حالةٍ لا سطر، كما كان.</summary>
    [Fact]
    public void ABillingWithoutZatca_HasNoLine() =>
        Assert.Null(Invoke(typeof(ProgressBillingPdfGenerator), "BuildComplianceLine", Billing(null, null)));

    /// <summary>
    /// ‏<b>ولا شاشةَ تعرض الاسمَ البرمجيّ</b> — إيصالُ نقطة البيع كان يعرضه حين لا رسالةَ معه.
    /// <b>ونقلُ الاسم نصّاً بين الخدمة والمولّد مشروع</b> (يُحلَّل هناك)، فالحارسُ على الواجهة وحدَها.
    /// </summary>
    [Fact]
    public void NoScreenShowsARawStatusName()
    {
        var desktop = Path.Combine(RepositoryRoot(), "src", "Brqosh.Desktop");
        var files = Directory.EnumerateFiles(desktop, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.True(files.Count > 100, $"زار {files.Count} ملفّاً فقط — الماسحُ أعمى؟");

        var offenders = files
            .Where(f => File.ReadAllText(f).Contains("ComplianceStatus.ToString()", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(desktop, f))
            .ToList();

        Assert.True(offenders.Count == 0, "اسمُ الحالة البرمجيُّ على شاشة: " + string.Join(" · ", offenders));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }

    private static string BillingLine(ZatcaComplianceStatus status) =>
        Invoke(typeof(ProgressBillingPdfGenerator), "BuildComplianceLine", Billing(7, status.ToString()))
        ?? throw new InvalidOperationException("لا سطر.");

    private static ProgressBillingPdfGenerator.BillingPrintData Billing(int? icv, string? status) =>
        new("PB-1", new DateOnly(2026, 7, 9), "عميل", "مشروع", "يوليو", 1_000m, 10m, 100m, 900m, 150m, 1_150m,
            PrintDocumentKind.StandardSalesInvoice, 0.15m, "311111111100003", "الرياض",
            Icv: icv, ComplianceStatus: status);

    /// <summary>
    /// ‏بالانعكاس — <b>فالقياسُ نفسُه يجري قبل الإصلاح وبعده</b>: الدالّتان خاصّتان في الأصل.
    /// </summary>
    private static string? Invoke(Type generator, string name, object argument)
    {
        var method = generator.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static,
            [argument.GetType()])
            ?? throw new InvalidOperationException($"{generator.Name}.{name} غير موجودة.");
        return (string?)method.Invoke(null, [argument]);
    }
}
