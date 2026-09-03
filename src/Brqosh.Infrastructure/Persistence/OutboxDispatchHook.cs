namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// خطّاف ما بعد الالتزام لتفريغ طابور الأحداث (Outbox — ع-D2 / CEEF-204).
///
/// <see cref="UnitOfWork"/> ساكن ولا يملك حاوية حقن، فيُوصَل المرسِل من تسجيل DI
/// على نمط <c>DomainEventHub</c> المتّبع في المشروع. إن لم يُوصَل — كما في اختبارات
/// الوحدة — لا يحدث شيء، وتبقى الأحداث في الطابور بأمان.
/// </summary>
public static class OutboxDispatchHook
{
    private static Func<BrqoshDbContext, CancellationToken, Task>? _handler;

    public static void Wire(Func<BrqoshDbContext, CancellationToken, Task> handler) =>
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    public static void Reset() => _handler = null;

    internal static async Task OnCommittedAsync(BrqoshDbContext db, CancellationToken ct)
    {
        var handler = _handler;
        if (handler is null)
            return;

        try
        {
            await handler(db, ct);

            // ‏**والنجاحُ يمحو الأثر** — وإلّا بقي عطلٌ أُصلح معروضاً في شاشة الحالة
            // ‏إلى أن يُغلق التطبيق، **فيُبحث عن سببٍ لم يعد قائماً**.
            LastFailure = null;
        }
        catch (Exception ex)
        {
            // ‏**التسامحُ يبقى ولا يُنزَع**: الإرسالُ الخارجيّ لا يُفشل عمليةً محاسبيةً
            // ‏التُزمت بالفعل، والأحداثُ غير المُرسَلة تبقى معلَّقةً لإعادة المحاولة.
            //
            // ‏(CEEF-231 · O-36) **لكنّ الابتلاعَ الصامتَ كان يُخفي عطلاً دائماً**:
            // ‏نقطةُ استقبالٍ ماتت، أو شهادةٌ انتهت، أو مفتاحٌ بُدِّل — فيتراكم الطابورُ
            // ‏شهوراً و**لا سطرَ واحدٌ يقول لماذا**. والقِدَمُ في الطابور علامةُ عطلٍ لا
            // ‏علامةُ انتهاء (انظر `DataRetentionService`).
            LastFailure = ex.GetBaseException().Message;
            Operations.CrashLog.Write(ex, "OutboxDispatchHook.OnCommitted");
        }
    }

    /// <summary>
    /// آخرُ سببِ فشلٍ في تفريغ الطابور — <b>لشاشة حالة النظام</b>، و<c>null</c> إن لم يقع.
    /// </summary>
    /// <remarks>
    /// <b>وهو أثرٌ في الذاكرة لا سجلٌّ</b>: السجلُّ على القرص في <c>CrashLog</c>، وهذا
    /// <b>لتُقرأ الحالةُ من الشاشة بلا فتح ملفّ</b>.
    /// </remarks>
    public static string? LastFailure { get; private set; }

    /// <summary>يمحو الأثرَ — <b>للاختبارات وحدَها</b>، فحالةٌ ساكنةٌ تتسرّب بينها.</summary>
    internal static void ResetFailureForTests() => LastFailure = null;
}
