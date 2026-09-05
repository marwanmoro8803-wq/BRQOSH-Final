using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Brqosh.VendorIssuer;

public record SelfTestRow(string Name, bool Ok, string Detail);

/// <summary>
/// <b>الاختبارُ الذاتيّ — لأنّ نسخةَ قواعدٍ لا تُقاس تفارق أصلَها صامتة.</b>
///
/// <para>هذه الأداةُ تحمل نسخةً من قواعد تحقّق المنتج (<c>LicenseCore.Validate</c>). ونسخةٌ
/// كهذه لا تنكسر بضجيج: تظلّ تُصدر تراخيصَ تبدو سليمةً وتُرفض على أجهزة العملاء. فتُقاس
/// هنا بمتّجهاتٍ <b>موجَبةٍ وسالبةٍ معاً</b> — والسالبةُ هي المهمّة: بوّابةٌ لا تَرفض شيئاً
/// عمياءُ وإن قالت «نجح».</para>
///
/// <para>يعمل عند الإقلاع. وإن أخفق شيءٌ منه <b>يُمنع الإصدار</b> — لا يُعرض تحذيرٌ يُتجاوز.</para>
/// </summary>
public static class SelfTest
{
    private const string Company = "شركة برقوش للإنشاءات";
    private const string Machine = "CF0304A7C4C4ABA1";

    public static List<SelfTestRow> RunAll()
    {
        var rows = new List<SelfTestRow>();
        void Row(string name, bool ok, string detail) => rows.Add(new SelfTestRow(name, ok, detail));

        // ① مرساةُ الثقة المنسوخة: مفتاحٌ حقيقيٌّ، وبصمتُه هي البصمةُ المُعلَنة.
        try
        {
            using var anchor = RSA.Create();
            anchor.ImportFromPem(TrustAnchor.PublicKeyPem);
            var fp = LicenseCore.Fingerprint(anchor);
            Row("مرساةُ الثقة المنسوخة تُستورد وبصمتُها معلنة",
                anchor.KeySize >= 2048 && fp == TrustAnchor.ExpectedFingerprint,
                $"KeySize={anchor.KeySize} · {fp[..16]}…");
        }
        catch (Exception ex)
        {
            Row("مرساةُ الثقة المنسوخة تُستورد وبصمتُها معلنة", false, ex.Message);
        }

        using var ephemeral = RSA.Create(2048);
        var ephemeralPublicPem = ephemeral.ExportSubjectPublicKeyInfoPem();
        var payload = new LicensePayload(Company, Machine, "standard",
            new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc), ["core"]);
        var envelope = LicenseCore.BuildSignedEnvelope(payload, ephemeral, out var signedRaw);

        // ② هُويّةُ البايتات: النصُّ الموقَّعُ هو النصُّ المكتوب — وهذا هو الفخُّ الأوّل في هذا العقد.
        try
        {
            using var doc = JsonDocument.Parse(envelope);
            var writtenRaw = doc.RootElement.GetProperty("payload").GetRawText();
            Row("النصُّ الموقَّع هو عينُ النصّ المكتوب في الغلاف",
                string.Equals(writtenRaw, signedRaw, StringComparison.Ordinal),
                writtenRaw.Length == signedRaw.Length ? $"{signedRaw.Length} حرفاً" : "الطولان مختلفان");
        }
        catch (Exception ex)
        {
            Row("النصُّ الموقَّع هو عينُ النصّ المكتوب في الغلاف", false, ex.Message);
        }

        // ③ متّجهٌ موجَب.
        Row("ترخيصٌ صحيحٌ يُقبل",
            LicenseCore.Validate(Company, Machine, envelope, ephemeralPublicPem, out _) == LicenseVerdict.Valid,
            "Valid");

        // ④ متّجهاتٌ سالبةٌ مسمّاة — كلٌّ منها يُثبت أنّ بوّابةً بعينها لا تزال ترى.
        void Negative(string name, LicenseVerdict expected, string company, string machine, string json, string anchor)
        {
            var actual = LicenseCore.Validate(company, machine, json, anchor, out _);
            Row(name, actual == expected, $"{actual} (المنتظر {expected})");
        }

        Negative("اسمُ منشأةٍ مختلف يُرفض", LicenseVerdict.CompanyMismatch,
            Company + "x", Machine, envelope, ephemeralPublicPem);
        Negative("جهازٌ مختلف يُرفض", LicenseVerdict.MachineMismatch,
            Company, "0000000000000000", envelope, ephemeralPublicPem);
        Negative("مرساةُ ثقةٍ غائبة تُسمّى باسمها", LicenseVerdict.NoTrustAnchor,
            Company, Machine, envelope, "");
        Negative("توقيعٌ بمفتاحٍ آخر يُرفض", LicenseVerdict.BadSignature,
            Company, Machine, envelope, TrustAnchor.PublicKeyPem);
        Negative("حمولةٌ عُبث بها تُرفض", LicenseVerdict.BadSignature,
            Company, Machine, envelope.Replace("\"standard\"", "\"Enterprise\""), ephemeralPublicPem);
        Negative("غلافٌ بأسماءِ حقولٍ كبيرةِ الأحرف يُرفض شكلاً", LicenseVerdict.Malformed,
            Company, Machine, envelope.Replace("\"payload\"", "\"Payload\""), ephemeralPublicPem);

        var expired = LicenseCore.BuildSignedEnvelope(
            payload with { ExpiresUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, ephemeral, out _);
        Negative("ترخيصٌ منتهٍ يُرفض", LicenseVerdict.Expired, Company, Machine, expired, ephemeralPublicPem);

        // ⑤ معرّفُ الجهاز غيرُ حسّاسٍ للحالة — كما في المنتج. سالبٌ لو انقلب.
        Row("معرّفُ الجهاز بحروفٍ صغيرة يُقبل (كالمنتج)",
            LicenseCore.Validate(Company, Machine.ToLowerInvariant(), envelope, ephemeralPublicPem, out _)
                == LicenseVerdict.Valid,
            "OrdinalIgnoreCase");

        // ⑥ التقويم: على ويندوز عربيٍّ التقويمُ الافتراضيُّ هجريّ. تاريخٌ يُعرض بالهجريّ
        //    يجعل المشغّلَ يظنّ ترخيصاً صالحاً حتى 2027 منتهياً.
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            var shown = LicenseCore.FormatUtc(new DateTime(2027, 9, 4, 22, 44, 0, DateTimeKind.Utc));
            Row("التاريخُ يُعرض ميلاديّاً ولو كانت الثقافةُ ar-SA",
                shown.StartsWith("2027-09-04", StringComparison.Ordinal), shown);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        return rows;
    }
}
