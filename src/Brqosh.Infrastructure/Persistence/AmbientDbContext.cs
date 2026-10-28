namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// السياق الجاري داخل معاملة <see cref="UnitOfWork"/> — يتيح لناشر الأحداث الانضمام
/// إلى نفس المعاملة بدل فتح اتصال منفصل .
///
/// لماذا سياق محيط بدل تمرير db في التوقيعات؟ النشر يحدث في 37 موضعاً عبر
/// <c>DomainEventHub</c> الساكن؛ تمرير السياق يدوياً يعني تغييراً واسعاً وخطر نسيان موضع
/// واحد يعيد الثغرة صامتاً. السياق المحيط يجعل الانضمام للمعاملة هو السلوك الافتراضي.
///
/// AsyncLocal آمن هنا: النشر يقع داخل نفس سلسلة async للمعاملة (لا عبر نقاط دخول WPF).
/// </summary>
public static class AmbientDbContext
{
    private static readonly AsyncLocal<BrqoshDbContext?> Slot = new();

    public static BrqoshDbContext? Current => Slot.Value;

    /// <summary>يربط السياق لمدة المعاملة، ويفكّه عند التخلص — يدعم التعشيش.</summary>
    public static IDisposable Bind(BrqoshDbContext db)
    {
        var previous = Slot.Value;
        Slot.Value = db;
        return new Scope(previous);
    }

    private sealed class Scope(BrqoshDbContext? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Slot.Value = previous;
        }
    }
}
