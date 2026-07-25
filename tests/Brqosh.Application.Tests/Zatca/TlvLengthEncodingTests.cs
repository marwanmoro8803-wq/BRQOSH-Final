using System.Text;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-206) ترميز طول TLV في رمز ZATCA.
///
/// كان الفرع الطويل يكتب 0x81 ثم (byte)Length، فأي طول ≥ 256 يُقصّ إلى بايت واحد
/// (300 تصير 44) وينتج رمزاً تالفاً يقرأه الماسح خطأً — بلا أي استثناء وقت التوليد.
/// اسم بائع عربي طويل يتجاوز 255 بايتاً في UTF-8 يبلغ هذا الحد فعلاً.
/// </summary>
public class TlvLengthEncodingTests
{
    [Theory]
    [InlineData(10)]     // بايت طول واحد
    [InlineData(60)]     // أسفل حدّ 127 بايتاً (120 بايت UTF-8)
    [InlineData(120)]    // 0x81 + بايت (240 بايتاً)
    [InlineData(200)]    // الحالة المكسورة سابقاً: 400 بايت — تتطلب 0x82 + بايتين
    public void SellerName_OfAnyLength_DeclaresRecoverableLength(int nameChars)
    {
        var name = new string('م', nameChars);   // عربي: بايتان لكل حرف في UTF-8
        var expectedBytes = Encoding.UTF8.GetByteCount(name);

        var bytes = Convert.FromBase64String(ZatcaQrGenerator.GenerateTlvBase64(
            name, "300000000000003", new DateTime(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc), 115m, 15m));

        Assert.Equal(1, bytes[0]);                       // الوسم الأول: اسم البائع
        var (declared, valueStart) = ReadBerLength(bytes, 1);

        Assert.Equal(expectedBytes, declared);
        Assert.True(valueStart + declared <= bytes.Length, "الطول المعلن يتجاوز الحمولة — ترميز تالف.");
        Assert.Equal(name, Encoding.UTF8.GetString(bytes, valueStart, declared));
    }

    /// <summary>الوسوم التالية تبقى قابلة للقراءة بعد اسم طويل — أي أن الإزاحة لم تنحرف.</summary>
    [Fact]
    public void LongSellerName_KeepsSubsequentTagsReadable()
    {
        var name = new string('م', 200);         // 400 بايت
        var bytes = Convert.FromBase64String(ZatcaQrGenerator.GenerateTlvBase64(
            name, "300000000000003", new DateTime(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc), 115m, 15m));

        var (nameLen, nameStart) = ReadBerLength(bytes, 1);
        var next = nameStart + nameLen;

        Assert.True(next < bytes.Length, "لا يوجد وسم تالٍ — الترميز ابتلع بقية الحمولة.");
        Assert.Equal(2, bytes[next]);            // الوسم الثاني: الرقم الضريبي

        var (vatLen, vatStart) = ReadBerLength(bytes, next + 1);
        Assert.Equal("300000000000003", Encoding.UTF8.GetString(bytes, vatStart, vatLen));
    }

    /// <summary>يقرأ طول BER بصوره الثلاث ويعيد (الطول، موضع بداية القيمة).</summary>
    private static (int Length, int ValueStart) ReadBerLength(byte[] bytes, int offset)
    {
        var first = bytes[offset];
        return first switch
        {
            <= 127 => (first, offset + 1),
            0x81 => (bytes[offset + 1], offset + 2),
            0x82 => ((bytes[offset + 1] << 8) | bytes[offset + 2], offset + 3),
            _ => throw new InvalidOperationException($"صيغة طول غير متوقعة: 0x{first:X2}"),
        };
    }
}
