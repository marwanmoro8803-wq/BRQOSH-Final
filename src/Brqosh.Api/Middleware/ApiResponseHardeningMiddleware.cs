using System.Text.RegularExpressions;

namespace Brqosh.Api.Middleware;

/// <summary>
/// (جاهزيّةُ الإنتاج · C/F) <b>ترويساتُ الأمان ومعرّفُ الترابط وآخرُ مُلتقِطٍ للاستثناء — في موضعٍ واحد.</b>
///
/// <para><b>ما كان:</b> الواجهةُ لا تضع ترويسةَ أمانٍ واحدة (لا <c>nosniff</c> ولا منعَ تأطير ولا <c>no-store</c>) —
/// وردودُها أرصدةُ عملاءَ وفواتير، فوكيلٌ أو متصفّحٌ يخزّنها ويُعيدها لغير صاحبها. <b>ولا مُلتقِطَ عامّ</b>: استثناءٌ في
/// مسارٍ يبلغ Kestrel فيُردّ ٥٠٠ فارغاً بلا ما يربطه بسطرٍ في السجلّ، والدعمُ يبحث بين آلاف الأسطر.</para>
///
/// <para><b>ما صار:</b> كلُّ ردٍّ يحمل <c>X-Correlation-Id</c> (يُعاد ما أرسله العميلُ إن كان سليمَ الشكل، وإلّا
/// <c>TraceIdentifier</c>)؛ والاستثناءُ غيرُ الملتقَط يُسجَّل كاملاً بالمعرّف نفسِه <b>ويُردّ للعميل نوعُه العامُّ والمعرّفُ
/// وحدَهما</b> — فنصُّ الاستثناء (مضيفٌ، اسمُ جدول، مقطعُ SQL) لا يخرج إلى طالبٍ لا نعرفه، والدعمُ يجد السطرَ بالمعرّف.</para>
///
/// <para><b>و<c>Content-Security-Policy</c> لا تُطبَّق على <c>/swagger</c></b>: الواجهةُ التفاعليّةُ تحتاج سكربتاتِها، وهي
/// لا تُفتح إلّا في بيئة التطوير (<c>Program.cs</c>). ما عداها JSON محضٌ لا يُنفَّذ فيه شيء، فـ<c>default-src 'none'</c> صحيحة.</para>
/// </summary>
public sealed class ApiResponseHardeningMiddleware(RequestDelegate next, ILogger<ApiResponseHardeningMiddleware> logger)
{
    /// <summary>ترويسةُ معرّف الترابط — تُقرأ من الطلب وتُكتب في الردّ.</summary>
    public const string CorrelationHeader = "X-Correlation-Id";

    // ‏معرّفٌ يرسله العميل يُقبل بشكلٍ محدود فقط: لا يُحقن في السجلّ سطرٌ جديد ولا نصٌّ طويل.
    private static readonly Regex SafeCorrelationId = new("^[A-Za-z0-9._:-]{1,64}$", RegexOptions.CultureInvariant);

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationHeader, out var presented)
                            && SafeCorrelationId.IsMatch(presented.ToString())
            ? presented.ToString()
            : context.TraceIdentifier;
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            ApplyHeaders(context, correlationId);
            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // ‏العميلُ قطع الاتّصال — لا ردَّ يُكتب، ولا يُسجَّل عطلاً بل أثراً على مستوى التصحيح.
            logger.LogDebug("Request aborted by the client {CorrelationId} {Method} {Path}",
                correlationId, context.Request.Method, context.Request.Path.Value);
        }
        catch (BadHttpRequestException bad) when (!context.Response.HasStarted)
        {
            // ‏طلبٌ معيبٌ من العميل (معاملٌ ناقص، جسمٌ لا يُقرأ) — رمزُه يحمله الاستثناء (٤٠٠/٤١٣…) وليس عطلاً عندنا.
            logger.LogInformation("Bad request {CorrelationId} {Method} {Path}: {Reason}",
                correlationId, context.Request.Method, context.Request.Path.Value, bad.Message);
            context.Response.Clear();
            ApplyHeaders(context, correlationId);
            context.Response.StatusCode = bad.StatusCode;
            await context.Response.WriteAsJsonAsync(new { error = "bad_request", correlationId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled API exception {CorrelationId} {Method} {Path}",
                correlationId, context.Request.Method, context.Request.Path.Value);

            if (context.Response.HasStarted)
                throw;   // ‏بدأ الردُّ: لا يمكن استبدالُه، والخادمُ يقطعه — والسجلُّ كُتب.

            context.Response.Clear();
            ApplyHeaders(context, correlationId);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "internal_error", correlationId });
        }
    }

    internal static void ApplyHeaders(HttpContext context, string correlationId)
    {
        var headers = context.Response.Headers;
        headers[CorrelationHeader] = correlationId;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        if (!context.Request.Path.StartsWithSegments("/swagger"))
        {
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            // ‏أرصدةٌ وفواتير: لا وكيلَ ولا متصفّحَ يخزّنها.
            headers.CacheControl = "no-store";
            headers.Pragma = "no-cache";
        }
    }
}
