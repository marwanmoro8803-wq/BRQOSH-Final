namespace Brqosh.Infrastructure.Webhooks;

/// <param name="TenantId">
/// (م-٣ · ن-٢٧) <b>هويّةُ المستأجر — وكان النوعُ لا يحملها، فالعزلُ مستحيلٌ بنيويّاً لا منسيّاً.</b>
///
/// <para>‏<c>GET /api/v1/webhooks/subscriptions</c> كان يُعيد <c>List()</c> — <b>قائمةَ العمليّة
/// كلَّها</b> — لأيّ حاملِ مفتاح. فتُكشف <b>عناوينُ وجهةٍ سجّلها مستأجرون آخرون</b>، وهي
/// عناوينُ أنظمتِهم الداخليّة. <b>ولا يُرشَّح ما لا يُسجَّل</b>، فالعلاجُ يبدأ من النوع.</para>
///
/// <para>و<c>null</c> معناها «سُجّلت بلا هويّةِ مستأجر» — وهو الوضعُ أحاديُّ المستأجر
/// (التنصيبُ المحلّيّ)، وفيه لا يوجد من يُعزَل عنه.</para>
/// </param>
public sealed record WebhookSubscription(
    long Id, Guid? TenantId, string EventType, string TargetUrl, DateTime CreatedAtUtc);

public sealed record WebhookSubscribeRequest(string EventType, string TargetUrl);

/// <summary>سجل اشتراكات Webhook — في الذاكرة (M9) · DB لاحقاً في SaaS.</summary>
public static class WebhookSubscriptionRegistry
{
    private static long _nextId = 1;
    private static readonly List<WebhookSubscription> Items = [];
    private static readonly Lock Gate = new();

    public static WebhookSubscription Register(
        string eventType, string targetUrl, Guid? tenantId = null, bool allowInsecureLocal = false)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("eventType required");
        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out _))
            throw new ArgumentException("targetUrl must be absolute URI");

        WebhookUrlValidator.ValidateOrThrow(targetUrl, allowInsecureLocal);

        lock (Gate)
        {
            var sub = new WebhookSubscription(
                _nextId++, tenantId, eventType.Trim(), targetUrl.Trim(), DateTime.UtcNow);
            Items.Add(sub);
            return sub;
        }
    }

    /// <summary>
    /// (م-٣ · ن-٢٧) <b>سردٌ غيرُ معزول — والاسمُ يقولها.</b>
    ///
    /// <para>كان اسمُه <c>List()</c>، ومسارُ الواجهة يناديه فيُعيد قائمةَ العمليّة كلَّها لكلّ
    /// حاملِ مفتاح. <b>والاسمُ الحياديُّ هو ما جعل الخطأَ يبدو صواباً</b> — من يقرأ
    /// <c>List()</c> يظنّه «القائمة»، ولا يسأل: قائمةُ مَن؟ فصار الاسمُ يُصرّح بما يفعل،
    /// <b>ولا يُنادى من مسارٍ يخدم طلبَ مستأجر</b>. استعمل <see cref="ListForTenant"/>.</para>
    /// </summary>
    public static IReadOnlyList<WebhookSubscription> ListAll()
    {
        lock (Gate)
            return Items.ToList();
    }

    /// <summary>
    /// (م-٣ · ن-٢٧) اشتراكاتُ مستأجرٍ واحدٍ وحدَه.
    ///
    /// <para>و<c>tenantId == null</c> تعني وضعاً أحاديَّ المستأجر، فتُعاد الاشتراكاتُ التي
    /// سُجّلت كذلك — <b>لا كلُّ شيء</b>. فمستأجرٌ سجّل بهويّةٍ لا تظهر لمن لا هويّةَ له،
    /// والمسارُ نفسُه يرفض السردَ بلا هويّةٍ متى كان العزلُ مُلزَماً.</para>
    /// </summary>
    public static IReadOnlyList<WebhookSubscription> ListForTenant(Guid? tenantId)
    {
        lock (Gate)
            return Items.Where(s => s.TenantId == tenantId).ToList();
    }

    /// <summary>
    /// <b>غيرُ معزولٍ بعدُ — وهذا مكتوبٌ لا مسكوتٌ عنه.</b>
    ///
    /// <para>التسليمُ يجب أن يُعزَل بالمستأجر كذلك. <b>لكنّه لا يقع اليوم أصلاً</b>:
    /// <c>OutboxDispatchHook</c> غيرُ موصولٍ عند الإقلاع (بند ن-٠٩)، فلا مستدعيَ إنتاجيّاً
    /// لـ<c>WebhookDispatcher</c>. <b>ولا يُصمَّم عزلٌ لمسارٍ لا يعمل</b> — تُخمَّن عندئذٍ
    /// إشارةُ المستأجر ومصدرُها، ويُكتب معاملٌ لا يمرّره أحد. فيُغلق مع وصلِ ن-٠٩.</para>
    /// </summary>
    public static IReadOnlyList<WebhookSubscription> ListForEvent(string eventType)
    {
        lock (Gate)
        {
            return Items
                .Where(s => s.EventType == "*" ||
                            string.Equals(s.EventType, eventType, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s.EventType + "Event", eventType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public static void ResetForTests()
    {
        lock (Gate)
        {
            Items.Clear();
            _nextId = 1;
        }
    }
}
