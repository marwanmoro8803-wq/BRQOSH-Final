using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>نوع خلية — يحدد التنسيق في الملف الناتج.</summary>
public enum XlsxCellKind
{
    Text = 0,
    Number = 1,
    Date = 2,
}

/// <summary>
/// (CEEF-215) مولّد ملفات Excel حقيقية (.xlsx) — بلا أي حزمة خارجية.
///
/// كان التصدير CSV بامتداد يوحي بـExcel: يفتح، لكن **بلا أي تنسيق** — لا عرض أعمدة،
/// ولا صيغ أرقام (فتظهر 1234.5 بدل 1,234.50)، ولا اتجاه من اليمين، ولا تمييز للرؤوس.
/// وأسوأ: نصّ يبدأ بصفر أو يشبه تاريخاً يُفسده Excel عند الفتح.
///
/// صيغة Open XML ليست إلا أرشيف ZIP من ملفات XML، و<c>System.IO.Compression</c> مدمج —
/// فلا حاجة لإضافة تبعية لأجل تنسيق.
/// </summary>
public static class XlsxWriter
{
    private const string SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>خانة واحدة بقيمتها ونوعها.</summary>
    public readonly record struct Cell(object? Value, XlsxCellKind Kind = XlsxCellKind.Text)
    {
        public static Cell Text(object? v) => new(v);
        public static Cell Number(decimal? v) => new(v, XlsxCellKind.Number);
        public static Cell Date(DateOnly? v) => new(v, XlsxCellKind.Date);
    }

    /// <summary>
    /// يبني مصنّفاً بورقة واحدة: صفّ رؤوس مميّز، وأعمدة بعرض مناسب، وأرقام بصيغة محاسبية،
    /// والورقة من اليمين لليسار (الواجهة عربية).
    /// </summary>
    public static byte[] Build(
        string sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<Cell>> rows,
        bool rightToLeft = true)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes());
            Write(zip, "_rels/.rels", RootRels());
            Write(zip, "xl/workbook.xml", Workbook(SafeSheetName(sheetName)));
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
            Write(zip, "xl/styles.xml", Styles());
            Write(zip, "xl/worksheets/sheet1.xml", Sheet(headers, rows, rightToLeft));
        }

        return buffer.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string xml)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(xml);
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>اسم الورقة في Excel: 31 حرفاً كحدّ أقصى وبلا الرموز المحجوزة.</summary>
    private static string SafeSheetName(string name)
    {
        var cleaned = new string((name ?? "Sheet1")
            .Where(c => c is not (':' or '\\' or '/' or '?' or '*' or '[' or ']'))
            .ToArray());
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "Sheet1";
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private static string ContentTypes() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;

    private static string RootRels() =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="{RelNs}/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string Workbook(string sheetName) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="{SheetNs}" xmlns:r="{RelNs}">
          <sheets><sheet name="{Escape(sheetName)}" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;

    private static string WorkbookRels() =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="{RelNs}/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="{RelNs}/styles" Target="styles.xml"/>
        </Relationships>
        """;

    /// <summary>
    /// أنماط: 0 عادي · 1 رأس (عريض على خلفية) · 2 رقم محاسبي #,##0.00 · 3 تاريخ yyyy-mm-dd.
    /// </summary>
    private static string Styles() =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="{SheetNs}">
          <numFmts count="2">
            <numFmt numFmtId="164" formatCode="#,##0.00"/>
            <numFmt numFmtId="165" formatCode="yyyy\-mm\-dd"/>
          </numFmts>
          <fonts count="2">
            <font><sz val="11"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
          </fonts>
          <fills count="3">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF1F3A5F"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="4">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
            <xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
          </cellXfs>
        </styleSheet>
        """;

    private static string Sheet(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<Cell>> rows,
        bool rightToLeft)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append($"<worksheet xmlns=\"{SheetNs}\">");

        // اتجاه الورقة + تجميد صفّ الرؤوس ليبقى ظاهراً عند التمرير.
        sb.Append($"<sheetViews><sheetView workbookViewId=\"0\" rightToLeft=\"{(rightToLeft ? 1 : 0)}\" tabSelected=\"1\">");
        sb.Append("<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>");
        sb.Append("</sheetView></sheetViews>");

        sb.Append("<cols>");
        for (var c = 0; c < headers.Count; c++)
            sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{ColumnWidth(headers, rows, c)}\" customWidth=\"1\"/>");
        sb.Append("</cols>");

        sb.Append("<sheetData>");

        sb.Append("<row r=\"1\">");
        for (var c = 0; c < headers.Count; c++)
            sb.Append($"<c r=\"{Reference(c, 1)}\" s=\"1\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(headers[c])}</t></is></c>");
        sb.Append("</row>");

        for (var r = 0; r < rows.Count; r++)
        {
            sb.Append($"<row r=\"{r + 2}\">");
            var row = rows[r];
            for (var c = 0; c < row.Count; c++)
                sb.Append(RenderCell(row[c], Reference(c, r + 2)));
            sb.Append("</row>");
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string RenderCell(Cell cell, string reference)
    {
        switch (cell.Kind)
        {
            case XlsxCellKind.Number when cell.Value is decimal d:
                return $"<c r=\"{reference}\" s=\"2\"><v>{d.ToString(CultureInfo.InvariantCulture)}</v></c>";

            case XlsxCellKind.Date when cell.Value is DateOnly date:
                // تسلسل تواريخ Excel: 1899-12-30 هو الصفر (يشمل خلل السنة الكبيسة 1900).
                var serial = date.ToDateTime(TimeOnly.MinValue) - new DateTime(1899, 12, 30);
                return $"<c r=\"{reference}\" s=\"3\"><v>{(int)serial.TotalDays}</v></c>";

            default:
                var text = cell.Value?.ToString() ?? "";
                return text.Length == 0
                    ? $"<c r=\"{reference}\"/>"
                    : $"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(text)}</t></is></c>";
        }
    }

    /// <summary>عرض العمود من أطول محتوى فيه، ضمن حدّ معقول.</summary>
    private static int ColumnWidth(
        IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<Cell>> rows, int column)
    {
        var longest = headers[column].Length;
        foreach (var row in rows)
        {
            if (column >= row.Count) continue;
            var cell = row[column];
            var length = cell.Kind switch
            {
                XlsxCellKind.Number => 12,
                XlsxCellKind.Date => 12,
                _ => cell.Value?.ToString()?.Length ?? 0,
            };
            if (length > longest) longest = length;
        }

        return Math.Clamp(longest + 4, 10, 60);
    }

    private static string Reference(int columnIndex, int rowNumber)
    {
        var name = "";
        var i = columnIndex;
        do
        {
            name = (char)('A' + i % 26) + name;
            i = i / 26 - 1;
        } while (i >= 0);
        return name + rowNumber;
    }

    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            // XML 1.0 يرفض أحرف التحكم؛ إسقاطها أسلم من إنتاج ملف لا يُفتح.
            if (ch is < ' ' and not '\t' and not '\n' and not '\r')
                continue;

            sb.Append(ch switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&apos;",
                _ => ch.ToString(),
            });
        }
        return sb.ToString();
    }
}
