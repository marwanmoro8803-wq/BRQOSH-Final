using System.IO.Compression;
using System.Xml.Linq;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-215) مولّد ملفات Excel الحقيقية.
///
/// كان التصدير CSV: يفتح في Excel لكن بلا أي تنسيق — لا عرض أعمدة، ولا صيغ أرقام
/// (فتظهر 1234.5 بدل 1,234.50)، ولا اتجاه من اليمين، ولا تمييز للرؤوس.
/// </summary>
public class XlsxWriterTests
{
    private static readonly string[] Headers = ["رقم الحساب", "اسم الحساب", "الرصيد", "التاريخ"];

    private static byte[] Sample() => XlsxWriter.Build(
        "ميزان المراجعة",
        Headers,
        [
            [
                XlsxWriter.Cell.Text("1101"),
                XlsxWriter.Cell.Text("النقدية في الصندوق"),
                XlsxWriter.Cell.Number(1234.50m),
                XlsxWriter.Cell.Date(new DateOnly(2026, 7, 25)),
            ],
            [
                XlsxWriter.Cell.Text("2101"),
                XlsxWriter.Cell.Text("الذمم الدائنة"),
                XlsxWriter.Cell.Number(-987.25m),
                XlsxWriter.Cell.Date(new DateOnly(2026, 1, 1)),
            ],
        ]);

    private static XDocument Part(byte[] xlsx, string path)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        var entry = zip.GetEntry(path);
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        return XDocument.Load(stream);
    }

    /// <summary>الملف أرشيف Open XML صحيح البنية — لا CSV بامتداد مضلِّل.</summary>
    [Fact]
    public void Output_IsValidOpenXmlPackage()
    {
        var xlsx = Sample();

        Assert.Equal(0x50, xlsx[0]);   // PK — توقيع ZIP
        Assert.Equal(0x4B, xlsx[1]);

        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        foreach (var required in new[]
                 {
                     "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml",
                     "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml",
                 })
        {
            Assert.True(zip.GetEntry(required) is not null, $"جزء مفقود: {required}");
        }
    }

    /// <summary>الأرقام تُكتب كأرقام لا كنصّ — وإلا تعذّر الجمع في Excel.</summary>
    [Fact]
    public void Numbers_AreNumericCells_WithAccountingFormat()
    {
        var sheet = Part(Sample(), "xl/worksheets/sheet1.xml");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var balance = sheet.Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == "C2");
        Assert.Null(balance.Attribute("t"));                       // ليست inlineStr
        Assert.Equal("1234.50", balance.Element(ns + "v")?.Value);
        Assert.Equal("2", (string?)balance.Attribute("s"));        // نمط الرقم المحاسبي

        var styles = Part(Sample(), "xl/styles.xml");
        Assert.Contains(styles.Descendants().Where(e => e.Name.LocalName == "numFmt"),
            e => (string?)e.Attribute("formatCode") == "#,##0.00");
    }

    /// <summary>التواريخ تُكتب بتسلسل Excel لا كنصّ — فتُفرز وتُصفّى زمنياً.</summary>
    [Fact]
    public void Dates_AreSerialNumbers()
    {
        var sheet = Part(Sample(), "xl/worksheets/sheet1.xml");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var cell = sheet.Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == "D2");
        var expected = (int)(new DateTime(2026, 7, 25) - new DateTime(1899, 12, 30)).TotalDays;

        Assert.Equal(expected.ToString(), cell.Element(ns + "v")?.Value);
        Assert.Equal("3", (string?)cell.Attribute("s"));
    }

    /// <summary>الورقة من اليمين لليسار ورأسها مجمَّد — الواجهة عربية.</summary>
    [Fact]
    public void Sheet_IsRightToLeft_WithFrozenHeader()
    {
        var sheet = Part(Sample(), "xl/worksheets/sheet1.xml");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var view = sheet.Descendants(ns + "sheetView").Single();
        Assert.Equal("1", (string?)view.Attribute("rightToLeft"));
        Assert.Equal("frozen", (string?)view.Element(ns + "pane")?.Attribute("state"));

        Assert.Equal(Headers.Length, sheet.Descendants(ns + "col").Count());   // عرض لكل عمود
    }

    /// <summary>العربية تصل سليمة، والرموز الخاصة لا تُفسد الـXML.</summary>
    [Fact]
    public void ArabicAndSpecialCharacters_SurviveIntact()
    {
        var xlsx = XlsxWriter.Build("ورقة", ["الوصف"],
            [[XlsxWriter.Cell.Text("قوس & \"اقتباس\" <وسم> 'مفرد'")]]);

        var sheet = Part(xlsx, "xl/worksheets/sheet1.xml");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var text = sheet.Descendants(ns + "t").Last().Value;
        Assert.Equal("قوس & \"اقتباس\" <وسم> 'مفرد'", text);
    }

    /// <summary>اسم الورقة يُقصّ ويُنظَّف — Excel يرفض ما زاد عن 31 حرفاً أو حمل رموزاً محجوزة.</summary>
    [Fact]
    public void SheetName_IsSanitizedForExcel()
    {
        var xlsx = XlsxWriter.Build(new string('ط', 40) + "[محجوز]/:*?", ["أ"], [[XlsxWriter.Cell.Text("ب")]]);

        var workbook = Part(xlsx, "xl/workbook.xml");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var name = (string?)workbook.Descendants(ns + "sheet").Single().Attribute("name");

        Assert.NotNull(name);
        Assert.True(name!.Length <= 31, $"طول الاسم {name.Length}");
        Assert.DoesNotContain('[', name);
        Assert.DoesNotContain('/', name);
    }
}
