using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Zatca;

public record ZatcaConfigurationStatus(
    bool IsEnabled,
    bool CertificatePresent,
    bool PrivateKeyPresent,
    bool ApiConfigured,
    bool ReadyForSubmission,
    IReadOnlyList<string> Warnings);

/// <summary>التحقق من جاهزية إعدادات ZATCA.</summary>
public class ZatcaConfigurationValidator(BrqoshDbContext db)
{
    public async Task<ZatcaConfigurationStatus> ValidateAsync(long companyId)
    {
        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId);
        var warnings = new List<string>();

        if (settings is null || !settings.IsEnabled)
            return new ZatcaConfigurationStatus(false, false, false, false, false, ["ZATCA غير مفعّلة."]);

        var certOk = File.Exists(settings.CertificateFilePath);
        var keyOk = File.Exists(settings.PrivateKeyFilePath);
        var apiOk = !string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
                    && !string.IsNullOrWhiteSpace(ZatcaSettingsSecrets.GetApiToken(settings));

        if (!certOk) warnings.Add("ملف الشهادة غير موجود.");
        if (!keyOk) warnings.Add("ملف المفتاح الخاص غير موجود.");
        if (!apiOk) warnings.Add("عنوان API أو رمز المصادقة غير مضبوط.");

        // (CEEF-229 · Z-10) بيانات البائع النظامية — لا الرقم الضريبيّ وحده.
        //
        // كان الفحص يقتصر على الرقم الضريبيّ، فتظهر المنشأة «جاهزة للإرسال» بينما ينقصها
        // السجل التجاريّ والاسم المسجَّل والعنوان الوطنيّ — وهي حقولٌ إلزامية يُرفض المستند
        // بدونها في تصديق المخطّط. والرسائل عربية صريحة تقول **ما** ينقص لا «غير جاهز».
        var company = await db.Companies.SingleAsync(c => c.Id == companyId);
        var sellerGaps = company.RegulatoryGaps();
        warnings.AddRange(sellerGaps);

        var ready = certOk && keyOk && apiOk && sellerGaps.Count == 0;
        return new ZatcaConfigurationStatus(settings.IsEnabled, certOk, keyOk, apiOk, ready, warnings);
    }
}

/// <summary>إرسال مستند ZATCA — في وضع المحاكاة يُقبل محلياً دون اتصال.</summary>
public class ZatcaSubmissionService(BrqoshDbContext db)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    /// <summary>
    /// إرسالٌ <b>بطلب المستخدم</b> — من شاشة ZATCA أو إعادة محاولةٍ يدوية. يتطلّب صلاحية
    /// العمليات المحاسبية، فهو قرارُ مستخدمٍ مستقلٌّ عن أيّ ترحيل.
    /// </summary>
    public async Task<ZatcaEInvoiceDocument> SubmitAsync(long documentId)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await SubmitCoreAsync(documentId);
    }

    /// <summary>
    /// (CEEF-229 · م2-١) إرسالٌ <b>كأثرٍ تابعٍ لترحيلٍ مأذونٍ به</b> — لا يُعيد فحص الدور، عمداً.
    ///
    /// <para><b>العطل الذي أوجب هذا الفصل، وكيف ظهر:</b> كان المسار التلقائيّ يستدعي
    /// <see cref="SubmitAsync"/> بحارسه، وذاك يرفض <c>Cashier</c>. وأمين الصندوق هو <b>مُشغّل
    /// نقطة البيع فعلاً</b>. فكانت كل بيعةٍ يُجريها تُنتج مستنداً يُرفض إرساله، ويبتلع
    /// <see cref="ZatcaAutoSubmissionHelper"/> الرفضَ مع أخطاء الشبكة، فيبقى المستند
    /// <c>ReadyToReport</c> بلا أن يعلم أحد: البيعة تنجح، والإيصال يُطبع، والفاتورة لا تصل
    /// الهيئة أبداً.</para>
    ///
    /// <para><b>ولم يكشفه اختبارٌ قطّ</b> لأن اختبارات نقطة البيع كانت تعمل بلا فاعل أصلاً
    /// (<c>BRQOSH_TEST=1</c> يُطفئ الطبقة)، فلم تُشغَّل نقطةُ البيع بدور أمين صندوق ولا مرّة.
    /// ظهر في اللحظة التي أُعطي فيها اختبارُ نقطة البيع فاعلَه الحقيقيّ.</para>
    ///
    /// <para><b>ولماذا لا يُخفَّض حارس <see cref="SubmitAsync"/> بدل ذلك:</b> لأن الإذن
    /// مُنح عند <b>البيعة</b> لا عند الإرسال. أمين الصندوق مأذونٌ له بالبيع، والإبلاغ عن البيع
    /// واجبٌ نظاميّ يتبعه لا عمليةٌ منفصلة يقرّرها. أمّا الإرسال اليدويّ لمستندٍ بعينه فقرارٌ
    /// مستقلّ، ويبقى حارسه. والاسم الصريح يجعل غياب الفحص هنا <b>مقروءاً ومقصوداً</b> لا ثغرة.</para>
    /// </summary>
    internal async Task<ZatcaEInvoiceDocument> SubmitAsFollowUpToPostingAsync(long documentId)
        => await SubmitCoreAsync(documentId);

    private async Task<ZatcaEInvoiceDocument> SubmitCoreAsync(long documentId)
    {
        var doc = await db.ZatcaEInvoiceDocuments.SingleAsync(d => d.Id == documentId);
        var settings = await db.ZatcaSettings.SingleAsync(s => s.CompanyId == doc.CompanyId);

        if (doc.ComplianceStatus is ZatcaComplianceStatus.Reported or ZatcaComplianceStatus.Cleared
            or ZatcaComplianceStatus.Accepted)
            throw new AccountingException("المستند مُرسل مسبقاً.");

        var status = await new ZatcaConfigurationValidator(db).ValidateAsync(doc.CompanyId);
        if (!status.ReadyForSubmission && settings.Environment == ZatcaEnvironment.Production)
            throw new AccountingException("إعدادات ZATCA غير جاهزة للإرسال: " + string.Join(" ", status.Warnings));

        if (settings.Environment == ZatcaEnvironment.Simulation)
        {
            // (CEEF-229 · م0-١٠) لا تُعلَّم مقبولةً — لم يرها أحد خارج هذا الجهاز.
            //
            // والتجربة الجافّة تبقى متاحة قبل اكتمال الإعداد — منعُها يُغلق حالة استخدام مشروعة —
            // لكنّ النواقص تُسجَّل في جسم الردّ بدل أن تُبتلع، فيعرف المالك ما يفصله عن الإرسال.
            var gaps = status.ReadyForSubmission ? [] : status.Warnings;
            doc.MarkSubmitted(
                ZatcaComplianceStatus.SimulatedLocally,
                JsonSerializer.Serialize(new
                {
                    simulation = true,
                    message = "مُحاكى محلياً — لم يُرسل إلى هيئة الزكاة والضريبة والجمارك",
                    readyForSubmission = status.ReadyForSubmission,
                    gaps,
                }));
            await db.SaveChangesAsync();
            return doc;
        }

        var xmlPath = doc.SignedXmlFilePath ?? doc.XmlFilePath;
        var xmlB64 = Convert.ToBase64String(await File.ReadAllBytesAsync(xmlPath));
        var payload = JsonSerializer.Serialize(new
        {
            invoiceHash = doc.InvoiceHash,
            uuid = doc.InvoiceUuid,
            invoice = xmlB64,
        });

        var request = BuildSubmissionRequest(
            doc.TransmissionMode, settings.ApiBaseUrl, payload,
            ZatcaSettingsSecrets.GetApiToken(settings));

        var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            doc.MarkRejected(body);
            await db.SaveChangesAsync();
            throw new AccountingException($"رفضت الهيئة المستند: {response.StatusCode}");
        }

        // (CEEF-237 · ز-٢) ‏**حالةُ الهيئة تُقرأ، لا تُفترض من رمز HTTP.**
        //
        // ‏كان أيُّ 2xx يُعلّم الفاتورة Cleared/Reported. **والهيئةُ تردّ 200 ومعها
        // ‏`clearanceStatus: NOT_CLEARED`** (أو `reportingStatus: NOT_REPORTED`) حين ترفض
        // ‏المحتوى وتقبل الطلب — فكانت فاتورةٌ مرفوضةٌ تُخزَّن «مخلَّصة»، ويُسلَّم المشتري
        // ‏مستنداً لا سندَ له، ولا يظهر الخللُ إلا في مطابقةِ الهيئة بعد شهور.
        var verdict = ReadAuthorityVerdict(body, doc.TransmissionMode);
        if (!verdict.Accepted)
        {
            doc.MarkRejected(body);
            await db.SaveChangesAsync();
            throw new AccountingException(
                $"لم تقبل الهيئةُ المستند: {verdict.Status}"
                + (verdict.Reason is null ? "" : $" — {verdict.Reason}"));
        }

        // ‏(م-٢ · ن-٠٥) ‏**الترتيبُ هو الإصلاح: يُكتب الملفُّ على القرص قبل أن تُطفَّر الحالة.**
        //
        // ‏كان `MarkSubmitted(Cleared)` يسبق `File.WriteAllBytesAsync`. فإن رمت الكتابةُ —
        // ‏قرصٌ ممتلئ، أو مجلّدٌ فقد صلاحيةَ الكتابة، أو مشاركةُ شبكةٍ انقطعت — لم يُنفَّذ
        // ‏`SaveChangesAsync` **هنا**، لكنّ الكيانَ صار `Cleared` في المتعقّب.
        //
        // ‏ثمّ تصعد الرميةُ إلى `ZatcaAutoSubmissionHelper` فيلتقطها ويستدعي
        // ‏`RecordSubmissionFailure` ثمّ `SaveChangesAsync` — **على الكيان المطفَّر نفسِه**،
        // ‏لأنّ خريطةَ هويّة EF تُعيد المثيلَ ذاته. فتُحفَظ ثلاثةُ أخطاءٍ معاً:
        // ‏  · الفاتورةُ «مخلَّصة» **بلا مستندٍ مخلَّص** — والمُخلَّصُ هو ما يُسلَّم للمشتري
        // ‏  · وملاحظتُنا تدهس **ردَّ الهيئة** المحفوظَ في `body`
        // ‏  · وبابُ إعادة المحاولة يُغلق: `Cleared` ليست حالةً يلتقطها إرسالٌ لاحق
        //
        // ‏وبالترتيب الجديد، رميةُ القرص تترك المستندَ في `ReadyToClear` — وهو بالضبط ما
        // ‏يعِد به تعليقُ المُساعد: «‏المستند يبقى بحالته فيلتقطه إرسالٌ لاحق».
        string? clearedPath = null;
        if (doc.TransmissionMode == ZatcaTransmissionMode.Clearance)
        {
            var cleared = ReadClearedInvoice(body);

            // ‏(م-٢ · ن-٠٥ تبعيّة) ‏**بابٌ ثانٍ إلى الحالة الكاذبة — حقلٌ غائبٌ لا رميةُ قرص.**
            //
            // ‏تردّ الهيئةُ `clearanceStatus: CLEARED` **وقد لا تحمل `clearedInvoice`** أو
            // ‏تحمله تالفاً — وهي حالةٌ يغطّيها
            // ‏`AnAbsentOrCorruptClearedInvoice_ReturnsNothingRatherThanThrowing` صراحةً،
            // ‏فيعود `ReadClearedInvoice` بـ`null`. وكان المستندُ يُعلَن «مخلَّصاً» **بلا
            // ‏مستندٍ مخلَّص** — عينَ ما أُغلق من باب رميةِ الكتابة، من بابٍ آخر.
            //
            // ‏**ويُرمى هنا لا في النطاق** — وإن كان النطاقُ يرفضها كذلك. فرسالةُ النطاق
            // ‏موجَّهةٌ إلى من يكتب الكود («اكتب المستندَ أوّلاً ثمّ مرّر مسارَه»)، وهذه ليست
            // ‏غلطةَ برمجةٍ بل **خبرٌ عن استجابةِ الهيئة يقرؤه مُشغِّل**. و`Describe` في
            // ‏`ZatcaAutoSubmissionHelper` يُمرّر نصَّ `AccountingException` كما هو ويُلحق
            // ‏باسمِ النوعِ ما عداه — فالفرقُ بين النوعين فرقُ رسالةٍ تُقرأ ورسالةٍ تُخيف.
            if (cleared is null)
                throw new AccountingException(
                    "قبلت الهيئةُ المستند ولم تُعِد نسخةً مخلَّصة — والنسخةُ المخلَّصةُ هي "
                    + "الوثيقةُ النظاميّةُ التي تُسلَّم للمشتري. لم تُعلَن الفاتورةُ مخلَّصةً، "
                    + "وردُّ الهيئة محفوظٌ كما ورد. راجِع الاستجابة قبل إعادة الإرسال — "
                    + "فقد تكون الفاتورةُ مخلَّصةً لديها.");

            clearedPath = Path.Combine(
                Path.GetDirectoryName(xmlPath)!,
                Path.GetFileNameWithoutExtension(xmlPath) + ".cleared.xml");
            await File.WriteAllBytesAsync(clearedPath, cleared);
        }

        // ‏نداءٌ واحدٌ ذرّيٌّ: الحالةُ والمسارُ معاً، والنطاقُ يرفض إحداهما بلا الأخرى.
        doc.MarkSubmitted(
            doc.TransmissionMode == ZatcaTransmissionMode.Reporting
                ? ZatcaComplianceStatus.Reported
                : ZatcaComplianceStatus.Cleared,
            body,
            clearedPath);

        await db.SaveChangesAsync();
        return doc;
    }

    /// <summary>حكمُ الهيئة كما ورد في جسم الردّ — لا كما يُستنتج من رمز HTTP.</summary>
    internal readonly record struct AuthorityVerdict(bool Accepted, string Status, string? Reason);

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٩) <b>بناءُ الطلب — مُستخرَجٌ كي يُقاس.</b>
    ///
    /// <para><b>وترويسةُ <c>Clearance-Status</c> كانت غائبة</b>: نداءُ
    /// <c>/invoices/clearance/single</c> يشترطها، <b>وغيابُها يردّ الطلبَ وإن صحّ المسارُ
    /// والحمولةُ والتوقيع</b> — وهو بعينه ما فعله غيابُ <c>Accept-Version</c> في CEEF-206.
    /// و<c>1</c> تعني «خلِّصْ هذه الفاتورة».</para>
    ///
    /// <para><b>ولا تُرسَل على مسار الإبلاغ</b>: المبسّطةُ تُبلَّغ ولا تُخلَّص.
    /// <b>والشرطُ هو الشرطُ نفسُه الذي يختار المسار</b> — لا فحصٌ ثانٍ قد يفترق عنه بعد تعديل.</para>
    ///
    /// <para><b>ولمَ يُستخرَج:</b> <c>HttpClient</c> ساكنٌ بلا درزٍ يُحقن، <b>فترويسةٌ داخل
    /// تابعِ الإرسال لا تُقاس إلّا بنداءٍ حقيقيٍّ للهيئة</b> — وهو ما لا يقع في اختبار.
    /// وهي السابقةُ نفسُها التي جعلت <see cref="ReadAuthorityVerdict"/> مُستخرَجاً.</para>
    /// </summary>
    internal static HttpRequestMessage BuildSubmissionRequest(
        ZatcaTransmissionMode mode, string apiBaseUrl, string payload, string apiToken)
    {
        var endpoint = mode == ZatcaTransmissionMode.Clearance
            ? "/invoices/clearance/single"
            : "/invoices/reporting/single";

        var request = new HttpRequestMessage(HttpMethod.Post, apiBaseUrl.TrimEnd('/') + endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        request.Headers.Authorization = ParseAuth(apiToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // (CEEF-206) واجهات الهيئة تشترط Accept-Version؛ غيابها يرفض الطلب رغم صحة المسار والحمولة.
        request.Headers.Add("Accept-Version", "V2");

        if (mode == ZatcaTransmissionMode.Clearance)
            request.Headers.Add("Clearance-Status", "1");

        return request;
    }

    internal static AuthorityVerdict ReadAuthorityVerdict(string body, ZatcaTransmissionMode mode)
    {
        JsonElement root;
        try { root = JsonDocument.Parse(body).RootElement; }
        catch (JsonException)
        {
            // ‏ردٌّ ليس JSON بعد 2xx: لا يُفترض قبولاً.
            return new AuthorityVerdict(false, "ردٌّ غيرُ مقروء", Truncate(body, 200));
        }

        var field = mode == ZatcaTransmissionMode.Reporting ? "reportingStatus" : "clearanceStatus";
        var status = root.TryGetProperty(field, out var s) ? s.GetString() ?? "" : "";

        // ‏غيابُ الحقل نفسِه إخفاقٌ مُعلَن: لا يُقرأ صمتُ الهيئة قبولاً.
        if (string.IsNullOrWhiteSpace(status))
            return new AuthorityVerdict(false, $"{field} غائب", FirstMessage(root));

        var accepted = status.Equals("CLEARED", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("REPORTED", StringComparison.OrdinalIgnoreCase);
        return new AuthorityVerdict(accepted, status, accepted ? null : FirstMessage(root));
    }

    private static string? FirstMessage(JsonElement root)
    {
        foreach (var name in new[] { "errorMessages", "warningMessages" })
        {
            if (root.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array
                && arr.GetArrayLength() > 0)
            {
                var first = arr[0];
                return first.TryGetProperty("message", out var m) ? m.GetString() : first.ToString();
            }
        }
        return null;
    }

    /// <summary>الفاتورةُ المختومة من الهيئة (base64) إن وُجدت.</summary>
    internal static byte[]? ReadClearedInvoice(string body)
    {
        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            if (!root.TryGetProperty("clearedInvoice", out var el)) return null;
            var b64 = el.GetString();
            return string.IsNullOrWhiteSpace(b64) ? null : Convert.FromBase64String(b64);
        }
        catch (Exception e) when (e is JsonException or FormatException) { return null; }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private static AuthenticationHeaderValue ParseAuth(string token)
    {
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return new AuthenticationHeaderValue("Bearer", token[7..].Trim());
        if (token.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return AuthenticationHeaderValue.Parse(token);
        return new AuthenticationHeaderValue("Basic", token);
    }
}
