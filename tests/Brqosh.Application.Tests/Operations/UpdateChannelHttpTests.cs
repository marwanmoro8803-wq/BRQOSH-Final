using System.Net;
using System.Security.Cryptography;
using System.Text;
using Brqosh.Infrastructure.Operations;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (CEEF-231 · O-33) <b>قناةُ التحديث عبر الشبكة — والتوقيعُ يبقى شرطاً، والتنصيبُ بيد المالك.</b>
///
/// <para><b>ما كان:</b> أُغلق <c>ط5-٥</c> بقناةٍ تقرأ <b>مسارَ ملفٍّ أو مشاركةَ شبكة</b>،
/// وهو صحيحٌ لمنشآتٍ على شبكةٍ مغلقة. <b>لكنّ البائعَ لا يملك مشاركةً في شبكة كلّ
/// عميل</b> — فبقي <c>O-33</c> مفتوحاً: منشأةٌ تُشغّل نسخةً فيها عطلٌ أُصلح منذ شهور
/// <b>ولا شيءَ يخبرها</b>، والبائعُ يتّصل بعملائه واحداً واحداً.</para>
///
/// <para><b>ولم يُنزَع شيءٌ ممّا حَرَس.</b> التوقيعُ شرطٌ كما كان، والبصمةُ من البيان
/// الموقَّع لا من الخادم، <b>والتراجعُ مرفوض</b>، <b>ولا تُنصَّب حزمةٌ آلياً</b>.
/// والمُضافُ ناقلٌ فقط.</para>
///
/// <para><b>و<c>http://</c> يُرفض ولا يُرقّى صامتاً.</b> فالتوقيعُ يحمي المحتوى،
/// <b>لكنّ القناةَ المكشوفة تُمكّن من حجب التحديث</b>: يُخدَم بيانٌ قديمٌ توقيعُه صحيح
/// فيبقى العميلُ على إصدارٍ فيه ثغرةٌ أُصلحت — <b>وهو بعينه ما بُني رفضُ التراجع
/// لمنعه</b>. ورفعُ العنوان إلى https من عند البرنامج <b>يُخفي عن المالك أنّ قناتَه
/// مكشوفة</b>.</para>
///
/// <para><b>وهويّةُ ح-٨ باقيةٌ على الناقل الجديد</b>: ما لا يُثبت توقيعُه لا يُقبَل،
/// <b>وانقطاعُ الشبكة يُقرأ «لا قناة» لا «مُلاعَبٌ بها»</b>.</para>
/// </summary>
public class UpdateChannelHttpTests : IDisposable
{
    private readonly RSA _publisher = RSA.Create(2048);

    private const string Endpoint = "https://updates.example.test/brqosh/update.json";

    public UpdateChannelHttpTests() =>
        UpdateCheckService.PublicKeyPemOverride = _publisher.ExportSubjectPublicKeyInfoPem();

    public void Dispose()
    {
        UpdateCheckService.PublicKeyPemOverride = null;
        _publisher.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم: بيانٌ موقَّعٌ يصل عبر https فيُقرأ «يوجد تحديث».</b>
    /// </summary>
    [Fact]
    public async Task ASignedManifestOverHttps_ReportsAnUpdate()
    {
        var json = UpdateCheckService.CreateSignedJson(
            Manifest("999.0.0"), _publisher.ExportRSAPrivateKeyPem());

        var result = await Service(StubHandler.Ok(json)).CheckAsync(Endpoint);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("999.0.0", result.AvailableVersion);
    }

    /// <summary>
    /// <b>و<c>http</c> يُرفض بالاسم — ولا يُرقّى إلى <c>https</c> من عند البرنامج.</b>
    ///
    /// <para><b>والترقيةُ الصامتةُ أسوأُ من الرفض</b>: تُخفي عن المالك أنّ ما كتبه
    /// مكشوف، <b>فيبقى مكتوباً هكذا في كلّ تثبيتٍ بعده</b>.</para>
    /// </summary>
    [Fact]
    public async Task APlainHttpChannel_IsRefused_AndNotUpgraded()
    {
        var handler = StubHandler.Ok("{}");

        var result = await Service(handler).CheckAsync("http://updates.example.test/update.json");

        Assert.Equal(UpdateStatus.Untrusted, result.Status);
        Assert.Contains("https", result.Failure!, StringComparison.OrdinalIgnoreCase);

        // ‏**ولا طلبَ خرج أصلاً** — والرفضُ قبل الاتصال لا بعده.
        Assert.Equal(0, handler.Calls);
    }

    /// <summary>
    /// <b>وانقطاعُ الشبكة يُقرأ «لا قناة» — لا «مُلاعَبٌ بها».</b>
    ///
    /// <para><b>والفرقُ ليس لفظياً</b>: «مُلاعَبٌ به» يعني «اتّصلْ بالدعم فوراً»،
    /// و«لا قناة» يعني «أعِدْ لاحقاً». <b>ومنشأةٌ خلف جدارٍ ناريٍّ ترى الثانيةَ كلَّ
    /// يوم</b> — فإنذارٌ كاذبٌ يوميٌّ يُدرَّب المالكُ على تجاهله، <b>فلا يقرؤه يومَ
    /// يقع التلاعبُ حقاً</b>.</para>
    /// </summary>
    [Fact]
    public async Task ANetworkFailure_IsNoChannel_NotUntrusted()
    {
        var result = await Service(StubHandler.Throws()).CheckAsync(Endpoint);

        Assert.Equal(UpdateStatus.NoChannel, result.Status);
    }

    /// <summary>
    /// <b>وردٌّ غيرُ ناجحٍ كذلك — فـ404 قناةٌ لم تُهيَّأ لا هجوم.</b>
    /// </summary>
    [Fact]
    public async Task ANotFoundResponse_IsNoChannel()
    {
        var result = await Service(StubHandler.Status(HttpStatusCode.NotFound)).CheckAsync(Endpoint);

        Assert.Equal(UpdateStatus.NoChannel, result.Status);
    }

    /// <summary>
    /// <b>وبيانٌ يتجاوز الحدَّ يُرفض — ونقطةٌ تبثّ بلا نهايةٍ لا تُقرأ.</b>
    ///
    /// <para><b>والحدُّ يُطبَّق على المقروء لا على المُعلَن</b>: ترويسةُ طولٍ كاذبةٌ أو
    /// غائبةٌ لا تُلزم أحداً، <b>وخادمٌ يبثّ بلا ترويسةٍ يستنزف الذاكرة</b> قبل أن يبلغ
    /// التحقّقُ من التوقيع سطرَه الأوّل.</para>
    /// </summary>
    [Fact]
    public async Task AnOversizedManifest_IsRefused_WithoutADeclaredLength()
    {
        var result = await Service(StubHandler.Flood()).CheckAsync(Endpoint);

        Assert.Equal(UpdateStatus.Untrusted, result.Status);
        // ‏**والمقارنةُ بجذرٍ بلا تشكيل عمداً**: «الحدّ» و«الحدَّ» يبدوان سواءً وليسا،
        // ‏وهو مطبُّ `I18nCoverageTests` نفسُه — وقد أوقعني في يوم كتابة هذا الحارس.
        Assert.Contains("تجاوز", result.Failure!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وطولٌ مُعلَنٌ يتجاوز الحدَّ يُرفض قبل قراءة بايتٍ واحد.</b>
    ///
    /// <para><b>وهذا نصفُ الحدّ الثاني</b>: ترويسةٌ صادقةٌ تقول «عشرةُ ميجابايت» تُغني
    /// عن تنزيلها كلِّها، <b>وحارسٌ يقيس فرعَ القراءة وحدَه يمرّ على فرعِ الترويسة
    /// معطَّلاً</b>.</para>
    /// </summary>
    [Fact]
    public async Task AnOversizedDeclaredLength_IsRefused_BeforeReading()
    {
        var handler = StubHandler.Declared(10 * 1024 * 1024);

        var result = await Service(handler).CheckAsync(Endpoint);

        Assert.Equal(UpdateStatus.Untrusted, result.Status);
        Assert.Contains("أكبر", result.Failure!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وح-٨ باقيةٌ على الناقل الجديد: عبثٌ بعد التوقيع يُرفض ولو وصل عبر https.</b>
    ///
    /// <para><b>وهذا هو الهجومُ الحقيقيّ</b> — لا انتحالُ القناة بل تبديلُ بصمة الحزمة
    /// في بيانٍ صحيحٍ في كلّ ما عداها.</para>
    /// </summary>
    [Fact]
    public async Task ATamperedManifestOverHttps_IsUntrusted()
    {
        var json = UpdateCheckService.CreateSignedJson(
            Manifest("999.0.0"), _publisher.ExportRSAPrivateKeyPem());

        var tampered = json.Replace("\"PackageSha256\": \"", "\"PackageSha256\": \"00", StringComparison.Ordinal);
        Assert.NotEqual(json, tampered);

        var result = await Service(StubHandler.Ok(tampered)).CheckAsync(Endpoint);

        Assert.Equal(UpdateStatus.Untrusted, result.Status);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private static UpdateCheckService Service(HttpMessageHandler handler) =>
        new() { Transport = handler };

    private static UpdateManifest Manifest(string version) =>
        new(version, "2026-09-03", "BRQOSH-Setup.msi",
            Convert.ToHexString(SHA256.HashData(new byte[] { 7, 7, 7 })), 3, "إصلاحات");

    /// <summary>ناقلٌ مُصطنَع — <b>ولا شبكةَ في اختبار</b>.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _respond;

        public int Calls { get; private set; }

        private StubHandler(Func<HttpResponseMessage> respond) => _respond = respond;

        public static StubHandler Ok(string body) => new(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

        public static StubHandler Status(HttpStatusCode code) => new(() => new HttpResponseMessage(code)
        {
            Content = new StringContent("", Encoding.UTF8, "application/json"),
        });

        public static StubHandler Throws() => new(() => throw new HttpRequestException("no route to host"));

        /// <summary>ردٌّ يُعلن طولاً يتجاوز الحدَّ — <b>ولا يُقرأ منه شيء</b>.</summary>
        public static StubHandler Declared(long length) => new(() =>
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            content.Headers.ContentLength = length;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        /// <summary>ردٌّ ضخمٌ <b>بلا ترويسةِ طول</b> — فالحدُّ يُقاس على المقروء.</summary>
        public static StubHandler Flood() => new(() =>
        {
            var content = new StreamContent(new EndlessStream());
            content.Headers.ContentLength = null;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_respond());
        }
    }

    /// <summary>تيّارٌ لا ينتهي — <b>وهو ما يفعله خادمٌ مخترَق</b>.</summary>
    private sealed class EndlessStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Fill(buffer, (byte)'x', offset, count);
            return count;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
