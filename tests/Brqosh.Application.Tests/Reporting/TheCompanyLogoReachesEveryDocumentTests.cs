using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Sales;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «هل يُختار شعارُ الشركة ليظهر على الفواتير بالتنسيق المعتمد؟») <b>الشعارُ الذي ترفعه المنشأةُ في إعداداتها يصل إلى
/// كلِّ مطبوع</b> — لا الفاتورةِ وحدَها. يُقاس بفرق حجم الملف: ورقةٌ بشعارٍ مضغوطٍ بالضجيج أكبرُ بآلاف البايتات من الورقة نفسِها بلا شعار.
/// </summary>
public class TheCompanyLogoReachesEveryDocumentTests
{
    /// <summary>
    /// صورةٌ من ضجيجٍ لا تُضغط — فيظهر وجودُها في حجم الملف بوضوح. تُبنى PNG بيدٍ (رأسٌ + IDAT بلا ضغط + نهاية) بلا اعتمادٍ على مكتبة صور.
    /// </summary>
    private static byte[] NoisyLogoPng()
    {
        const int size = 96;
        var random = new Random(7);
        var raw = new byte[size * (1 + size * 3)];
        for (var y = 0; y < size; y++)
        {
            var row = y * (1 + size * 3);
            raw[row] = 0;                                   // مرشّح «بلا»
            for (var i = 1; i <= size * 3; i++)
                raw[row + i] = (byte)random.Next(256);
        }

        using var deflated = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(deflated, System.IO.Compression.CompressionLevel.NoCompression, leaveOpen: true))
            z.Write(raw, 0, raw.Length);

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), size);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), size);
        header[8] = 8;   // عمق البت
        header[9] = 2;   // RGB
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", deflated.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var body = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        stream.Write(body);
        var crc = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(body));
        stream.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }

    private static readonly PrintBranding Plain = new(
        "شركة برقوش للتجارة", "300123456700003", "Brqosh Trading", "الرياض، حي العليا");

    private static Dictionary<string, byte[]> Documents(PrintBranding branding)
    {
        var statementLines = new List<CustomerStatementLine>
        {
            new(new DateOnly(2026, 9, 1), "INV-2026-000101", "فاتورة ضريبية", 11500m, 0m, 11500m),
        };
        var trial = new List<TrialBalanceRow>
        {
            new("1101", "الصندوق", AccountType.Asset, 52000m, 12000m),
            new("4101", "إيرادات المبيعات", AccountType.Revenue, 0m, 40000m),
        };
        var income = new IncomeStatementResult(new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 31),
            [new("4101", "إيرادات المبيعات", AccountType.Revenue, 121000m)],
            [new("5101", "تكلفة المبيعات", AccountType.Expense, 64000m)]);

        return new Dictionary<string, byte[]>
        {
            ["سند قبض"] = VoucherPdfGenerator.ReceiptVoucher(branding, "RV-1", new DateOnly(2026, 10, 20), "عميل", 4370m, "بنك", "T1", ""),
            ["سند صرف"] = VoucherPdfGenerator.PaymentVoucher(branding, "PV-1", new DateOnly(2026, 10, 21), "مورد", 2500m, "شيك", "", ""),
            ["كشف حساب"] = StatementPdfGenerator.CustomerStatement(branding, "عميل", 11500m, statementLines),
            ["ميزان مراجعة"] = TrialBalancePdfGenerator.Generate(branding, new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 31), trial),
            ["قائمة دخل"] = FinancialReportPdfGenerator.IncomeStatement(branding, income),
            ["قيد يومية"] = JournalEntryPdfGenerator.Generate(branding, "JE-1", new DateOnly(2026, 10, 20), "مرحَّل", "إثبات", "INV-1",
                [new("1201", "العملاء", 4370m, 0m), new("4101", "إيرادات", 0m, 4370m)], 4370m, 4370m),
            ["قسيمة راتب"] = PayslipPdfGenerator.Build(branding, new PayslipData("2026-09", new DateOnly(2026, 9, 28),
                "موظف", "E-1", "1098765432", "محاسب", null, 7000m, 1750m, 700m, 300m, 787.5m)),
            ["تقرير نقاط البيع"] = PosReportPdfGenerator.DailySummary(branding,
                [new PosReportQuery.DailySaleRow("2026-10-19", 42, 18250.5m, 2380.5m)]),
        };
    }

    [Fact]
    public void EveryDocument_CarriesTheCompanyLogo_WhenOneIsSet()
    {
        var logo = NoisyLogoPng();
        var without = Documents(Plain);
        var with = Documents(Plain with { LogoImageBytes = logo });

        foreach (var (name, plainPdf) in without)
        {
            var delta = with[name].Length - plainPdf.Length;
            Assert.True(delta > 3_000, $"{name}: لم يصل الشعارُ إلى الملف (فرق الحجم {delta} بايت).");
        }
    }

    [Fact]
    public void ThePreparerAndTheAuditor_AreAddedToTheFooter_WithoutBreakingAnyDocument()
    {
        var withPeople = Documents(Plain with { PreparedBy = "خالد العتيبي", AuditorName = "مكتب الأمانة للمراجعة" });
        var plain = Documents(Plain);

        foreach (var (name, pdf) in withPeople)
        {
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
            Assert.True(pdf.Length > plain[name].Length, $"{name}: لم يضف التذييلُ سطرَ «أعدّه».");
        }
    }
}
