using System.Windows;
using Brqosh.Application.Setup;

namespace Brqosh.Desktop.Views;

/// <summary>
/// (CEEF-236 · م-١٣) <b>نافذةُ إنقاذٍ تكسر الحجر: ملفُّ اتصالٍ لا يُقرأ لم يعد يعني تركيباً ميّتاً.</b>
///
/// <para><b>ما كان:</b> معالجُ التهيئة يُبنى في موضعٍ واحدٍ تحت
/// <c>if (setup is null || !setup.IsCompleted)</c>، <b>ولا شيءَ في المنتج يمسح ذلك العلَم</b>. فمتى
/// اكتمل الإعدادُ ثمّ تعذّرت قراءةُ <c>database.json</c> — حُذف، أو تلف، أو <b>خُتم بحساب مستخدمٍ
/// آخر</b> — رمى <c>LoadDatabaseConfig</c>، وعرض <c>App</c> الرسالةَ ثمّ <c>Shutdown()</c>.
/// <b>وكلتا الرسالتَين تصفان علاجاً لا سبيلَ إليه</b>: «أعد تشغيل معالج التهيئة» ومعالجُ التهيئة
/// لا يُعاد، و«أعد ضبط الاتصال من هذا الحساب» ولا شاشةَ تضبطه.</para>
///
/// <para><b>وهذا الطريقُ صار مسلوكاً بإصلاح ت-٣ نفسِه:</b> قبله لم يكن شيءٌ في المنتج يُنتج سلسلةً
/// محميّة، فكان فرعُ «تعذّر الفكّ» شيفرةً ميّتة. بعده <b>كلُّ تركيبٍ إنتاجيٍّ يحمل سرّاً مختوماً
/// بملفّ تعريف المستخدم</b> — ونقلُ المجلّد إلى جهازٍ آخر، أو إعادةُ تعيين كلمة مرور ويندوز من
/// حساب مسؤول، يُتلف مفتاحَ DPAPI فيتحوّل التركيبُ إلى حجر. والدفاترُ سليمةٌ في PostgreSQL طوال ذلك.</para>
///
/// <para><b>وما صار:</b> هذه النافذةُ تُعرض بدل الإغلاق، وتحمل <see cref="Controls.DatabaseConnectionPanel"/>
/// نفسَها التي يحملها المعالج — فيُعاد إدخالُ الاتصال ويُجرَّب ويُحفَظ، ثمّ يُعاد التحميلُ ويُتابَع.</para>
/// </summary>
public partial class DatabaseConnectionWindow : Window
{
    /// <summary>هل حُفظ اتصالٌ صالحٌ فعلاً — فيُعيد المُستدعي المحاولة.</summary>
    public bool Saved { get; private set; }

    public DatabaseConnectionWindow(string reason)
    {
        InitializeComponent();
        ReasonText.Text = reason;
        ConnectionPanel.Initialize(AppEnvironmentConfig.IsProduction);
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        SaveButton.IsEnabled = false;
        try
        {
            var error = await ConnectionPanel.ValidateProbeAndSaveAsync();
            if (error is not null)
            {
                ErrorText.Text = error;
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }

        Saved = true;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
