// ‏(Phase 7 · تجهيزُ تجربة ZATCA في بيئة المحاكاة) فحصٌ محلّيٌّ بلا اتصال لكلّ ما يسبق OTP.
//
// ‏يقرأ إعدادَ التسجيل نفسَه الذي يقرؤه POST /zatca-signer/onboard — متغيّراتُ البيئة BRQOSH_ZATCA_*:
//   ENVIRONMENT (الافتراضيّ simulation) · VAT · CR · ORGANIZATION_NAME · BRANCH · ADDRESS · BUSINESS_CATEGORY
//   · INVOICE_TYPE (1100) · SOLUTION_NAME · OTP (اختياريّ — لا يُستعمل هنا) · API_BASE_URL (اختياريّ)
// ‏ويكتب في مجلّد الإخراج: request.csr، وعيّناتِ الامتثال المختومة (*.xml)، وpreflight-report.txt.
// ‏لا يُرسل شيئاً، ولا يكتب مفتاحاً خاصّاً (المفتاحُ في الذاكرة ويُرمى)، ولا يمسّ قاعدةَ بيانات.
//
// ‏الخروج: 0 جاهز · 1 فحصٌ فشل · 2 إعدادٌ ناقص.
// ‏التشغيل: dotnet run --project tools/ZatcaPreflight -- [مجلّد الإخراج]   (أو scripts/zatca-simulation-preflight.ps1)

using System.Text;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Zatca;

Console.OutputEncoding = Encoding.UTF8;

string? Config(string key)
{
    var value = Environment.GetEnvironmentVariable(key);
    if (string.IsNullOrWhiteSpace(value) && key == "BRQOSH_ZATCA_ENVIRONMENT") return "simulation";
    return value;
}

ZatcaOnboardingOptions options;
try
{
    options = ZatcaOnboardingOptions.FromConfiguration(Config);
}
catch (AccountingException ex)
{
    Console.Error.WriteLine("الإعدادُ ناقص: " + ex.Message);
    Console.Error.WriteLine("المتغيّرات: BRQOSH_ZATCA_VAT, BRQOSH_ZATCA_ORGANIZATION_NAME, BRQOSH_ZATCA_ADDRESS, BRQOSH_ZATCA_BUSINESS_CATEGORY, ...");
    return 2;
}

var output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "brqosh-zatca-preflight-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
var report = ZatcaSimulationPreflight.Run(options);
var path = ZatcaSimulationPreflight.WriteTo(report, output);

Console.WriteLine(ZatcaSimulationPreflight.Format(report));
Console.WriteLine($"الملفّات: {output}");
Console.WriteLine($"التقرير : {path}");
return report.Ready ? 0 : 1;
