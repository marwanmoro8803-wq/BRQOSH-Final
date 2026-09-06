using Brqosh.Domain.Companies;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Shared;

namespace Brqosh.Domain.Tests;

/// <summary>
/// (م-٢ · ن-٣٠) <b>الأرقامُ العربية-الهندية كانت تجتاز تحقّقَ الرقم الضريبيّ.</b>
///
/// <para><c>char.IsDigit</c> يسأل عن <b>فئة يونيكود</b> لا عن <c>'0'</c>…<c>'9'</c>، والأرقامُ
/// العربية-الهندية (U+0660…U+0669) في تلك الفئة. فمستخدمٌ بلوحة مفاتيح عربية يكتب
/// «٣١٢٣…» فيجتاز التحقّق — <b>ويُكتب كما هو في <c>cbc:CompanyID</c> بمستند الهيئة</b>.</para>
///
/// <para><b>والاختبارُ يقيس الطرفين:</b> أنّ العربيّة-الهنديّة تُرفض، <b>وأنّ اللاتينيّة
/// ما زالت تُقبل</b>. فحارسٌ يرفض كلَّ شيءٍ يُغلق الثغرةَ ويُعطّل النظام، ولا يُميّز بينهما
/// إلّا الشاهدُ الموجب.</para>
/// </summary>
public class ArabicIndicDigitsAreRefusedTests
{
    private const string LatinVat = "310000000000003";
    private const string ArabicIndicVat = "٣١٠٠٠٠٠٠٠٠٠٠٠٠٣";
    private const string PersianVat = "۳۱۰۰۰۰۰۰۰۰۰۰۰۰۳";

    /// <summary>
    /// <b>القياسُ الذي يشرح العطلَ كلَّه:</b> الدالّةُ القديمةُ تقبل، والجديدةُ ترفض.
    ///
    /// <para>وهو مُثبَّتٌ هنا لا في تعليقٍ — لأنّ من يقرأ الكودَ بعد سنةٍ لن يُصدّق أنّ
    /// <c>char.IsDigit('٣')</c> صحيح، فيُعيدها «تبسيطاً».</para>
    /// </summary>
    [Fact]
    public void CharIsDigit_AcceptsArabicIndic_WhichIsWhyItCannotGuardAVatNumber()
    {
        Assert.True(char.IsDigit('٣'), "‏لو صار هذا false يوماً فقد تغيّرت المنصّة — راجِع الحارس.");
        Assert.True(char.IsDigit('۳'));

        Assert.False(AsciiDigits.Is('٣'));
        Assert.False(AsciiDigits.Is('۳'));
        Assert.True(AsciiDigits.Is('3'));
    }

    [Theory]
    [InlineData(ArabicIndicVat)]
    [InlineData(PersianVat)]
    public void Company_RefusesANonLatinVatNumber(string vat)
    {
        var ex = Assert.Throws<ArgumentException>(() => Company.Create("منشأة", vat, BusinessProfile.General));
        Assert.Contains("15", ex.Message);
    }

    /// <summary><b>شاهدُ ضبطٍ موجب:</b> اللاتينيّةُ ما زالت تُقبل — الإصلاحُ لم يُعطّل شيئاً.</summary>
    [Fact]
    public void Company_StillAcceptsALatinVatNumber()
    {
        var company = Company.Create("منشأة", LatinVat, BusinessProfile.General);
        Assert.Equal(LatinVat, company.VatRegistrationNumber);
    }

    [Theory]
    [InlineData(ArabicIndicVat)]
    [InlineData(PersianVat)]
    public void Customer_RefusesANonLatinVatNumber(string vat) =>
        Assert.Throws<ArgumentException>(() => Customer.Create(1, "CUS-1", "عميل", vat));

    [Theory]
    [InlineData(ArabicIndicVat)]
    [InlineData(PersianVat)]
    public void Supplier_RefusesANonLatinVatNumber(string vat) =>
        Assert.Throws<ArgumentException>(() => Supplier.Create(1, "SUP-1", "مورّد", vat));

    /// <summary>وأكوادُ الحسابات كذلك — تُطابَق نصّياً وتخرج في كلّ تقرير.</summary>
    [Fact]
    public void Account_RefusesANonLatinCode()
    {
        Assert.Throws<AccountingException>(() =>
            Account.Create("١١١١", "نقد", "Cash", AccountType.Asset));

        // ‏شاهدُ ضبطٍ موجب في الاختبار نفسِه.
        var ok = Account.Create("1111", "نقد", "Cash", AccountType.Asset);
        Assert.Equal("1111", ok.Code);
    }

    /// <summary>
    /// ورقمُ المبنى والرمزُ البريديّ — كلاهما يخرج في العنوان الوطنيّ بمستند الهيئة.
    ///
    /// <para><b>وهنا تنبيهٌ لا مانع</b> بتصميم النوع (‏انظر وصفَه): قيمةٌ بطولٍ مختلفٍ
    /// تُقبل ويُبلَّغ عنها في <c>Problems</c>. فالتوكيدُ على القائمة لا على الرمي.</para>
    /// </summary>
    [Theory]
    [InlineData("١٢٣٤", "12345", "رقم المبنى")]
    [InlineData("1234", "١٢٣٤٥", "الرمز البريدي")]
    public void NationalAddress_FlagsNonLatinDigits(string building, string postal, string expected)
    {
        var address = NationalAddress.Create(building, "شارع", "حي", "الرياض", postal, "الرياض");

        Assert.Contains(address.Problems, p => p.Contains(expected, StringComparison.Ordinal));
    }

    /// <summary>وشاهدُ ضبطٍ موجب: العنوانُ اللاتينيُّ الكاملُ بلا مشاكلَ ومكتمل.</summary>
    [Fact]
    public void NationalAddress_StillAcceptsLatinDigits()
    {
        var address = NationalAddress.Create("1234", "شارع", "حي", "الرياض", "12345", "الرياض");

        Assert.Empty(address.Problems);
        Assert.True(address.IsComplete);
    }
}
