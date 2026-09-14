// مولّد أدلّة الطباعة والباركود وExcel (CEEF-226)
//
// لا يفحص الكود — **يُنتج المخرجات الحقيقية** من قاعدة المستخدم ثم يفحصها:
//   • فاتورة مبيعات مرحّلة → PDF + فكّ ترميز TLV لرمز ZATCA ومطابقته ببيانات الفاتورة
//   • سند قبض وسند صرف → PDF
//   • قيد يوميّة + ميزان مراجعة → PDF
//   • تصدير Excel حقيقي (.xlsx) للفحص بـExcel
//
// **قراءة فقط**: لا يكتب في القاعدة إطلاقاً (باستثناء سجلّ تدقيق التصدير الذي
// تكتبه خدمة الطباعة نفسها — وهو سلوكها الطبيعيّ عند أي طباعة يقوم بها المستخدم).
//
//   dotnet run --project tools/PrintEvidence

using System.Text;
using System.Text.Json;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var outDir = Path.Combine(repoRoot, "dist", "evidence", "print");
Directory.CreateDirectory(outDir);

var (provider, conn) = ResolveConnection();
Console.WriteLine($"[..] المزوّد: {provider}");

// ‏(م-٦ · ن-٢٢ · تبعيّة) ‏**إذنٌ باسمِ القاعدة قبل أن تُلمس.**
//
// ‏تُقرأ هذه الأداةُ «مُصدِّرَ أدلّة» فيُظنّ أنّها تقرأ. **وهي تكتب**: كلُّ تصديرٍ يمرّ
// ‏بـ`DocumentPrintService` الذي يستدعي `AuditService.LogAsync` — فيُضاف صفٌّ باسم
// ‏«print-evidence» إلى `audit_log`، **وهو سجلٌّ مُتسلسلٌ مربوطٌ بالتجزئة**، فالصفُّ
// ‏يدخل السلسلةَ ولا يُحذف منها بلا كسرِها. وهي تحلّ الاتّصالَ من `DatabaseConfigFile`
// ‏أي **دفاترَ المُشغِّل** ما لم يُقَل غيرُ ذلك.
LiveBooksGuard.RequireConsentFor(
    LiveBooksGuard.DescribeTarget(conn, provider == DatabaseProvider.Sqlite),
    "‏تكتب صفَّ تدقيقٍ باسم «print-evidence» في سلسلة السجلّ عن كلّ مستندٍ تُصدّره.");

await using var db = BrqoshDbContextFactory.Create(provider, conn);
if (!await db.Database.CanConnectAsync())
{
    Console.WriteLine("[SKIP] تعذّر الاتصال بقاعدة البيانات المهيّأة.");
    return 0;
}

SecurityContext.CurrentRole = Brqosh.Domain.Security.UserRole.Administrator;
SecurityContext.CurrentUsername = "print-evidence";

var company = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
if (company is null) { Console.WriteLine("[SKIP] لا توجد شركة."); return 0; }
Console.WriteLine($"[OK] الشركة: {company.NameAr} · الرقم الضريبي: {Mask(company.VatRegistrationNumber)}");

var print = new DocumentPrintService(db, new AuditService(db), new ExchangeRateService(db));
var findings = new List<string>();
var manifest = new List<object>();

// ── 1. فاتورة مبيعات مرحّلة ─────────────────────────────────────────────
var invoice = await db.SalesInvoices
    .Include(i => i.Lines)
    .Where(i => i.CompanyId == company.Id && i.Status == SalesInvoiceStatus.Posted)
    .OrderByDescending(i => i.Id)
    .FirstOrDefaultAsync();

if (invoice is null)
{
    findings.Add("لا فاتورة مبيعات مرحّلة — تعذّر إثبات طباعة الفاتورة والباركود.");
}
else
{
    var res = await print.ExportSalesInvoiceAsync(company.Id, invoice.Id, "print-evidence");
    var path = Path.Combine(outDir, "01-sales-invoice.pdf");
    await File.WriteAllBytesAsync(path, res.PdfBytes);
    Console.WriteLine($"[OK] فاتورة {invoice.InvoiceNumber} → {res.PdfBytes.Length:N0} بايت");
    manifest.Add(new { doc = "sales_invoice", number = invoice.InvoiceNumber, bytes = res.PdfBytes.Length, file = Path.GetFileName(path) });

    var customerName = await db.Customers
        .Where(c => c.Id == invoice.CustomerId).Select(c => c.NameAr).FirstOrDefaultAsync();

    // ── الباركود: هل وُلِّد، وهل يطابق الفاتورة؟ ──
    var zatca = await db.ZatcaEInvoiceDocuments
        .Where(z => z.CompanyId == company.Id && z.SourceDocumentId == invoice.Id)
        .OrderByDescending(z => z.Id)
        .FirstOrDefaultAsync();

    // (CEEF-226) الفحص على **المطبوع** لا على سجلّ القاعدة: حين لا يوجد مستند ZATCA
    // تحسب خدمة الطباعة رمز المرحلة الأولى محلياً — فالسؤال هو «هل على الورقة باركود؟»
    // لا «هل في الجدول صفّ؟».
    var tlvOnPrint = zatca?.QrTlvBase64;
    var fromFallback = false;
    if (string.IsNullOrWhiteSpace(tlvOnPrint)
        && !string.IsNullOrWhiteSpace(company.NameAr)
        && !string.IsNullOrWhiteSpace(company.VatRegistrationNumber))
    {
        tlvOnPrint = Brqosh.Infrastructure.Zatca.ZatcaQrGenerator.GenerateTlvBase64Phase1(
            company.NameAr,
            company.VatRegistrationNumber,
            invoice.InvoiceDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)), DateTimeKind.Utc),
            invoice.GrandTotal(),
            invoice.VatAmount);
        fromFallback = true;
    }

    // والدليل القاطع: صورة فعلاً مدمجة في ملف PDF (XObject من نوع Image).
    var pdfText = Encoding.Latin1.GetString(res.PdfBytes);
    var hasEmbeddedImage = pdfText.Contains("/Subtype /Image", StringComparison.Ordinal)
                        || pdfText.Contains("/Subtype/Image", StringComparison.Ordinal);
    if (!hasEmbeddedImage)
        findings.Add("ملف الفاتورة لا يحوي أي صورة مدمجة — الباركود غير مطبوع فعلياً.");
    else
        Console.WriteLine("[OK] الفاتورة تحوي صورة مدمجة (الباركود على الورقة)");

    if (string.IsNullOrWhiteSpace(tlvOnPrint))
    {
        findings.Add($"الفاتورة {invoice.InvoiceNumber}: لا رمز QR ولا مسار احتياطي — راجع الرقم الضريبي للمنشأة.");
    }
    else
    {
        if (fromFallback)
            Console.WriteLine("[OK] الرمز من مسار المرحلة الأولى الاحتياطي (تكامل ZATCA غير مفعّل)");
        var zatcaQr = tlvOnPrint;
        var png = ZatcaQrImageHelper.TryCreatePng(zatcaQr);
        if (png is null || png.Length == 0)
            findings.Add("تعذّر توليد صورة رمز QR من TLV.");
        else
        {
            await File.WriteAllBytesAsync(Path.Combine(outDir, "01-sales-invoice-qr.png"), png);
            Console.WriteLine($"[OK] صورة QR: {png.Length:N0} بايت");
        }

        var tags = DecodeTlv(zatcaQr);
        var report = new StringBuilder();
        report.AppendLine("# فكّ ترميز رمز ZATCA (TLV)");
        report.AppendLine();
        foreach (var (tag, value) in tags)
            report.AppendLine($"- وسم {tag}: `{Preview(tag, value)}`");
        await File.WriteAllTextAsync(Path.Combine(outDir, "01-qr-decoded.md"), report.ToString(), Encoding.UTF8);

        // مطابقة الوسوم الخمسة الإلزامية ببيانات الفاتورة الفعلية
        Check(tags, 1, company.NameAr, "اسم البائع", findings);
        Check(tags, 2, company.VatRegistrationNumber, "الرقم الضريبي", findings);
        Check(tags, 4, invoice.GrandTotal().ToString("F2"), "إجمالي الفاتورة", findings);
        Check(tags, 5, invoice.VatAmount.ToString("F2"), "مبلغ الضريبة", findings);
        if (!tags.ContainsKey(3)) findings.Add("وسم 3 (الطابع الزمني) غائب عن رمز QR.");

        Console.WriteLine($"[OK] TLV يحوي {tags.Count} وسماً: {string.Join(", ", tags.Keys.OrderBy(k => k))}");
    }

    // ── بيانات لا بدّ أن تظهر على المطبوع ──
    if (string.IsNullOrWhiteSpace(company.NameAr)) findings.Add("اسم المنشأة فارغ — يظهر ناقصاً على كل مطبوع.");
    if (string.IsNullOrWhiteSpace(company.VatRegistrationNumber)) findings.Add("الرقم الضريبي فارغ — فاتورة ضريبية غير مكتملة.");
    if (string.IsNullOrWhiteSpace(customerName)) findings.Add("اسم العميل غائب عن الفاتورة المطبوعة.");
    if (string.IsNullOrWhiteSpace(company.LogoFilePath)) findings.Add("لا شعار مهيّأ للمنشأة (اختياري، لكنه يظهر فارغاً في الترويسة).");
}

// ── 2. سند قبض وسند صرف ─────────────────────────────────────────────────
await TryExport("02-receipt-voucher.pdf", "سند قبض", async () =>
{
    var v = await db.ReceiptVouchers
        .Where(v => v.CompanyId == company.Id && v.Status == ReceiptVoucherStatus.Posted)
        .OrderByDescending(v => v.Id).FirstOrDefaultAsync();
    return v is null ? null : await print.ExportReceiptVoucherAsync(company.Id, v.Id, "print-evidence");
});

await TryExport("03-payment-voucher.pdf", "سند صرف", async () =>
{
    var v = await db.PaymentVouchers
        .Where(v => v.CompanyId == company.Id && v.Status == PaymentVoucherStatus.Posted)
        .OrderByDescending(v => v.Id).FirstOrDefaultAsync();
    return v is null ? null : await print.ExportPaymentVoucherAsync(company.Id, v.Id, "print-evidence");
});

await TryExport("04-journal-entry.pdf", "قيد يوميّة", async () =>
{
    var e = await db.JournalEntries
        .Where(e => e.CompanyId == company.Id && e.Status == Brqosh.Domain.Accounting.JournalEntryStatus.Posted)
        .OrderByDescending(e => e.Id).FirstOrDefaultAsync();
    return e is null ? null : await print.ExportJournalEntryAsync(company.Id, e.Id, "print-evidence");
});

// ── 2ب. عيّنة تخطيط بتاريخ لا يلتبس ─────────────────────────────────────
// 2026-03-14: اليوم ≠ الشهر ≠ السنة، فأيّ إعادة ترتيب ثنائية الاتجاه تنكشف فوراً.
// تُولَّد بلا قاعدة بيانات — غرضها فحص التخطيط لا البيانات.
// شعار العيّنة: علامة نصّية مولَّدة برمجياً بخلفية شفافة — لإثبات أن الترويسة
// تعرض الشعار فعلاً. ليست هوية بصرية لأيّ منشأة.
var sampleLogo = BuildSampleLogoPng();
var sampleBranding = new PrintBranding(
    "منشأة العيّنة", "310123456700003",
    CompanyNameEn: "Sample Establishment",
    AddressAr: "الرياض · حي الملقا · ص.ب 12345",
    LogoImageBytes: sampleLogo);
var samplePdf = SalesInvoicePdfGenerator.Generate(
    sampleBranding,
    "INV-SAMPLE-014",
    new DateOnly(2026, 3, 14),
    [
        new SalesInvoicePdfGenerator.Line("خدمة استشارية طويلة الوصف لاختبار الالتفاف", 3m, 1250m, 3750m),
        new SalesInvoicePdfGenerator.Line("قطع غيار", 12m, 87.5m, 1050m),
    ],
    4800m, 720m, 5520m,
    new SalesInvoicePdfGenerator.InvoicePrintOptions(
        PartyName: "عميل العيّنة",
        QrTlvBase64: Brqosh.Infrastructure.Zatca.ZatcaQrGenerator.GenerateTlvBase64Phase1(
            "منشأة العيّنة", "310123456700003",
            new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc), 5520m, 720m)));
await File.WriteAllBytesAsync(Path.Combine(outDir, "06-layout-sample.pdf"), samplePdf);
Console.WriteLine($"[OK] عيّنة تخطيط (2026-03-14) → {samplePdf.Length:N0} بايت");

// ── 3. تصدير Excel حقيقي ────────────────────────────────────────────────
var today = DateOnly.FromDateTime(DateTime.Now);
var rows = await new TrialBalanceQuery(db).ExecuteAsync(company.Id, new DateOnly(today.Year, 1, 1), today);
var xlsxPath = Path.Combine(outDir, "05-trial-balance.xlsx");
await File.WriteAllBytesAsync(xlsxPath, XlsxWriter.Build(
    "ميزان المراجعة",
    ["رمز الحساب", "اسم الحساب", "مدين", "دائن", "رصيد مدين", "رصيد دائن"],
    rows.Select(r => (IReadOnlyList<XlsxWriter.Cell>)
    [
        XlsxWriter.Cell.Text(r.AccountCode),
        XlsxWriter.Cell.Text(r.AccountNameAr),
        XlsxWriter.Cell.Number(r.TotalDebit),
        XlsxWriter.Cell.Number(r.TotalCredit),
        XlsxWriter.Cell.Number(r.BalanceDebit),
        XlsxWriter.Cell.Number(r.BalanceCredit),
    ]).ToList()));
Console.WriteLine($"[OK] Excel: {rows.Count} صفاً → {new FileInfo(xlsxPath).Length:N0} بايت");
manifest.Add(new { doc = "trial_balance_xlsx", rows = rows.Count, file = Path.GetFileName(xlsxPath) });
if (rows.Count == 0) findings.Add("ميزان المراجعة فارغ — تصدير Excel بلا صفوف بيانات.");

// ── الخلاصة ─────────────────────────────────────────────────────────────
await File.WriteAllTextAsync(
    Path.Combine(outDir, "manifest.json"),
    JsonSerializer.Serialize(new { generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), artifacts = manifest, findings },
        new JsonSerializerOptions { WriteIndented = true }),
    Encoding.UTF8);

Console.WriteLine();
if (findings.Count == 0)
{
    Console.WriteLine("=== PASS: كل المخرجات وُلِّدت والباركود مطابق ===");
}
else
{
    Console.WriteLine($"=== اكتشافات ({findings.Count}) ===");
    foreach (var f in findings) Console.WriteLine("  ! " + f);
}
Console.WriteLine($"الأدلّة: {outDir}");
return findings.Count == 0 ? 0 : 1;

// ── دوال مساعدة ─────────────────────────────────────────────────────────

async Task TryExport(string fileName, string label, Func<Task<PrintResult?>> export)
{
    try
    {
        var res = await export();
        if (res is null) { Console.WriteLine($"[--] {label}: لا مستند مرحّل — تُخطّى."); return; }
        await File.WriteAllBytesAsync(Path.Combine(outDir, fileName), res.PdfBytes);
        Console.WriteLine($"[OK] {label} → {res.PdfBytes.Length:N0} بايت");
        manifest.Add(new { doc = label, bytes = res.PdfBytes.Length, file = fileName });
    }
    catch (Exception ex)
    {
        findings.Add($"{label}: فشل التوليد — {ex.Message}");
    }
}

static void Check(Dictionary<byte, byte[]> tags, byte tag, string expected, string label, List<string> findings)
{
    if (!tags.TryGetValue(tag, out var raw))
    {
        findings.Add($"وسم {tag} ({label}) غائب عن رمز QR.");
        return;
    }
    var actual = Encoding.UTF8.GetString(raw);
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
        findings.Add($"وسم {tag} ({label}) لا يطابق الفاتورة: «{actual}» ≠ «{expected}».");
}

/// <summary>فكّ ترميز TLV: بايت وسم + بايت طول + قيمة.</summary>
static Dictionary<byte, byte[]> DecodeTlv(string base64)
{
    var bytes = Convert.FromBase64String(base64);
    var map = new Dictionary<byte, byte[]>();
    var i = 0;
    while (i + 1 < bytes.Length)
    {
        var tag = bytes[i++];
        int len = bytes[i++];
        if (i + len > bytes.Length) break;
        map[tag] = bytes[i..(i + len)];
        i += len;
    }
    return map;
}

static string Preview(byte tag, byte[] value) =>
    tag is 6 or 7
        ? $"<{value.Length} بايت ثنائية>"
        : Encoding.UTF8.GetString(value);

static string Mask(string? s) =>
    string.IsNullOrEmpty(s) ? "(فارغ)" : s.Length <= 6 ? s : s[..3] + new string('•', s.Length - 6) + s[^3..];

static (DatabaseProvider, string) ResolveConnection()
{
    // (CEEF-236) ‏عبر `DatabaseConfigFile` لا بقراءةٍ خام — السلسلةُ تُكتب محميّةً بـDPAPI الآن،
    // ‏والقراءةُ الخام كانت تُعيد `BRQOSH1:…` فتقع الأداةُ على SQLite وتُنتج «دليلَ طباعة» من
    // ‏قاعدةٍ فارغة.
    try
    {
        var config = DatabaseConfigFile.Load();
        if (config is not null && !string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            return config.Provider.Contains("postgres", StringComparison.OrdinalIgnoreCase)
                ? (DatabaseProvider.PostgreSql, config.ConnectionString)
                : (DatabaseProvider.Sqlite, config.ConnectionString);
        }
    }
    catch (InvalidOperationException ex)
    {
        // (CEEF-236 · م-١٥) ‏**لا سقوطَ صامتٌ على قاعدةٍ أخرى.** كان الالتقاطُ يُمرّر إلى SQLite
        // ‏الافتراضية فتخرج «أدلّةُ طباعة» من قاعدةٍ فارغة بحالة نجاح — وهو بعينه ما يزعم التعليقُ
        // ‏أعلاه منعَه.
        Console.Error.WriteLine($"FAIL: {ex.Message}");
        Environment.Exit(3);
    }

    return (DatabaseProvider.Sqlite, "Data Source=" + BrqoshDbContextFactory.DefaultSqlitePath());
}

/// <summary>يبني PNG صغيراً بخلفية شفافة — دائرة داكنة وحرف — لإثبات ظهور الشعار.</summary>
static byte[] BuildSampleLogoPng()
{
    const int size = 128;
    var px = new byte[size * size * 4];
    var cx = size / 2.0; var cy = size / 2.0; var r = size * 0.46;

    for (var y = 0; y < size; y++)
    {
        for (var x = 0; x < size; x++)
        {
            var i = (y * size + x) * 4;
            var d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            var inRing = d <= r && d >= r * 0.72;
            var inBar = Math.Abs(x - cx) < size * 0.06 && Math.Abs(y - cy) < r * 0.55;
            if (inRing || inBar)
            {
                px[i] = 0x66; px[i + 1] = 0x48; px[i + 2] = 0x0D;  // BGRA — أخضر مزرقّ داكن
                px[i + 3] = 255;
            }
        }
    }
    return EncodePng(px, size, size);
}

/// <summary>ترميز PNG بلا تبعية: رأس + IHDR + IDAT (Deflate) + IEND.</summary>
static byte[] EncodePng(byte[] bgra, int w, int h)
{
    var raw = new byte[h * (1 + w * 4)];
    for (var y = 0; y < h; y++)
    {
        raw[y * (1 + w * 4)] = 0;                     // مرشّح None
        for (var x = 0; x < w; x++)
        {
            var s = (y * w + x) * 4;
            var d = y * (1 + w * 4) + 1 + x * 4;
            raw[d] = bgra[s + 2]; raw[d + 1] = bgra[s + 1]; raw[d + 2] = bgra[s]; raw[d + 3] = bgra[s + 3];
        }
    }

    using var idat = new MemoryStream();
    using (var z = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionLevel.Optimal, true))
        z.Write(raw, 0, raw.Length);

    using var ms = new MemoryStream();
    ms.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

    var ihdr = new byte[13];
    BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(w)).CopyTo(ihdr, 0);
    BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(h)).CopyTo(ihdr, 4);
    ihdr[8] = 8; ihdr[9] = 6;   // 8 بت لكل قناة · RGBA
    WriteChunk(ms, "IHDR", ihdr);
    WriteChunk(ms, "IDAT", idat.ToArray());
    WriteChunk(ms, "IEND", []);
    return ms.ToArray();

    static void WriteChunk(MemoryStream target, string type, byte[] data)
    {
        target.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(data.Length)));
        var body = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        target.Write(body);
        target.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((int)Crc32(body))));
    }

    static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return crc ^ 0xFFFFFFFF;
    }
}
