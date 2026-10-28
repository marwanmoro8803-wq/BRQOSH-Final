using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Brqosh.VendorIssuer;

/// <summary>
/// <b>سجلُّ ما أُصدر — بياناتُ وصفٍ لا اعتمادات.</b>
///
/// <para>يُكتب سطرٌ لكلّ إصدار: المفتاحُ نفسُه (فهو مربوطٌ بجهاز العميل ومنشأته، ولا يعمل
/// على غيرهما)، ولمن، ولأيّ جهاز، وأيُّ خطةٍ، ومتى ينتهي. <b>والملفُّ سرٌّ من أسرار
/// المورِّد</b> — يُحفظ احتياطيّاً مع مفتاح الأداة ولا يُشارَك. وفقدانُه لا يُبطل مفاتيحَ
/// صدرت (كلُّ مفتاحٍ مستقلٌّ بذاته)، لكنّه يُفقد سجلَّ من أُصدر له ماذا.</para>
/// </summary>
public static class IssueLedger
{
    /// <summary>سطرٌ في السجلّ.</summary>
    public sealed record LedgerRow(
        string Code, string Company, string MachineId, LicensePlan Plan,
        string IssuedOn, string Expires, DateTime AtUtc);

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BRQOSH", "vendor-signing", "issued-licenses.jsonl");

    /// <summary>يُلحق إصداراً. يُعيد رسالةَ الخطأ عند الإخفاق — <b>ولا يُسقط الإصدار</b>.</summary>
    public static string? TryAppend(LedgerRow row) => AppendTo(DefaultPath, row);

    internal static string? AppendTo(string path, LedgerRow row)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, Serialize(row) + Environment.NewLine, new UTF8Encoding(false));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return ex.Message;
        }
    }

    /// <summary>سطرُ السجلّ نصّاً — يُقاس ذهابُه وعودتُه بلا وسيطٍ على القرص.</summary>
    internal static string Serialize(LedgerRow row) => JsonSerializer.Serialize(new
    {
        code = row.Code,
        company = row.Company,
        machineId = row.MachineId,
        plan = LicenseCodec.PlanName(row.Plan),
        issuedOn = row.IssuedOn,
        expires = row.Expires,
        atUtc = row.AtUtc.ToString("O", CultureInfo.InvariantCulture),
    });

    /// <summary>يقرأ سطراً — ويعيد <c>null</c> للسطر التالف بلا انهيار.</summary>
    internal static LedgerRow? Parse(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var plan = root.TryGetProperty("plan", out var planEl)
                ? ParsePlan(planEl.GetString())
                : LicensePlan.Annual;
            var at = root.TryGetProperty("atUtc", out var atEl)
                     && DateTime.TryParse(atEl.GetString(), null,
                         DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTime.MinValue;
            return new LedgerRow(
                root.GetProperty("code").GetString() ?? "",
                root.GetProperty("company").GetString() ?? "",
                root.GetProperty("machineId").GetString() ?? "",
                plan,
                root.TryGetProperty("issuedOn", out var issuedEl) ? issuedEl.GetString() ?? "" : "",
                root.TryGetProperty("expires", out var expEl) ? expEl.GetString() ?? "" : "",
                at);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException
                                      or InvalidOperationException or FormatException)
        {
            // ‏سطرٌ تالفٌ لا يُسقط السجلَّ كلَّه.
            return null;
        }
    }

    /// <summary>آخرُ ما أُصدر — الأحدثُ أوّلاً. ويتجاوز السطورَ التالفةَ بلا انهيار.</summary>
    public static IReadOnlyList<LedgerRow> ReadRecent(int count = 10) => ReadRecentFrom(DefaultPath, count);

    internal static IReadOnlyList<LedgerRow> ReadRecentFrom(string path, int count = 10)
    {
        if (!File.Exists(path))
            return [];

        try
        {
            var rows = new List<LedgerRow>();
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (Parse(line) is { } row)
                    rows.Add(row);
            }

            return rows.OrderByDescending(r => r.AtUtc).Take(count).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static LicensePlan ParsePlan(string? name) => name switch
    {
        "Trial7" => LicensePlan.Trial7,
        "Lifetime" => LicensePlan.Lifetime,
        _ => LicensePlan.Annual,
    };
}
