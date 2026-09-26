// مسبارُ CSID الامتثال على بيئةٍ غير إنتاجيّة (جاهزيّةُ الإطلاق · tax-zatca#15)
//
// ما كان: ZatcaLiveCsidTests في حزمة الاختبار — يرجع بلا OTP، فيُعدّ «Passed» في كلّ تشغيلٍ في 0.0008s
// ولم يُرسل شيئاً قطّ، ويُحسب في «صفرٌ متخطًّى». وطلبٌ حيٌّ برمزٍ من بوّابة الهيئة لا يكون اختبارَ انحدار:
// إمّا «نجح» ولم يجرِ، وإمّا «تُخطّي» فتسقط بوّابةُ CI التي تشترط صفراً. فصار أداةً يُشغّلها مَن يملك الرمز،
// وتقول «لم يُرسَل شيء» بخروجٍ غير الصفر حين لا تُعطاه.
//
// آمن بثلاثة قيود، كلٌّ قبل ما يحرسه:
//   [1] جذرُ بياناتٍ مؤقّتٌ قبل أيّ مفتاح — GenerateKeyPairAndCsr يكتب private.key تحت جذر البيانات،
//       وجذرُ المُشغِّل الحقيقيُّ فيه مفتاحُ ختمه؛ فالمسبارُ لا يستبدله أبداً.
//   [2] بلا OTP أو رقمٍ ضريبيّ: لا طلب، والخروجُ 2.
//   [3] عنوانُ الإنتاج (/core) مرفوض، والخروجُ 3 — رمزُ الإنتاج يُسجّل وحدةَ إصدارٍ حقيقيّةً للمنشأة،
//       والمسبارُ يرمي مفتاحَها بعده. والإنتاجُ طريقُه معالجُ CSID في التطبيق.
//
// ويقيس ما يُرسله المنتجُ كما هو: الـCSR نفسُه الذي يولّده المعالج (القالبُ الافتراضيّ للمحاكاة)،
// وطلبُ /compliance نفسُه — فإن رفضته الهيئةُ فالرفضُ دليلٌ على المنتج لا على المسبار.
//
// التشغيل:
//   $env:BRQOSH_ZATCA_OTP = "<OTP>"; $env:BRQOSH_ZATCA_VAT = "<VAT15>"
//   dotnet run --project tools/ZatcaLiveCsidProbe
// واختياريّاً BRQOSH_ZATCA_ORG و BRQOSH_ZATCA_API_URL (الافتراضيّ بوّابةُ المطوّرين).

using System.Text;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Zatca;

Console.OutputEncoding = Encoding.UTF8;

// [1] الجذرُ أوّلاً.
var root = Directory.CreateTempSubdirectory("brqosh-zatca-probe-").FullName;
Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, root);

const string DeveloperPortal = "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal";

var otp = Environment.GetEnvironmentVariable("BRQOSH_ZATCA_OTP");
var vat = Environment.GetEnvironmentVariable("BRQOSH_ZATCA_VAT");
var org = Environment.GetEnvironmentVariable("BRQOSH_ZATCA_ORG") is { Length: > 0 } o ? o : "BRQOSH Probe";
var apiUrl = Environment.GetEnvironmentVariable("BRQOSH_ZATCA_API_URL") is { Length: > 0 } u ? u : DeveloperPortal;

// [2] بلا مدخلاتٍ لا طلب.
if (string.IsNullOrWhiteSpace(otp) || string.IsNullOrWhiteSpace(vat))
{
    Console.Error.WriteLine("لم يُرسَل شيء: BRQOSH_ZATCA_OTP و BRQOSH_ZATCA_VAT مطلوبان.");
    return 2;
}

// [3] الإنتاجُ ليس لهذا المسبار — يُحكم بآخر مقطعٍ في المسار، لا بوجود الكلمة في أيّ موضع.
var path = Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri) ? uri.AbsolutePath.TrimEnd('/') : "";
if (!path.EndsWith("/developer-portal", StringComparison.OrdinalIgnoreCase)
    && !path.EndsWith("/simulation", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"لم يُرسَل شيء: {apiUrl} ليس بوّابةَ المطوّرين ولا المحاكاة. "
                          + "رمزُ الإنتاج يُسجّل وحدةَ إصدارٍ حقيقيّة — وطريقُه معالجُ CSID في التطبيق.");
    return 3;
}

Console.WriteLine($"DataRoot : {root}");
Console.WriteLine($"Endpoint : {apiUrl.TrimEnd('/')}/compliance");

var service = new ZatcaCsidOnboardingService();
try
{
    var material = service.GenerateKeyPairAndCsr(1, org, vat);
    var result = await service.RequestComplianceCsidAsync(apiUrl, otp, material.CsrBase64, 1);

    if (string.IsNullOrWhiteSpace(result.RequestId) || !File.Exists(result.CertificatePemPath))
    {
        Console.Error.WriteLine("[FAIL] ردٌّ بلا requestID أو بلا شهادةٍ مكتوبة.");
        return 1;
    }

    Console.WriteLine($"[OK] requestID   : {result.RequestId}");
    Console.WriteLine($"[OK] certificate : {result.CertificatePemPath}");
    return 0;
}
catch (Exception ex) when (ex is AccountingException or HttpRequestException or TaskCanceledException)
{
    Console.Error.WriteLine($"[FAIL] {ex.GetType().Name}: {ex.Message}");
    return 1;
}
