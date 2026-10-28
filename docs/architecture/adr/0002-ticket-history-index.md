# ADR 0002 — فهرس سرد التذاكر المنقول من تعليقات الكود

أُزيلت أرقامُ التذاكر من تعليقات الكود (يبقى نصُّ «لماذا»)، وحُفظ أثرُها هنا: الملف، العضو الذي يسبقه التعليق، التذكرة، وأوّل التعليق.

| الملف | العضو | التذكرة | التعليق |
|---|---|---|---|
| VatReturnQuery.cs | public record VatReturnBucket(Brqosh.Domain.Tax.VatCategory Category, decimal TaxableAmoun | O-21، CEEF-219 | الإقرار يعكس وقائع الفترة لا حالة المستند اليوم. |
| VatReturnQuery.cs | public record VatReturnBucket(Brqosh.Domain.Tax.VatCategory Category, decimal TaxableAmoun | CEEF-229 · A-15 | وعاءٌ ضريبيّ واحد في الإقرار: فئةٌ ومبلغٌ خاضع وضريبة. |
| VatReturnQuery.cs | public record VatReturnBoxLine(int Box, decimal Amount, decimal Adjustments, decimal VatAm | جاهزيّةُ الإطلاق · ح-١٦٥ · <c>tax-zatca#12</c> | خانةٌ واحدةٌ من نموذج الإقرار بأعمدته في البوّابة: |
| VatReturnQuery.cs | decimal SalesBaseNet, | جاهزيّةُ الإطلاق · ح-٤١ | ‏الأبُ الذي تُعرض الأوعيةُ تحته — صافي الوعاء من إجماليّات |
| VatReturnQuery.cs | decimal CancelledOutputVat = 0m, | CEEF-219 | تسويات إلغاء مستندات صدرت في فترات سابقة وأُلغيت داخل هذه الفترة . |
| VatReturnQuery.cs | decimal JournalOutputVat = 0m, | CEEF-232 · ض-١ | ضريبةٌ قُيّدت في الأستاذ بقيدٍ ليس مستنداً محسوباً ولا تسوية. |
| VatReturnQuery.cs | IReadOnlyList<VatReturnBoxLine>? SalesBoxes = null, | جاهزيّةُ الإطلاق · ح-١٦٥ | ‏خاناتُ النموذج — من الأوعية نفسِها مقسومةً برمز الإعفاء؛ انظر VatReturnBox. |
| VatReturnQuery.cs | decimal ReverseChargeVat = 0m) | جاهزيّةُ الإطلاق · tax-zatca#12 | ‏ضريبةُ الاحتساب العكسيّ — مخرجاتٌ هنا ومدخلاتٌ في DeductibleInputVat معاً (الخانة 9). |
| VatReturnQuery.cs | public IReadOnlyList<VatReturnBucket> Sales => SalesByCategory ?? []; | CEEF-229 · A-15 | المبيعات مفكَّكة حسب الفئة الضريبية — صافيةً من الإشعارات الدائنة. |
| VatReturnQuery.cs | public IReadOnlyList<VatReturnBucket> Sales => SalesByCategory ?? []; | جاهزيّةُ الإطلاق · ح-٤١ |  وصارت بالريال، بعد خصم المستند، وتشمل كلَّ ما في |
| VatReturnQuery.cs | public decimal NetOutputVat => Brqosh.Application.Reporting.VatReturnNet.Output( | CEEF-232 · ض-١ | ضريبةُ المخرجات — من المستندات ومن الأستاذ معاً. |
| VatReturnQuery.cs | public decimal NetOutputVat => Brqosh.Application.Reporting.VatReturnNet.Output( | جاهزيّةُ الإطلاق · ح-٤٠ |  والمعادلةُ في VatReturnNet لا هنا — |
| VatReturnQuery.cs | public decimal NetDeductibleInputVat => Brqosh.Application.Reporting.VatReturnNet.Input( | CEEF-232 · ض-١ | والمدخلاتُ كذلك — والاتجاهُ هنا في صالح المنشأة: عماها عنه |
| VatReturnQuery.cs | public decimal? GlVariance => | CEEF-229 · م2-١٦ | غيابُ البيان ليس صفراً. |
| VatReturnQuery.cs | var cancelledSales = await db.SalesInvoices | CEEF-219 | الشقّ الثاني: مستندات صدرت قبل هذه الفترة وأُلغيت داخلها. |
| VatReturnQuery.cs | var reverseChargeVat = | جاهزيّةُ الإطلاق · tax-zatca#12 | ‏الاحتسابُ العكسيُّ مخرجاتٌ ومدخلاتٌ معاً (دليلُ الاستيراد §6.3): يُضاف إلى صافي |
| VatReturnQuery.cs | var salesByCategory = SumByCategory( | CEEF-229 · A-15 | التفكيك بالفئة — من السطور المحمَّلة أصلاً، بلا استعلام إضافيّ. |
| VatReturnQuery.cs | var salesByCategory = SumByCategory( | جاهزيّةُ الإطلاق · ح-٤١ · tax-zatca#12 | ‏لكلّ مستندٍ أوعيتُه بخصمه وسعره — ثمّ |
| VatReturnQuery.cs | var depositParts = await DepositBoxPartsAsync(companyId, from, to); | جاهزيّةُ الإطلاق · ح-١٦٥ · tax-zatca#12 | ‏ ‏وخاناتُ النموذج من الأوعية نفسِها — كلُّ وعاءٍ يُقسم |
| VatReturnQuery.cs | private static decimal TaxBaseOf(PurchaseInvoice p) => | جاهزيّةُ الإطلاق · <c>tax-zatca#12</c> | وعاءُ فاتورة الشراء في الإقرار — صافيها، ولمستند التخليص قيمتُه الجمركيّة: |
| VatReturnQuery.cs | private async Task<IReadOnlyList<BoxPart>> DepositBoxPartsAsync(long companyId, DateOnly f | جاهزيّةُ الإطلاق · <c>sector-modules#6</c> | العرابينُ في الخانة 1 — لها فاتورةُ دفعةٍ مقدّمة (386): القبضُ «مبلغ» |
| VatReturnQuery.cs | private static IEnumerable<BoxPart> DocumentBoxParts( | جاهزيّةُ الإطلاق · ح-١٦٥ | أوعيةُ المستند مقسومةً على خاناتها. |
| VatReturnQuery.cs | private static IEnumerable<VatReturnBucket> DocumentBuckets( | جاهزيّةُ الإطلاق · ح-٤١ | أوعيةُ مستندٍ واحدٍ بالريال — كما يحسبها المستندُ لنفسه. |
| VatReturnQuery.cs | private static readonly string[] CountedDocumentPrefixes = | CEEF-232 · ض-١ | بادئاتُ المستندات الخمسة التي يقرؤها الإقرارُ أصلاً. |
| VatReturnQuery.cs | private async Task<(decimal OutputVat, decimal InputVat)> ReadJournalOnlyVatAsync( | CEEF-232 · ض-١ | الضريبةُ التي قُيّدت في الأستاذ ولم يحملها مستندٌ محسوب. |
| VatReturnQuery.cs | .WhereStanding(db.JournalEntries) | CEEF-232 · ض-٣ | ‏وزوجُ العكس يُستبعد بطرفيه معاً لا يُترك ليتقاصّ. |
| VatReturnQuery.cs | var entryOutput = 0m; | CEEF-232 · ض-٣ | ‏الحركةُ تُجمع على مستوى القيد ثمّ يُحكم عليها — لا سطراً سطراً. |
| VatReturnQuery.cs | if (entryOutput > 0m) outputVat += entryOutput; | CEEF-232 · ض-٣ | ‏والنقصُ لا يُقرأ توريداً سالباً — بل تسوية. |
| VatReturnQuery.cs | else if (entryOutput < 0m && entry.DepositVatOffset) outputVat += entryOutput; | جاهزيّةُ الإطلاق · ح-٨١ | ‏ ‏ونقصٌ ثانٍ معلومٌ غيرُ السداد: ردُّ ضريبة العربون عند إنزاله. |
| ZatcaEInvoiceGenerator.cs | invoice.ForeignSubtotal, invoice.ForeignVatAmount, invoice.ForeignGrandTotal, | CEEF-230 · ط1-٤ | ‏المستندُ يخرج بعملته: الوعاءُ والضريبةُ والإجماليُّ |
| ZatcaEInvoiceGenerator.cs | private async Task<IReadOnlyList<ZatcaUblInvoiceBuilder.PrepaymentReference>?> Prepayments | جاهزيّةُ الإطلاق · <c>sector-modules#6</c> | فواتيرُ الدفعات المقدّمة (386) التي تُسوّيها فاتورةُ المغادرة — عرابينُ |
| ZatcaEInvoiceGenerator.cs | public async Task<ZatcaEInvoiceDocument?> TryGenerateForPrepaymentAsync(long depositEntryI | جاهزيّةُ الإطلاق · <c>sector-modules#6</c> | فاتورةُ الدفعة المقدّمة (386) عن عربون حجز — يومَ قبضه، فهو نقطةُ |
| ZatcaEInvoiceGenerator.cs | note.ForeignSubtotal, note.ForeignVatAmount, note.ForeignGrandTotal, | جاهزيّةُ الإطلاق · ح-٣٥ | ‏والإشعارُ يخرج بعملتِه كفاتورتِه. |
| ZatcaEInvoiceGenerator.cs | public async Task<ZatcaEInvoiceDocument?> TryGenerateForAssetDisposalAsync(long fixedAsset | جاهزيّةُ الإطلاق · ح-١٦ | فاتورةُ ضريبةٍ عن بيع أصلٍ ثابت. |
| ZatcaEInvoiceGenerator.cs | billing.TaxableAmount, billing.VatAmount, billing.TotalAmount, lines, mode, | جاهزيّةُ الإطلاق · ح-٣٦ | ‏وسببُ الإلغاء يُكتب كما يُكتب |
| ZatcaEInvoiceGenerator.cs | public async Task<ZatcaEInvoiceDocument?> TryGenerateRefundForPrepaymentAsync(long deposit | جاهزيّةُ الإطلاق · <c>sector-modules#6</c> | إشعارٌ دائنٌ بردّ عربون حجزٍ أُلغي — يُشير إلى فاتورة دفعته (386) |
| ZatcaEInvoiceGenerator.cs | public async Task<ZatcaEInvoiceDocument?> TryGenerateCancellationForAssetDisposalAsync( | جاهزيّةُ الإطلاق · ح-١٦١ | ‏ إشعارٌ دائنٌ عند عكس استبعاد أصلٍ بيع بفاتورة — بمبالغ الفاتورة نفسِها ومشتريها، ومرجعُه هي. |
| ZatcaEInvoiceGenerator.cs | note.ForeignSubtotal, note.ForeignVatAmount, note.ForeignGrandTotal, note.Lines, mode, | جاهزيّةُ الإطلاق · ح-٣٦ | ‏ومستندُ الإلغاء يحمل ما يحملُه |
| ZatcaEInvoiceGenerator.cs | if (await db.ZatcaEInvoiceDocuments.AnyAsync(d => | جاهزيّةُ الإطلاق · tax-zatca#8 | ‏ ‏فاتورةٌ قياسيّةٌ رفضت الهيئةُ اعتمادَها لم تصدر نظاماً — فلا إشعارَ دائناً يُرسل عنها؛ |
| ZatcaEInvoiceGenerator.cs | invoice.ForeignSubtotal, invoice.ForeignVatAmount, invoice.ForeignGrandTotal, | جاهزيّةُ الإطلاق · ح-٣٦ | ‏ومستندُ الإلغاء يحمل ما يحملُه |
| ZatcaEInvoiceGenerator.cs | private async Task<IReadOnlyList<string>> ResolveUnitCodesAsync( | CEEF-230 · ط1-١ | ‏يحلّ رمزَ UN/ECE لكلّ سطر. |
| ZatcaEInvoiceGenerator.cs | var gaps = company.RegulatoryGaps(); | CEEF-229 · م2-١٣أ | هويةُ البائع تُفحص قبل استهلاك أيّ عدّاد. |
| ZatcaEInvoiceGenerator.cs | var zatcaSettings = await GetSettingsAsync(companyId); | CEEF-229 · م2-١٣ب | بلا شهادةٍ لا يُصدَر مستند — لا يُصدَر غيرَ موقَّع بصمت. |
| ZatcaEInvoiceGenerator.cs | if (zatcaSettings is not null && !ZatcaXmlSigner.CanSign(zatcaSettings) | جاهزيّةُ الإطلاق · tax-zatca#16 | ‏ ‏وعلى جهازٍ غيرِ جهاز الختم فالسببُ الجهازُ لا الشهادة — والرسالةُ القديمةُ تدعو إلى |
| ZatcaEInvoiceGenerator.cs | var buyerGaps = customer.StandardInvoiceGaps(); | CEEF-229 · م1-١ب | ومشترٍ ناقصُ الهوية لا تُصدَر له فاتورةٌ قياسية. |
| ZatcaEInvoiceGenerator.cs | var exemptionGaps = VatExemptionGaps.For(lines); | CEEF-229 · م1-١ب/الشقّ الثاني | وسطرٌ غيرُ خاضعٍ للنسبة الأساسية يقول سببَه. |
| ZatcaEInvoiceGenerator.cs | await AcquireIcvLockAsync(companyId, CancellationToken.None); | CEEF-229 · Z-4 | قفل استشاريّ قبل قراءة العدّاد — النمط نفسه في JournalEntryNumberGenerator. |
| ZatcaEInvoiceGenerator.cs | var generatedAtUtc = DateTime.UtcNow; | CEEF-229 · م2-٢١ | لحظةُ الإصدار حقيقيةٌ، والمستندُ ورمزُه يقولانها معاً. |
| ZatcaEInvoiceGenerator.cs | Buyer: customer.SupportsStandardTaxInvoice | CEEF-229 · م1-١ب | هويةُ المشتري للقياسية وحدها — والمبسّطة بلا مشترٍ معرَّف. |
| ZatcaEInvoiceGenerator.cs | DocumentDiscount: documentDiscount, | CEEF-230 · ط1-٢ | ‏خصمُ المستند — يخرج AllowanceCharge لكلّ وعاءٍ ضريبيّ. |
| ZatcaEInvoiceGenerator.cs | LineUnitCodes: await ResolveUnitCodesAsync(companyId, lines), | CEEF-230 · ط1-١ | ‏رمزُ الوحدة لكلّ سطر — من وحدة السطر إن ذُكرت، وإلا من |
| ZatcaEInvoiceGenerator.cs | CurrencyCode: currencyCode, | CEEF-230 · ط1-٤ | ‏عملةُ المستند وسعرُها، والضريبةُ بالريال للإفصاح BT-111. |
| ZatcaEInvoiceGenerator.cs | var doc = ZatcaEInvoiceDocument.Create( | CEEF-233 · ظ-١ | ‏ واللحظةُ المقروءةُ أعلاه تُمرَّر — فلا يقرؤها الكيانُ ثانيةً. |
| ZatcaEInvoiceGenerator.cs | var dir = Path.Combine( | CEEF-202/203 | جذر البيانات الموحّد — يحترم BRQOSH_DATA_ROOT . |
| ZatcaEInvoiceGenerator.cs | switch (docType) | CEEF-229 · Z-3 | الإرسال حسب نوع المستند لا بالتخمين، وبتصفية الشركة دائماً. |
| ZatcaEInvoiceGenerator.cs | case ZatcaDocumentType.DisposalCancel: | جاهزيّةُ الإطلاق · ح-٣٦ | ‏وإلغاءُ المستخلص مرجعُه مستخلصُه. |
| ZatcaEInvoiceGenerator.cs | if (db.Database.CurrentTransaction is null) | CEEF-229 · م1-٦ / Z-4 | قفلٌ بلا معاملةٍ قفلٌ ميّت — ويُرفض صراحةً. |
| SaudiChartOfAccounts.cs | new("1115", "تسوية إعادة تقييم ذمم مدينة بعملة أجنبية", "FX Revaluation Adjustment - Recei | CEEF-230 · ط4-٧ | ‏تسويةُ إعادة تقييم الذمم بعملةٍ أجنبية — حسابٌ مستقلٌّ |
| SaudiChartOfAccounts.cs | new("1116", "سُلَف الموظفين", "Employee Advances", AccountType.Asset, "11", IsGroup: false | CEEF-230 · ط2-٥ | ‏سُلفةُ الموظف ذمّةٌ عليه لا مصروفُ راتب. |
| SaudiChartOfAccounts.cs | new("1117", "عُهَد رحلات التوصيل", "Trip Cash Floats", AccountType.Asset, "11", IsGroup: f | CEEF-230 · ط3-٤ب | ‏عهدةُ السائق أصلٌ لا مصروف حتى تُسوَّى. |
| SaudiChartOfAccounts.cs | new("1118", "إيرادات إقامة مستحقة غير مفوترة", "Accrued Room Revenue - Unbilled", AccountT | جاهزيّةُ الإطلاق · ح-٨٢ | ‏ليلةُ الإقامة إيرادُ شهرها وإن لم تُفوتَر. فالفاتورةُ يومَ المغادرة، وكانت ليالي |
| SaudiChartOfAccounts.cs | new("1119", "إنتاج تحت التشغيل (أوامر الإنتاج)", "Work in Process - Production Orders", Ac | جاهزيّةُ الإطلاق · ح-٩٨ · `manufacturing#8` | ‏إنتاجُ أوامر التصنيع تحت التشغيل — حسابُه لا أصلُ عقود المقاولات. |
| SaudiChartOfAccounts.cs | new("2112", "بضاعة مستلمة غير مفوترة", "Goods Received Not Invoiced", AccountType.Liabilit | CEEF-229 · م1-١٩ / I-5 | بضاعةٌ استُلمت ولم تُفوتَر — التزامٌ قائمٌ بين الاستلام |
| SaudiChartOfAccounts.cs | new("2113", "تأمينات مستأجرين محتفظ بها", "Tenant Security Deposits Held", AccountType.Lia | CEEF-230 · ط3-١ | ‏تأميناتُ المستأجرين — أمانةٌ لا إيراد. وقيدُها إيراداً |
| SaudiChartOfAccounts.cs | new("2114", "تحصيلٌ نيابةً عن التجّار (الدفع عند الاستلام)", "Cash on Delivery Payable", A | CEEF-230 · ط3-٤ | ‏ما يُحصَّل نيابةً عن التجّار — أمانةٌ لا إيراد. وقيدُه |
| SaudiChartOfAccounts.cs | new("2115", "تسوية إعادة تقييم ذمم دائنة بعملة أجنبية", "FX Revaluation Adjustment - Payab | CEEF-230 · ط4-٧ | ‏ونظيرُه لجانب الموردين — يحرس هـ-٢ بالمنطق نفسِه. |
| SaudiChartOfAccounts.cs | new("2116", "إكراميات مستحقة للعاملين", "Tips Payable to Staff", AccountType.Liability, "2 | CEEF-230 · ط3-٣د | ‏الإكراميةُ مالُ العامل لا إيرادُ المطعم. |
| SaudiChartOfAccounts.cs | new("2117", "إيرادات إيجار مقبوضة مقدّماً", "Unearned Rent Revenue", AccountType.Liability | جاهزيّةُ الإطلاق · ح-٧٩ | ‏الإيجارُ المقبوضُ مقدّماً التزامٌ حتى يُكتسب. فاتورةُ ربعٍ أو سنةٍ مقدَّماً كانت إيراداً |
| SaudiChartOfAccounts.cs | new("2203", "مخصص الإجازات", "Leave Provision", AccountType.Liability, "22", IsGroup: fals | CEEF-233 · ز-٣ | ‏مخصّصُ الإجازات — التزامٌ يتراكم يوماً بيوم (المادة 109)، |
| SaudiChartOfAccounts.cs | new("4108", "أرباح فروق عملة محققة", "Realized FX Gain", AccountType.Revenue, "4", IsGroup | CEEF-230 · ط1-٤ | ‏المحقَّقُ غيرُ غير المحقَّق — والفصلُ بينهما ليس تصنيفاً: |
| SaudiChartOfAccounts.cs | new("5108", "فروق أسعار المشتريات", "Purchase Price Variance", AccountType.Expense, "51",  | CEEF-229 · م1-١٩ | فرقُ سعر الفاتورة عن سعر الاستلام. غيرُ نظاميّ عمداً: |
| SaudiChartOfAccounts.cs | new("5219", "مصروف مكافأة نهاية الخدمة", "End of Service Benefit Expense", AccountType.Exp | CEEF-230 · ط2-٣ | ‏مصروفُ المكافأة — يقابل المخصّصَ 2202 الذي كان بلا كاتب. |
| SaudiChartOfAccounts.cs | new("5220", "مصروف الإجازات", "Leave Expense", AccountType.Expense, "52", IsGroup: false,  | CEEF-233 · ز-٣ | ‏مصروفُ الإجازات — يقابل المخصّصَ 2203. |
| SaudiChartOfAccounts.cs | public const string AccruedExpenses = "2104"; | جاهزيّةُ الإطلاق · ح-٢٧ | مصروفاتٌ مستحقّةٌ بلا فاتورةٍ بعد — |
| SaudiChartOfAccounts.cs | public const string FxAdjustmentReceivable = "1115"; | CEEF-230 · ط4-٧ | ‏حسابا تسوية إعادة التقييم — يحفظان هـ-١ وهـ-٢. |
| SaudiChartOfAccounts.cs | public const string EmployeeAdvances = "1116"; | CEEF-230 · ط2-٥ |  سُلَفُ الموظفين — أصلٌ يُستردّ بالاستقطاع. |
| SaudiChartOfAccounts.cs | public const string LeaveProvision = "2203"; | CEEF-233 · ز-٣ |  مخصّصُ الإجازات ومصروفُها. |
| SaudiChartOfAccounts.cs | public const string RealizedFxGain = "4108"; | CEEF-230 · ط1-٤ |  فرقُ عملةٍ وقع بسدادٍ فعليّ — لا يُعكس ولا يُعاد تقديرُه. |
| SaudiChartOfAccounts.cs | public const string TenantSecurityDeposits = "2113"; | CEEF-230 · ط3-١ | ‏تأميناتُ المستأجرين المحتفظ بها. |
| SaudiChartOfAccounts.cs | public const string CodPayable = "2114"; | CEEF-230 · ط3-٤ | ‏تحصيلٌ نيابةً عن التجّار. |
| SaudiChartOfAccounts.cs | public const string TipsPayable = "2116"; | CEEF-230 · ط3-٣د |  إكرامياتٌ مستحقّةٌ للعاملين — التزامٌ لا إيراد. |
| SaudiChartOfAccounts.cs | public const string UnearnedRent = "2117"; | جاهزيّةُ الإطلاق · ح-٧٩ |  إيجارٌ مقبوضٌ مقدّماً — التزامٌ يُكتسب شهراً بشهر. |
| SaudiChartOfAccounts.cs | public const string TripCashFloat = "1117"; | CEEF-230 · ط3-٤ب |  عُهَدُ رحلات التوصيل — أصلٌ يُسوَّى مع السائق. |
| SaudiChartOfAccounts.cs | public const string AccruedRevenue = "1118"; | جاهزيّةُ الإطلاق · ح-٨٢ |  إيرادُ ليالٍ مضت ولم تُفوتَر — أصلٌ تُطلقه فاتورةُ المغادرة. |
