using System.Security.Cryptography;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>طرفٌ في اتفاقية الترخيص.</summary>
public sealed record LicenseAgreementParty(string Name, string? CommercialRegistration, string? VatNumber, string? Address);

/// <summary>
/// ‏بيانات المورِّد (الطرف الأول) — تُدخَل من إعدادات المنشأة وتُحفظ على الجهاز، فتظهر في كل اتفاقيةٍ تُنشأ تلقائياً.
/// </summary>
public static class LicenseAgreementVendor
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Brqosh", "vendor-identity.json");

    public static LicenseAgreementParty Party => Load();

    public static LicenseAgreementParty Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return System.Text.Json.JsonSerializer.Deserialize<LicenseAgreementParty>(File.ReadAllText(FilePath))
                       ?? Empty;
        }
        catch (Exception)
        {
            // ‏ملفٌّ تالف يُعامَل كأنه غير موجود.
        }
        return Empty;
    }

    public static void Save(LicenseAgreementParty party)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, System.Text.Json.JsonSerializer.Serialize(party));
    }

    private static LicenseAgreementParty Empty => new("", null, null, null);
}

/// <summary>
/// ‏اتفاقية ترخيص الاستخدام: تُنشأ وتُحفظ تلقائياً PDF بمجرّد توفّر اسم الطرف الثاني ورقمه الضريبي،
/// ‏وتُعاد كتابتها فقط إذا تغيّرت بيانات أحد الطرفين أو الترخيص.
/// </summary>
public static class LicenseAgreementDocument
{
    static LicenseAgreementDocument() => QuestPDF.Settings.License = LicenseType.Community;

    public static string AgreementsRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Brqosh", "Agreements");

    public static string PathFor(long companyId) =>
        Path.Combine(AgreementsRoot, companyId.ToString(), "license-agreement.pdf");

    public static bool CanGenerate(LicenseAgreementParty customer) =>
        !string.IsNullOrWhiteSpace(customer.Name) && !string.IsNullOrWhiteSpace(customer.VatNumber);

    /// <summary>يحفظ الاتفاقية إن اكتملت بيانات الطرف الثاني؛ ويعيد مسارها أو null.</summary>
    public static string? EnsureSaved(long companyId, LicenseAgreementParty customer, string? licenseCode, string? planLabel)
    {
        if (!CanGenerate(customer)) return null;
        var vendor = LicenseAgreementVendor.Party;
        var path = PathFor(companyId);
        var stamp = Fingerprint(vendor, customer, licenseCode, planLabel);
        var stampPath = Path.ChangeExtension(path, ".stamp");
        if (File.Exists(path) && File.Exists(stampPath) && File.ReadAllText(stampPath) == stamp)
            return path;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Generate(vendor, customer, licenseCode, planLabel, DateOnly.FromDateTime(DateTime.Today)));
        File.WriteAllText(stampPath, stamp);
        return path;
    }

    private static string Fingerprint(LicenseAgreementParty v, LicenseAgreementParty c, string? code, string? plan)
    {
        var raw = string.Join("|", v, c, code, plan);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static string Or(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    public static byte[] Generate(LicenseAgreementParty vendor, LicenseAgreementParty customer,
        string? licenseCode, string? planLabel, DateOnly date)
    {
        var branding = PrintBranding.From(customer.Name, customer.VatNumber ?? "");
        var clauses = new (string Title, string Body)[]
        {
            ("١. منح الترخيص", "يمنح الطرفُ الأول الطرفَ الثاني ترخيصاً غير حصريٍّ وغير قابلٍ للتحويل لاستخدام برنامج BRQOSH داخل منشأته، ضمن الفئة وعدد الشركات والمحطات التي يحدّدها مفتاح الترخيص."),
            ("٢. القيود", "لا يحقّ للطرف الثاني نسخ البرنامج لغير النسخ الاحتياطي، أو تفكيكه أو الهندسة العكسية له، أو تأجيره أو إعادة بيعه، أو تجاوز حدود الترخيص أو آلية التفعيل."),
            ("٣. الملكية", "يبقى البرنامج وحقوق الملكية الفكرية للطرف الأول، وبيانات الطرف الثاني ملكٌ له وحده."),
            ("٤. الرسوم والدفع", "تُدفع الرسوم حسب الباقة المعتمدة، وتُضاف إليها ضريبة القيمة المضافة حسب النظام، وتُصدر فاتورة ضريبية إلكترونية لكل دفعة."),
            ("٥. الدعم والتحديثات", "يقدّم الطرف الأول الدعم حسب مستوى الخدمة المعتمد، وتشمل التحديثاتُ التنظيمية (هيئة الزكاة والضريبة والجمارك وضريبة القيمة المضافة) مدةَ سريان الدعم."),
            ("٦. حماية البيانات", "يلتزم الطرفان بنظام حماية البيانات الشخصية، ولا يطّلع الطرف الأول على بيانات الطرف الثاني إلا بإذنه ولغرض الدعم فقط، ويتيح البرنامج تصدير بيانات الشخص وإخفاءها."),
            ("٧. الالتزام التنظيمي", "يوفّر البرنامج أدوات الفوترة الإلكترونية، ويبقى الطرف الثاني مسؤولاً عن صحة بياناته وإقراراته."),
            ("٨. حدود المسؤولية", "لا تتجاوز مسؤولية الطرف الأول ما دفعه الطرف الثاني خلال الاثني عشر شهراً السابقة، ولا يُسأل عن الأضرار غير المباشرة أو فوات الربح."),
            ("٩. المدة والإنهاء", "يسري الترخيص طوال مدة مفتاح الترخيص، ولكل طرفٍ الإنهاء عند الإخلال الجوهري إذا لم يُصحَّح خلال ثلاثين يوماً من الإشعار، ويحقّ للطرف الثاني عندها تصدير بياناته."),
            ("١٠. القانون الواجب التطبيق", "تخضع هذه الاتفاقية لأنظمة المملكة العربية السعودية."),
        };

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => branding.PageTextStyle(10.5f));
                page.Header().Element(h => branding.RenderHeader(h, "اتفاقية ترخيص استخدام برنامج BRQOSH"));

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(6);
                    col.Item().AlignRight().Text($"تاريخ الاتفاقية: {date:yyyy-MM-dd}");
                    col.Item().AlignRight().Text($"الفئة: {Or(planLabel)}    مفتاح الترخيص: {Or(licenseCode)}");
                    PartyBlock(col, "الطرف الأول (المورِّد)", vendor);
                    PartyBlock(col, "الطرف الثاني (العميل)", customer);
                    foreach (var (title, body) in clauses)
                    {
                        col.Item().PaddingTop(4).AlignRight().Text(title).Bold();
                        col.Item().AlignRight().Text(body);
                    }
                });

                page.Footer().PaddingTop(12).Row(row =>
                {
                    row.RelativeItem().Element(c => PrintTheme.SignatureLine(c, "الطرف الثاني"));
                    row.RelativeItem().Element(c => PrintTheme.SignatureLine(c, "الطرف الأول"));
                });
            });
        }).GeneratePdf();
    }

    private static void PartyBlock(ColumnDescriptor col, string title, LicenseAgreementParty party)
    {
        col.Item().PaddingTop(6).AlignRight().Text(title).Bold();
        col.Item().AlignRight().Text($"الاسم: {Or(party.Name)}    السجل التجاري: {Or(party.CommercialRegistration)}    الرقم الضريبي: {Or(party.VatNumber)}");
        col.Item().AlignRight().Text($"العنوان: {Or(party.Address)}");
    }
}
