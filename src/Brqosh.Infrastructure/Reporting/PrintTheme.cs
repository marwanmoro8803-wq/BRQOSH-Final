using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// هويّةُ المطبوعات البصريّة في مكانٍ واحد: كحليٌّ للعناوين والأرقام، وذهبيٌّ خطَّ تمييزٍ لا أكثر،
/// ورماديٌّ هادئٌ للحدود. <b>كلُّ مستندٍ يبني على هذه المكوّنات</b> فيتّسق الشكلُ من السند إلى الفاتورة
/// إلى القوائم المالية، ويتغيّر اللونُ بسطرٍ واحدٍ لا بتتبّع خمسة عشر مولِّداً.
///
/// <para>الألوانُ عاليةُ التباين على الورق الأبيض وعلى الطباعة بالأبيض والأسود (الكحليُّ داكنٌ فيبقى مقروءاً
/// رماديّاً، والذهبيُّ لا يحمل معنىً بمفرده).</para>
/// </summary>
public static class PrintTheme
{
    public const string Navy = "#0F3D5E";
    public const string Gold = "#C9A227";
    public const string Ink = "#1B2733";
    public const string Muted = "#5B6B7A";
    public const string Line = "#C9D3DD";
    public const string Tint = "#EAF1F7";
    public const string Zebra = "#F6F9FC";

    /// <summary>شارةُ نوع المستند: كتلةٌ كحليّةٌ بنصٍّ أبيض — أوّلُ ما تراه العين.</summary>
    public static void TitleBadge(IContainer container, string title, float fontSize = 13)
    {
        container.Background(Navy).PaddingVertical(9).PaddingHorizontal(14).AlignCenter().AlignMiddle()
            .Text(title).Bold().FontSize(fontSize).FontColor("#FFFFFF");
    }

    /// <summary>بطاقةٌ بإطارٍ رماديٍّ هادئ — لحزم البيانات (المستند، الطرف، المجاميع).</summary>
    public static IContainer Card(this IContainer container) =>
        container.Border(0.75f).BorderColor(Line);

    /// <summary>عنوانُ بطاقةٍ على شريطٍ فاتحٍ بخطٍّ ذهبيٍّ سفليّ.</summary>
    public static void CardTitle(IContainer container, string title)
    {
        container.Background(Tint).BorderBottom(1.5f).BorderColor(Gold).PaddingVertical(5).PaddingHorizontal(10)
            .AlignRight().Text(title).Bold().FontSize(10).FontColor(Navy);
    }

    /// <summary>سطرُ «اسم : قيمة» داخل بطاقة — الاسمُ رماديٌّ والقيمةُ داكنةٌ فتُقرأ القيمةُ أوّلاً.</summary>
    public static void LabelValue(IContainer container, string label, string value, bool emphasizeValue = false,
        float labelWidth = 92)
    {
        // الصفحةُ تُبنى من اليسار: فالقيمةُ أوّلاً (يساراً) والاسمُ أخيراً (أقصى اليمين) — وهذا ترتيبُ القراءة العربيّة.
        container.ContentFromLeftToRight().PaddingVertical(4).PaddingHorizontal(10).Row(row =>
        {
            var valueText = row.RelativeItem().AlignRight().Text(value).FontSize(10).FontColor(Ink);
            if (emphasizeValue)
                valueText.Bold();
            row.ConstantItem(10);
            row.ConstantItem(labelWidth).AlignRight().Text(label).FontSize(9).FontColor(Muted);
        });
    }

    /// <summary>
    /// «المبلغ كتابةً» على شريطٍ فاتحٍ بخطٍّ ذهبيٍّ جانبيّ — يمنع التلاعبَ بالرقم وحدَه
    /// (يُستعمل في السند والفاتورة والمستخلص).
    /// </summary>
    public static void AmountInWords(IContainer container, decimal amount)
    {
        container.Background(Tint).BorderRight(3).BorderColor(Gold).PaddingVertical(7).PaddingHorizontal(10)
            .AlignRight().Text(text =>
            {
                text.Span("المبلغ كتابةً: ").Bold().FontSize(9).FontColor(Navy);
                text.Span(ArabicAmountWords.ForRiyals(amount)).FontSize(9).FontColor(Ink);
            });
    }

    /// <summary>خانةُ توقيعٍ: خطٌّ ثمّ الصفةُ تحته.</summary>
    public static void SignatureLine(IContainer container, string role)
    {
        container.PaddingHorizontal(14).Column(col =>
        {
            col.Item().PaddingTop(34).LineHorizontal(0.75f).LineColor(Ink);
            col.Item().PaddingTop(3).AlignCenter().Text(role).FontSize(9).FontColor(Muted);
        });
    }

    /// <summary>
    /// جدولُ التقارير الموحّد: رأسٌ كحليٌّ بنصٍّ أبيض، وصفوفٌ بتظليلٍ متناوب، وحدودٌ رماديّةٌ هادئة.
    /// <b>تُعدَّد الأعمدةُ بترتيب القراءة</b> — العمودُ الأوّلُ أقصى اليمين (الصفحةُ تُبنى من اليمين لليسار).
    /// </summary>
    public static void ReportTable(IContainer container, IReadOnlyList<PrintColumn> columns,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        container.ContentFromRightToLeft().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                foreach (var column in columns)
                {
                    if (column.FixedWidth is { } width)
                        c.ConstantColumn(width);
                    else
                        c.RelativeColumn(column.Weight);
                }
            });

            table.Header(h =>
            {
                foreach (var column in columns)
                    h.Cell().Background(Navy).Border(0.5f).BorderColor(Line).PaddingVertical(5).PaddingHorizontal(5)
                        .AlignCenter().Text(column.Header).Bold().FontSize(9).FontColor("#FFFFFF");
            });

            var index = 0;
            foreach (var row in rows)
            {
                var shaded = index++ % 2 == 1;
                for (var i = 0; i < columns.Count; i++)
                {
                    var cell = shaded ? table.Cell().Background(Zebra) : table.Cell();
                    var box = cell.Border(0.5f).BorderColor(Line).PaddingVertical(4).PaddingHorizontal(5);
                    var text = i < row.Count ? row[i] : string.Empty;
                    var slot = columns[i].Numeric ? box.AlignCenter() : box;
                    slot.Text(text).FontSize(9).FontColor(Ink);
                }
            }
        });
    }

    /// <summary>
    /// شريطُ الخلاصة تحت الجدول: بنودٌ «اسم · قيمة» متجاورة، <b>وآخرُها البندُ الذي يُراد أن يُرى</b> على الكحليّ.
    /// يحلّ محلّ سطر النصّ الذي كان يُطبع في التذييل مبتوراً.
    /// </summary>
    public static void SummaryBand(IContainer container, IReadOnlyList<(string Label, string Value)> items)
    {
        container.ContentFromRightToLeft().Border(0.75f).BorderColor(Line).Row(row =>
        {
            for (var i = 0; i < items.Count; i++)
            {
                var last = i == items.Count - 1;
                var cell = row.RelativeItem();
                var box = last ? cell.Background(Navy) : cell.Background(Tint);
                var labelColor = last ? "#FFFFFF" : Muted;
                var valueColor = last ? "#FFFFFF" : Navy;
                box.PaddingVertical(7).PaddingHorizontal(9).Column(col =>
                {
                    col.Item().AlignRight().Text(items[i].Label).FontSize(8.5f).FontColor(labelColor);
                    col.Item().AlignRight().Text(items[i].Value).Bold().FontSize(11).FontColor(valueColor);
                });
            }
        });
    }
}

/// <summary>عمودٌ في جدول التقارير: رأسُه ووزنُه (أو عرضٌ ثابت) وهل هو رقميٌّ يُوسَّط.</summary>
public sealed record PrintColumn(string Header, float Weight = 1f, bool Numeric = false, float? FixedWidth = null);
