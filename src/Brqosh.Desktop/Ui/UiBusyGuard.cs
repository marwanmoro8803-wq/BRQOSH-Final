using System.Windows.Controls.Primitives;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (CEEF-229 · م0-١٦) يمنع إعادة دخول معالج الترحيل — أي ازدواج المستند بضغطتين.
///
/// <para><b>العطل:</b> كل معالجات الترحيل بنمط <c>async void</c>: تبني المستند، تنتظر الترحيل،
/// ثم تُغلق الحوار. والزرّ <b>لا يُعطَّل أثناء الانتظار</b>. فضغطة مزدوجة — وهي عادة اللمس السريع
/// على شاشة نقطة بيع — تُنفّذ المعالج مرّتين، وكلٌّ منهما يبني <b>مستنداً جديداً</b>: فاتورتان،
/// وخصم مخزون مرّتين، وإيراد مضاعف، وسندا ZATCA. ولا يُنقذ منه <c>EnsureDraft</c> لأن كل نداء
/// يملك كياناً مستقلاً.</para>
///
/// <para>ويضاعفه أن أربعين حواراً تضع <c>IsDefault="True"</c> على زرّ الترحيل — فضغطة Enter
/// مزدوجة في أيّ حقل نصّيّ تُنتج النتيجة نفسها بلا فأرة.</para>
///
/// <para><b>الاستعمال</b> — سطران في رأس المعالج، ويخرج بنفسه عند أيّ عودة أو استثناء:</para>
/// <code>
/// private async void OnPost(object sender, RoutedEventArgs e)
/// {
///     using var busy = UiBusyGuard.Enter(sender);
///     if (!busy.Acquired) return;
///     …
/// }
/// </code>
///
/// <para><b>خيط الواجهة وحده:</b> معالجات WPF تعمل على خيط الـDispatcher، فلا حاجة لقفل —
/// والتداخل الذي نمنعه هو إعادة الدخول عبر حلقة الرسائل أثناء <c>await</c>، لا التوازي.</para>
/// </summary>
public static class UiBusyGuard
{
    private static readonly HashSet<object> Busy = new(ReferenceEqualityComparer.Instance);

    /// <summary>نطاق انشغال. <see cref="Scope.Acquired"/> يكون false إن كان المعالج يعمل أصلاً.</summary>
    public readonly struct Scope : IDisposable
    {
        private readonly object? _owner;
        private readonly bool _reEnable;

        internal Scope(object? owner, bool acquired, bool reEnable)
        {
            _owner = owner;
            Acquired = acquired;
            _reEnable = reEnable;
        }

        /// <summary>هل دخل هذا النداء فعلاً؟ إن كان false فالمعالج قيد التنفيذ ويجب العودة فوراً.</summary>
        public bool Acquired { get; }

        public void Dispose()
        {
            if (!Acquired || _owner is null)
                return;

            Busy.Remove(_owner);
            if (_reEnable && _owner is ButtonBase button)
                button.IsEnabled = true;
        }
    }

    /// <summary>
    /// يحاول دخول المعالج. يُعطّل الزرّ أثناء العمل — فالمستخدم <b>يرى</b> أن الطلب قيد التنفيذ
    /// بدل أن يظنّه ضاع فيضغط ثانيةً.
    /// </summary>
    public static Scope Enter(object? owner)
    {
        if (owner is null)
            return new Scope(null, acquired: true, reEnable: false);

        if (!Busy.Add(owner))
            return new Scope(owner, acquired: false, reEnable: false);

        var reEnable = false;
        if (owner is ButtonBase button && button.IsEnabled)
        {
            button.IsEnabled = false;
            reEnable = true;
        }

        return new Scope(owner, acquired: true, reEnable);
    }

    /// <summary>هل هذا المُرسِل مشغول الآن؟ — للاختبارات والتشخيص.</summary>
    internal static bool IsBusy(object owner) => Busy.Contains(owner);

    /// <summary>تفريغ الحالة بين الاختبارات.</summary>
    internal static void ResetForTests() => Busy.Clear();
}
