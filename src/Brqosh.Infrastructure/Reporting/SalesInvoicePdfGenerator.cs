using Brqosh.Domain.Tax;
using Brqosh.Domain.Zatca;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>PDF — sales / purchase invoices and credit-debit notes.</summary>
public static class SalesInvoicePdfGenerator
{
    static SalesInvoicePdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    /// <summary>
    /// سطر مطبوعة. <paramref name="Category"/> إلزاميّ دلالياً وإن كانت له قيمة افتراضية:
    /// بدونه كانت المطبوعة توزّع ضريبة المستند تناسبياً على كل السطور، فيظهر على سطر
    /// صادرات صفريّ مبلغُ ضريبةٍ لم يُحصَّل قط — والمطبوعة هي المستند المسلَّم للعميل.
    /// </summary>
    public record Line(
        string Description,
        decimal Qty,
        decimal Price,
        decimal Total,
        VatCategory Category = VatCategory.Standard,
        // (جاهزيّةُ الإطلاق · ح-٤٤) ‏خصمُ السطر مبلغاً — `Qty × Price − Discount = Total`.
        decimal Discount = 0m);

    public record InvoicePrintOptions(
        PrintDocumentKind Kind = PrintDocumentKind.SimplifiedSalesInvoice,
        string? PartyLabel = null,
        string? PartyName = null,
        string? QrTlvBase64 = null,
        int? Icv = null,
        string? ComplianceStatus = null,
        // (CEEF-227) الفاتورة الضريبية تلزمها بيانات المشتري لا اسمه وحده.
        string? PartyVatNumber = null,
        string? PartyAddress = null,
        string? NotesAr = null,
        // (جاهزيّةُ الإطلاق · ح-٤٢) عدمٌ = مستندٌ بالريال.
        CurrencyDisclosure? Currency = null,
        // (جاهزيّةُ الإطلاق · ح-٤٣ · ui-ux-i18n-printing#0) ‏**الإشعارُ يُسمّي ما يُصحّحه ولماذا.**
        // ‏كان الإشعارُ الدائنُ المطبوعُ بلا رقم الفاتورة الأصليّة ولا السبب — وكلاهما في
        // ‏المستند وفي الـXML — **فلا يُطابقه مدقّقٌ بفاتورة، ولا يخصم به المشتري مدخلاتِه**.
        string? ReferenceDocumentNumber = null,
        string? ReasonAr = null,
        // (جاهزيّةُ الإطلاق · ح-٥٣ · tax-zatca#9) ‏**مستندُ تخليصٍ لم يُعتمد يقول ذلك على الورقة** —
        // ‏يُملأ من `ZatcaHandOverCopy.MarkFor` وحدَه، ومعه يغيب الرمز.
        string? ClearanceNotice = null);

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٤٢ · tax-zatca#14) <b>مستندٌ بعملةٍ أجنبيّة: رمزُها وسعرُها
    /// وإفصاحُه بالريال.</b>
    ///
    /// <para><b>وكانت المطبوعةُ تخلط عملتين في صفحةٍ واحدة</b>: السطورُ بعملة المستند
    /// والإجماليّاتُ بالريال، <b>والكلُّ موسومٌ «ر.س»</b>. وقاس المُدقِّقان: فاتورةٌ بـUSD
    /// 1,000 بسعر 3.75 تُطبع سطراً بـ1,000 تحته «3,750.00 ر.س»، <b>ورأسُ عمود الضريبة
    /// «ضريبة 56.25%»</b> — ضريبةٌ بالريال مقسومةٌ على وعاءٍ بالدولار — <b>وإجماليُّ كلّ سطرٍ
    /// وعاءٌ بالدولار مضافاً إليه ضريبةٌ بالريال</b>.</para>
    ///
    /// <para><b>والصوابُ عملةٌ واحدةٌ للسطور والإجماليّات</b> — عملةُ المستند، وبها رمزُ QR
    /// والـXML — <b>ثمّ الريالُ إفصاحاً مستقلّاً</b>: الضريبةُ بالريال إلزاميّةٌ متى خالفت
    /// العملةُ الريال (BT-111).</para>
    /// </summary>
    public sealed record CurrencyDisclosure(
        string Code, decimal ExchangeRate, decimal SubtotalSar, decimal VatSar)
    {
        public decimal TotalSar => SubtotalSar + VatSar;
    }

    /// <summary>
    /// (ح-٤٢) <b>كلُّ ما تُطبع منه الصفحة</b> — يبنيه <c>DocumentPrintService</c> من المستند،
    /// <b>ويُقاس قبل أن يُرسَم</b>: محتوى PDF لا يُقرأ في الاختبار.
    /// </summary>
    public sealed record Request(
        PrintBranding Branding,
        string DocumentNumber,
        DateOnly Date,
        IReadOnlyList<Line> Lines,
        decimal Subtotal,
        decimal Vat,
        decimal Total,
        // (جاهزيّةُ الإطلاق · ح-٤٤) ‏**خصمُ المستند — إلزاميٌّ بلا افتراضيّ، ولا يُستنتَج.**
        // ‏استنتاجُه `Σ السطور − الوعاء` كان سيُخرج قبل ح-٤٢ «خصماً» قدرُه −٢٬٧٥٠ لفاتورةٍ
        // ‏دولاريّة — **رقمٌ حسنُ الشكل مصنوعٌ من خلط عملتين**. فيُسمّيه كلُّ مُنتِج.
        decimal DocumentDiscount,
        InvoicePrintOptions Options);

    /// <summary>
    /// (ح-٤٢) <b>أرقامُ الصفحة كما تُطبع</b> — دالّةٌ صافية، <b>والرسمُ يقرأ منها وحدَها</b>.
    /// </summary>
    public sealed record PrintFigures(
        string VatColumnHeader,
        string VatRateLabel,
        IReadOnlyList<decimal> LineVats,
        IReadOnlyList<decimal> LineTotals,
        string CurrencySuffix,
        decimal Subtotal,
        decimal Vat,
        decimal Total,
        CurrencyDisclosure? Sar,
        // (ح-٤٤) ‏خصمُ كلّ سطرٍ (خصمُه + حصّتُه من خصم المستند) ووعاؤه بعدهما.
        IReadOnlyList<decimal> LineDiscounts,
        IReadOnlyList<decimal> LineTaxables,
        decimal GrossTotal,
        decimal DiscountTotal,
        decimal DocumentDiscount)
    {
        /// <summary>(ح-٤٤) يُرسَم عمودُ الخصم وصفوفُه متى وُجد خصمٌ — وإلّا فالمطبوعةُ كما كانت.</summary>
        public bool HasDiscount => DiscountTotal != 0m;
    }

    /// <summary>
    /// (ح-٤٢) أرقامُ الصفحة من الطلب. <b>والسطورُ والإجماليّاتُ بعملةٍ واحدة</b> — فتوزيعُ
    /// الضريبة على السطور ونسبةُ الرأس صحيحان بلا استثناء.
    /// </summary>
    public static PrintFigures Figures(Request r)
    {
        var lines = r.Lines;
        // النسبة محسوبة من الأرقام لا مثبّتة بـ«15%»: فاتورة صفرية أو معفاة أو بنسبة
        // خليجية مختلفة كانت تُطبع بنسبة كاذبة.
        //
        // وتُحسب من **الوعاء الخاضع وحده** لا من إجمالي المستند: فاتورة فيها 1,000
        // أساسيّ و500 صفريّ بضريبة 150.00 كانت تُخرِج 150/1500 = «10%» — نسبةٌ لا وجود
        // لها في أي نظام ضريبيّ، تُطبع في رأس عمود الضريبة على مستند يراه المدقّق.
        var mixed = lines.Select(l => l.Category).Distinct().Count() > 1;

        // (جاهزيّةُ الإطلاق · ح-٤٤ · ui-ux-i18n-printing#2) ‏**خصمُ المستند يُوزَّع على السطور
        // ‏للعرض**، فكلُّ عمودٍ يجمع إلى إجماليّه: `الكمّيّة × السعر − الخصم = الخاضع`،
        // ‏و`Σ الخاضع = الوعاء`، و`Σ الضريبة = الضريبة`، و`Σ الإجمالي = الإجمالي`. **وكان
        // ‏عمودُ الإجمالي يزيد على الإجماليّ المطبوع بقدر خصم المستند، والخصمُ لا يظهر**.
        var shares = AllocateDocumentDiscount(lines, r.DocumentDiscount);
        var taxables = lines.Select((l, i) => l.Total - shares[i]).ToList();
        var lineDiscounts = lines.Select((l, i) => l.Discount + shares[i]).ToList();

        // ‏(ح-٤٤) **والنسبةُ من الوعاء الذي حُسبت عليه الضريبة — بعد خصم المستند.** وكانت
        // ‏تُقسَم على الوعاء قبله فتُطبع «14.25%» لفاتورةٍ بخمسة عشر.
        var standardBase = lines.Select((l, i) => (l, i))
            .Where(x => x.l.Category == VatCategory.Standard).Sum(x => taxables[x.i]);
        var vatRateLabel = PrintText.Ltr(standardBase == 0m
            ? "%"
            : (Math.Round(r.Vat / standardBase * 100m, 2, MidpointRounding.AwayFromZero)).ToString("0.##") + "%");
        // مستند مختلط الفئات: لا نسبةَ واحدة تصفه، فتُعلن الفئة على كل سطر بدلاً من الرأس.
        var vatColumnHeader = mixed ? "الضريبة" : $"ضريبة {vatRateLabel}";

        var lineVats = AllocateLineVat(
            [.. lines.Select((l, i) => l with { Total = taxables[i] })], r.Subtotal, r.Vat);
        var lineTotals = taxables.Select((taxable, i) => taxable + lineVats[i]).ToList();

        var currency = r.Options.Currency;
        return new PrintFigures(
            vatColumnHeader, vatRateLabel, lineVats, lineTotals,
            currency is null ? "ر.س" : currency.Code,
            r.Subtotal, r.Vat, r.Total, currency,
            lineDiscounts, taxables,
            GrossTotal: lines.Sum(l => l.Total + l.Discount),
            DiscountTotal: lineDiscounts.Sum(),
            DocumentDiscount: r.DocumentDiscount);
    }

    /// <summary>
    /// (ح-٤٤) حصّةُ كلّ سطرٍ من خصم المستند — بنسبة صافيه، <b>والباقي على أكبر سطر</b>
    /// (مذهبُ <see cref="AllocateLineVat"/> نفسُه): فمجموعُ الحصص يساوي الخصمَ حرفاً.
    /// </summary>
    internal static IReadOnlyList<decimal> AllocateDocumentDiscount(IReadOnlyList<Line> lines, decimal discount)
    {
        var shares = new decimal[lines.Count];
        var net = lines.Sum(l => l.Total);
        if (discount == 0m || lines.Count == 0 || net == 0m)
            return shares;

        for (var i = 0; i < lines.Count; i++)
            shares[i] = Math.Round(discount * lines[i].Total / net, 2, MidpointRounding.AwayFromZero);

        var largest = 0;
        for (var i = 1; i < lines.Count; i++)
            if (Math.Abs(lines[i].Total) > Math.Abs(lines[largest].Total))
                largest = i;
        shares[largest] += discount - shares.Sum();
        return shares;
    }

    /// <summary>(ح-٤٢) يرسم الطلبَ — من <see cref="Figures"/> وحدَها.</summary>
    public static byte[] Generate(Request r) => Render(r, Figures(r));

    public static byte[] Generate(
        string companyName, string vatNumber, string invoiceNumber, DateOnly date,
        IReadOnlyList<Line> lines, decimal subtotal, decimal vat, decimal total,
        string? qrBase64 = null) =>
        Generate(
            PrintBranding.From(companyName, vatNumber),
            invoiceNumber,
            date,
            lines,
            subtotal,
            vat,
            total,
            new InvoicePrintOptions(QrTlvBase64: qrBase64));

    public static byte[] Generate(
        PrintBranding branding,
        string documentNumber,
        DateOnly date,
        IReadOnlyList<Line> lines,
        decimal subtotal,
        decimal vat,
        decimal total,
        InvoicePrintOptions? options = null)
    {
        // ‏(ح-٤٤) المدخلُ القديمُ بلا خصمِ مستند — طباعاتُ المستندات الخمسة تمرّ من `Request`.
        var request = new Request(branding, documentNumber, date, lines, subtotal, vat, total, 0m,
            options ?? new InvoicePrintOptions());
        return Render(request, Figures(request));
    }

    /// <summary>(ح-٤٢ · ح-٤٤) يرسم الصفحةَ من أرقامها — <b>ولا رقمَ يُحسب هنا</b>.</summary>
    private static byte[] Render(Request request, PrintFigures figures)
    {
        var (branding, documentNumber, date, lines, _, _, _, _, options) = request;
        var title = TitleFor(options.Kind);
        var partyLabel = options.PartyLabel ?? PartyLabelFor(options.Kind);
        var qrPng = ZatcaQrImageHelper.TryCreatePng(options.QrTlvBase64);
        var compliance = BuildComplianceLine(options);

        // (ح-٤٢) ‏**الأرقامُ من `Figures` وحدَها** — فما يُقاس هو ما يُرسَم.
        var mixed = lines.Select(l => l.Category).Distinct().Count() > 1;
        var vatColumnHeader = figures.VatColumnHeader;
        var vatRateLabel = figures.VatRateLabel;
        var documentRows = new List<(string, string)>
        {
            ("رقم المستند", PrintText.Code(documentNumber)),
            ("التاريخ", PrintText.Date(date)),
            ("نوع المستند", title),
        };
        if (figures.Sar is { } fx)
            documentRows.Add(("العملة", PrintText.Code(fx.Code)));
        // ‏(ح-٤٣) المستندُ الأصليُّ والسببُ — للإشعارات ومستندات الإلغاء.
        if (!string.IsNullOrWhiteSpace(options.ReferenceDocumentNumber))
            documentRows.Add(("المستند الأصليّ", PrintText.Code(options.ReferenceDocumentNumber)));
        if (!string.IsNullOrWhiteSpace(options.ReasonAr))
            documentRows.Add(("السبب", options.ReasonAr));

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                // A4 لا A5: الفاتورة الضريبية تحمل جدول بنود بستّة أعمدة وكتلتَي
                // بائع/مشتري — والمقاس الأصغر كان يضغطها ويترك ثلثَ الورقة فارغاً.
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle(10));

                page.Header().Element(h => branding.RenderHeader(h, title));

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);

                    // ‏(ح-٥٣) **التنبيهُ أوّلَ الصفحة** — قبل أيّ رقمٍ يُقرأ.
                    if (!string.IsNullOrWhiteSpace(options.ClearanceNotice))
                        col.Item().Element(c => ClearanceNoticeBox(c, options.ClearanceNotice));

                    // ── كتلتان متجاورتان: بيانات المستند · بيانات الطرف ──
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c => LabelBox(c, "بيانات المستند", documentRows));

                        row.ConstantItem(12);

                        row.RelativeItem().Element(c => LabelBox(c, $"بيانات {partyLabel}",
                        [
                            ("الاسم", string.IsNullOrWhiteSpace(options.PartyName) ? "—" : options.PartyName),
                            ("الرقم الضريبي", string.IsNullOrWhiteSpace(options.PartyVatNumber)
                                ? "—" : PrintText.Code(options.PartyVatNumber)),
                            ("العنوان", string.IsNullOrWhiteSpace(options.PartyAddress) ? "—" : options.PartyAddress),
                        ]));
                    });

                    // ── جدول البنود: حدود كاملة، رأس مظلَّل، تناوب في الصفوف ──
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(26);      // #
                            c.RelativeColumn(4);       // الصنف
                            c.RelativeColumn(1.1f);    // الكمية
                            c.RelativeColumn(1.4f);    // السعر
                            if (figures.HasDiscount)
                                c.RelativeColumn(1.3f);  // الخصم (ح-٤٤)
                            c.RelativeColumn(1.6f);    // الخاضع للضريبة
                            c.RelativeColumn(1.4f);    // الضريبة
                            c.RelativeColumn(1.6f);    // الإجمالي
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h.Cell(), "#");
                            HeaderCell(h.Cell(), "الصنف");
                            HeaderCell(h.Cell(), "الكمية");
                            HeaderCell(h.Cell(), "السعر");
                            if (figures.HasDiscount)
                                HeaderCell(h.Cell(), "الخصم");
                            HeaderCell(h.Cell(), "الخاضع للضريبة");
                            HeaderCell(h.Cell(), vatColumnHeader);
                            HeaderCell(h.Cell(), "الإجمالي");
                        });

                        var index = 0;
                        foreach (var line in lines)
                        {
                            var lineVat = figures.LineVats[index];
                            var lineTotal = figures.LineTotals[index];
                            var lineDiscount = figures.LineDiscounts[index];
                            var lineTaxable = figures.LineTaxables[index];
                            index++;
                            var shade = index % 2 == 0;
                            // على مستند مختلط الفئات تُعلَن فئة كل سطر بجوار مبلغ ضريبته —
                            // الفاتورة الضريبية تلزمها فئة ونسبة لكل بند، وسطرٌ بـ«0.00» بلا
                            // بيان سببه يقرؤه المدقّق سهواً لا إعفاءً.
                            var vatCell = mixed
                                ? $"{lineVat:N2} · {line.Category.NameAr()}"
                                : $"{lineVat:N2}";

                            BodyCell(table.Cell(), index.ToString(), shade, center: true);
                            BodyCell(table.Cell(), line.Description, shade);
                            BodyCell(table.Cell(), $"{line.Qty:N2}", shade, center: true);
                            BodyCell(table.Cell(), $"{line.Price:N2}", shade, center: true);
                            // ‏(ح-٤٤) **الخصمُ يُطبع**، والخاضعُ بعده — `الكمّيّة × السعر − الخصم`.
                            if (figures.HasDiscount)
                                BodyCell(table.Cell(), $"{lineDiscount:N2}", shade, center: true);
                            BodyCell(table.Cell(), $"{lineTaxable:N2}", shade, center: true);
                            BodyCell(table.Cell(), vatCell, shade, center: true);
                            BodyCell(table.Cell(), $"{lineTotal:N2}", shade, center: true);
                        }
                    });

                    // ── الإجماليات في صندوق محاذٍ لليسار + رمز التحقق مقابله ──
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(inner =>
                        {
                            if (qrPng is not null)
                            {
                                inner.Item().Row(qr =>
                                {
                                    qr.ConstantItem(104).Height(104).Image(qrPng);
                                    qr.RelativeItem().PaddingRight(8).Column(t =>
                                    {
                                        t.Item().Text("رمز التحقق (ZATCA)").Bold().FontSize(9);
                                        t.Item().Text("امسح الرمز للتحقق من صحة الفاتورة").FontSize(8);
                                        if (!string.IsNullOrWhiteSpace(compliance))
                                            t.Item().PaddingTop(2).Text(compliance).FontSize(8);
                                    });
                                });
                            }
                            else if (!string.IsNullOrWhiteSpace(options.QrTlvBase64))
                            {
                                inner.Item().Text("تعذّر توليد رمز QR — راجع بيانات ZATCA").FontSize(8);
                            }
                        });

                        row.ConstantItem(12);

                        row.ConstantItem(220).AlignTop().Column(side =>
                        {
                            side.Spacing(6);
                            // ‏(ح-٤٤) **والخصمُ مُفصَحٌ عنه قبل الوعاء** — ومنه خصمُ المستند.
                            if (figures.HasDiscount)
                                side.Item().Element(c => DiscountBox(c, figures));
                            side.Item().Element(c => TotalsBox(c, figures.Subtotal, figures.Vat, figures.Total,
                                vatRateLabel, figures.CurrencySuffix));
                            // ‏(ح-٤٢) **والريالُ إفصاحٌ مستقلٌّ لا عملةٌ ثانيةٌ في الجدول.**
                            if (figures.Sar is { } sar)
                                side.Item().Element(c => SarDisclosureBox(c, sar));
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(options.NotesAr))
                        col.Item().Element(c => LabelBox(c, "ملاحظات", [("", options.NotesAr!)]));
                });

                page.Footer().Column(f =>
                {
                    f.Item().Element(x => branding.RenderFooterMeta(x, null));
                    f.Item().AlignCenter().Text(t =>
                    {
                        t.Span("صفحة ").FontSize(7);
                        t.CurrentPageNumber().FontSize(7);
                        t.Span(" من ").FontSize(7);
                        t.TotalPages().FontSize(7);
                    });
                });
            });
        }).GeneratePdf();
    }



    /// <summary>
    /// (CEEF-227) توزيع ضريبة الفاتورة على البنود بحصّة كلّ بند من الوعاء.
    ///
    /// **التقريب لكل سطر وحده لا يكفي:** ثلاثة بنود متساوية من 100 بضريبة 15.00
    /// تُعطي 5.00×3 = 15.00، لكن بنوداً بكسور تُعطي مجموعاً يخالف ذيل الفاتورة
    /// بهللة — وعمودٌ لا يجمع إلى الإجمالي المطبوع أسفله يُسقط ثقة المدقّق فوراً.
    /// لذلك يُحمَّل الفرق المتبقّي على **أكبر بند**: أقلّ أثر نسبيّ، ومجموعٌ مطابق دائماً.
    /// </summary>
    internal static IReadOnlyList<decimal> AllocateLineVat(
        IReadOnlyList<Line> lines, decimal subtotal, decimal vat)
    {
        var allocated = new decimal[lines.Count];
        if (lines.Count == 0)
            return allocated;

        // التوزيع يقع على **الوعاء الخاضع** لا على إجمالي المستند: البند الصفريّ أو
        // المعفى أو الخارج عن النطاق ضريبتُه صفر بحكم الفئة، لا حصةٌ نسبية من ضريبةٍ
        // حُسبت على بنود أخرى. التوزيع التناسبي الأعمى كان يطبع على سطر صادرات صفريّ
        // مبلغَ ضريبةٍ لم يُحصَّل قط — على المستند المسلَّم للعميل.
        var bearers = new List<int>();
        for (var i = 0; i < lines.Count; i++)
            if (lines[i].Category == VatCategory.Standard)
                bearers.Add(i);

        // احتياطٌ لا يُسقِط ريالاً: مستندٌ بضريبة موجبة وبلا سطر خاضع واحد يعني تعارضاً
        // بين البيانات والفئات. يُوزَّع حينها على كل السطور حتى يبقى العمود مطابقاً
        // للذيل — فعمودٌ لا يجمع إلى الإجمالي أسوأ من عمودٍ موزَّع بتحفّظ.
        var baseAmount = bearers.Sum(i => lines[i].Total);
        if (bearers.Count == 0 || baseAmount == 0m)
        {
            if (vat == 0m || subtotal == 0m)
                return allocated;
            bearers = [.. Enumerable.Range(0, lines.Count)];
            baseAmount = subtotal;
        }

        foreach (var i in bearers)
            allocated[i] = Math.Round(vat * (lines[i].Total / baseAmount), 2, MidpointRounding.AwayFromZero);

        // الفرق يُحمَّل على أكبر بند **خاضع** (أقلّ أثر نسبيّ) — لا على أكبر بند مطلقاً،
        // وإلا عاد الكسرُ إلى سطرٍ معفى فانتقض الإصلاح من حيث أُريد له أن يُحكم.
        var residual = vat - allocated.Sum();
        if (residual != 0m)
        {
            var largest = bearers[0];
            foreach (var i in bearers)
                if (Math.Abs(lines[i].Total) > Math.Abs(lines[largest].Total))
                    largest = i;
            allocated[largest] += residual;
        }

        return allocated;
    }

    // ── لبنات التخطيط ──────────────────────────────────────────────────────

    private const string BorderColour = "#94A3B8";
    private const string HeaderFill = "#E2E8F0";
    private const string RowFill = "#F5F7FA";
    private const string BoxTitleFill = "#EDF1F6";
    private const string NoticeColour = "#B91C1C";
    private const string NoticeFill = "#FEF2F2";

    /// <summary>صندوق معنون بصفوف «تسمية: قيمة» — لبيانات المستند والطرف.</summary>
    private static void LabelBox(IContainer container, string title, IReadOnlyList<(string Label, string Value)> rows)
    {
        container.Border(0.8f).BorderColor(BorderColour).Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                col.Item().Background(BoxTitleFill).PaddingVertical(4).PaddingHorizontal(8)
                    .Text(title).Bold().FontSize(9.5f);
            }

            foreach (var (label, value) in rows)
            {
                col.Item().PaddingVertical(3).PaddingHorizontal(8).Row(row =>
                {
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        row.ConstantItem(84).Text(label).FontSize(9).FontColor("#475569");
                        row.RelativeItem().Text(value).FontSize(9.5f);
                    }
                    else
                    {
                        row.RelativeItem().Text(value).FontSize(9.5f);
                    }
                });
            }
        });
    }

    private static void HeaderCell(IContainer cell, string text) =>
        cell.Background(HeaderFill).Border(0.8f).BorderColor(BorderColour)
            .PaddingVertical(5).PaddingHorizontal(4)
            .AlignCenter().Text(text).Bold().FontSize(9);

    private static void BodyCell(IContainer cell, string text, bool shaded, bool center = false)
    {
        var box = cell.Border(0.5f).BorderColor(BorderColour).PaddingVertical(4).PaddingHorizontal(4);
        if (shaded)
            box = box.Background(RowFill);
        var slot = center ? box.AlignCenter() : box;
        slot.Text(text).FontSize(9);
    }

    /// <summary>صندوق الإجماليات — السطر الأخير مميّز لأنه المبلغ المستحق.</summary>
    private static void TotalsBox(IContainer container, decimal subtotal, decimal vat, decimal total,
        string vatRateLabel, string suffix)
    {
        container.Border(0.8f).BorderColor(BorderColour).Column(col =>
        {
            TotalRow(col, "الإجمالي قبل الضريبة", subtotal, false, suffix, first: true);
            TotalRow(col, $"ضريبة القيمة المضافة {vatRateLabel}", vat, false, suffix);
            TotalRow(col, "الإجمالي شامل الضريبة", total, true, suffix);
        });
    }

    /// <summary>
    /// (ح-٤٤) <b>الخصمُ مُفصَحاً</b>: الإجماليُّ قبله، ثمّ الخصمُ — ومنه خصمُ المستند متى وُجد.
    /// <b>ويجمع إلى «الإجمالي قبل الضريبة» تحته</b>.
    /// </summary>
    /// <summary>
    /// (ح-٥٣) <b>مستندُ تخليصٍ لم تعتمده الهيئة</b> — بإطارٍ أحمرَ لا يُخطئه قارئ، فلا تُسلَّم
    /// الورقةُ على أنّها فاتورةٌ نافذة.
    /// </summary>
    internal static void ClearanceNoticeBox(IContainer container, string notice) =>
        container.Border(1.5f).BorderColor(NoticeColour).Background(NoticeFill)
            .PaddingVertical(6).PaddingHorizontal(10)
            .Text(notice).Bold().FontSize(10.5f).FontColor(NoticeColour);

    private static void DiscountBox(IContainer container, PrintFigures f)
    {
        container.Border(0.8f).BorderColor(BorderColour).Column(col =>
        {
            TotalRow(col, "الإجمالي قبل الخصم", f.GrossTotal, false, f.CurrencySuffix, first: true);
            TotalRow(col, "الخصم", -f.DiscountTotal, false, f.CurrencySuffix);
            if (f.DocumentDiscount != 0m)
                TotalRow(col, "منه خصمٌ على المستند", -f.DocumentDiscount, false, f.CurrencySuffix);
        });
    }

    /// <summary>
    /// (ح-٤٢) <b>الإفصاحُ بالريال</b> لمستندٍ بعملةٍ أجنبيّة — سعرُ الصرف ثمّ الأرقامُ الثلاثة
    /// كما في الدفاتر. <b>والضريبةُ بالريال منها إلزاميّة</b> (BT-111).
    /// </summary>
    private static void SarDisclosureBox(IContainer container, CurrencyDisclosure sar)
    {
        container.Border(0.8f).BorderColor(BorderColour).Column(col =>
        {
            col.Item().Background(BoxTitleFill).PaddingVertical(4).PaddingHorizontal(8)
                .Text(PrintText.Ltr($"بالريال السعودي — 1 {sar.Code} = {sar.ExchangeRate:0.######} ر.س"))
                .Bold().FontSize(9);
            TotalRow(col, "الإجمالي قبل الضريبة", sar.SubtotalSar, false, "ر.س", first: true);
            TotalRow(col, "ضريبة القيمة المضافة", sar.VatSar, false, "ر.س");
            TotalRow(col, "الإجمالي شامل الضريبة", sar.TotalSar, false, "ر.س");
        });
    }

    private static void TotalRow(ColumnDescriptor col, string label, decimal value, bool emphasis,
        string suffix, bool first = false)
    {
        var item = col.Item();
        if (emphasis)
            item = item.Background(HeaderFill);
        if (!first)
            item = item.BorderTop(0.5f).BorderColor(BorderColour);

        item.PaddingVertical(5).PaddingHorizontal(8).Row(row =>
        {
            var labelSpan = row.RelativeItem().Text(label).FontSize(emphasis ? 10 : 9);
            var valueSpan = row.ConstantItem(96).AlignLeft()
                .Text($"{value:N2} {suffix}").FontSize(emphasis ? 11 : 9.5f);
            if (emphasis)
            {
                labelSpan.Bold();
                valueSpan.Bold();
            }
        });
    }

    private static string TitleFor(PrintDocumentKind kind) => kind switch
    {
        PrintDocumentKind.StandardSalesInvoice => "فاتورة ضريبية",
        PrintDocumentKind.PurchaseInvoice => "فاتورة مشتريات",
        PrintDocumentKind.CreditNote => "إشعار دائن",
        PrintDocumentKind.PurchaseDebitNote => "إشعار مدين مشتريات",
        PrintDocumentKind.SalesDebitNote => "إشعار مدين",
        _ => "فاتورة ضريبية مبسّطة",
    };

    private static string PartyLabelFor(PrintDocumentKind kind) => kind switch
    {
        PrintDocumentKind.PurchaseInvoice or PrintDocumentKind.PurchaseDebitNote => "المورد",
        _ => "العميل",
    };

    // (CEEF-229 · م0-١٠ · ح-٥٤) ‏الحالةُ بالعربيّة والمحاكاةُ مُعلَنة — **من `PrintedCompliance` وحدَه**،
    // ‏فلا يفترق سطرُ الفاتورة عن سطر المستخلص بتعديلٍ في أحدهما.
    private static string? BuildComplianceLine(InvoicePrintOptions options) =>
        PrintedCompliance.Line(options.Icv, options.ComplianceStatus);
}
