using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٩٦ · <c>found#5</c>) <b>مهلةُ السداد تُضبط من نافذتَي العميل والمورد — وكانت لا تُضبط من أيّ مكان.</b>
///
/// <para><c>Customer.SetPaymentTerms</c> و<c>Supplier.SetPaymentTerms</c> موجودان منذ CEEF-230 (ط4-٣ج) و<b>لا يستدعيهما شيءٌ في الشجرة كلِّها</b>،
/// و<c>CashFlowForecastService</c> وحدَه يقرأ <c>PaymentTermDays</c> — فكلُّ عميلٍ ومورّدٍ بصفرٍ أبداً، <b>وكلُّ مفتوحٍ «مستحقٌّ الآن» في التنبّؤ</b>:
/// وهو عينُ ما قال تعليقُ الحقل إنّه «ليس تنبّؤاً بل صورةً للحاضر».</para>
///
/// <para><b>والقياسُ بنيويّ</b>: النافذتان تفتحان دفاترَ الإنتاج (<c>AppServices.CreateDb</c>) ولا تُشغَّلان في اختبار — على سُنّة ح-٦٦ وح-٧٧.</para>
/// </summary>
public class PaymentTermsCanBeSetFromTheirDialogsTests
{
    /// <summary><b>القياسُ الحاكم: نافذةُ العميل تحمل حقلَ المهلة وتضبطه عند الحفظ وتعرضه عند التعديل.</b></summary>
    [Fact]
    public void TheCustomerDialog_SetsAndShowsThePaymentTerms()
    {
        var (xaml, code) = DialogSources("CustomerDialog");

        Assert.Contains("x:Name=\"PaymentTermsBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains(".SetPaymentTerms(", code, StringComparison.Ordinal);
        Assert.Contains("PaymentTermsBox.Text = customer.PaymentTermDays", code, StringComparison.Ordinal);
    }

    /// <summary><b>ونافذةُ المورد كذلك</b> — فالتنبّؤ يقرأ مهلةَ المورد للدائن كما يقرأ مهلةَ العميل للمدين.</summary>
    [Fact]
    public void TheSupplierDialog_SetsAndShowsThePaymentTerms()
    {
        var (xaml, code) = DialogSources("SupplierDialog");

        Assert.Contains("x:Name=\"PaymentTermsBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains(".SetPaymentTerms(", code, StringComparison.Ordinal);
        Assert.Contains("PaymentTermsBox.Text = supplier.PaymentTermDays", code, StringComparison.Ordinal);
    }

    /// <summary><b>وحدُّ المهلة في المجال</b> — فالنافذةُ تمرّر ما كُتب ولا تُكرّر الحارس: ما فوق السنة وما دون الصفر يُرفض باسمه.</summary>
    [Fact]
    public void TheDomain_RefusesTermsOutsideAYear()
    {
        var customer = Customer.Create(1, "عميل");
        var supplier = Supplier.Create(1, "مورد");

        Assert.Throws<AccountingException>(() => customer.SetPaymentTerms(366));
        Assert.Throws<AccountingException>(() => supplier.SetPaymentTerms(-1));
        customer.SetPaymentTerms(30);
        supplier.SetPaymentTerms(60);
        Assert.Equal((30, 60), (customer.PaymentTermDays, supplier.PaymentTermDays));
    }

    private static (string Xaml, string Code) DialogSources(string name)
    {
        var pages = Path.Combine(RepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages");
        return (File.ReadAllText(Path.Combine(pages, name + ".xaml")), File.ReadAllText(Path.Combine(pages, name + ".xaml.cs")));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Brqosh.sln غير موجود فوق مجلد الاختبار.");
    }
}
