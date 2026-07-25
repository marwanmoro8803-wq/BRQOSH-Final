using System.Windows.Controls;
using System.Windows.Threading;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-220) نافذة «خطأ غير متوقع: An item with the same key has already been added»
/// التي رآها المستخدم على لوحة التحكم.
///
/// المصدر: <c>XamlUiLocalizer.Resolve</c> كان يستدعي
/// <c>ConditionalWeakTable.Add</c> **داخل فرعٍ لا يُدخَل إلا والمفتاح موجود** —
/// و<c>Add</c> يرمي متى وُجد المفتاح (بعكس القاموس العاديّ الذي يُستخدم هنا بالمفهرس).
/// أيّ عنصر عربيّ ديناميكيّ (بطاقة مؤشر، سطر نشاط، رسالة مساعد) يتغيّر نصّه ثم
/// يمرّ عليه المترجم مرّةً أخرى ⇒ رمية تصعد إلى DispatcherUnhandledException
/// فتظهر كنافذة خطأ للمستخدم. لا اختبار سابق كان يحرّك المسار لأن كل اختبارات
/// التوطين كانت تقرأ النصّ من الملفات ولا تُنشئ شجرة WPF حيّة.
/// </summary>
public class XamlUiLocalizerRefreshTests
{
    /// <summary>WPF يشترط STA لإنشاء أي عنصر واجهة — xUnit يشغّل على MTA.</summary>
    private static void OnUiThread(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "خيط الواجهة لم ينتهِ.");
        if (failure is not null)
            throw failure;
    }

    /// <summary>يبدأ بالعربية ويعيدها مهما فعل الاختبار — الحالة ساكنة عامة.</summary>
    private static void WithArabicStart(Action body)
    {
        if (LocaleManager.IsEnglish) LocaleManager.Toggle();
        try { body(); }
        finally { if (LocaleManager.IsEnglish) LocaleManager.Toggle(); }
    }

    /// <summary>العيب بعينه: نصّ عربيّ يتغيّر ثم يُترجَم ثانيةً — يجب ألّا يرمي.</summary>
    [Fact]
    public void ChangedArabicText_LocalizedAgain_DoesNotThrow()
    {
        OnUiThread(() => WithArabicStart(() =>
        {
            var block = new TextBlock { Text = "أذون الاستلام" };
            XamlUiLocalizer.Apply(block);

            block.Text = "أسعار الصرف";              // تحديث بيانات — نصّ عربيّ مختلف

            var thrown = Record.Exception(() => XamlUiLocalizer.Apply(block));

            Assert.Null(thrown);
        }));
    }

    /// <summary>وحارس المعنى: الأصل المحفوظ يُحدَّث فعلاً — الإنجليزية تتبع النصّ الجديد.</summary>
    [Fact]
    public void ChangedArabicText_TranslatesFromTheNewOriginal_NotTheStaleOne()
    {
        OnUiThread(() => WithArabicStart(() =>
        {
            var block = new TextBlock { Text = "أذون الاستلام" };
            XamlUiLocalizer.Apply(block);

            block.Text = "أسعار الصرف";
            XamlUiLocalizer.Apply(block);

            LocaleManager.Toggle();                  // → الإنجليزية
            XamlUiLocalizer.Apply(block);

            Assert.Equal("Exchange Rates", block.Text);
        }));
    }

    /// <summary>
    /// الذهاب والإياب لا يفقد الأصل العربيّ: بعد العودة يعود النصّ كما كان،
    /// لا يبقى إنجليزياً ولا يُترجَم عن الإنجليزية.
    /// </summary>
    [Fact]
    public void ToggleRoundTrip_RestoresArabicOriginal()
    {
        OnUiThread(() => WithArabicStart(() =>
        {
            var block = new TextBlock { Text = "أذون الاستلام" };
            XamlUiLocalizer.Apply(block);

            LocaleManager.Toggle();
            XamlUiLocalizer.Apply(block);
            Assert.Equal("Goods Receipts", block.Text);

            LocaleManager.Toggle();
            XamlUiLocalizer.Apply(block);
            Assert.Equal("أذون الاستلام", block.Text);
        }));
    }

    /// <summary>مرور متكرّر على نفس النصّ ثابت — لا يرمي ولا ينحرف.</summary>
    [Fact]
    public void RepeatedApply_OnUnchangedText_IsStable()
    {
        OnUiThread(() => WithArabicStart(() =>
        {
            var block = new TextBlock { Text = "أذون الاستلام" };

            for (var i = 0; i < 5; i++)
                XamlUiLocalizer.Apply(block);

            Assert.Equal("أذون الاستلام", block.Text);
        }));
    }
}
