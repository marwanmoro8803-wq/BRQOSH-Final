using Brqosh.Desktop.Ui;
using Brqosh.Domain.Zatca;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥) <b>شاشةُ مستندات ZATCA كانت تفتح مستنداً آخر.</b>
///
/// <para>الأنواعُ <b>ستّة</b> ومصادرُها <b>ثلاثة</b>: فاتورةُ مبيعات، وإشعارٌ دائن، ومستخلصُ
/// أعمال — <b>وكلُّ فضاءِ معرّفاتٍ يبدأ من واحد</b>. وكان السؤالُ في الشاشة ثنائيّاً: «أهي
/// فاتورةُ مبيعات؟ <b>وإلّا فإشعارٌ دائن</b>» — <b>فثلاثةٌ من الستّة تُقرأ من الجدول الخطأ</b>.</para>
///
/// <para><b>والأثرُ مرئيٌّ كاملاً ولا يُنبَّه عليه</b>: مستخلصُ أعمالٍ رقمُه خمسةٌ يُعرض
/// بسطور <b>إشعارٍ دائنٍ رقمُه خمسة</b> — أصنافُه ومبالغُه وضريبتُه — تحت عنوان «إشعار دائن».
/// <b>وهي الشاشةُ التي يُفتح فيها المستندُ الضريبيُّ للمراجعة.</b></para>
///
/// <para><b>ولا شيءَ في النظام كان يقيسها</b>: القرارُ كان شرطاً داخل معالجِ ضغطٍ يحتاج
/// قاعدةَ بياناتٍ وخيطَ واجهة. فصار دالّةً — <see cref="ZatcaDocumentPresentation"/>.</para>
/// </summary>
public class ZatcaDocumentPresentationTests
{
    /// <summary>
    /// <b>الجدولُ الحاكم</b> — وهو مقروءٌ من المولّد نفسِه: أيُّ معرّفٍ يُكتب في
    /// <c>SourceDocumentId</c> لكلّ نوع.
    /// </summary>
    public static TheoryData<ZatcaDocumentType, ZatcaDocumentPresentation.SourceKind> Sources => new()
    {
        { ZatcaDocumentType.SalesInvoice, ZatcaDocumentPresentation.SourceKind.SalesInvoice },
        { ZatcaDocumentType.CreditNote, ZatcaDocumentPresentation.SourceKind.CreditNote },
        { ZatcaDocumentType.ProgressBilling, ZatcaDocumentPresentation.SourceKind.ProgressBilling },
        // ‏إشعارٌ مدينٌ يُصدَر لإلغاء **إشعارٍ دائن** — فمصدرُه الإشعارُ الملغى.
        { ZatcaDocumentType.DebitNote, ZatcaDocumentPresentation.SourceKind.CreditNote },
        // ‏وإشعارٌ دائنٌ يُصدَر لإلغاء **فاتورة** — فمصدرُه الفاتورةُ الملغاة لا إشعار.
        { ZatcaDocumentType.InvoiceCancel, ZatcaDocumentPresentation.SourceKind.SalesInvoice },
        // ‏وكذلك إلغاءُ المستخلص — مصدرُه المستخلص.
        { ZatcaDocumentType.BillingCancel, ZatcaDocumentPresentation.SourceKind.ProgressBilling },
        // ‏(ح-١٦) وفاتورةُ بيع أصلٍ ثابت — **فضاءُ معرّفاتٍ رابع**، مصدرُه `fixed_assets`.
        { ZatcaDocumentType.AssetDisposal, ZatcaDocumentPresentation.SourceKind.FixedAssetDisposal },
    };

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> كلُّ نوعٍ يُشير إلى مصدره الصحيح — <b>والثلاثةُ التي كانت
    /// تُقرأ من <c>credit_notes</c> بغير حقٍّ داخلةٌ في الجدول.</b>
    /// </summary>
    [Theory]
    [MemberData(nameof(Sources))]
    public void EveryDocumentType_ResolvesToItsRealSource(
        ZatcaDocumentType type, ZatcaDocumentPresentation.SourceKind expected)
    {
        Assert.Equal(expected, ZatcaDocumentPresentation.SourceOf(type));
    }

    /// <summary>
    /// <b>وهذه الأنواعُ مصدرُها ليس إشعاراً دائناً</b> — وهو نفيُ القاعدة القديمة صراحةً:
    /// «ما ليس فاتورةَ مبيعاتٍ فهو إشعارٌ دائن». <b>وكلُّ واحدٍ منها كان يُقرأ من الجدول
    /// الخطأ.</b>
    /// </summary>
    [Fact]
    public void TheOldRule_WouldMisreadEveryTypeWhoseSourceIsNotACreditNote()
    {
        var misread = Enum.GetValues<ZatcaDocumentType>()
            .Where(t => t != ZatcaDocumentType.SalesInvoice)
            .Where(t => ZatcaDocumentPresentation.SourceOf(t)
                        != ZatcaDocumentPresentation.SourceKind.CreditNote)
            .ToList();

        Assert.Equal(
            [ZatcaDocumentType.ProgressBilling,
             ZatcaDocumentType.InvoiceCancel,
             ZatcaDocumentType.BillingCancel,
             ZatcaDocumentType.AssetDisposal],
            misread);
    }

    // ── الأسماء ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>ولكلّ نوعٍ اسمُه</b>: أربعةٌ من الستّة كانت تُعرض جميعاً «إشعار دائن» —
    /// <b>ومنها مستخلصُ الأعمال</b>، وليس إشعاراً بحال.
    /// </summary>
    [Fact]
    public void EveryDocumentType_HasItsOwnLabel()
    {
        var labels = Enum.GetValues<ZatcaDocumentType>()
            .Select(ZatcaDocumentPresentation.TypeLabel)
            .ToList();

        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("مستخلص أعمال", ZatcaDocumentPresentation.TypeLabel(ZatcaDocumentType.ProgressBilling));
    }

    /// <summary>
    /// <b>ورسالةُ الغياب تُسمّي المستندَ المصدر</b> — فلا تقول «إشعار الدائن غير موجود»
    /// عن مستخلصٍ لم يُفقد.
    /// </summary>
    [Fact]
    public void TheMissingMessage_NamesTheRealSource()
    {
        Assert.Contains("مستخلص أعمال",
            ZatcaDocumentPresentation.SourceMissingMessage(ZatcaDocumentType.BillingCancel),
            StringComparison.Ordinal);
    }

    // ── الحارسُ البنيويّ ────────────────────────────────────────────────────

    /// <summary>
    /// <b>ونوعٌ سابعٌ لا يمرّ صامتاً.</b>
    ///
    /// <para>العطلُ الأصليُّ كان <b>فرعاً افتراضيّاً يبتلع كلَّ ما لم يُذكر</b>. فلو بقي
    /// فرعٌ كهذا لتكرّر العطلُ حرفاً عند إضافة نوعٍ جديد — <b>وهذا ما يمنعه هذا الاختبار:</b>
    /// من يضيف قيمةً إلى <see cref="ZatcaDocumentType"/> ولا يُحدّث القراءةَ يجد رميةً
    /// لا عرضاً خاطئاً.</para>
    /// </summary>
    [Fact]
    public void AnUnknownType_Throws_RatherThanBeingReadAsACreditNote()
    {
        var unknown = (ZatcaDocumentType)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => ZatcaDocumentPresentation.SourceOf(unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => ZatcaDocumentPresentation.TypeLabel(unknown));
    }

    /// <summary>
    /// <b>وشاهدُ ضبط:</b> كلُّ قيمةٍ معرَّفةٍ اليومَ <b>تمرّ</b> — فالحارسُ أعلاه يمنع
    /// المجهولَ ولا يمنع المعروف. <b>ولولاه لجاز أن يكون «الحارسُ» رميةً على الجميع.</b>
    /// </summary>
    [Fact]
    public void EveryDefinedType_IsAnswered_NotThrown()
    {
        foreach (var type in Enum.GetValues<ZatcaDocumentType>())
        {
            _ = ZatcaDocumentPresentation.SourceOf(type);
            _ = ZatcaDocumentPresentation.TypeLabel(type);
            _ = ZatcaDocumentPresentation.SourceTitle(type);
        }
    }

    /// <summary>
    /// <b>والجدولُ أعلاه يغطّي كلَّ الأنواع</b> — فلا يُضاف نوعٌ ويبقى الجدولُ ساكتاً عنه
    /// فيُقال «كلُّها مقيسة» وهي ناقصة.
    /// </summary>
    [Fact]
    public void TheGoverningTable_CoversEveryType()
    {
        var covered = Sources.Select(r => (ZatcaDocumentType)r[0]).ToHashSet();
        Assert.Equal(Enum.GetValues<ZatcaDocumentType>().ToHashSet(), covered);
    }
}
