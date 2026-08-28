using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-229 · م0-١٦) منع ازدواج المستند بضغطة مزدوجة.
///
/// <para><b>العطل:</b> معالجات الترحيل كلها <c>async void</c>، والزرّ <b>لا يُعطَّل أثناء
/// الانتظار</b>. فضغطة مزدوجة — عادةُ اللمس السريع على شاشة نقطة بيع — تُنفّذ المعالج مرّتين،
/// وكلٌّ منهما يبني <b>مستنداً جديداً</b>: فاتورتان وخصم مخزون مرّتين وإيراد مضاعف. ولا يُنقذ
/// منه <c>EnsureDraft</c> لأن كل نداء يملك كياناً مستقلاً.</para>
///
/// <para><b>ما يقيسه هذا الملف:</b> دلالة إعادة الدخول نفسها — وهي جوهر الإصلاح. أمّا توصيله
/// بالمعالجات الاثني عشر فمُتحقَّق منه بالقراءة لا بالاختبار: لا يوجد في المستودع اختبار واحد
/// يُنشئ صفحة WPF (T-7)، وسدُّ ذلك بند م2-٥. أُصرّح بالحدّ بدل أن أكتب حارساً نصّياً يبحث عن
/// وجود الاستدعاء في الملفات — فذلك يُثبت حضور السطر لا صحّة السلوك.</para>
/// </summary>
public class UiBusyGuardTests : IDisposable
{
    public UiBusyGuardTests() => UiBusyGuard.ResetForTests();

    /// <summary>
    /// <b>القياس الأساسيّ.</b> النداء الثاني على نفس الزرّ أثناء عمل الأول <b>لا يدخل</b>.
    /// </summary>
    [Fact]
    public void SecondEntryWhileBusy_IsRefused()
    {
        var button = new object();

        using var first = UiBusyGuard.Enter(button);
        using var second = UiBusyGuard.Enter(button);

        Assert.True(first.Acquired);
        Assert.False(second.Acquired, "الضغطة الثانية دخلت — سيُنشأ مستند ثانٍ");
    }

    /// <summary>وبعد انتهاء الأول يصير الزرّ متاحاً — الحارس يمنع التزامن لا الاستعمال.</summary>
    [Fact]
    public void AfterFirstCompletes_ButtonIsUsableAgain()
    {
        var button = new object();

        using (var first = UiBusyGuard.Enter(button))
            Assert.True(first.Acquired);

        using var later = UiBusyGuard.Enter(button);
        Assert.True(later.Acquired);
    }

    /// <summary>
    /// زرّان مختلفان لا يحجب أحدهما الآخر — وإلا لتعطّل البرنامج كلّه بأوّل ترحيل.
    /// </summary>
    [Fact]
    public void DifferentButtons_DoNotBlockEachOther()
    {
        var post = new object();
        var save = new object();

        using var a = UiBusyGuard.Enter(post);
        using var b = UiBusyGuard.Enter(save);

        Assert.True(a.Acquired);
        Assert.True(b.Acquired);
    }

    /// <summary>
    /// <b>الأهمّ:</b> الاستثناء لا يترك الزرّ مقفولاً إلى الأبد. نطاق <c>using</c> اختير لهذا
    /// بالذات — الترحيل يرمي كثيراً (رصيد غير كافٍ، فترة مقفلة)، ولو بقي القفل لصار المستخدم
    /// عاجزاً عن إعادة المحاولة إلا بإغلاق البرنامج.
    /// </summary>
    [Fact]
    public void ExceptionInsideHandler_StillReleasesTheButton()
    {
        var button = new object();

        static void FailingHandler(object b)
        {
            using var busy = UiBusyGuard.Enter(b);
            Assert.True(busy.Acquired);
            throw new InvalidOperationException("فشل ترحيل");
        }

        Assert.Throws<InvalidOperationException>(() => FailingHandler(button));

        Assert.False(UiBusyGuard.IsBusy(button));
        using var retry = UiBusyGuard.Enter(button);
        Assert.True(retry.Acquired, "الزرّ بقي مقفولاً بعد فشل — لا يمكن إعادة المحاولة");
    }

    /// <summary>والعودة المبكرة كذلك — وهي أكثر ما يقع: تحقّقٌ يفشل فيعود المعالج.</summary>
    [Fact]
    public void EarlyReturn_StillReleasesTheButton()
    {
        var button = new object();

        static void Handler(object b)
        {
            using var busy = UiBusyGuard.Enter(b);
            if (!busy.Acquired) return;
            return;   // «التاريخ مطلوب» مثلاً
        }

        Handler(button);
        Handler(button);

        Assert.False(UiBusyGuard.IsBusy(button));
    }

    /// <summary>
    /// النطاق المرفوض لا يُحرّر قفل صاحبه عند التخلّص منه — وإلا لفتحت الضغطةُ الثانية
    /// البابَ للثالثة، وانتقض الحارس من حيث أُريد له أن يُحكم.
    /// </summary>
    [Fact]
    public void DisposingARefusedScope_DoesNotReleaseTheOwner()
    {
        var button = new object();

        using var first = UiBusyGuard.Enter(button);
        using (var refused = UiBusyGuard.Enter(button))
            Assert.False(refused.Acquired);

        Assert.True(UiBusyGuard.IsBusy(button), "النطاق المرفوض حرّر قفل غيره");
        Assert.False(UiBusyGuard.Enter(button).Acquired);
    }

    /// <summary>
    /// كائنان متساويان بالقيمة لكن مختلفان بالمرجع لا يتشاركان القفل — المقارنة بالمرجع
    /// مقصودة، فالمُرسِل هو عنصر الواجهة نفسه.
    /// </summary>
    [Fact]
    public void ComparisonIsByReference_NotByValue()
    {
        var a = new string(['b', 'u', 't', 't', 'o', 'n']);
        var b = new string(['b', 'u', 't', 't', 'o', 'n']);
        Assert.Equal(a, b);
        Assert.NotSame(a, b);

        using var first = UiBusyGuard.Enter(a);
        using var second = UiBusyGuard.Enter(b);

        Assert.True(first.Acquired);
        Assert.True(second.Acquired);
    }

    /// <summary>مُرسِل غائب لا يُسقط المعالج — يدخل بلا حراسة بدل أن يرمي.</summary>
    [Fact]
    public void NullOwner_EntersWithoutGuarding()
    {
        using var scope = UiBusyGuard.Enter(null);

        Assert.True(scope.Acquired);
    }

    public void Dispose()
    {
        UiBusyGuard.ResetForTests();
        GC.SuppressFinalize(this);
    }
}
