using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٨) <b>خلايا الجداول تبقى عربيّةً بعد التبديل للإنجليزية —
/// و<c>I18nCoverageTests</c> لا يبلغها بالبناء.</b>
///
/// <para><b>ولماذا لا يبلغها:</b> <c>XamlUiLocalizer</c> يترجم <b>خصائصَ العناصر</b> —
/// ومنها <c>DataGridColumn.Header</c> — <b>ولا يبلغ قيمَ الخلايا</b>، فهي تأتي من الكائن
/// المربوط لا من خاصّيّةٍ على عنصر. ومستخرِجُ <c>I18nCoverageTests</c> يلتقط
/// <c>.Text =</c> و<c>.Content =</c> وما يُمرَّر لمساعدات الواجهة — <b>وذراعُ
/// <c>switch</c> تُعيد نصّاً ليست واحدةً منهما</b>.</para>
///
/// <para><b>والعلاجُ ليس إدخالَها في <c>ui-overrides.json</c></b>: السجلُّ يخدم
/// <c>XamlUiLocalizer</c>، وما لا يبلغه المُترجِمُ <b>يبقى عربيّاً والبوّابةُ خضراء</b> —
/// <b>أي يصير الحارسُ كاذباً</b>. وهو بعينه ما يحذّر منه <c>I18nCoverageTests</c> نصّاً في
/// شأن <c>MessageBox</c>. <b>فالعلاجُ <see cref="Brqosh.Desktop.Ui.LocaleManager.T"/></b>
/// عند بناء الصفّ.</para>
///
/// <para><b>وهذه البوّابةُ تُجمّد الدَّينَ ولا تدّعي سدَّه:</b> ما كان يومَ كتابتها
/// <b>٢١٠ مواضعَ في ٣٧ ملفّاً</b> مُقَرٌّ به بعدده، <b>فلا يزيد موضعٌ واحدٌ بعد اليوم</b>،
/// ولا يُنقَص موضعٌ دون أن يُسجَّل نقصُه.</para>
///
/// <para><b>وحدٌّ مُصرَّحٌ به:</b> المسحُ يرى صيغةَ <c>=> "…"</c> وحدَها (ذراعُ
/// <c>switch</c> والعضوُ ذو الجسم التعبيريّ) — وهي الصيغةُ الغالبة. <b>ولا يرى الشرطيَّ
/// الثلاثيَّ العاري</b> (<c>x ? "نعم" : "لا"</c>)، لأنّ نقطتَي الشرطيّ تردان في سياقاتٍ
/// كثيرةٍ لا علاقةَ لها. <b>وذِكرُ الحدّ خيرٌ من ادّعاءِ شمولٍ لا يقع.</b></para>
/// </summary>
public class BilingualGridCellsTests
{
    /// <summary>ذراعٌ تُعيد نصّاً عربيّاً مباشرةً — لا عبر <c>LocaleManager.T</c>.</summary>
    private static readonly Regex ArabicArrowLiteral =
        new(@"=>\s*""[^""\n]*[؀-ۿ][^""\n]*""", RegexOptions.Compiled);

    /// <summary>
    /// <b>السببُ واحدٌ لكلّ هذه الملفّات</b> — فلا يُكرَّر سطراً سطراً: نصُّ خليّةٍ يُبنى في
    /// الشيفرة ولا يبلغه المُترجِم، <b>وترجمتُه تحتاج مراجعةَ مصطلحٍ لكلّ وحدةِ عمل</b>
    /// (فـ«مستخلص» ليست <i>extract</i> بل <i>Progress Billing</i>)، <b>وسَوقُها سوقاً
    /// واحداً يُنتج مصطلحاً خاطئاً بثقة</b>.
    /// </summary>
    private const string Reason =
        "خلايا جدولٍ تُبنى في الشيفرة — لا يبلغها XamlUiLocalizer، وترجمتُها تحتاج مراجعةَ "
        + "مصطلحٍ لكلّ وحدةِ عمل، فلا تُساق سوقاً واحداً.";

    /// <summary>ما كان يومَ سنّ البوّابة — <b>بعدده لا بوجوده</b>.</summary>
    private static readonly Dictionary<string, int> Declared = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ui/VatReturnRows.cs"] = 4,
        ["Views/Pages/CompanySettingsPage.xaml.cs"] = 21,
        ["Views/Pages/CreditNotesPage.xaml.cs"] = 3,
        ["Views/Pages/CrmActivitiesPage.xaml.cs"] = 4,
        ["Views/Pages/CrmLeadsPage.xaml.cs"] = 4,
        ["Views/Pages/CrmOpportunitiesPage.xaml.cs"] = 5,
        ["Views/Pages/DebitNotesPage.xaml.cs"] = 3,
        ["Views/Pages/DeliveryTripsPage.xaml.cs"] = 5,
        ["Views/Pages/EmployeeAdvancesPage.xaml.cs"] = 4,
        ["Views/Pages/GoodsReceiptsPage.xaml.cs"] = 3,
        ["Views/Pages/InsuranceClaimsPage.xaml.cs"] = 7,
        ["Views/Pages/JournalEntriesPage.xaml.cs"] = 3,
        ["Views/Pages/JournalEntryViewDialog.xaml.cs"] = 3,
        ["Views/Pages/LeasesPage.xaml.cs"] = 4,
        ["Views/Pages/LeaveRequestsPage.xaml.cs"] = 12,
        ["Views/Pages/MaintenanceOrdersPage.xaml.cs"] = 8,
        ["Views/Pages/MenuItemsPage.xaml.cs"] = 5,
        ["Views/Pages/OrganizationsPage.xaml.cs"] = 11,
        ["Views/Pages/PaymentVouchersPage.xaml.cs"] = 8,
        ["Views/Pages/PayrollPage.xaml.cs"] = 3,
        ["Views/Pages/PosShiftsPage.xaml.cs"] = 1,
        ["Views/Pages/PropertiesPage.xaml.cs"] = 3,
        ["Views/Pages/PurchaseInvoicesPage.xaml.cs"] = 3,
        ["Views/Pages/PurchaseOrdersPage.xaml.cs"] = 10,
        ["Views/Pages/PurchaseRequestsPage.xaml.cs"] = 9,
        ["Views/Pages/ReceiptVoucherDialog.xaml.cs"] = 2,
        ["Views/Pages/ReceiptVouchersPage.xaml.cs"] = 3,
        ["Views/Pages/RecurringPlansPage.xaml.cs"] = 8,
        ["Views/Pages/ReservationsPage.xaml.cs"] = 5,
        ["Views/Pages/RoomsPage.xaml.cs"] = 8,
        ["Views/Pages/SalesInvoicesPage.xaml.cs"] = 3,
        ["Views/Pages/SalesOrdersPage.xaml.cs"] = 4,
        ["Views/Pages/SalesQuotationsPage.xaml.cs"] = 5,
        ["Views/Pages/ShipmentsPage.xaml.cs"] = 5,
        ["Views/Pages/StockMovementDialog.xaml.cs"] = 5,
        ["Views/Pages/StockMovementsPage.xaml.cs"] = 10,
        ["Views/Pages/VisitsPage.xaml.cs"] = 6,
    };

    // ── البوّابة ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>لا يزيد موضعٌ، ولا يُنقَص موضعٌ صامتاً.</b>
    ///
    /// <para>ملفٌّ غيرُ مُقَرٍّ به ⟵ سقوط: <b>خليّةٌ جديدةٌ تبقى عربيّةً بالإنجليزية</b>.
    /// وعددٌ يزيد على المُقَرّ ⟵ سقوط: <b>موضعٌ جديدٌ اختبأ تحت غطاء إقرارٍ قديم</b>.</para>
    /// </summary>
    [Fact]
    public void NoNewUntranslatedGridCell_IsAddedBeyondTheFrozenDebt()
    {
        var found = Scan();
        var problems = new List<string>();

        foreach (var (path, count) in found.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!Declared.TryGetValue(path, out var declared))
            {
                problems.Add($"{path} — {count} نصَّ خليّةٍ يبقى عربياً بالإنجليزية بلا إقرار. "
                           + "استعمل LocaleManager.T(عربي، english) عند بناء الصفّ.");
                continue;
            }

            if (declared != count)
                problems.Add($"{path} — أُقرّ له {declared} والموجودُ {count}: "
                           + (count > declared
                               ? "موضعٌ جديدٌ دخل تحت غطاء إقرارٍ قديم."
                               : "نقص الدَّينُ ولم يُحدَّث الإقرار — فالتقدّمُ لا يُقاس."));
        }

        foreach (var path in Declared.Keys.Where(k => !found.ContainsKey(k))
                     .OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            problems.Add($"{path} — إقرارٌ باقٍ ولا موضعَ فيه: أثرُ عملٍ تمّ ولم يُسجَّل.");

        Assert.True(problems.Count == 0,
            "خلايا جداولٍ تبقى عربيّةً بعد التبديل للإنجليزية — ولا يبلغها مسحُ "
            + "I18nCoverageTests بالبناء:\n  " + string.Join("\n  ", problems));
    }

    // ── حارسُ الحارس ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>ولو عمي المسحُ لمرّت البوّابةُ خضراءَ وهي لا ترى شيئاً.</b>
    ///
    /// <para>فيُثبَّت شاهدٌ حيّ: الدَّينُ المُقَرُّ به <b>موجودٌ فعلاً</b> ويُعثر عليه.
    /// <b>وصفرُ نتائجَ يعني انكسارَ الاستخراج لا نظافةَ المستودع.</b></para>
    /// </summary>
    [Fact]
    public void TheScan_StillSeesTheDeclaredDebt()
    {
        var found = Scan();

        Assert.NotEmpty(found);
        Assert.True(found.Values.Sum() >= 200,
            $"المسحُ وجد {found.Values.Sum()} موضعاً فقط — والمُقَرُّ به {Declared.Values.Sum()}. "
            + "انكسر الاستخراج، ولو مرّ لكان حارساً أعمى.");
    }

    /// <summary>
    /// <b>وصيغةُ العلاج يراها المسحُ علاجاً.</b>
    ///
    /// <para>شاشةُ مستندات ZATCA وصنفُ عرضها <b>عولجا بـ<c>LocaleManager.T</c></b> في ح-١٨،
    /// <b>فغيابُهما عن النتائج هو الدليلُ الحيُّ</b> على أنّ البوّابةَ تُفرّق بين النصِّ
    /// المُترجَم والنصِّ العاري — <b>ولولاه لجاز أن تكون «نظيفةً» لأنّها لا ترى الصيغتين
    /// معاً</b>.</para>
    /// </summary>
    [Fact]
    public void TheFixedScreens_AreNotReportedAsDebt()
    {
        var found = Scan();

        Assert.DoesNotContain("Views/Pages/ZatcaInvoicesPage.xaml.cs", found.Keys);
        Assert.DoesNotContain("Ui/ZatcaDocumentPresentation.cs", found.Keys);
    }

    // ── المسح ───────────────────────────────────────────────────────────────

    private static Dictionary<string, int> Scan()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        Assert.True(Directory.Exists(root), $"Desktop source root not found: {root}");

        var found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".g.cs", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var count = ArabicArrowLiteral.Matches(StripComments(File.ReadAllText(file))).Count;
            if (count == 0) continue;

            found[Path.GetRelativePath(root, file).Replace('\\', '/')] = count;
        }

        return found;
    }

    /// <summary>يُسقط التعليقات — <b>فشرحُ النمط ليس وقوعاً له</b>.</summary>
    private static string StripComments(string source)
    {
        var text = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(text, @"^\s*//.*$", "", RegexOptions.Multiline);
    }
}
