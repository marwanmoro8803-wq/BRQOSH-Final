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
| ReportingContracts.cs | public record VatReturnCategoryDto(string Code, decimal TaxableAmount, decimal VatAmount); | CEEF-229 · م1-١٠ / Z-12 | وعاءٌ وضريبةٌ لفئةٍ واحدة — على الحدّ الذي تقرؤه الشاشة. |
| ReportingContracts.cs | public record VatReturnBoxDto(int Box, decimal Amount, decimal Adjustments, decimal VatAmo | جاهزيّةُ الإطلاق · ح-١٦٥ · <c>tax-zatca#12</c> | خانةٌ من نموذج الإقرار في بوّابة الهيئة — رقمُها ومبلغُها |
| ReportingContracts.cs | public static class VatReturnNet | جاهزيّةُ الإطلاق · ح-٤٠ | صافي الإقرار — مالكٌ واحد. |
| ReportingContracts.cs | public static class VatReturnNet | CEEF-219 | (تقرؤه الشاشة)، كلٌّ يشتقّ الصافيَ بنفسه. فأُضيفت تسويةُ الإلغاء |
| ReportingContracts.cs | public static class VatReturnNet | CEEF-232 · ض-١ | والضريبةُ المقيّدة بلا مستند إلى الأوّل وبقي الثاني على المعادلة |
| ReportingContracts.cs | public record VatReturnDto( | جاهزيّةُ الإطلاق · ح-٤٠ | والحقولُ الأربعة إلزاميّةٌ بلا قيمةٍ افتراضيّة — وموضعُها |
| ReportingContracts.cs | decimal SalesBaseNet, | جاهزيّةُ الإطلاق · ح-٤١ | ‏الأبُ الذي تُعرض أوعيةُ الفئات تحته — صافي الوعاء من |
| ReportingContracts.cs | decimal ReverseChargeVat = 0m) | جاهزيّةُ الإطلاق · tax-zatca#12 | ‏ضريبةُ الاحتساب العكسيّ — مخرجاتٌ هنا ومدخلاتٌ في DeductibleInputVat معاً (الخانة 9). |
| ReportingContracts.cs | public decimal? GlVariance => | CEEF-229 · م2-١٦ | null = «لا مطابقة ممكنة» وهو غير الصفر = «طابق تماماً». |
| AppEnvironment.cs | private static AppEnvironment Resolve() => DecideForBuild( | CEEF-228 | يفشل إلى الأمان: غياب BRQOSH_ENV يعني إنتاجاً لا تطويراً. |
| AppEnvironment.cs | internal static AppEnvironment Decide(AppEnvironment? authoritative, string? processEnv, s | CEEF-229 · م0-١٢ | قرار البيئة — دالة نقيّة، كل مدخلاتها ظاهرة. |
| AppEnvironment.cs | private static AppEnvironment? ResolveAuthoritative() | CEEF-229 · م0-١٢ | البيئة من مصدرٍ لا يملكه المستخدم. |
| TreasuryContracts.cs | decimal ProjectedBalance = 0m); | CEEF-230 · ط4-٣ج | الرصيدُ المتوقَّع آخرَ الأسبوع — وهو الجوابُ المطلوب. |
| Account.cs | public void Deactivate() | CEEF-229 · م0-١٣ |  كانت هذه الدالة تضبط DeletedAtUtc أيضاً، أي أن «إيقاف حساب» |
| Budget.cs | public class Budget | CEEF-230 · ط1-٦ | موازنةُ سنةٍ ماليّة. |
| Budget.cs | public class BudgetLine | CEEF-230 · ط1-٦ | سطرُ موازنة — حسابٌ في شهرٍ، ومركزُ تكلفةٍ اختياريّ. |
| CostCenter.cs | public void Deactivate() => IsActive = false; | CEEF-229 · م2-١٧ |  كان يضبط DeletedAtUtc أيضاً، ومرشّح الاستعلام العامّ |
| DocumentLineScale.cs | public static class DocumentLineScale | CEEF-229 · م2-٢ | سعرُ الوحدة لا يتجاوز خانتَي الهللة. |
| LineDocumentTotalsCalculator.cs | public static IReadOnlyList<VatSubtotal> ComputeSubtotals( | CEEF-207 | الأوعية الضريبية مجمَّعة بالفئة . |
| LineDocumentTotalsCalculator.cs | var netTotal = groups.Sum(g => g.Sum(l => l.LineTotal)); | CEEF-230 · ط1-٢ | ‏توزيعُ خصم المستند على الأوعية — والفلسُ الأخير مقصود. |
| LineDocumentTotalsCalculator.cs | public static IReadOnlyList<(VatCategory Category, decimal Rate, decimal Amount)> | CEEF-230 · ط1-٢ | ‏حصّةُ كلّ وعاءٍ من خصم المستند — تُعرض في UBL عنصرَ |
| OpeningBalanceItem.cs | public class OpeningBalanceItem | جاهزيّةُ الإطلاق · ح-١٣٤ · <c>accounting-core#7</c> | ‏ ذمّةٌ مفتوحةٌ عند بدء التشغيل — فاتورةٌ من النظام السابق لم تُسدَّد. |
| FixedAsset.cs | DecliningBalance = 1, | جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c> | القسطُ المتناقصُ المضاعَف — ضعفُ نسبة الخطّ المستقيم على القيمة |
| FixedAsset.cs | public DateOnly? DepreciatedThrough { get; private set; } | جاهزيّةُ الإطلاق · ح-١٠٠ · <c>treasury-payroll-assets#12</c> | آخرُ يومٍ أُهلك قبل الدفاتر — لأصلٍ قائمٍ قبل بدء التشغيل |
| FixedAsset.cs | public DateOnly? CancelledOn { get; private set; } | جاهزيّةُ الإطلاق · ح-١٥٧ · <c>treasury-payroll-assets#10</c> | تاريخُ إلغاء الاقتناء — أصلٌ أُدخل خطأً فعُكس قيدُه. |
| FixedAsset.cs | public bool AwaitingInvoice { get; private set; } | جاهزيّةُ الإطلاق · ح-١٥٨ · <c>treasury-payroll-assets#12</c> | أصلٌ يُشترى من مورّدٍ بفاتورته — مسجَّلٌ ولم يُقتنَ بعد: |
| FixedAsset.cs | public long? DisposalBuyerCustomerId { get; private set; } | جاهزيّةُ الإطلاق · ح-١٦ | ── بيعُ الأصل ──────────────────────────────── |
| FixedAsset.cs | public long? DisposalBuyerCustomerId { get; private set; } | جاهزيّةُ الإطلاق · ح-١٦ | المشتري — ولم يكن للأصل مشترٍ قطّ. |
| FixedAsset.cs | public void UseDepreciationMethod(DepreciationMethod method) | جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c> | يحدّد طريقةَ الإهلاك — قبل أوّل إهلاكٍ وحدَه. فتغييرُها بعده |
| FixedAsset.cs | public static FixedAsset CreateOpening(long companyId, string code, string nameAr, DateOnl | جاهزيّةُ الإطلاق · ح-١٠٠ | أصلٌ قائمٌ قبل بدء التشغيل — بمجمّع إهلاكه حتى نهاية الشهر السابق لتاريخ الرصيد الافتتاحيّ. |
| FixedAsset.cs | public void Capitalize(decimal amount) | CEEF-232 · ع-٢ | يُرسمِل تحسيناً على الأصل — فتزيد تكلفتُه ويزيد قسطُه. |
| FixedAsset.cs | public void UndoDisposal() | جاهزيّةُ الإطلاق · ح-١٦١ | يُعيد المُستبعَدَ إلى السجلّ — بعد عكس قيد استبعاده. وتبقى بياناتُ البيع (المشتري والفاتورةُ |
| FixedAsset.cs | public void RecordDisposalSale( | جاهزيّةُ الإطلاق · ح-١٦ | تسجيلُ البيع على سجلّ الأصل — لا في سرد القيد وحدَه. |
| FixedAsset.cs | public void EnsureReadyForDisposal() | CEEF-229 · م0-١٥ | الاستبعاد مسموح في أيّ وقت من عمر الأصل. |
| FixedAsset.cs | public void UndoDepreciation(decimal amount) | جاهزيّةُ الإطلاق · ح-٩٢ | يُنقص المجمَّعَ بما أُهلك في قيدٍ عُكس — وإلّا بقي السجلُّ أقلَّ قيمةً من الأستاذ، ولا يُهلَك الشهرُ |
| FixedAsset.cs | public decimal ApplyDepreciation(int months) | جاهزيّةُ الإطلاق · ح-٥٨ · treasury-payroll-assets#11 | ‏أقساطُ عدّة أشهرٍ دفعةً — ولا تتجاوز القابلَ للإهلاك. |
| FixedAsset.cs | public int MonthsDue(DateOnly? lastDepreciatedMonth, DateOnly runDate) => | جاهزيّةُ الإطلاق · ح-٥٨ · treasury-payroll-assets#11 | ‏الأشهرُ المستحقّةُ حتى شهر التشغيل — من الشهر |
| FixedAsset.cs | public decimal ApplyDepreciationDue(DateOnly? lastDepreciatedMonth, DateOnly runDate, bool | جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c> · قرارُ المالك 2026-10-20 | إهلاكُ تشغيلٍ — وشهرُ الاقتناء بأيّامه. |
| FixedAsset.cs | private decimal ApplyDecliningBalanceDue(DateOnly? lastDepreciatedMonth, DateOnly runDate, | جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c> | القسطُ المتناقصُ شهراً شهراً — فالقسطُ ينقص كلَّ شهرٍ بنقص الدفتريّة، |
| Company.cs | public string? RegistrationName { get; private set; } | CEEF-229 · Z-10 | الاسم المسجَّل في السجل التجاريّ — cbc:RegistrationName. |
| Company.cs | public NationalAddress NationalAddress { get; private set; } = NationalAddress.Empty; | CEEF-229 · Z-10 | العنوان الوطنيّ بحقوله المفكَّكة — cac:PostalAddress. |
| Company.cs | public decimal ManualJournalApprovalThreshold { get; private set; } = DefaultManualJournal | جاهزيّةُ الإطلاق · <c>accounting-core#6</c> · قرارُ المالك 2026-10-18 | ‏ حدُّ اعتماد القيد اليدويّ — قيدٌ يدويٌّ |
| Company.cs | public void UpdateRegulatoryIdentity( | CEEF-229 · Z-10 | بيانات المنشأة النظامية للفوترة الإلكترونية. |
| NationalAddress.cs | public record NationalAddress( | CEEF-229 · Z-10 | العنوان الوطنيّ السعوديّ — بحقوله المفكَّكة كما تفرضها الفاتورة الإلكترونية. |
| ConstructionProject.cs | public void Deactivate() => IsActive = false; | CEEF-229 · م2-١٧ |  كان يضبط DeletedAtUtc أيضاً، ومرشّح الاستعلام العامّ |
| ProgressBilling.cs | public DateOnly? CancelledAt { get; private set; } | O-21، CEEF-219 | تاريخ الإلغاء . الإقرار الضريبي يعكس وقائع الفترة: |
| CrmLead.cs | public void Deactivate() => IsActive = false; | CEEF-229 · م2-١٧ |  كان يضبط DeletedAtUtc أيضاً، ومرشّح الاستعلام العامّ |
| CoverageCalculator.cs | public static class CoverageCalculator | CEEF-230 · ط3-٢ | قسمةُ فاتورة الزيارة — وهي كلُّ الفرق بين عيادةٍ تُحصّل وعيادةٍ تخسر. |
| InsuranceClaim.cs | public class InsuranceClaim : IBranchScoped | CEEF-230 · ط3-٢ب | دورةُ المطالبات — والمالُ الذي يضيع في العيادات يضيع هنا. |
| InsurancePolicy.cs | public class InsurancePayer | CEEF-230 · ط3-٢ | شركةُ تأمينٍ — وهي عميلٌ في دفتر الذمم قبل أن تكون جهةً. |
| InsurancePolicy.cs | public class InsurancePolicy | CEEF-230 · ط3-٢ | وثيقةُ تأمينٍ تربط مريضاً بشركة. |
| Patient.cs | public class Patient : IBranchScoped | CEEF-230 · ط3-٢ | مريضٌ — وملفُّه رقمٌ لا يتكرّر. |
| Visit.cs | public class Visit : IBranchScoped | CEEF-230 · ط3-٢ | زيارةٌ — والخدماتُ المصروفةُ فيها. |
| Visit.cs | public void AddLine(string description, decimal quantity, decimal unitPrice, | CEEF-231 · ط3-٢ج | رقمُ الدفعة المصروفة — ويلزم لكلّ دواءٍ متتبَّع. |
| Visit.cs | public long? ProductId { get; private set; } | CEEF-231 · ط3-٢ج | صنفُ الصيدلية المصروف — وسطرُ الخدمة بلا صنف. |
| Visit.cs | public string? BatchNumber { get; private set; } | CEEF-231 · ط3-٢ج | رقمُ الدفعة المصروفة — والدواءُ المتتبَّع لا يخرج بلا رقم. |
| MenuItem.cs | public class MenuItem | CEEF-230 · ط3-٣ | صنفٌ في قائمة الطعام — ووصفتُه هي ما يربطه بالمخزون. |
| MenuItem.cs | public long? SaleProductId { get; private set; } | CEEF-230 · ط3-٣ج | الصنفُ الذي يُباع به الطبقُ في الفواتير ونقطة البيع. |
| Reservation.cs | public class Room | CEEF-230 · ط3-٣ب | غرفةٌ — وحالتُها قيدٌ لا وصف. |
| Reservation.cs | public class Reservation : IBranchScoped | CEEF-230 · ط3-٣ب | حجزٌ — وحسابُ نزيلٍ يتراكم ليلةً بليلة. |
| Reservation.cs | public void Cancel() | جاهزيّةُ الإطلاق · ح-٨٣ | لا يُلغى إلّا حجزٌ قائم — فحجزٌ نزل صاحبُه يُغادر ولا يُلغى، وحجزٌ أُلغي أو غاب |
| Reservation.cs | public void MarkNoShow(DateOnly on) | جاهزيّةُ الإطلاق · ح-٨٣ | لم يحضر — حجزٌ قائمٌ مضى يومُ وصوله. ولا قبله: فالنزيلُ لم يتأخّر بعد، ومصادرةُ |
| Reservation.cs | public class GuestFolio | CEEF-230 · ط3-٣ب | حسابُ النزيل — يتراكم ثمّ يُصفّى بفاتورةٍ واحدة. |
| TipPool.cs | public class TipPool : IBranchScoped | CEEF-230 · ط3-٣د | وعاءُ الإكراميات — مالُ النُّدُل يُقيَّد إيراداً للمطعم. |
| BatchRef.cs | public readonly record struct BatchRef(string Number, DateOnly? Expiry = null) | CEEF-230 · ط1-٣ | هويّةُ دفعةٍ مصاحبةٌ لحركةٍ واحدة. |
| InventoryCostConsumption.cs | public class InventoryCostConsumption | CEEF-229 · م0-٣ / I-2 | سجلُّ ما استُهلك من أيّ طبقة — وبلا هذا لا يمكن ترميمُها. |
| InventoryTracking.cs | public enum InventoryTracking | CEEF-230 · ط1-٣ | نمطُ تتبّع الصنف. |
| Product.cs | public InventoryTracking Tracking { get; private set; } = InventoryTracking.None; | CEEF-230 · ط1-٣ | نمطُ التتبّع — والأصلُ None، فلا يتغيّر سلوكُ أيّ |
| Product.cs | public void SetWeightedAverageCost(decimal wac) => | جاهزيّةُ الإطلاق · ح-٢١ | ‏نُزعت ApplyReceipt — دالّةٌ ميّتةٌ تُفسد المتوسّطَ |
| Product.cs | public void Deactivate() => IsActive = false; | CEEF-229 · م2-١٧ |  كان يضبط DeletedAtUtc أيضاً، ومرشّح الاستعلام العامّ |
| Product.cs | public void SetTracking(InventoryTracking tracking, decimal currentOnHand) | CEEF-230 · ط1-٣ | يضبط نمطَ التتبّع — ويُرفض ما دام للصنف رصيد. |
| StockBatch.cs | public class StockBatch : IBranchScoped | CEEF-230 · ط1-٣ | رصيدُ دفعةٍ واحدةٍ من صنفٍ في مستودعٍ واحد. |
| StockMovement.cs | public record StockMovementLine( | CEEF-230 · ط1-٣ |  وBatchNumber يبقى على السطر بعد الترحيل عمداً: |
| UnitCatalog.cs | public static class UnitCatalog | CEEF-230 · ط1-١ | قالبُ الوحدات المبذور — وجسرٌ إلى النصّ الحرّ القديم. |
| UnitOfMeasure.cs | public class UnitOfMeasure | CEEF-230 · ط1-١ | وحدةُ قياس — وأسرةٌ تنتمي إليها. |
| Warehouse.cs | public void Deactivate() => IsActive = false; | CEEF-229 · م2-١٧ |  كان يضبط DeletedAtUtc أيضاً، ومرشّح الاستعلام العامّ |
| DeliveryTrip.cs | public class DeliveryTrip : IBranchScoped | CEEF-230 · ط3-٤ب | رحلةُ توصيل — والمسارُ الخاسرُ الذي لا يُرى. |
| Shipment.cs | public class Shipment : IBranchScoped | CEEF-230 · ط3-٤ | شحنةٌ ببوليصة — والدفعُ عند الاستلام أخطرُ ما فيها. |
| Shipment.cs | public bool CodCollectionPosted { get; private set; } | جاهزيّةُ الإطلاق · ح-١٢ | هل أُثبت التحصيلُ في الدفتر؟ — وهو غيرُ |
| Shipment.cs | public long? FreightInvoiceId { get; private set; } | CEEF-232 · ل-١ | فاتورةُ أجرة الشحن — وnull يعني أجرةً لم تُفوتَر بعد. |
| Shipment.cs | if (!CodCollectionPosted) | جاهزيّةُ الإطلاق · ح-١٢ | ‏ ‏ولا يُورَّد ما لم يُقيَّد تحصيلُه. |
| BillOfMaterials.cs | public decimal LabourCostPerUnit { get; private set; } | جاهزيّةُ الإطلاق · ح-١٣ | أجرُ العمالة المباشرة لكلّ وحدةٍ تامّة. |
| BillOfMaterials.cs | public decimal OverheadRatePercent { get; private set; } | جاهزيّةُ الإطلاق · ح-١٣ | نصيبُ التكاليف الصناعيّة غير المباشرة — نسبةً من |
| BillOfMaterials.cs | public void Deactivate() => IsActive = false; | CEEF-229 · م2-١٧ |  كان يضبط DeletedAtUtc أيضاً، ومرشّح الاستعلام العامّ |
| WorkOrder.cs | public class WorkOrderMaterial | جاهزيّةُ الإطلاق · ح-٧٧ · <c>manufacturing#5</c> | بندٌ من وصفة الأمر كما كان يومَ صدوره — مكوّنٌ وكمّيّتُه |
| WorkOrder.cs | public IReadOnlyList<WorkOrderMaterial> Materials => _materials.AsReadOnly(); | جاهزيّةُ الإطلاق · ح-٧٧ · <c>manufacturing#5</c> | وصفةُ الأمر — تُجمَّد عند إصداره، ولا تتبع القائمةَ بعده. |
| WorkOrder.cs | public long? MaterialsWarehouseId { get; private set; } | جاهزيّةُ الإطلاق · ح-٩٧ · <c>manufacturing#7</c> | مستودعُ الموادّ — منه تُصرف الخامات. وفارغُه هو المستودعُ الافتراضيّ |
| WorkOrder.cs | public decimal IssuedMaterialCost { get; private set; } | جاهزيّةُ الإطلاق · ح-١٤٧ · <c>manufacturing#4</c> | تكلفةُ ما صُرف فعلاً للأمر — بصرفٍ واحدٍ أو أكثر قبل الإكمال. |
| WorkOrder.cs | public int CompletionCount { get; private set; } | جاهزيّةُ الإطلاق · ح-١٤٨ · <c>manufacturing#4</c> | عددُ دفعات الإكمال — جزئيّةً قبل الإغلاق ثمّ الإغلاق؛ |
| ModuleCatalog.cs | }; | جاهزيّةُ الإطلاق · ح-١١٩ | ‏ ‏والمشترياتُ لا تجرّ المخزون — الخدماتُ والمصروفاتُ تُشترى بمورّدٍ وفاتورةٍ |
| ModuleCatalog.cs | }; | CEEF-230 · ط6-٤ | ‏والقطاعاتُ الأربعة كلُّها تلزمها المبيعات: عقدُ الإيجار |
| ModuleCatalog.cs | }; | CEEF-230 · ط0-١ | ‏أُضيفت Zatca — مَن يبيع في السعودية يُفوتِر إلكترونياً. |
| ModuleCatalog.cs | }; | CEEF-229 · م2-١٩ | أُضيفت Purchasing — كانت غائبة عن التجزئة والضيافة وحدهما |
| ModuleCatalog.cs | public static IReadOnlySet<ModuleKey> RestrictToLicense(IReadOnlySet<ModuleKey> active, IR | جاهزيّةُ الإطلاق · ح-١٦٤ · security-licensing-tenancy#2 | ‏ وحداتُ نظام العمل التي يمنحها الترخيص. |
| ModuleKey.cs | RealEstate, | CEEF-230 · ط6-٤ | ‏القطاعاتُ الأربعة — ومفاتيحُها أُخِّرت عمداً حتّى بُنيت شاشاتُها. |
| Employee.cs | public enum GosiRegime | جاهزيّةُ الإطلاق · ح-١٣٨ | ‏ فئةُ المشترك في التأمينات الاجتماعيّة. |
| Employee.cs | public class Employee : IBranchScoped | CEEF-230 · ط2-١ | سجلُّ الموظّف. |
| Employee.cs | public long? ProjectId { get; private set; } | جاهزيّةُ الإطلاق · ح-١٤٦ · <c>sector-modules#9</c> | المشروعُ الذي يعمل عليه الموظّف — فتُحمَّل عليه |
| Employee.cs | public bool IsEmployedDuring(DateOnly from, DateOnly to) => | جاهزيّةُ الإطلاق · ح-٨٦ | على رأس العمل يوماً واحداً على الأقلّ بين التاريخين — فيُدرَج في دورة شهره ويُصرف له |
| EmployeeAdvance.cs | public class EmployeeAdvance : IBranchScoped | CEEF-230 · ط2-٥ | سُلفةُ موظّف — مالٌ يخرج ويُنسى. |
| EmployeeAdvance.cs | public decimal InstallmentAmount { get; private set; } | جاهزيّةُ الإطلاق · ح-٤٩ | ‏ وكان هذا التعليقُ يقول «يُرفض» ولا حارسَ في الشجرة كلِّها |
| EmployeeAdvance.cs | public void ReverseDisbursement() | جاهزيّةُ الإطلاق · ح-٩١ | يُعيدها «معتمدةً» بعد عكسِ صرفها — فتُلغى أو تُصرف من جديد. وكان Cancel يقول «اعكسْ صرفَها» |
| EndOfServiceCalculator.cs | public static class EndOfServiceCalculator | CEEF-230 · ط2-٣ | مكافأةُ نهاية الخدمة — نظامُ العمل السعوديّ، المادتان 84 و85. |
| GosiPolicy.cs | public class GosiPolicy | CEEF-230 · ط2-٢ | نِسَبُ التأمينات الاجتماعية — مُخزَّنةٌ لكلّ منشأةٍ لا مثبَّتةٌ في الشيفرة. |
| GosiPolicy.cs | public decimal NewLawEmployeePercent { get; private set; } = 9.75m; | جاهزيّةُ الإطلاق · ح-١٣٨ | ── النظامُ الجديدُ (٣ يوليو ٢٠٢٤) — جدولُ نسبة المعاش ────────────────────────────── |
| LeaveEntitlementCalculator.cs | public static class LeaveEntitlementCalculator | CEEF-233 · ز-١ | الإجازاتُ والغياب — نظامُ العمل السعوديّ، المواد 109 و111 و117. |
| LeaveEntitlementCalculator.cs | public static int? StatutoryMaxDays(LeaveType type) => type switch | جاهزيّةُ الإطلاق · ح-١١٧ · ‏`treasury-payroll-assets#9` | ‏ أقصى مدّةٍ نظاميّةٍ لإجازةٍ مدفوعةٍ بنصّ — وnull لما لا مدّةَ له. |
| LeaveEntitlementCalculator.cs | public static decimal SickDeductionDays( | جاهزيّةُ الإطلاق · ح-١١٦ · ‏`treasury-payroll-assets#9` | ‏ أيّامُ الخصم المرضيّ في شهرٍ — موزونةً بتدرّج المادة 117. |
| LeaveRequest.cs | public class LeaveRequest : IBranchScoped | CEEF-233 · ز-٢ | طلبُ إجازةٍ أو غياب — والمستندُ الذي يُبنى منه كلُّ شيء. |
| PayrollRun.cs | public long? EmployeeId { get; private set; } | CEEF-230 · ط2-١ | معرّفُ الموظّف — وnull يعني سطراً كُتب يدوياً قبل |
| PayrollRun.cs | public decimal AdvanceDeduction { get; private set; } | CEEF-230 · ط2-٥ | قسطُ السُّلفة المُستقطَع هذا الشهر. |
| PayrollRun.cs | public decimal AbsenceDeduction { get; private set; } | CEEF-233 · ز-٦ | خصمُ الغياب بلا أجر — وهو ليس كخصم السُّلفة. |
| PayrollRun.cs | public decimal TotalAdvanceDeduction => _lines.Sum(l => l.AdvanceDeduction); | CEEF-230 · ط2-٥ |  مجموعُ أقساط السُّلَف — وهو ما يُعفي حسابَ 1116. |
| PayrollRun.cs | public decimal TotalAbsenceDeduction => _lines.Sum(l => l.AbsenceDeduction); | CEEF-233 · ز-٦ |  مجموعُ خصم الغياب — وهو يُنقص المصروف لا يُعفي حساباً. |
| PayrollRun.cs | if (employeeId is long id && _lines.Any(l => l.EmployeeId == id)) | CEEF-230 · ط2-١ | ‏ولا يُدرَج موظّفٌ مرّتين في دورةٍ واحدة: راتبٌ مزدوجٌ |
| PayrollRun.cs | if (_lines[^1].GrossPay < 0m) | CEEF-233 · ز-٦ | ‏وغيابٌ يتجاوز الأجرَ يُرفض — فأجرٌ سالبٌ ليس خصماً بل |
| PayrollRun.cs | var line = _lines[^1]; | CEEF-230 · ط2-٥ | ‏واستقطاعٌ يبتلع الصافيَ يُرفض. |
| PayrollRun.cs | public void AddEmployee(Employee employee, GosiPolicy policy, decimal advanceDeduction = 0 | CEEF-230 · ط2-٢ | يُضيف موظّفاً من سجلّه، وتُحسب تأميناتُه. |
| PayrollRun.cs | var (monthStart, monthEnd) = PeriodBounds(); | جاهزيّةُ الإطلاق · ح-٨٦ | ‏ ‏والحارسُ بالشهر لا بيوم الصرف — وكان يرفض مغادراً في الخامس عشر لأنّه ليس على رأس العمل |
| PayrollRun.cs | public static decimal GosiEmployeeShareWithin(decimal gosiEmployee, decimal wageDue) => | جاهزيّةُ الإطلاق · ح-١١٦ | ‏ حصّةُ الموظّف من التأمينات لا تتجاوز أجرَه المستحقّ. |
| PayrollRun.cs | public (DateOnly Start, DateOnly End) PeriodBounds() | جاهزيّةُ الإطلاق · ح-٨٦ |  أوّلُ شهر الدورة وآخرُه — من رمزها yyyy-MM. |
| PayrollRun.cs | public JournalEntry Post(Account salariesExpense, Account gosiExpense, Account salariesPay | CEEF-232 · م-١ | مركزُ تكلفةِ كلِّ موظّف — وبه تُفكَّك مصروفاتُ الأجر على مراكزها. |
| PayrollRun.cs | var projects = employeeProjects ?? new Dictionary<long, long>(); | CEEF-230 · ط2-٤ | ‏البدلاتُ تُفصَل عن الأساسيّ. |
| PayrollRun.cs | var projects = employeeProjects ?? new Dictionary<long, long>(); | جاهزيّةُ الإطلاق · ح-٢ | ‏والمصروفُ يُؤخذ بعد الغياب — وكان يُؤخذ كاملاً. |
| PayrollRun.cs | var projects = employeeProjects ?? new Dictionary<long, long>(); | جاهزيّةُ الإطلاق · ح-١٤٦ · ‏`sector-modules#9` | ‏موظّفُ المشروع تُحمَّل تكلفتُه على المشروع — فتخرج سطورُه |
| PayrollRun.cs | if (TotalAdvanceDeduction > 0m) | CEEF-230 · ط2-٥ | ‏والاستقطاعُ يُعفي السُّلفةَ لا يُنقص المصروف. |
| PayrollRun.cs | private void AddExpense(JournalEntry entry, Account account, | CEEF-232 · م-١ | يُضيف مصروفَ أجرٍ مفكَّكاً على مراكز التكلفة — أو سطراً |
| DebitNote.cs | public string CurrencyCode { get; private set; } = "SAR"; | جاهزيّةُ الإطلاق · ح-٣٩ | والإشعارُ المدينُ يحمل عملةَ فاتورتِه — |
| DebitNote.cs | public DateOnly? CancelledAt { get; private set; } | O-21، CEEF-219 | تاريخ الإلغاء . الإقرار الضريبي يعكس وقائع الفترة: |
| DebitNote.cs | BatchNumber: string.IsNullOrWhiteSpace(batchNumber) ? null : batchNumber.Trim())); | جاهزيّةُ الإطلاق · ح-١٠٥ · ‏`order-to-cash-procure-to-pay-inventory#7` | ‏ ‏والدفعةُ المرتجَعةُ تُسمّى — فيخرج المرتجعُ إلى المورّد منها وحدَها لا من أقدم ما استُلم. |
| DebitNote.cs | public JournalEntry Post(string noteNumber, Account purchasesOrInventory, Account vatInput | CEEF-234 |  يجب أن يطابق شرط الفاتورة الأصلية |
| DebitNote.cs | if (priceVariance is null) | جاهزيّةُ الإطلاق · ح-١٠٧ · كشفه العمل | ‏ ‏مرتجعٌ مُسعَّرٌ دون تكلفته: الفرقُ خسارةٌ تتحمّلها |
| GoodsReceipt.cs | public string? BatchNumber { get; private set; } | CEEF-230 · ط1-٣ |  رقمُ الدفعة أو الرقمُ التسلسليّ — هويّةُ ما دخل. |
| GoodsReceipt.cs | public DateOnly? ExpiryDate { get; private set; } | CEEF-230 · ط1-٣ |  تاريخُ انتهاء صلاحية الدفعة — اختياريّ. |
| GoodsReceipt.cs | public string? UnitCode { get; private set; } | CEEF-230 · ط1-١ |  وحدةُ الاستلام — وnull تعني وحدةَ الصنف. |
| GoodsReceipt.cs | public long? JournalEntryId { get; private set; } | CEEF-229 · م1-١٩ | قيدُ الاستحقاق — ومرساةُ عكسه عند الإلغاء. |
| GoodsReceipt.cs | public void AddLine(string description, decimal orderedQuantity, decimal receivedQuantity, | CEEF-230 · ط1-٣ | ‏و هويّةُ ما دخل. |
| PurchaseInvoice.cs | public enum PurchaseVatTreatment | جاهزيّةُ الإطلاق · <c>tax-zatca#12</c> | معاملةُ ضريبة المستند في الإقرار — من دليل الهيئة «الاستيراد والتصدير» |
| PurchaseInvoice.cs | public DateOnly? CancelledAt { get; private set; } | O-21، CEEF-219 | تاريخ الإلغاء . الإقرار الضريبي يعكس وقائع الفترة: |
| PurchaseInvoice.cs | var batch = string.IsNullOrWhiteSpace(batchNumber) ? null : batchNumber.Trim(); | جاهزيّةُ الإطلاق · ح-١٠٨ · ‏`order-to-cash-procure-to-pay-inventory#7` | ‏ ‏الدفعةُ وانتهاؤها — ‏يحملهما سطرُ الشراء المباشر إلى حركة الاستلام؛ ‏والانتهاءُ بلا دفعةٍ لا معنى له. |
| PurchaseInvoice.cs | public string CurrencyCode { get; private set; } = "SAR"; | CEEF-230 · ط4-٥ | عملةُ الفاتورة — وSAR هي الأصل. |
| PurchaseInvoice.cs | public void SetVatTreatment(PurchaseVatTreatment treatment) | جاهزيّةُ الإطلاق · <c>tax-zatca#12</c> | يحدّد معاملةَ الضريبة — في المسوّدة وحدَها، ولمن يخصم المدخلات: فالإقرارُ يعدّ |
| PurchaseInvoice.cs | var expensePart = this.GrandTotal() - inventorySubtotal - grniRelief - priceVariance; | جاهزيّةُ الإطلاق · ح-٣ | ‏وكان هذا الفرعُ لا يُقفل استحقاقَ الاستلام أبداً. |
| PurchaseInvoice.cs | public long? CostCenterId { get; private set; } | CEEF-230 · ط1-٧ | مركزُ تكلفة المستند — يُختم على سطور المصروف في قيده. |
| PurchaseInvoice.cs | public long? ProjectId { get; private set; } | جاهزيّةُ الإطلاق · ح-١٤٤ · ‏<c>sector-modules#9</c> | المشروعُ الذي تُحمَّل عليه الفاتورة. |
| PurchaseInvoice.cs | public long? FixedAssetId { get; private set; } | جاهزيّةُ الإطلاق · ح-١٥٨ · <c>treasury-payroll-assets#12</c> | الأصلُ الثابتُ الذي تشتريه الفاتورة. |
| PurchaseInvoice.cs | public long? WarehouseId { get; private set; } | جاهزيّةُ الإطلاق · ح-١١١ · <c>order-to-cash-procure-to-pay-inventory#12</c> | المستودعُ الذي يُستلم إليه الشراءُ المباشر. وفارغُه |
| PurchaseInvoice.cs | private static void AddAccrualRelief( | CEEF-229 · م1-١٩ | إقفالُ استحقاق الاستلام، وفرقُ السعر إن اختلف عن سعر الاستلام. |
| Supplier.cs | public int PaymentTermDays { get; private set; } | CEEF-230 · ط4-٣ج | مهلةُ السداد بالأيام — وبها وحدَها يصير التنبّؤ تنبّؤاً. |
| Supplier.cs | public void Deactivate() => IsActive = false; | CEEF-229 · م2-١٧ |  كان يضبط DeletedAtUtc أيضاً، ومرشّح الاستعلام العامّ |
| LeaseContract.cs | public class LeaseContract : IBranchScoped | CEEF-230 · ط3-١ | عقدُ إيجار. |
| LeaseContract.cs | public bool IsResidential { get; private set; } | جاهزيّةُ الإطلاق · ح-١٠ | أسكنيٌّ هذا الإيجار أم تجاريّ — والفرقُ ضريبةٌ لا وصف. |
| MaintenanceOrder.cs | CapitalImprovement = 3, | CEEF-232 · ع-٢ | تجديدٌ يزيد في العقار — يُرسمَل ولا يُصرَف. |
| MaintenanceOrder.cs | public class MaintenanceOrder : IBranchScoped | CEEF-230 · ط3-١ب | أمرُ صيانة — والتكلفةُ التي لا يعرف أحدٌ أيَّ وحدةٍ أكلتها. |
| MaintenanceOrder.cs | public long? PurchaseInvoiceId { get; private set; } | جاهزيّةُ الإطلاق · <c>sector-modules#11</c> · قرارُ المالك 2026-10-20 | فاتورةُ المورّد التي سندُ التكلفة — بها خُصمت |
| Property.cs | public class Property : IBranchScoped | CEEF-230 · ط3-١ | عقارٌ — والوحداتُ تحته. |
| Property.cs | public long? FixedAssetId { get; private set; } | CEEF-232 · ع-١ | الأصلُ الثابتُ الذي يُمثّل هذا العقار — وnull يعني عقاراً |
| Property.cs | public class PropertyUnit | CEEF-230 · ط3-١ | وحدةٌ مؤجَّرة — شقّةٌ أو محلٌّ أو مكتب. |
| Property.cs | internal void OccupyByLease() => Status = UnitStatus.Leased; | CEEF-230 · ط3-١ | ‏ وحالةُ الوحدة لا تُضبط إلا من عقد. |
| PosShift.cs | public decimal CashSales { get; private set; } | CEEF-228 | المبيعات النقدية وحدها — وهي ما يُتوقَّع وجوده في الدرج. |
| CreditNote.cs | public string CurrencyCode { get; private set; } = "SAR"; | جاهزيّةُ الإطلاق · ح-٣٥ | والإشعارُ يحمل عملةَ فاتورتِه. |
| CreditNote.cs | public DateTime? PostedAtUtc { get; private set; } | O-21، CEEF-219 | تاريخ الإلغاء . الإقرار الضريبي يعكس وقائع الفترة: |
| CreditNote.cs | public DateTime? PostedAtUtc { get; private set; } | CEEF-229 · م2-٢٤ | لحظةُ الترحيل — لا تاريخُه وحده. |
| CreditNote.cs | public decimal DocumentDiscountAmount { get; private set; } | جاهزيّةُ الإطلاق · ح-٦ | خصمُ المستند على الإشعار — وكان لا يُمثَّل أصلاً. |
| CreditNote.cs | public static decimal DocumentDiscountShareFor(SalesInvoice invoice, decimal returnedNet) | جاهزيّةُ الإطلاق · ح-٦ | حصّةُ المُرتجَع من خصم الفاتورة — بنسبة صافيه إلى صافيها. |
| CreditNote.cs | if (Tax.VatExemptionReason.IsRequiredFor(vatCategory)) | CEEF-229 · م1-١ب الشقّ الثاني | الرفضُ عند بناء السطر — أبكرُ موضعٍ ممكن. |
| CreditNote.cs | BatchNumber: string.IsNullOrWhiteSpace(batchNumber) ? null : batchNumber.Trim(), | جاهزيّةُ الإطلاق · ح-١٠٢ | ‏ ‏والدفعةُ المرتجَعةُ تُسمّى — تنسخها الشاشةُ من سطر الفاتورة، فيعود المرتجعُ إليها لا إلى أقدم ما بيع. |
| Customer.cs | public string? RegistrationName { get; private set; } | CEEF-229 · م1-١ب | الاسم المسجَّل للمشتري — cbc:RegistrationName في الفاتورة القياسية. |
| Customer.cs | public int PaymentTermDays { get; private set; } | CEEF-230 · ط4-٣ج | مهلةُ السداد بالأيام — وبها وحدَها يصير التنبّؤ تنبّؤاً. |
| Customer.cs | public decimal? CreditLimit { get; private set; } | جاهزيّةُ الإطلاق · ح-٩٥ · <c>order-to-cash-procure-to-pay-inventory#13</c> | حدُّ ائتمان العميل — أقصى ما يُترك عليه مفتوحاً |
| Customer.cs | public IReadOnlyList<string> StandardInvoiceGaps() | CEEF-229 · م1-١ب | ما ينقص هذا العميل لتصحّ له فاتورةٌ قياسية — رسائل عربية تُعرض كما هي. |
| Customer.cs | public void Deactivate() => IsActive = false; | CEEF-229 · م2-١٧ |  كان يضبط DeletedAtUtc أيضاً، ومرشّح الاستعلام العامّ |
| RecurringInvoicePlan.cs | public class RecurringInvoicePlan : IBranchScoped | CEEF-230 · ط1-٥ | خطةُ فوترةٍ دورية — إيجارٌ أو اشتراكٌ أو عقدُ صيانة. |
| RecurringInvoicePlan.cs | public class RecurringInvoiceRun | CEEF-230 · ط1-٥ | أثرُ تكرارٍ وُلِّد — وهو حارسُ الازدواج. |
| SalesInvoice.cs | public long? WarehouseId { get; private set; } | جاهزيّةُ الإطلاق · ح-١١٠ · <c>order-to-cash-procure-to-pay-inventory#12</c> | المستودعُ الذي يُصرف منه البيع. وفارغُه هو |
| SalesInvoice.cs | public long? SettlesReservationId { get; private set; } | جاهزيّةُ الإطلاق · <c>sector-modules#6</c> | الحجزُ الذي تُصفّيه هذه الفاتورة — فاتورةُ المغادرة. يُضبط قبل |
| SalesInvoice.cs | public string CurrencyCode { get; private set; } = "SAR"; | CEEF-230 · ط1-٤ | عملةُ المستند — وSAR هي الأصل. |
| SalesInvoice.cs | public DateTime? PostedAtUtc { get; private set; } | O-21، CEEF-219 | تاريخ الإلغاء . الإقرار الضريبي يعكس وقائع الفترة: |
| SalesInvoice.cs | public DateTime? PostedAtUtc { get; private set; } | CEEF-229 · م2-٢٤ | لحظةُ الترحيل — لا تاريخُه وحده. |
| SalesInvoice.cs | public void AddLine(string description, decimal quantity, decimal unitPrice, long? product | CEEF-230 · ط1-٣ | ‏و الدفعةُ التي تُصرَف من المستودع. |
| SalesInvoice.cs | if (discountPercent < 0m \|\| discountPercent > 100m) | CEEF-230 · ط1-٢ | ‏الخصمُ نسبةٌ بين صفرٍ ومئة. والمئةُ مقبولة — عيّنةٌ |
| SalesInvoice.cs | if (Tax.VatExemptionReason.IsRequiredFor(vatCategory)) | CEEF-229 · م1-١ب الشقّ الثاني | الرفضُ عند بناء السطر — أبكرُ موضعٍ ممكن. |
| SalesInvoice.cs | public decimal DocumentDiscountAmount { get; private set; } | CEEF-230 · ط1-٢ | خصمُ المستند — يُوزَّع على الأوعية الضريبية لا يُطرح من الإجمالي. |
| SalesInvoice.cs | public void SetCurrency(string currencyCode, decimal exchangeRate, string? booksCurrencyCo | CEEF-230 · ط1-٤ | يضبط عملةَ المستند وسعرَ صرفها. |
| SalesInvoice.cs | string? BatchNumber = null, | CEEF-230 · ط1-٣ |  الدفعةُ المصروفة — هويّةُ ما خرج، ويلزم للصنف المتتبَّع. |
| SalesInvoice.cs | decimal DiscountPercent = 0m, | CEEF-230 · ط1-٢ | نسبةُ خصم السطر — نسبةً لا مبلغاً. |
| SalesInvoice.cs | string? UnitCode = null, | CEEF-230 · ط1-١ | وحدةُ السطر — وnull تعني وحدةَ الصنف، وهو الأصل |
| SalesInvoice.cs | DateOnly? ExpiryDate = null) | جاهزيّةُ الإطلاق · ح-١٠٨ · ‏`order-to-cash-procure-to-pay-inventory#7` | ‏ ‏انتهاءُ الدفعة المشتراة — |
| SalesInvoice.cs | public decimal LineTotal => GrossAmount - DiscountAmount; | CEEF-230 · ط1-٢ |  صار بعد الخصم. والضريبةُ على الصافي نظاماً: احتسابُها |
| SalesInvoice.cs | public SalesInvoiceLine(string Description, decimal Quantity, decimal UnitPrice, long? Pro | جاهزيّةُ الإطلاق · ح-١٠٨ | ‏ ‏منشئٌ بلا الانتهاء لـEF وحدَه — العمودُ مُهمَلٌ في ثمانية جداولَ من التسعة، |
| SalesOrder.cs | if (Tax.VatExemptionReason.IsRequiredFor(vatCategory)) | CEEF-229 · م1-١ب الشقّ الثاني | الرفضُ عند بناء السطر — أبكرُ موضعٍ ممكن. |
| SalesQuotation.cs | public void Accept() | جاهزيّةُ الإطلاق · ح-١١٥ · ‏`order-to-cash-procure-to-pay-inventory#8` | ‏ ‏القبولُ والرفضُ من «مُرسَل» وحده — |
| AppUser.cs | public string? RecoveryKeyHash { get; private set; } | جاهزيّةُ الإطلاق · <c>security-licensing-tenancy#7</c> · قرارُ المالك 2026-10-18 | ‏ تجزئةُ مفتاح الاستعادة — |
| AuditLogEntry.cs | public class AuditLogEntry | O-19، CEEF-212 | كل إدخال يحمل تجزئة محتواه مربوطة بتجزئة سابقه — سلسلة. |
| VatCategory.cs | public enum VatCategory | CEEF-207 | كانت النسبة 15% مسمّرة في كل المسارات، فيستحيل إصدار فاتورة فيها سطر |
| VatExemptionGaps.cs | public static class VatExemptionGaps | CEEF-229 · م1-١ب الشقّ الثاني | ما ينقص سطراً غيرَ خاضعٍ للنسبة الأساسية — باسمه. |
| VatExemptionReason.cs | public sealed record VatExemptionReason( | CEEF-229 · م1-١ب الشقّ الثاني | سببُ الإعفاء — قائمةٌ نظاميةٌ مغلقة، لا نصٌّ حرّ. |
| VatExemptionReason.cs | public const string ResidentialRentCode = "VATEX-SA-30"; | جاهزيّةُ الإطلاق · ح-١٠ | رمزُ إعفاء الإيجار السكنيّ — المادة 30 من اللائحة. |
| VatReturnBox.cs | public static class VatReturnBox | جاهزيّةُ الإطلاق · ح-١٦٥ · <c>tax-zatca#12</c> | خاناتُ نموذج إقرار ضريبة القيمة المضافة — كما في بوّابة الهيئة. |
| BankStatement.cs | public decimal? OpeningBalance { get; private set; } | جاهزيّةُ الإطلاق · ح-١٥٤ · <c>treasury-payroll-assets#13</c> | رصيدا الكشف كما يقولهما البنك — الافتتاحيُّ والختاميّ. |
| Cheque.cs | public JournalEntry PostDeposit(DateOnly depositDate, Account bankGlAccount, Account chequ | جاهزيّةُ الإطلاق · ح-٣٢ | تاريخُ الحدث يُمرّر — ولا يُشتقّ من |
| PaymentVoucher.cs | public long? CostCenterId { get; private set; } | CEEF-230 · ط1-٧ | مركزُ تكلفة السند — يُختم على سطور المصروف في قيده. |
| PaymentVoucher.cs | public string CurrencyCode { get; private set; } = "SAR"; | CEEF-230 · ط4-٥ | عملةُ السند — وSAR هي الأصل، و بها. |
| PaymentVoucher.cs | public decimal SettlementDiscount { get; private set; } | CEEF-231 · ط1-٢ج | خصمُ السداد المبكر المكتسَب — والحسابُ 5214 كان بلا كاتب. |
| PaymentVoucher.cs | if (SettlementDiscount > 0m) | CEEF-231 · ط1-٢ج | ‏والخصمُ يُطفئ من الذمّة ما لم يخرج من الصندوق. |
| ReceiptVoucher.cs | public string CurrencyCode { get; private set; } = "SAR"; | CEEF-230 · ط1-٤ | عملةُ السند — وSAR هي الأصل، و بها. |
| ReceiptVoucher.cs | public decimal ReceivableRelieved { get; private set; } | CEEF-230 · ط1-٤ | ما حُرِّر من ذمّة العميل بعملة الدفاتر — وهو ما قُيّد |
| ReceiptVoucher.cs | public decimal SettlementDiscount { get; private set; } | CEEF-231 · ط1-٢ج | خصمُ السداد المبكر — والحسابُ 4103 كان بلا كاتب. |
| ReceiptVoucher.cs | public JournalEntry Post(string voucherNumber, Account cashOrBankAccount, Account receivab | CEEF-230 · ط1-٤ |   هو القيمةُ التي |
| ReceiptVoucher.cs | if (SettlementDiscount > 0m) | CEEF-231 · ط1-٢ج | ‏والخصمُ يُطفئ من الذمّة ما لم يدخل الصندوق. |
| ReconciledBankEntry.cs | public long JournalLineId { get; private set; } | جاهزيّةُ الإطلاق · ح-١٥٥ · <c>treasury-payroll-assets#13</c> | ‏ سطرُ القيد المُسوّى — والصفرُ تسويةُ القيد كلِّه |
| ZatcaEInvoiceDocument.cs | public string? ClearedXmlFilePath { get; private set; } | CEEF-237 · ز-٢ | المستندُ المُخلَّص كما أعادته الهيئة — لا نسختُنا الموقَّعة محلياً. |
| ZatcaEInvoiceDocument.cs | public static ZatcaEInvoiceDocument Create( | CEEF-233 · ظ-١ | لحظةُ التوليد — تُمرَّر ولا تُقرأ هنا. |
| ZatcaEInvoiceDocument.cs | private void AttachClearedXml(string clearedXmlPath) | CEEF-237 · ز-٢ | تسجيلُ مسارِ المستند المُخلَّص العائد من الهيئة. |
| ZatcaEInvoiceDocument.cs | public void RecordSubmissionFailure(string reason) | CEEF-229 · م2-١ | تسجيل سببِ فشلِ إرسالٍ تلقائيّ — بلا تغيير الحالة. |
| ZatcaEnums.cs | InvoiceCancel = 5, | CEEF-229 · Z-3 | إشعار دائن ناتج عن إلغاء فاتورة مبيعات — مصدره فاتورة لا إشعار. |
| ZatcaEnums.cs | AssetDisposal = 7, | جاهزيّةُ الإطلاق · ح-١٦ | فاتورةُ ضريبةٍ عن بيع أصلٍ ثابت — مصدرُها |
| ZatcaEnums.cs | DisposalCancel = 8, | جاهزيّةُ الإطلاق · ح-١٦١ | إشعارٌ دائنٌ عن عكس استبعاد أصلٍ بيع بفاتورة — مصدرُه الأصلُ كفاتورته |
| ZatcaEnums.cs | Prepayment = 9, | جاهزيّةُ الإطلاق · <c>sector-modules#6</c> | فاتورةُ دفعةٍ مقدّمة (UBL 386) — عن عربون حجزٍ يومَ قبضه، وهو نقطةُ |
| ZatcaEnums.cs | SimulatedLocally = 7, | CEEF-229 · م0-١٠ | مُولَّد ومُحاكى محلياً — لم يصل الهيئة. |
| ZatcaSettings.cs | public string SigningMachineId { get; private set; } = ""; | جاهزيّةُ الإطلاق · <c>tax-zatca#16</c> | الجهازُ الذي يحمل مفتاحَ الختم — مفتاحٌ مشفَّرٌ لجهازه (DPAPI) وسرُّ الشهادة لمستخدمه، |
| ZatcaSettings.cs | UpdatedAtUtc = DateTime.UtcNow; | CEEF-229 · م0-١٠ | لا يُفرض وضع المحاكاة عند التفعيل. |
| FinancialPostingSupport.cs | public static async Task EnsureCostCenterAsync( | CEEF-230 · ط1-٧ | مركزُ تكلفةٍ موجودٌ ونشطٌ وللمنشأة نفسِها. |
| FxClosingTranslationService.cs | public sealed record FxClosingTranslationFigure( | جاهزيّةُ الإطلاق · ح-٤٦ · accounting-core#4 | فرقُ ترجمة الفترة لشركةٍ تابعةٍ بعملةٍ غير |
| FxClosingTranslationService.cs | public class FxClosingTranslationService(BrqoshDbContext db, ExchangeRateService rateServi | جاهزيّةُ الإطلاق · ح-٤٦ · accounting-core#4 |  وكان يُرحَّل قيداً في دفاتر الشركة |
| JournalPostingService.cs | private async Task EnsureApprovalAboveThresholdAsync(JournalEntry entry) | جاهزيّةُ الإطلاق · <c>accounting-core#6</c> · قرارُ المالك 2026-10-18 | القيدُ اليدويُّ فوق حدّ المنشأة لا يُرحَّل بلا اعتماد. |
| JournalPostingService.cs | private static void EnsureNoSubledgerControlAccount(JournalEntry entry) | جاهزيّةُ الإطلاق · ح-١٥٣ · <c>accounting-core#6</c> | القيدُ اليدويُّ لا يمسّ حسابَ مراقبةٍ يُبنى من دفترٍ مساعد. |
| JournalPostingService.cs | private async Task EnsureCostCentersExistAsync(JournalEntry entry) | CEEF-230 · ط1-٧ | مركزُ التكلفة يجب أن يكون موجوداً ونشطاً وللمنشأة نفسِها. |
| JournalPostingService.cs | public async Task EnsurePeriodOpenAsync(long companyId, DateOnly date) | CEEF-229 · م2-٦ | اسمُها «تحقُّق» وفعلُها كتابة. |
| JournalPostingService.cs | public async Task EnsurePeriodOpenAsync(long companyId, DateOnly date) | CEEF-229 | الفترةُ تُنشأ مرّةً واحدة — وكانت تُنشأ بعدد المرحّلين. |
| JournalReversalService.cs | if (await db.ReconciledBankEntries.AnyAsync( | جاهزيّةُ الإطلاق · ح-٩٠ · found#4 | ‏ المُسوّى مع كشف البنك لا يُعكس حتى تُلغى تسويتُه — مذهبُ المقاصّة: |
| ManualJournalApprovalPolicy.cs | public class ManualJournalApprovalPolicy(BrqoshDbContext db) | جاهزيّةُ الإطلاق · <c>accounting-core#6</c> · قرارُ المالك 2026-10-18 | حدُّ اعتماد القيد اليدويّ يغيّره المديرُ وحده — ويُسجَّل. |
| MonetaryRevaluationService.cs | public class MonetaryRevaluationService(BrqoshDbContext db) | CEEF-230 · ط4-٧ | إعادةُ تقييم الذمم بعملةٍ أجنبية آخرَ الفترة. |
| MonetaryRevaluationService.cs | var already = await db.JournalEntries | جاهزيّةُ الإطلاق · ح-٣٨ | ‏والسؤالُ عن تقييمٍ حيٍّ لا عن أثرٍ باق. |
| MonetaryRevaluationService.cs | public async Task<int> CancelAsync( | جاهزيّةُ الإطلاق · ح-٣٨ | إلغاءُ تقييمٍ أُعيد بسعرٍ خاطئ — |
| StandingOpeningBalanceItems.cs | public static class StandingOpeningBalanceItems | جاهزيّةُ الإطلاق · ح-١٣٤ | ‏ الذممُ الافتتاحيّةُ القائمة — قرارٌ واحدٌ يقرؤه كلُّ طرفٍ في الدفتر المساعد. |
| SystemAccountProvisioning.cs | public static class SystemAccountProvisioning | CEEF-230 · ط1-٤ | حسابُ نظامٍ موجودٌ — ويُنشأ من القالب إن غاب. |
| YearEndCloseService.cs | await LedgerYearLock.AcquireForClosingAsync(db.Database, companyId, year, token); | CEEF-229 · م1-١٥ / B-4 | القفلُ قبل الفحص — وإلا كان الفحصُ حكايةً. |
| YearEndCloseService.cs | var closeMark = SourceDocumentFor(year); | جاهزيّةُ الإطلاق · ح-٤٧ | ‏ ‏والسؤالُ عن إقفالٍ قائمٍ لا عن أثرٍ باق. كان |
| YearEndCloseService.cs | var pnl = await new IncomeStatementQuery(db).ExecuteAsync(companyId, yearStart, yearEnd); | CEEF-229 · م0-١٤ | حُذف حارس «قيود بعد نهاية السنة». |
| YearEndCloseService.cs | entry.MarkEntityLevel(); | CEEF-231 · O-32 | ‏الإقفالُ على الشخصية الاعتبارية لا على فرع. |
| YearEndCloseService.cs | public async Task<YearEndReopenResult> ReopenYearAsync( | جاهزيّةُ الإطلاق · ح-٤٧ · <c>accounting-core#1</c> | ‏ إعادةُ فتح سنةٍ مقفلة — وكانت |
| YearEndCloseService.cs | private async Task<Account> AccountAsync(long companyId, string code, CancellationToken ct | CEEF-229 · م0-١٣ | يتجاوز فلتر الحذف الناعم عمداً. |
| DepreciationService.cs | var lastDepreciated = await db.JournalEntries | جاهزيّةُ الإطلاق · ح-٥٨ · treasury-payroll-assets#11 | ‏ ‏آخرُ شهرٍ أُهلك — ثمّ كلُّ ما استحقّ بعده. |
| DepreciationService.cs | public async Task<int> ReverseRunAsync(long companyId, DateOnly runDate, DateOnly reversal | جاهزيّةُ الإطلاق · ح-٩٢ · <c>treasury-payroll-assets#10</c> | عكسُ تشغيل الإهلاك بتاريخه — وكان لا يُعكس: العكسُ العامُّ يرفض |
| FixedAssetAcquisitionPostingService.cs | public class FixedAssetAcquisitionPostingService(BrqoshDbContext db) | جاهزيّةُ الإطلاق · ح-٩٩ · <c>treasury-payroll-assets#12</c> |  الأصلُ وقيدُ اقتنائه في معاملةٍ واحدة — ومن حيث دُفع. |
| FixedAssetAcquisitionPostingService.cs | public async Task RegisterAwaitingInvoiceAsync(FixedAsset asset, CancellationToken ct = de | جاهزيّةُ الإطلاق · ح-١٠٠ · <c>treasury-payroll-assets#12</c> | رصيدٌ افتتاحيٌّ لأصلٍ قائمٍ قبل بدء التشغيل — مدين الأصل |
| FixedAssetAcquisitionPostingService.cs | public async Task RegisterAwaitingInvoiceAsync(FixedAsset asset, CancellationToken ct = de | جاهزيّةُ الإطلاق · ح-١٥٨ · ‏`treasury-payroll-assets#12` | ‏ يُسجّل أصلاً تقتنيه فاتورةُ مورّد — بلا قيد: فالقيدُ |
| FixedAssetCancellationService.cs | public class FixedAssetCancellationService(BrqoshDbContext db) | جاهزيّةُ الإطلاق · ح-١٥٧ · <c>treasury-payroll-assets#10</c> | إلغاءُ اقتناء أصلٍ أُدخل خطأً — بعكس قيده. |
| FixedAssetDisposalPostingService.cs | public async Task<JournalEntry> PostDisposalAsync( | CEEF-232 · ض-٢ | المقابلُ قبل الضريبة — لا المحصَّل. |
| FixedAssetDisposalPostingService.cs | public async Task<JournalEntry> PostDisposalAsync( | CEEF-232 · ض-٢ | فئةُ التوريد — والأساسيّةُ هي الأصل: بيعُ أصلٍ من أصول |
| FixedAssetDisposalPostingService.cs | public async Task<JournalEntry> PostDisposalAsync( | جاهزيّةُ الإطلاق · ح-١٦ | المشتري — ولم يكن للاستبعاد مشترٍ قطّ. |
| FixedAssetDisposalPostingService.cs | saleProceeds = Math.Round(saleProceeds, 2, MidpointRounding.AwayFromZero); | CEEF-229 · م0-١٥ | العائد يُقرَّب إلى منزلتين قبل أيّ استعمال. |
| FixedAssetDisposalPostingService.cs | var company = await db.Companies.SingleAsync(c => c.Id == asset.CompanyId, token); | CEEF-232 · ض-٤ | ‏ومنشأةٌ غير مسجَّلةٍ لا تُصدر ضريبةً أصلاً. |
| FixedAssetDisposalPostingService.cs | var vat = Math.Round( | CEEF-232 · ض-٢ | ‏وحسابُ المخرجات يُقرأ عند الحاجة وحدَها — فمنشأةٌ لا تبيع |
| FixedAssetDisposalPostingService.cs | if (saleProceeds + vat > 0) | CEEF-232 · ض-٢ | ‏والنقدُ الداخل هو المقابلُ زائد الضريبة — وهو ما يقبضه |
| FixedAssetDisposalPostingService.cs | if (saleProceeds > 0 && effectiveCategory != VatCategory.OutOfScope) | جاهزيّةُ الإطلاق · ح-١٦ | ‏وبيعُ أصلٍ توريدٌ خاضعٌ نظاماً — فله فاتورةُ ضريبة. |
| FixedAssetDisposalReversalService.cs | public class FixedAssetDisposalReversalService(BrqoshDbContext db) | جاهزيّةُ الإطلاق · ح-١٦١ · <c>treasury-payroll-assets#10</c> | عكسُ استبعاد أصل — وبيعُه المُفوتَرُ يُلغى بإشعارٍ دائن. |
| BusinessProfileChangeService.cs | public class BusinessProfileChangeService(BrqoshDbContext db) | جاهزيّةُ الإطلاق · <c>ui-ux-i18n-printing#7</c> · قرارُ المالك 2026-10-18 | ملفُّ النشاط يغيّره المديرُ بعد الإعداد — بحارس. |
| ConstructionProjectService.cs | await new ContractRevenueTrueUp(db).ApplyAsync(companyId, projectId, DateOnly.FromDateTime | جاهزيّةُ الإطلاق · ح-٨٠ | ‏ ‏ومراجعةُ التقدير أو قيمة العقد لحاقٌ تراكميّ — يُقيَّد يومَ المراجعة. |
| ContractRevenueTrueUp.cs | public class ContractRevenueTrueUp(BrqoshDbContext db) | جاهزيّةُ الإطلاق · ح-٨٠ · <c>sector-modules#3</c> | إيرادُ عقد المقاولات يُسوّى تراكميّاً بنسبة الإنجاز — وكان كلُّ مستخلصٍ |
| ProgressBillingCancellationService.cs | await new ContractRevenueTrueUp(db).ApplyAsync(companyId, billing.ProjectId, cancellationD | جاهزيّةُ الإطلاق · ح-٨٠ | ‏ ‏وإلغاءُ المستخلص يُعيد العقدَ إلى ما اكتُسب بتاريخ الإلغاء. |
| ProgressBillingCancellationService.cs | if (settings?.IsEnabled == true) | CEEF-229 · م1-٦ / Z-4 | التوليدُ داخل المعاملة — والإرسالُ خارجها. |
| ProgressBillingPostingService.cs | if (billing.Id == 0) | CEEF-230 · ط2-٠ | ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ JournalPostingService نفسُه. |
| ProgressBillingPostingService.cs | await new ContractRevenueTrueUp(db).ApplyAsync(billing.CompanyId, billing.ProjectId, billi | جاهزيّةُ الإطلاق · ح-٨٠ | ‏ ‏والمستخلصُ يُعاد إلى موضعه من الإنجاز — ما فُوتر فوق المكتسب التزامُ عقد، وما سبقه أصلُ عقد. |
| ProjectCostService.cs | await new ContractRevenueTrueUp(db).ApplyAsync(companyId, projectId, costDate, token); | جاهزيّةُ الإطلاق · ح-٨٠ | ‏ ‏والتكلفةُ تُغيّر نسبةَ الإنجاز فيُسوّى إيرادُ العقد — قُيّدت هنا أو جاءت من مستندٍ آخر. |
| ProjectCostService.cs | public async Task<Account> CreditAccountAsync(long companyId, string? code, CancellationTo | جاهزيّةُ الإطلاق · ح-١٤٥ · <c>sector-modules#9</c> | الحسابُ الدائنُ يُختار — لا الصندوقُ دائماً. |
| ClaimSettlementService.cs | public class ClaimSettlementService(BrqoshDbContext db) | CEEF-230 · ط3-٢ب | يُخرج المرفوضَ من ذمّة الشركة — بمستندٍ لا بقيدٍ يدويّ. |
| PharmacyDispenseService.cs | public class PharmacyDispenseService(BrqoshDbContext db) | CEEF-231 · ط3-٢ج | صرفُ دواءِ الزيارة من المستودع — الرابطُ الغائب في كلّ مستوصف. |
| VisitBillingService.cs | public class VisitBillingService(BrqoshDbContext db) | CEEF-230 · ط3-٢ | فوترةُ الزيارة — فاتورةٌ للمريض وأخرى لشركة التأمين. |
| VisitBillingService.cs | await new PharmacyDispenseService(db).DispenseForVisitAsync( | CEEF-231 · ط3-٢ج | ‏وما صُرف من الصيدلية يخرج من المستودع هنا. |
| VisitBillingService.cs | foreach (var invoiceId in new[] { result.PatientInvoiceId, result.PayerInvoiceId }) | CEEF-230 · ط6-٧ | ‏والمستندُ وُلِّد داخل المعاملة، والإبلاغُ من هنا — بعد |
| FolioService.cs | public class FolioService(BrqoshDbContext db) | CEEF-230 · ط3-٣ب | حسابُ النزيل — الليالي تُقيَّد ليلةً بليلة، والحساب يُصفّى مرّةً. |
| FolioService.cs | await new RoomNightAccrual(db).AccrueAsync( | جاهزيّةُ الإطلاق · ح-٨٢ | ‏والليلةُ تبلغ الأستاذَ في شهرها — كانت تُقيَّد في حساب النزيل وحدَه، فيُقفل |
| FolioService.cs | await new RoomNightAccrual(db).ReleaseAsync(companyId, folio.Id, on, token); | جاهزيّةُ الإطلاق · ح-٨٢ | ‏والفاتورةُ تحمل الليالي كلَّها، فيُطلَق ما استُحقّ منها — وإلّا اعتُرف بإيرادها |
| FolioService.cs | await new ReservationDepositService(db) | CEEF-232 · ح-١ | ‏والعربونُ يُنزَل هنا — داخلَ معاملة المغادرة نفسِها. |
| FolioService.cs | await ZatcaPostingOrchestrator.SubmitAfterCommitAsync( | CEEF-230 · ط6-٧ | ‏والمستندُ وُلِّد مع الترحيل، والإبلاغُ بعد الـcommit — |
| FolioService.cs | public Task CancelAsync(long companyId, long reservationId, DateOnly on, DepositOutcome de | جاهزيّةُ الإطلاق · ح-٨٣ · <c>sector-modules#5</c> | يُلغي حجزاً قائماً — وعربونُه يُردّ أو يُصادَر. |
| FolioService.cs | public Task MarkNoShowAsync(long companyId, long reservationId, DateOnly on, DepositOutcom | جاهزيّةُ الإطلاق · ح-٨٣ | لم يحضر النزيل — من يوم الوصول لا قبله؛ وعربونُه يُردّ أو يُصادَر. |
| KitchenIssueService.cs | public class KitchenIssueService(BrqoshDbContext db) | CEEF-230 · ط3-٣ | بيعُ طبقٍ يصرف مكوّناتِه — وهذا هو الرابطُ الغائب في كلّ مطعم. |
| KitchenIssueService.cs | var tracked = await TrackedProductsAsync(companyId, required, token); | CEEF-231 · ط1-٣ج | ‏والمتتبَّعُ يُصرف بالأقدم انتهاءً — ولم يكن يُصرف. |
| KitchenIssueService.cs | public async Task EnsureSaleProductLinkableAsync(long companyId, long? saleProductId, | CEEF-230 · ط3-٣ج | يتحقّق أنّ صنفَ البيع صالحٌ لأن يُربط بوصفة. |
| KitchenIssueService.cs | public async Task<KitchenIssueResult?> IssueForSoldProductsAsync( | CEEF-230 · ط3-٣ج | يصرف مكوّناتِ ما بيع من أصنافٍ مرتبطةٍ بوصفات — ويصمت عن غيرها. |
| KitchenIssueService.cs | private async Task<HashSet<long>> TrackedProductsAsync(long companyId, | CEEF-231 · ط1-٣ج | أيُّ المكوّنات متتبَّعٌ — فيُخصَّص بالأقدم انتهاءً. |
| ReservationDepositService.cs | public enum DepositOutcome | جاهزيّةُ الإطلاق · ح-٨٣ |  ما يُصنع بعربون حجزٍ أُلغي أو غاب صاحبُه. |
| ReservationDepositService.cs | public class ReservationDepositService(BrqoshDbContext db) | CEEF-232 · ح-١ | عربونُ الحجز — مالٌ يُقبض قبل أن يُستحقّ. |
| ReservationDepositService.cs | var (net, vat) = SplitVatInclusive(amount); | جاهزيّةُ الإطلاق · ح-٨١ · `sector-modules#6` | ‏ ‏والعربونُ نقطةُ استحقاقٍ للضريبة — يوم القبض. |
| ReservationDepositService.cs | if (vat > 0m) | جاهزيّةُ الإطلاق · sector-modules#6 | ‏ ‏وللعربون فاتورةُ دفعةٍ مقدّمة (386) يومَ قبضه — والتوليدُ داخل المعاملة |
| ReservationDepositService.cs | internal async Task SettleOnCancellationAsync( | جاهزيّةُ الإطلاق · ح-٨٣ | حجزٌ أُلغي أو غاب صاحبُه — وعربونُه يُردّ أو يُصادَر. وكان لا مسارَ له إلّا الإنزالُ عند |
| ReservationDepositService.cs | if (refund) | جاهزيّةُ الإطلاق · sector-modules#6 | ‏ ‏والردُّ إشعارٌ دائنٌ لكلّ فاتورةِ دفعة (386) — يُشير إليها بمبلغها. والمصادرةُ |
| RoomNightAccrual.cs | public class RoomNightAccrual(BrqoshDbContext db) | جاهزيّةُ الإطلاق · ح-٨٢ · <c>sector-modules#4</c> | ليالي الإقامة تبلغ الأستاذَ في شهرها — وكانت تُقيَّد في حساب النزيل |
| TipPoolService.cs | public class TipPoolService(BrqoshDbContext db) | CEEF-230 · ط3-٣د | يُقيّد الإكرامياتِ التزاماً لا إيراداً — ثمّ يدفعها. |
| TipPoolService.cs | var bank = bankAccountCode is null | جاهزيّةُ الإطلاق · ح-٤٨ · `sector-modules#8` | ‏ ‏البنكُ من مالكه لا من قيمةٍ |
| InfrastructureServiceCollectionExtensions.cs | options.UseNpgsql(connectionString, npgsql => npgsql.ExecutionStrategy(deps => | CEEF-231 · ط5-٧ | ‏والمساران معاً — ودرسُ ط4-٣: إصلاحُ أحد |
| InfrastructureServiceCollectionExtensions.cs | options.AddInterceptors(new AuditLogImmutabilityInterceptor(), new ClosedPeriodPostingInte | جاهزيّةُ الإطلاق · ح-٧٨ | ‏ ‏وحارسُ الفترة المقفلة عند الحفظ — كما في المصنع. |
| InfrastructureServiceCollectionExtensions.cs | services.AddScoped<Sales.RecurringInvoiceService>(); | CEEF-230 · ط6-٣ | ‏خدمةُ الفوترة الدورية — تُطلَب من الشاشة، فتُسجَّل. |
| InfrastructureServiceCollectionExtensions.cs | services.AddScoped<RealEstate.LeaseService>(); | CEEF-230 · ط6-٤ | ‏خدماتُ القطاعات — تُطلَب من شاشاتها، فتُسجَّل. |
| InfrastructureServiceCollectionExtensions.cs | services.AddScoped<Payroll.EndOfServiceAccrualService>(); | CEEF-230 · ط6-٥ | ‏تخصيصُ مكافأة نهاية الخدمة — ولم يكن مسجَّلاً في الحقن |
| InfrastructureServiceCollectionExtensions.cs | services.AddScoped<Payroll.PayrollPaymentService>(); | جاهزيّةُ الإطلاق · ح-٨٤ | ‏صرفُ الرواتب وسدادُ التأمينات ودفعُ مستحقّ نهاية الخدمة — وكان 2108 و2109 لا يُدينهما شيء. |
| InfrastructureServiceCollectionExtensions.cs | services.AddScoped<Treasury.BankStatementImportService>(); | جاهزيّةُ الإطلاق · ح-٨٥ | ‏أربعٌ تطلبها الشاشاتُ بـGetService ولم تكن تُحَلّ — ثلاثٌ تُبنى بـnew داخل محوِّلات |
| InfrastructureServiceCollectionExtensions.cs | services.AddScoped<Accounting.MonetaryRevaluationService>(); | CEEF-230 · ط4-٧ | ‏إعادةُ تقييم الذمم بالعملات — إلزامُ المعيار 21. |
| InfrastructureServiceCollectionExtensions.cs | services.AddScoped<Operations.OffsiteBackupService>(); | CEEF-231 · ط5-٤ | ‏تكرارُ النسخة خارج الجهاز. |
| InfrastructureServiceCollectionExtensions.cs | services.AddScoped<Operations.UpdateCheckService>(); | CEEF-231 · ط5-٥ | ‏فحصُ قناة التحديث. |
| InfrastructureServiceCollectionExtensions.cs | services.AddScoped<Operations.DataRetentionService>(); | CEEF-231 · ط5-١١ | ‏تقليمُ ما يجوز — ولا يمسّ سجلَّ التدقيق ولا المعلَّق في الطابور. |
| InfrastructureServiceCollectionExtensions.cs | OutboxDispatchHook.Wire((db, ct) => | ع-D2، CEEF-204 | Outbox : تفريغ الطابور بعد الالتزام حصراً. |
| InfrastructureServiceCollectionExtensions.cs | return VatReturnMapping.From(r); | CEEF-229 · م1-١٠ | العبورُ في موضعٍ يُقاس — لا سطراً داخل مُحوِّلٍ لا يُرى. |
| BatchAllocationService.cs | public class BatchAllocationService(BrqoshDbContext db) | CEEF-231 · ط1-٣ج | الصرفُ بالأقدم انتهاءً — FEFO. |
| CostingMethodService.cs | public class CostingMethodService(BrqoshDbContext db) | CEEF-229 · م1-٢٢ / I-3 | تبديلُ طريقة تكلفة المخزون — ممنوعٌ ما دام هناك مخزون. |
| InventoryCostingService.cs | public async Task<bool> AddCostLayerAsync( | CEEF-229 · م0-٥ | ‏تسجيلُ طبقة التكلفة وحدها، بلا لمس المتوسط المرجّح. |
| InventoryCostingService.cs | public async Task<(decimal TotalCost, decimal UnitCost)> ConsumeForIssueAsync( | CEEF-229 · م0-٣ |  إلزاميّ — وهو مفتاحُ ترميم |
| InventoryCostingService.cs | public async Task<bool> RestoreConsumedLayersAsync( | CEEF-229 · م0-٣ | يُعيد إلى كلّ طبقةٍ ما أُخذ منها بعينه — لا يُنشئ طبقةً جديدة. |
| InventoryCostingService.cs | public async Task<bool> ReverseReceiptLayersAsync( | CEEF-229 · م0-٣ب | يُزيل الطبقةَ التي أنشأها مستندٌ داخل — مرآةُ الترميم. |
| InventoryCostingService.cs | await db.SaveChangesAsync(ct); | CEEF-229 · م0-٣ج | يُحفَظ قبل إعادة الحساب. RemoveRange تُعلّم الصفوفَ للحذف |
| InventoryCostingService.cs | if (preferredSources is { Count: > 0 }) | جاهزيّةُ الإطلاق · ح-١٠٧ | ‏ ‏مصادرُ مفضَّلةٌ تُستهلك أوّلاً — مرتجعُ المشتريات يُخرج طبقاتِ فاتورته لا أقدمَ ما في المستودع، |
| InventoryCostingService.cs | if (layer.RemainingQuantity <= 0) continue; | جاهزيّةُ الإطلاق · ح-١٠٣ | ‏ ‏طبقةٌ استنفدها سطرٌ سابقٌ في المستند نفسِه تُتخطّى. الشرطُ «المتبقّي موجب» يُقيَّم في |
| InventoryService.cs | public class InventoryService(BrqoshDbContext db) | CEEF-229 · م2-١٨ |  قراءات الأصناف هنا تتجاوز مرشّح الحذف الناعم عمداً. |
| InventoryService.cs | private readonly HashSet<(string Source, bool Inbound)> _costReversed = []; | جاهزيّةُ الإطلاق · ح-١١٢ | ‏ ‏مصادرُ عُكست تكلفتُها في وحدة العمل هذه، باتّجاهها. الطبقاتُ مفتاحُها المستند لا الحركة، والإلغاءُ يعكس |
| InventoryService.cs | if (warehouse is null | CEEF-229 · م2-١٧د | الاختراعُ لمن لا مستودعَ له، لا لمن أوقف مستودعاته. |
| InventoryService.cs | var remainingQty = await GetCompanyOnHandAsync(companyId, productId); | CEEF-228 | المستدعيان (إلغاء فاتورة مشتريات وإلغاء إذن استلام) يخصمان الرصيد |
| InventoryService.cs | public Task<StockMovement> PostDocumentMovementAsync(long companyId, long warehouseId, | CEEF-230 · ط1-٣ | ‏التحميلُ القديم — مسارٌ بلا هويّةِ دفعة. |
| InventoryService.cs | public async Task<StockMovement> TransferAsync(long companyId, long fromWarehouseId, long  | CEEF-231 · ط4-٤ |  وما كان: التحويلُ يُحرّك الكمّياتِ ولا يمسّ |
| InventoryService.cs | var number = await DocumentNumberGenerator.NextAsync(db, | CEEF-231 · ط4-٤ | ‏الرقمُ يُخصَّص قبل استهلاك الطبقات — فهو مفتاحُ سجلّ |
| InventoryService.cs | await ApplyBalanceDeltaAsync(companyId, fromWarehouseId, productId, -qty, | CEEF-230 · ط1-٣ | ‏التحويلُ لا يحمل هويّةَ دفعةٍ بعد — ووسيطُه |
| InventoryService.cs | public static void EnsureTradable(IReadOnlyDictionary<long, Product> products, string acti | CEEF-229 · م2-١٧ب | الإيقافُ يمنع تجارةً جديدة — لا يمنع ردَّ ما بيع. |
| InventoryService.cs | public async Task ReverseDocumentMovementAsync(StockMovement movement) | CEEF-229 · م0-١ | عكسٌ كاملٌ لحركة مخزنية: الكمية والتكلفة معاً. |
| InventoryService.cs | if (movement.Type == StockMovementType.Transfer) | CEEF-231 · ط4-٤ | ‏والتحويلُ يُعكَس في الطرفين معاً. |
| InventoryService.cs | if (!string.IsNullOrWhiteSpace(movement.SourceDocument)) | CEEF-229 · م0-٣ | الصادرُ يُرمّم طبقاتِه المستهلَكة — لا يُنشئ طبقةً جديدة. |
| InventoryService.cs | if (!restored) | جاهزيّةُ الإطلاق · ح-٣١ب | ‏و«لا سجلّ» لا يُبتلع. |
| InventoryService.cs | if (!string.IsNullOrWhiteSpace(movement.SourceDocument) && _costReversed.Contains((movemen | CEEF-229 · م0-٣ب | وللداخل: تُزال الطبقةُ التي أنشأها هذا المستند بعينها. |
| InventoryService.cs | foreach (var group in movement.Lines.GroupBy(l => l.ProductId)) | CEEF-229 · م0-٥ / I-NEW-2 | ‏التجميعُ بالصنف قبل عكس التكلفة. |
| InventoryService.cs | var batch = BatchRef.From(line.BatchNumber, line.ExpiryDate); | CEEF-230 · ط1-٣ | ‏الدفعةُ تُقرأ من السطر المحفوظ — فيعود ما خرج بعينه. |
| InventoryService.cs | private async Task ApplyBalanceDeltaAsync(long companyId, long warehouseId, long productId | CEEF-230 · ط1-٣ | المَخنَقُ الوحيد لكلّ تغيّرٍ في كمّية المخزون. |
| InventoryService.cs | private async Task ApplyBatchDeltaAsync(long companyId, long warehouseId, long productId, | CEEF-230 · ط1-٣ | يفرض هويّةَ الدفعة ويُحرّك رصيدَها. |
| InventoryService.cs | if (delta < 0 && type == StockMovementType.Issue && !expiredMayLeave && row.IsExpiredOn(on | جاهزيّةُ الإطلاق · ح-١٠٥ | ‏ ‏والمنتهيةُ تخرج إلى مورّدها وحدَه — expiredMayLeave يمرّره مرتجعُ المشتريات لا غيرُه، |
| LandedCostAllocationService.cs | var creditAccount = creditCash | جاهزيّةُ الإطلاق · ح-٢٧ | ‏التكلفةُ الإضافيّةُ غيرُ المدفوعة مصروفٌ مستحقّ |
| StockBalanceQuery.cs | bool IsStopped = false); | CEEF-229 · م2-١٧ب/ج | صنفٌ موقوفٌ — أو مستودعٌ موقوف — ما زالت بضاعتُه قائمة: |
| StockBalanceQuery.cs | var products = await db.Products | CEEF-229 · م2-١٧ب | الإيقافُ لا يُخفي البضاعة. |
| StockCountAdjustmentService.cs | public Task<int> PostVariancesAsync( | CEEF-229 · م2-٢٤ |  OpeningUnitCost تكلفةٌ يُدخلها المالك لصنفٍ لا تكلفة |
| StockCountAdjustmentService.cs | public async Task<int> PostVariancesAsync( | جاهزيّةُ الإطلاق · ح-١٠٩ · <c>order-to-cash-procure-to-pay-inventory#7</c> | ‏الجردُ بالدفعة. الصنفُ المتتبَّع يُجرد بدفعاته: |
| StockCountAdjustmentService.cs | var inventory = new InventoryService(db); | CEEF-229 · م1-١٢ / A-5 | ‏اللقطةُ تُعاد قراءتُها هنا — داخل المعاملة. |
| StockCountAdjustmentService.cs | var inventory = new InventoryService(db); | جاهزيّةُ الإطلاق · ح-١٠٩ · `order-to-cash-procure-to-pay-inventory#7` | ‏ ‏والمتتبَّع يُقارَن بدفعته لا بمجموعه — وبلا دفعةٍ يُرفض: |
| StockMovementCancellationService.cs | await new InventoryService(db).ReverseDocumentMovementAsync(movement); | CEEF-229 · م0-١ | عكسٌ كامل عبر المسار الموحّد — كمية وتكلفة. |
| StockMovementPostingService.cs | var costing = new InventoryCostingService(db); | CEEF-229 · م0-٥ / I-NEW-2 | ‏المتوسطُ يُحسب لكلّ صنفٍ مرّةً، والطبقةُ لكلّ سطر. |
| StockMovementPostingService.cs | if (movement.Type == StockMovementType.Transfer && movement.ToWarehouseId is long destinat | CEEF-231 · ط4-٤ | ‏والتحويلُ ينقل طبقاتِ التكلفة معه. |
| StockMovementPostingService.cs | if (movement.Id == 0) | CEEF-230 · ط2-٠ | ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ JournalPostingService نفسُه. |
| StockMovementPostingService.cs | return movement.Type switch | CEEF-229 · م2-٢١ | حركةٌ قيمتُها صفر لا قيدَ لها — لا قيدٌ بأصفار. |
| StockMovementPostingService.cs | var afterTheOpeningTrialBalance = await db.JournalEntries | جاهزيّةُ الإطلاق · ح-١٣٥ | ‏ بعد الميزان الافتتاحيّ تُدائَن 3107 لا الأرباحُ المبقاة: الميزانُ أقام حقوقَ الملكية |
| StockMovementPostingService.cs | private async Task<JournalEntry?> AdjustmentInEntryAsync(StockMovement movement, Account i | جاهزيّةُ الإطلاق · ح-٢٣ | فائضُ الجرد ربحُ الفترة — لا إضافةٌ إلى حقوق الملكية. |
| UnitConversionService.cs | public class UnitConversionService(BrqoshDbContext db) | CEEF-230 · ط1-١ | تحويلُ كمّيةٍ من وحدة السطر إلى وحدة الصنف. |
| ClockRollbackDetector.cs | public static class ClockRollbackDetector | CEEF-237 · ت-١٢ | رجوعُ ساعة الجهاز يُكشف — وكانت الصلاحيةُ تُقاس بها وحدَها. |
| EmbeddedLicenseKeys.cs | internal static class EmbeddedLicenseKeys | CEEF-236 · ت-١/ت-٢ | مفتاحُ الثقة العامّ — مُضمَّنٌ في التجميعة فعلاً. |
| LicenseStore.cs | public static string MachineId() | CEEF-237 · ت-٥ | هويّةُ الجهاز صارت تصمد لتحديث ويندوز وتبديل الحساب. |
| LicenseStore.cs | if (!AppEnvironmentConfig.IsProduction && | CEEF-228 | تجاوز الترخيص للتدقيق الآلي - معطّل قسرياً في الإنتاج. |
| LicensedModules.cs | public static class LicensedModules | جاهزيّةُ الإطلاق · ح-١٦٤ · security-licensing-tenancy#2 | ‏ الوحداتُ التي يمنحها ترخيصٌ موقَّع — من حقل Features. |
| ReadOnlyBooks.cs | public static class ReadOnlyBooks | جاهزيّةُ الإطلاق · ح-١٢٠ · ‏`commercial-readiness#6` | ‏ وضعُ القراءة فقط — حين انقضى ترخيصٌ أصيل. |
| ReadOnlyBooks.cs | public static class LicenseClock | جاهزيّةُ الإطلاق · ح-١٢٧ · ‏`security-licensing-tenancy#1` | ‏ الوقتُ الذي تُقاس عليه الصلاحية — لا ساعةُ الجهاز وحدَها. |
| RsaLicenseValidator.cs | public enum LicenseVerdict | CEEF-236 · ت-١ | حكمُ التحقّق — سببٌ مسمّىً لا «غير صالح». |
| RsaLicenseValidator.cs | public static LicenseVerdict Validate(string companyName, string machineId, string license | CEEF-237 · ت-٥ | معرّفٌ بديلٌ مقبولٌ لهذا الجهاز — يُمرَّر عند الانتقال من الصيغة |
| RsaLicenseValidator.cs | public static bool IsGenuineButExpired(string companyName, string machineId, string licens | جاهزيّةُ الإطلاق · ح-١٢٠ | ‏ ترخيصٌ أصيلٌ انقضت صلاحيتُه — منشأتُه وجهازُه وتوقيعُه صحيحة، وتاريخُه فات. |
| RsaLicenseValidator.cs | return LicenseVerdict.Malformed; | CEEF-236 | ‏GetString() على عنصرٍ ليس نصّاً (‏"signature": 12‎) يرمي هذا لا |
| RsaLicenseValidator.cs | var candidates = new List<string> { machineId.Trim() }; | CEEF-237 · ت-٥ | ‏ يُقبل المعرّفُ المطلوب، أو معرّفٌ بديلٌ يُمرَّر صراحةً. |
| CodSettlementService.cs | public class CodSettlementService(BrqoshDbContext db) | CEEF-230 · ط3-٤ | الدفعُ عند الاستلام — مالُ التجّار في يد الناقل. |
| CodSettlementService.cs | var alreadyPosted = shipments.Where(s => s.CodCollectionPosted).ToList(); | جاهزيّةُ الإطلاق · ح-١٢ | ‏ ‏وبوليصةٌ أُثبت تحصيلُها لا تُثبَت ثانية. |
| CodSettlementService.cs | var due = await db.Shipments | جاهزيّةُ الإطلاق · ح-١٢ | ‏ ‏والمستحقُّ هو ما قُيِّد تحصيلُه، لا ما سُلِّم. |
| CodSettlementService.cs | var cash = await BankGlAccountResolver.ResolveCodeAsync(db, companyId, cashAccountCode, ct | جاهزيّةُ الإطلاق · ح-٤٨ · `sector-modules#8` | ‏ ‏التوريدُ من البنك بمالكه لا بقيمةٍ |
| DeliveryTripService.cs | public class DeliveryTripService(BrqoshDbContext db) | CEEF-230 · ط3-٤ب | عهدةُ الرحلة ومصاريفُها وربحيّةُ المسار. |
| ShipmentBillingService.cs | public class ShipmentBillingService(BrqoshDbContext db) | CEEF-232 · ل-١ | أجرةُ الشحن — إيرادُ القطاع كلِّه، ولم تكن تبلغ الأستاذ أبداً. |
| BillOfMaterialsService.cs | public class BillOfMaterialsService(BrqoshDbContext db) : Brqosh.Application.Manufacturing | جاهزيّةُ الإطلاق · ح-٧٧ · <c>manufacturing#5</c> | تعديلُ قائمة الموادّ يُسجَّل — وكان يُكتب من النافذة بلا أثر. |
| ProductionCostSheetQuery.cs | public sealed class ProductionCostSheetQuery(BrqoshDbContext db) | جاهزيّةُ الإطلاق · ح-١٢٢ · ‏`manufacturing#9` | ‏ كشفُ تكلفة الإنتاج واستهلاكُ الموادّ — من الدفتر لا من حسابٍ موازٍ. |
| WipQuery.cs | public class WipQuery(BrqoshDbContext db) | CEEF-230 · ط6-٨ | الإنتاجُ تحت التنفيذ — وكانت الشاشةُ تسقط بقائمةِ موادٍ محذوفة. |
| WorkOrderService.cs | SecurityContext.EnsureCanPost(); | CEEF-229 · م2-١ | جارتاها في هذا الملف محروستان (CompleteAsync بـ EnsureCanPost |
| WorkOrderService.cs | var bom = await db.BillsOfMaterials.SingleAsync(b => b.CompanyId == companyId && b.Id == o | جاهزيّةُ الإطلاق · ح-٧٧ | ‏ ‏والوصفةُ تُجمَّد عند الإصدار — فتُقرأ القائمةُ قبل حجز الرقم، |
| WorkOrderService.cs | public async Task<WorkOrder> CompleteAsync(long companyId, long workOrderId, decimal produ | جاهزيّةُ الإطلاق · ح-٣٤ | الإكمالُ يُؤرّخ يومَ وقوعه لا يومَ |
| WorkOrderService.cs | public async Task<WorkOrder> CompleteAsync(long companyId, long workOrderId, decimal produ | جاهزيّةُ الإطلاق · ح-١٠١ · <c>manufacturing#6</c> |  دفعةُ الإنتاج — مطلوبةٌ لمنتجٍ تامٍّ |
| WorkOrderService.cs | public async Task<WorkOrder> CompleteAsync(long companyId, long workOrderId, decimal produ | جاهزيّةُ الإطلاق · ح-١٥٩ · <c>manufacturing#6</c> | أرقامُ التامّ المتتبَّع تسلسليّاً — رقمٌ لكلّ وحدةٍ منتَجة، بعددها |
| WorkOrderService.cs | if (order.Status == WorkOrderStatus.Released && !order.RecipeFrozen) | جاهزيّةُ الإطلاق · ح-٧٧ · ‏`manufacturing#5` | ‏ ‏الوصفةُ وصفةُ الأمر يومَ صدوره — لا وصفةُ القائمة اليوم. |
| WorkOrderService.cs | var materialsWarehouseId = await WarehouseForAsync(inv, companyId, order.MaterialsWarehous | جاهزيّةُ الإطلاق · ح-٩٧ · ‏`manufacturing#7` | ‏ ‏مستودعا الأمر — لا الافتراضيُّ أبداً. |
| WorkOrderService.cs | var finished = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == bom.Finishe | جاهزيّةُ الإطلاق · ح-١٠١ · ‏`manufacturing#6` | ‏ ‏التامُّ المتتبَّعُ يدخل بدفعته — ويُسأل عنها قبل أيّ صرف. |
| WorkOrderService.cs | decimal materialCost; | جاهزيّةُ الإطلاق · ح-١٤٧ · ‏`manufacturing#4` | ‏ ‏أمرٌ صُرف له فعلاً يُكمَل بما صُرف — لا بالوصفة. |
| WorkOrderService.cs | var labourCost = Math.Round( | جاهزيّةُ الإطلاق · ح-١٣ | ‏ ‏وتكاليفُ التحويل تدخل التكلفة — وكانت الموادُّ وحدَها. |
| WorkOrderService.cs | var byProductLines = await ByProductLinesAsync(companyId, bom.FinishedProductId, byProduct | جاهزيّةُ الإطلاق · ح-١٤٩ · ‏`manufacturing#4` | ‏ ‏المنتجُ الثانويّ يدخل المخزونَ بقيمته المعلنة، وتُطرح من تكلفة الرئيسيّ |
| WorkOrderService.cs | var wip = await SystemAccountProvisioning.EnsureAsync( | جاهزيّةُ الإطلاق · ح-٩٨ · ‏`manufacturing#8` | ‏ ‏تحت التشغيل حسابُ المصنع — لا أصلُ عقود المقاولات (1113). |
| WorkOrderService.cs | public async Task<decimal> IssueMaterialsAsync(long companyId, long workOrderId, | جاهزيّةُ الإطلاق · ح-١٤٧ · <c>manufacturing#4</c> | صرفُ موادّ فعليٌّ للأمر قبل إكماله — بالكميّة التي خرجت. |
| WorkOrderService.cs | var (totalCost, componentUnitCost) = await costing.ConsumeForIssueAsync( | جاهزيّةُ الإطلاق · ح-٣٠ | ‏مفتاحُ الاستهلاك هو مفتاحُ الحركة بعينه (WorkOrder:{Id}) — فيُرمّم الإلغاءُ ما استُهلك. |
| WorkOrderService.cs | public async Task<bool> OutputIsTrackedAsync(long companyId, long workOrderId, Cancellatio | جاهزيّةُ الإطلاق · ح-١٠١ | هل منتجُ الأمر التامُّ متتبَّع — فتسأل الشاشةُ عن دفعة الإنتاج قبل الإكمال. |
| WorkOrderService.cs | var inv = new InventoryService(db); | جاهزيّةُ الإطلاق · ح-٣٠ | ‏يُعكَس الأصلُ — لا تُرحَّل حركةٌ مضادّة. |
| WorkOrderService.cs | var inv = new InventoryService(db); | ‏CEEF-229 · م0-٣ و م0-٣ب | ‏ . وخمسُ خدماتِ إلغاءٍ تستعمله، وهذه لم تكن منها — |
| BackupScheduleService.cs | public sealed class BackupScheduleService(BackupService backups) | CEEF-237 · ت-٤ | النسخُ الاحتياطيّ صار يقع من نفسِه — وكان زرّاً ينتظر من يضغطه. |
| BackupService.cs | public sealed class BackupService(BrqoshDbContext db, string? preRestoreFolder = null) | CEEF-229 · م3-٢ | نسخٌ احتياطيّ واستعادة — وكانا غائبَين عن المنتج كلِّه. |
| BackupService.cs | internal const string CompanionSuffix = ".zatca.zip"; | جاهزيّةُ الإطلاق · ح-٥ | رفيقُ النسخة — ما لا تحمله القاعدة. |
| BackupToolReadiness.cs | public static class BackupToolReadiness | جاهزيّةُ الإطلاق · ح-١٢٥ · ‏`installer-deployment-ops#10` · ‏`data-upgrade-performance#6` | ‏ أدواتُ النسخ تُفحص قبل أوّل نسخة. |
| CrashLog.cs | public static class CrashLog | CEEF-231 · ط5-٦ | سجلُّ الأعطال — وبغيره لا يُشخَّص عطلٌ في الميدان. |
| CrashLog.cs | private static long _sequence; | جاهزيّةُ الإطلاق | تسلسلٌ في العمليّة يجعل الاسمَ لا يتكرّر — والتقليمُ كان يُعيده. |
| DataRetentionService.cs | public class DataRetentionService(BrqoshDbContext db) | CEEF-231 · ط5-١١ | تقليمُ ما يجوز تقليمُه — ولا يُمَسّ ما لا يجوز. |
| OffsiteBackupService.cs | public sealed class OffsiteBackupService | CEEF-231 · ط5-٤ | النسخةُ الاحتياطية خارج الجهاز — والقرصُ الواحد ليس نسخة. |
| OffsiteBackupService.cs | var companionSource = sourcePath + BackupService.CompanionSuffix; | جاهزيّةُ الإطلاق · ح-٥ | ‏ ‏ورفيقُ النسخة يسافر معها. |
| PgToolLocator.cs | public static class PgToolLocator | CEEF-229 · م3-٢ | العثورُ على pg_dump/pg_restore — ومطابقةُ إصداريهما بالخادم. |
| PreUpgradeBackup.cs | public sealed class PreUpgradeBackup( | جاهزيّةُ الإطلاق · ح-١٢٩ · <c>data-upgrade-performance#2</c> | ‏ الترقيةُ تنسخ الدفاترَ قبل أن تُهاجرها. |
| UpdateCheckService.cs | public sealed class UpdateCheckService | CEEF-231 · ط5-٥ | التحديثُ عن بُعد — وقناةُ تحديثٍ بلا توقيعٍ بابٌ خلفيّ. |
| UpdateCheckService.cs | internal HttpMessageHandler? Transport { get; init; } | CEEF-231 · O-33 | ناقلٌ يُحقن للاختبار — وnull يعني الشبكةَ الحقيقية. |
| EmployeeAdvanceService.cs | public class EmployeeAdvanceService(BrqoshDbContext db) | CEEF-230 · ط2-٥ | صرفُ السُّلَف واشتقاقُ أرصدتها — والاستقطاعُ يُعفيها. |
| EmployeeAdvanceService.cs | public async Task ReverseDisbursementAsync(long companyId, long advanceId, DateOnly revers | جاهزيّةُ الإطلاق · ح-٩١ · <c>treasury-payroll-assets#10</c> | عكسُ صرف السُّلفة — وكان لا يُعكس: العكسُ العامُّ يرفض قيداً له |
| EmployeeAdvanceService.cs | var settled = await db.JournalEntries | جاهزيّةُ الإطلاق · ح-٨٧ | ‏والمستردُّ في المخالصة النهائيّة سدادٌ كالمستقطَع من الراتب — دائنُ 1116 في قيد التصفية. |
| EndOfServiceAccrualService.cs | public class EndOfServiceAccrualService(BrqoshDbContext db) | CEEF-230 · ط2-٣ | تخصيصُ مكافأة نهاية الخدمة — شهراً بشهر. |
| EndOfServiceAccrualService.cs | var settled = await EndOfServiceSettlementService | CEEF-232 · ر-١ | ‏ ومَن صُفّيت مستحقّاتُه يخرج — وبغير هذا يُبطل التخصيصُ |
| EndOfServiceSettlementService.cs | public class EndOfServiceSettlementService(BrqoshDbContext db) | CEEF-232 · ر-١ | تصفيةُ نهاية الخدمة — الطرفُ الذي يُبرئ المخصّص. |
| EndOfServiceSettlementService.cs | private const string LeaveProvisionCode = SaudiChartOfAccounts.SystemCodes.LeaveProvision; | CEEF-233 · ز-٥ |  مخصّصُ الإجازات ومصروفُها — لبدل الإجازة (المادة 111). |
| EndOfServiceSettlementService.cs | var leaveBalance = await new LeaveBalanceQuery(db) | CEEF-233 · ز-٥ | ‏وبدلُ الإجازة يُدفع مع المكافأة — المادة 111. |
| EndOfServiceSettlementService.cs | var advanceOwed = (await new EmployeeAdvanceService(db).BalancesAsync(companyId, employeeI | جاهزيّةُ الإطلاق · ح-٨٧ · `treasury-payroll-assets#4` | ‏والسُّلفةُ القائمةُ تُستردّ من المستحقّات — وكانت تبقى |
| EndOfServiceSettlementService.cs | public async Task ReverseAsync(long companyId, long employeeId, DateOnly reversalDate, str | جاهزيّةُ الإطلاق · ح-٩١ · <c>treasury-payroll-assets#10</c> | عكسُ تصفية نهاية الخدمة — وكانت لا تُعكس، فتصفيةٌ بسببٍ خطأٍ |
| LeaveAccrualService.cs | public class LeaveAccrualService(BrqoshDbContext db) | CEEF-233 · ز-٣ | تخصيصُ الإجازات — التزامٌ يتراكم كلَّ يومٍ ولم يكن في الدفاتر. |
| LeaveBalanceQuery.cs | public class LeaveBalanceQuery(BrqoshDbContext db) | CEEF-233 · ز-٤ | رصيدُ الإجازات — يُشتقّ من المستندات لا من عدّاد. |
| LeaveRequestService.cs | public class LeaveRequestService(BrqoshDbContext db) | CEEF-233 · ز-٢ | تقديمُ طلبات الإجازة والبتُّ فيها. |
| LeaveRequestService.cs | private async Task EnsureWithinStatutoryLimitAsync( | جاهزيّةُ الإطلاق · ح-١١٧ · ‏`treasury-payroll-assets#9` | ‏ الإجازةُ المدفوعةُ بنصٍّ لا تتجاوز مدّتَها النظاميّة، |
| PayrollPaymentService.cs | public class PayrollPaymentService(BrqoshDbContext db) | جاهزيّةُ الإطلاق · ح-٨٤ · <c>treasury-payroll-assets#3</c> | صرفُ الرواتب وسدادُ التأمينات ودفعُ مستحقّ نهاية الخدمة — وكان |
| PayrollPaymentService.cs | public Task ReverseSalariesPaymentAsync(long companyId, long runId, DateOnly reversalDate, | جاهزيّةُ الإطلاق · ح-٨٨ · <c>treasury-payroll-assets#10</c> | عكسُ صرف رواتب الدورة — وكان الصرفُ لا يُعكس، فدورةٌ صُرفت |
| PayrollPostingService.cs | var allowances = await SystemAccountProvisioning.EnsureAsync(db, run.CompanyId, await Brqo | CEEF-230 · ط2-٤ | ‏حسابُ البدلات — يُنشأ من القالب إن غاب، فقواعدُ العملاء |
| PayrollPostingService.cs | var advances = run.TotalAdvanceDeduction > 0m | CEEF-230 · ط2-٥ | ‏حسابُ السُّلَف — ولا يُنشأ إلا حين يوجد استقطاع، فقاعدةٌ |
| PayrollPostingService.cs | var employeeIds = run.Lines | CEEF-232 · م-١ | ‏ومراكزُ تكلفةِ الموظّفين تُقرأ فيُفكَّك بها مصروفُ الأجر. |
| PayrollPostingService.cs | var employeeProjects = await EmployeeProjectsAsync(run.CompanyId, employeeIds, token); | جاهزيّةُ الإطلاق · ح-١٤٦ · ‏`sector-modules#9` | ‏ ‏موظّفو المشاريع — وحالةُ كلّ مشروعٍ تُسأل قبل القيد. |
| PayrollPostingService.cs | if (run.Id == 0) | CEEF-230 · ط2-٠ | ‏لا تُضاف دورةٌ محفوظةٌ سلفاً. |
| PayrollPostingService.cs | await new EmployeeAdvanceService(db).RefreshStatusesAsync(run.CompanyId, token); | CEEF-231 · ط2-٧ | ‏والسُّلفةُ المسدَّدةُ تُقفَل هنا لا في شاشتها. |
| PayrollReversalService.cs | if (await new PayrollPaymentService(db).AnyPaidAsync(companyId, runId, token)) | جاهزيّةُ الإطلاق · ح-٨٤ | ‏ولا تُعكس دورةٌ خرج مالُها — فعكسُها يُعيد 2108 مديناً بما صُرف والمالُ خارجٌ، |
| PayrollReversalService.cs | await new EmployeeAdvanceService(db).RefreshStatusesAsync(companyId, token); | CEEF-231 · ط2-٧ | ‏والعكسُ يُعيد الدَّين. |
| PayrollRunBuilder.cs | public class PayrollRunBuilder(BrqoshDbContext db) | CEEF-231 · ط2-٧ | دورةُ الرواتب تُبنى من سجلّ الموظفين — لا تُكتب بالأصابع. |
| PayrollRunBuilder.cs | var monthStart = new DateOnly(payDate.Year, payDate.Month, 1); | جاهزيّةُ الإطلاق · ح-٨٦ | ‏ ‏والمعاينةُ بشهر يوم الصرف لا بيومه — والدورةُ تُبنى ممّن تُرشّحهم؛ فمغادرٌ في منتصف الشهر |
| PayrollRunBuilder.cs | var monthStart = new DateOnly(year, month, 1); | جاهزيّةُ الإطلاق · ح-٨٦ | ‏ ‏والمغادرُ في الشهر يُقرأ بتاريخ انتهاء خدمته — Terminate يُطفئ علَمَ النشاط، فكان يسقط |
| PayrollRunBuilder.cs | var unpaidPeriods = await UnpaidLeavePeriodsAsync(companyId, monthStart, monthEnd, ct); | CEEF-233 · ز-٦ | ‏وأيّامُ الغياب بلا أجرٍ تُقرأ هنا لا تُدخَل يدوياً. |
| PayrollRunBuilder.cs | internal static decimal AdvanceDeduction(decimal dueInstallment, decimal wageDue, decimal  | جاهزيّةُ الإطلاق · ح-٤٩ · <c>treasury-payroll-assets#6</c> | ‏ قسطُ السُّلفة لا يأخذ أكثرَ |
| PayrollRunBuilder.cs | private async Task<ILookup<long, (DateOnly From, DateOnly To)>> UnpaidLeavePeriodsAsync( | CEEF-233 · ز-٦ | فتراتُ الغياب بلا أجرٍ المعتمدةُ التي تمسّ الشهر لكلّ موظّف. |
| PayrollRunBuilder.cs | internal static decimal UnpaidDaysWithin( | CEEF-233 · ز-٦ | أيّامُ الغياب بلا أجرٍ الواقعةُ داخل المدّة — والمدّةُ ما خدمه الموظّفُ من الشهر (ح-١١٧). |
| PayslipQuery.cs | public class PayslipQuery(BrqoshDbContext db) | CEEF-230 · ط2-٦ | قسائمُ دورةِ رواتبٍ — سطراً سطراً. |
| PayslipQuery.cs | line.AbsenceDeduction, line.AdvanceDeduction); | جاهزيّةُ الإطلاق · ح-٢٤ | ‏الأرقامُ على السطر ولم تُمرَّر، |
| WageProtectionFileBuilder.cs | public class WageProtectionFileBuilder(BrqoshDbContext db) | CEEF-230 · ط2-٥ | ملفُّ حماية الأجور. |
| WageProtectionFileBuilder.cs | var deductions = line.GosiEmployee + line.AdvanceDeduction; | CEEF-231 · ط2-٧ | ‏والاستقطاعاتُ كلُّها، لا التأميناتِ وحدَها. |
| AmbientDbContext.cs | public static class AmbientDbContext | نمط Outbox، ع-D2 / CEEF-204 | إلى نفس المعاملة بدل فتح اتصال منفصل . |
| BrqoshDataRoot.cs | public static class BrqoshDataRoot | CEEF-203 | : بلا هذا التوجيه كانت الاختبارات تكتب في بيانات المستخدم الحقيقية — |
| BrqoshDbContext.cs | public DbSet<Patient> Patients => Set<Patient>(); | CEEF-230 · ط3-٢ | ‏الصحّة. |
| BrqoshDbContext.cs | public DbSet<Brqosh.Domain.Payroll.LeaveRequest> LeaveRequests => | CEEF-233 · ز-٢ | ‏طلباتُ الإجازة والغياب — والمستندُ الذي يُشتقّ منه الرصيدُ والخصم. |
| BrqoshDbContext.cs | public DbSet<MenuItem> MenuItems => Set<MenuItem>(); | CEEF-230 · ط3-٣ | ‏الضيافة. |
| BrqoshDbContext.cs | public DbSet<Shipment> Shipments => Set<Shipment>(); | CEEF-230 · ط3-٤ | ‏اللوجستيات. |
| BrqoshDbContext.cs | entity.Property(c => c.RegistrationName).HasMaxLength(256); | CEEF-229 · Z-10 | بيانات البائع النظامية — كائنٌ مملوك يُسطَّح في نفس الجدول |
| BrqoshDbContext.cs | entity.Property(e => e.IsEntityLevel).HasDefaultValue(false); | CEEF-231 · O-32 | ‏قيدٌ على مستوى المنشأة — والافتراضُ false، فلا |
| BrqoshDbContext.cs | lines.Property(l => l.CurrencyCode).HasMaxLength(3); | CEEF-231 · O-34 | ‏مذكرةُ العملة — والمبلغان أعلاه بالعملة الوظيفية دائماً. |
| BrqoshDbContext.cs | lines.HasIndex(l => l.CostCenterId); | CEEF-230 · ط1-٧ | ‏فهرسٌ لمركز التكلفة — وبلا مفتاحٍ أجنبيّ عمداً: |
| BrqoshDbContext.cs | entity.Property(c => c.CreditLimit).HasPrecision(18, 2); | جاهزيّةُ الإطلاق · ح-٩٥ | ‏ حدُّ الائتمان — وفارغُه بلا حدّ. |
| BrqoshDbContext.cs | entity.Property(c => c.RegistrationName).HasMaxLength(256); | CEEF-229 · م1-١ب | هوية المشتري النظامية — العروضُ تطابق أعمدة BrqoshSchemaUpdater |
| BrqoshDbContext.cs | entity.Property(i => i.DocumentDiscountAmount).HasPrecision(18, 2); | CEEF-230 · ط1-٢ | ‏خصمُ المستند — يُوزَّع على الأوعية الضريبية عند الحساب. |
| BrqoshDbContext.cs | entity.Property(i => i.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR"); | CEEF-230 · ط1-٤ | ‏عملةُ المستند ومبالغُها — والريالُ يبقى في Subtotal |
| BrqoshDbContext.cs | lines.Property(l => l.BatchNumber).HasMaxLength(64); | CEEF-230 · ط1-٣ | ‏سقفُ الطول في النموذج لا في القاعدة وحدَها — |
| BrqoshDbContext.cs | lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16); | CEEF-207 | فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم. |
| BrqoshDbContext.cs | entity.Property(n => n.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR"); | جاهزيّةُ الإطلاق · ح-٣٥ | ‏ عملةُ الإشعار — نظيرُ أعمدة الفاتورة. |
| BrqoshDbContext.cs | entity.Property(n => n.DocumentDiscountAmount).HasPrecision(18, 2); | جاهزيّةُ الإطلاق · ح-٦ | ‏ خصمُ المستند على الإشعار — نظيرُ عمود الفاتورة. |
| BrqoshDbContext.cs | lines.Property(l => l.BatchNumber).HasMaxLength(64); | CEEF-230 · ط1-٣ | ‏سقفُ الطول في النموذج لا في القاعدة وحدَها — |
| BrqoshDbContext.cs | lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16); | CEEF-207 | فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم. |
| BrqoshDbContext.cs | entity.Property(v => v.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR"); | CEEF-230 · ط1-٤ | ‏عملةُ السند وسعرُها يوم التحصيل — ومن فرقِه عن |
| BrqoshDbContext.cs | entity.Property(v => v.SettlementDiscount).HasPrecision(18, 2).HasDefaultValue(0m); | CEEF-231 · ط1-٢ج | ‏خصمُ السداد المبكر — يُطفئ من الذمّة ما لم يدخل الصندوق. |
| BrqoshDbContext.cs | entity.Property(i => i.CurrencyCode).HasMaxLength(3); | CEEF-230 · ط4-٥ | ‏عملةُ الفاتورة وسعرُها وأساسُها الأجنبيّ. |
| BrqoshDbContext.cs | entity.Property(i => i.VatTreatment).HasConversion<string>().HasMaxLength(24); | جاهزيّةُ الإطلاق · tax-zatca#12 | ‏ ‏معاملةُ الضريبة — «Domestic» لكلّ فاتورةٍ قائمة — وضريبةُ الاحتساب العكسيّ والقيمةُ الجمركيّة. |
| BrqoshDbContext.cs | lines.Property(l => l.BatchNumber).HasMaxLength(64); | CEEF-230 · ط1-٣ | ‏سقفُ الطول في النموذج لا في القاعدة وحدَها — |
| BrqoshDbContext.cs | lines.Property(l => l.ExpiryDate); | جاهزيّةُ الإطلاق · ح-١٠٨ · ‏`order-to-cash-procure-to-pay-inventory#7` | ‏ ‏انتهاءُ دفعة الشراء المباشر — لفاتورة الشراء وحدَها. |
| BrqoshDbContext.cs | lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16); | CEEF-207 | فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم. |
| BrqoshDbContext.cs | entity.Property(v => v.CurrencyCode).HasMaxLength(3); | CEEF-230 · ط4-٥ | ‏عملةُ السند وما حُرِّر من الذمّة بعملة الدفاتر. |
| BrqoshDbContext.cs | entity.Property(v => v.SettlementDiscount).HasPrecision(18, 2).HasDefaultValue(0m); | CEEF-231 · ط1-٢ج | ‏خصمُ السداد المكتسَب — يُطفئ من الذمّة ما لم يخرج من الصندوق. |
| BrqoshDbContext.cs | if (Database.IsNpgsql()) | CEEF-231 · ط5-٨ | ‏رمزُ تزامنٍ — وهو صنفُ عطلِ ع-D1 بعينه في موضعٍ آخر. |
| BrqoshDbContext.cs | entity.Property(p => p.Tracking).HasConversion<string>().HasMaxLength(16) | CEEF-230 · ط1-٣ | ‏نصّاً لا رقماً — كبقيّة تعدادات هذا المخطّط، وليُقرأ في |
| BrqoshDbContext.cs | modelBuilder.Entity<StockBatch>(entity => | CEEF-230 · ط1-٣ | ‏رصيدُ الدفعة — تفصيلٌ لرصيد المخزون تربطه به الهويّةُ هـ-٩. |
| BrqoshDbContext.cs | if (Database.IsNpgsql()) | CEEF-231 · ط5-٨ | ‏رمزُ تزامنٍ — وهو صنفُ عطلِ ع-D1 بعينه في موضعٍ آخر. |
| BrqoshDbContext.cs | modelBuilder.Entity<UnitOfMeasure>(entity => | CEEF-230 · ط1-١ | ‏وحداتُ القياس — قالبٌ مبذورٌ لكلّ منشأة قابلٌ للتعديل. |
| BrqoshDbContext.cs | modelBuilder.Entity<Property>(entity => | CEEF-230 · ط3-١ | ‏العقاراتُ والوحداتُ وعقودُ الإيجار. |
| BrqoshDbContext.cs | modelBuilder.Entity<Patient>(entity => | CEEF-230 · ط3-٢ | ‏المرضى والوثائقُ والزيارات. |
| BrqoshDbContext.cs | lines.Property(l => l.BatchNumber).HasMaxLength(64); | CEEF-231 · ط3-٢ج | ‏دفعةُ الدواء المصروف — الطولُ نفسُه في كلّ مستند. |
| BrqoshDbContext.cs | modelBuilder.Entity<Brqosh.Domain.Payroll.EmployeeAdvance>(entity => | CEEF-230 · ط2-٥ | ‏سُلَفُ الموظفين — أصلٌ يُستردّ بالاستقطاع. |
| BrqoshDbContext.cs | modelBuilder.Entity<Brqosh.Domain.Payroll.LeaveRequest>(entity => | CEEF-233 · ز-٢ | ‏طلباتُ الإجازة. |
| BrqoshDbContext.cs | modelBuilder.Entity<InsuranceClaim>(entity => | CEEF-230 · ط3-٢ب | ‏دفعاتُ المطالبات وقراراتُ الشركة عليها. |
| BrqoshDbContext.cs | modelBuilder.Entity<Domain.RealEstate.MaintenanceOrder>(entity => | CEEF-230 · ط3-١ب | ‏أوامرُ الصيانة وسطورُ تكلفتها. |
| BrqoshDbContext.cs | modelBuilder.Entity<TipPool>(entity => | CEEF-230 · ط3-٣د | ‏أوعيةُ الإكراميات وتحصيلُها وتوزيعُها. |
| BrqoshDbContext.cs | modelBuilder.Entity<Domain.Logistics.DeliveryTrip>(entity => | CEEF-230 · ط3-٤ب | ‏رحلاتُ التوصيل وشحناتُها ومصاريفُها. |
| BrqoshDbContext.cs | modelBuilder.Entity<MenuItem>(entity => | CEEF-230 · ط3-٣ | ‏قائمةُ الطعام ووصفاتُها. |
| BrqoshDbContext.cs | modelBuilder.Entity<Room>(entity => | CEEF-230 · ط3-٣ب | ‏الغرفُ والحجوزُ وحساباتُ النزلاء. |
| BrqoshDbContext.cs | modelBuilder.Entity<Shipment>(entity => | CEEF-230 · ط3-٤ | ‏البوالص. |
| BrqoshDbContext.cs | entity.OwnsMany(sh => sh.Events, ev => | CEEF-230 · ط3-٤ج | ‏أثرُ الشحنة — سندُ التسليم لا زينةُ عرض. |
| BrqoshDbContext.cs | modelBuilder.Entity<Employee>(entity => | CEEF-230 · ط2-١ | ‏سجلُّ الموظفين — وكان الاسمُ نصّاً حرّاً يُعاد كتابتُه شهرياً. |
| BrqoshDbContext.cs | modelBuilder.Entity<GosiPolicy>(entity => | CEEF-230 · ط2-٢ | ‏نِسَبُ التأمينات — لكلّ منشأةٍ نسختُها، فالمُنظِّم يُعدّلها. |
| BrqoshDbContext.cs | modelBuilder.Entity<Budget>(entity => | CEEF-230 · ط1-٦ | ‏الموازنة — حبّتُها شهريّةٌ بمفتاح الفترة المالية نفسِه. |
| BrqoshDbContext.cs | modelBuilder.Entity<RecurringInvoicePlan>(entity => | CEEF-230 · ط1-٥ | ‏خططُ الفوترة الدورية وأثرُ تكراراتها. |
| BrqoshDbContext.cs | if (Database.IsNpgsql()) | ع-D1، CEEF-205 | رمز تزامن : تحديث الرصيد قراءة-ثم-كتابة بقيمة مطلقة، |
| BrqoshDbContext.cs | entity.HasIndex(d => new { d.CompanyId, d.Icv }).IsUnique(); | CEEF-229 · Z-4 | شبكة الأمان في القاعدة لسلسلة العدّاد التسلسليّ. |
| BrqoshDbContext.cs | lines.Property(l => l.BatchNumber).HasMaxLength(64); | CEEF-230 · ط1-٣ | ‏سقفُ الطول في النموذج لا في القاعدة وحدَها — |
| BrqoshDbContext.cs | lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16); | CEEF-207 | فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم. |
| BrqoshDbContext.cs | lines.Property(l => l.BatchNumber).HasMaxLength(64); | CEEF-230 · ط1-٣ | ‏سقفُ الطول في النموذج لا في القاعدة وحدَها — |
| BrqoshDbContext.cs | lines.Property(l => l.BatchNumber).HasMaxLength(64); | CEEF-230 · ط1-٣ | ‏سقفُ الطول في النموذج لا في القاعدة وحدَها — |
| BrqoshDbContext.cs | entity.Property(n => n.CurrencyCode).HasMaxLength(3).IsRequired().HasDefaultValue("SAR"); | جاهزيّةُ الإطلاق · ح-٣٩ | ‏ عملةُ الإشعار المدين — نظيرُ أعمدة فاتورة الشراء. |
| BrqoshDbContext.cs | lines.Property(l => l.BatchNumber).HasMaxLength(64); | CEEF-230 · ط1-٣ | ‏سقفُ الطول في النموذج لا في القاعدة وحدَها — |
| BrqoshDbContext.cs | lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16); | CEEF-207 | فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم. |
| BrqoshDbContext.cs | entity.Property(b => b.LabourCostPerUnit).HasPrecision(18, 2); | جاهزيّةُ الإطلاق · ح-١٣ | ‏ تكاليفُ التحويل — الأجرُ المباشرُ ونسبةُ التحميل. |
| BrqoshDbContext.cs | entity.Property(w => w.RecipeLabourCostPerUnit).HasPrecision(18, 2); | جاهزيّةُ الإطلاق · ح-٧٧ | ‏ ‏وصفةُ الأمر المجمَّدة عند إصداره — بدقّة قائمة الموادّ نفسِها. |
| BrqoshDbContext.cs | entity.Property(a => a.PreviousHash).HasMaxLength(64).IsRequired(); | O-19، CEEF-212 | سلسلة تجزئة كاشفة للعبث. |
| BrqoshDbContext.cs | entity.Property(a => a.Method).HasColumnName("DepreciationMethod").HasConversion<string>() | جاهزيّةُ الإطلاق · treasury-payroll-assets#11 | ‏ ‏طريقةُ الإهلاك — «StraightLine» لكلّ أصلٍ قائم، والافتراضُ في DDL وحدَه. |
| BrqoshDbContext.cs | entity.Property(a => a.DisposalInvoiceNumber).HasMaxLength(64); | جاهزيّةُ الإطلاق · ح-١٦ | بيعُ الأصل — المشتري ورقمُ فاتورته ومقابلُه وفئتُه. |
| BrqoshDbContext.cs | lines.Property(l => l.BatchNumber).HasMaxLength(64); | CEEF-230 · ط1-٣ | ‏سقفُ الطول في النموذج لا في القاعدة وحدَها — |
| BrqoshDbContext.cs | lines.Property(l => l.BatchNumber).HasMaxLength(64); | CEEF-230 · ط1-٣ | ‏سقفُ الطول في النموذج لا في القاعدة وحدَها — |
| BrqoshDbContext.cs | lines.Property(l => l.VatCategory).HasConversion<string>().HasMaxLength(16); | CEEF-207 | فئة الضريبة لكل سطر — نصّاً لقراءتها في القاعدة ولثبات القيم. |
| BrqoshDbContext.cs | if (Database.IsNpgsql()) | CEEF-231 · ط5-٨ | ‏رمزُ تزامنٍ — وهو صنفُ عطلِ ع-D1 بعينه في موضعٍ آخر. |
| BrqoshDbContext.cs | modelBuilder.Entity<InventoryCostConsumption>(entity => | CEEF-229 · م0-٣ | سجلُّ الاستهلاك — مفتاحُه المستند، فالترميمُ يبدأ منه. |
| BrqoshDbContext.cs | entity.HasIndex(e => e.DispatchedAtUtc); | CEEF-204 | طابور Outbox: استعلام «غير المُرسَل» يجري بعد كل ترحيل . |
| BrqoshDbContext.cs | private async Task StampJournalEntryBranchesAsync(CancellationToken ct) | CEEF-231 · ط4-٢ | المَخنَقُ الوحيد لختم الفرع على القيود. |
| BrqoshDbContext.cs | var unstamped = ChangeTracker.Entries<Brqosh.Domain.Accounting.JournalEntry>() | CEEF-231 · O-32 | ‏والمُعلَنُ على مستوى المنشأة يُتخطّى — الإقفالُ السنويُّ |
| BrqoshDesignTimeDbContextFactory.cs | public class BrqoshDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BrqoshDbContex | ح-2، CEEF-201 | : توليدها سابقاً على SQLite أنتج أعمدة INTEGER/TEXT ووسم Sqlite:Autoincrement |
| BrqoshSchemaUpdater.cs | public IReadOnlyList<SchemaStepFailure> SuppressedFailures => _suppressed; | CEEF-231 · ط5-١٠ | ما ابتُلع أثناء ترقية المخطّط — ولا يُبتلع بلا أثر. |
| BrqoshSchemaUpdater.cs | public static IReadOnlyList<SchemaStepFailure> LastRunFailures { get; private set; } = []; | CEEF-231 · ط5-١٠ | فشلُ آخرِ ترقيةٍ جرت في هذه العملية — لتقرأه شاشةُ الحالة. |
| BrqoshSchemaUpdater.cs | internal Task<bool> TrySchemaSqlForTests(string step, string sql, CancellationToken ct) => | CEEF-231 · ط5-١٠ | مَنفذُ قياسٍ للمُجمِّع — وفرعُ الفشل لا يُبلَغ من الخارج. |
| BrqoshSchemaUpdater.cs | private async Task PromoteSystemAccountsAsync(bool isPg, CancellationToken ct) | جاهزيّةُ الإطلاق · ح-٢٧ | يرفع وسمَ «نظاميّ» عن كلّ حسابٍ يقول القالبُ إنّه |
| BrqoshSchemaUpdater.cs | await EnsureCancelledAtColumnsAsync(isPg, ct); | O-21، CEEF-219 | تاريخ إلغاء المستندات الضريبية على القواعد القائمة. |
| BrqoshSchemaUpdater.cs | await EnsureRegulatoryIdentityColumnsAsync(isPg, ct); | CEEF-229 · Z-10 | هوية المنشأة النظامية — هنا للسبب نفسه المشروح أعلاه. |
| BrqoshSchemaUpdater.cs | await EnsureBatchTrackingAsync(isPg, ct); | CEEF-230 · ط1-٣ | تتبّعُ الدفعة — هنا للسبب نفسه، وقد وقعتُ فيه. |
| BrqoshSchemaUpdater.cs | await EnsureExemptionReasonColumnsAsync(isPg, ct); | CEEF-229 · م1-١ب الشقّ الثاني | سببُ الإعفاء — قبل ترقيعات المزوّد للسبب نفسه |
| BrqoshSchemaUpdater.cs | await EnsurePostedAtColumnsAsync(isPg, ct); | CEEF-229 · م2-٢٤ | لحظةُ الترحيل — قبل ترقيعات المزوّد للسبب نفسه: تلك |
| BrqoshSchemaUpdater.cs | await EnsureWorkOrderRecipeSchemaAsync(isPg, ct); | جاهزيّةُ الإطلاق · ح-٧٧ | ‏ ‏وصفةُ أمر الإنتاج — قبل ترقيعات المزوّد للسبب نفسه: بنودُها مملوكةٌ |
| BrqoshSchemaUpdater.cs | await EnsureCustomerCreditLimitColumnAsync(isPg, ct); | جاهزيّةُ الإطلاق · ح-٩٥ | ‏ ‏حدُّ ائتمان العميل — قبل ترقيعات المزوّد للسبب نفسه: كلُّ استعلامٍ عن العميل |
| BrqoshSchemaUpdater.cs | await EnsureWorkOrderWarehouseColumnsAsync(isPg, ct); | جاهزيّةُ الإطلاق · ح-٩٧ | ‏ ‏مستودعا أمر الإنتاج — قبل ترقيعات المزوّد للسبب نفسه: كلُّ استعلامٍ عن الأمر |
| BrqoshSchemaUpdater.cs | await EnsureSalesInvoiceWarehouseColumnAsync(isPg, ct); | جاهزيّةُ الإطلاق · ح-١١٠ | ‏ ‏مستودعُ فاتورة المبيعات — قبل ترقيعات المزوّد للسبب نفسه: كلُّ استعلامٍ عن الفاتورة |
| BrqoshSchemaUpdater.cs | await EnsurePosShiftAndRecurringPlanWarehouseColumnsAsync(isPg, ct); | جاهزيّةُ الإطلاق · ح-١١٤ | ‏ ‏مستودعُ الوردية والخطة الدورية — قبل ترقيعات المزوّد للسبب نفسه. |
| BrqoshSchemaUpdater.cs | await EnsureOpeningBalanceItemsTableAsync(isPg, ct); | جاهزيّةُ الإطلاق · ح-١٣٤ | ‏ الذممُ المفتوحةُ عند بدء التشغيل. |
| BrqoshSchemaUpdater.cs | await EnsureGosiContributorGroupColumnsAsync(isPg, ct); | جاهزيّةُ الإطلاق · ح-١٣٨ | ‏ فئتا مشتركي التأمينات وجدولُ النظام الجديد — بافتراضات النطاق لا أصفار. |
| BrqoshSchemaUpdater.cs | await EnsurePurchaseInvoiceWarehouseColumnAsync(isPg, ct); | جاهزيّةُ الإطلاق · ح-١١١ | ‏ ‏مستودعُ فاتورة المشتريات — قبل ترقيعات المزوّد للسبب نفسه. |
| BrqoshSchemaUpdater.cs | await PromoteSystemAccountsAsync(isPg, ct); | جاهزيّةُ الإطلاق · ح-٢٧ | ‏وسمُ «حسابٍ نظاميّ» يلحق القواعدَ القائمة. |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("goods_receipts", isPg, ct)) | CEEF-207 | فئة الضريبة لأسطر المستندات — بعد اكتمال إنشاء الجداول أعلاه، |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("goods_receipts", isPg, ct)) | CEEF-229 · م1-١٩ | قيدُ استحقاق الاستلام — قبل ترقيعات المزوّد، فتلك تُجري |
| BrqoshSchemaUpdater.cs | await EnsureAuditChainColumnsAsync(isPg, ct); | O-19، CEEF-212 | أعمدة سلسلة تجزئة سجل التدقيق على القواعد القائمة. |
| BrqoshSchemaUpdater.cs | await EnsurePosCashSalesColumnAsync(isPg, ct); | CEEF-228 | المبيعات النقدية وحدها في ورديات نقطة البيع. |
| BrqoshSchemaUpdater.cs | await FreezeOpenWorkOrderRecipesAsync(isPg, ct); | جاهزيّةُ الإطلاق · ح-٧٧ | ‏ ‏وأوامرُ صدرت قبل الترقية تُجمَّد وصفتُها — آخراً، فأعمدةُ تكاليف |
| BrqoshSchemaUpdater.cs | LastRunFailures = _suppressed.AsReadOnly(); | CEEF-231 · ط5-١٠ | ‏وما ابتُلع يُسجَّل ولا يُنسى. |
| BrqoshSchemaUpdater.cs | private static readonly string[] CancellableTaxDocumentTables = | CEEF-219 | جداول المستندات الضريبية التي تدخل الإقرار . |
| BrqoshSchemaUpdater.cs | private async Task EnsureAuditChainColumnsAsync(bool isPg, CancellationToken ct) | CEEF-229 |  كان يُضيف الأعمدةَ الثلاثة ويقف. فبقيت الصفوفُ القديمة كلُّها |
| BrqoshSchemaUpdater.cs | private static readonly string[] VatCategoryLineTables = | CEEF-207 | جداول أسطر المستندات التي تحمل فئة ضريبية . |
| BrqoshSchemaUpdater.cs | private static readonly string[] VatCategoryLineTables = | CEEF-230 | وخطّةُ الفوترة الدوريّة. وهو درسُ «ثمانيةُ جداولٍ لا واحد» نفسُه، طُبِّق على |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync("fixed_assets", "DisposalBuyerCustomerId", "BIGINT NULL", ct); | جاهزيّةُ الإطلاق · ح-١٦ | بيعُ الأصل: المشتري ورقمُ فاتورته ومقابلُه وفئتُه وسببُها. |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync("fixed_assets", "DepreciatedThrough", "DATE NULL", ct); | جاهزيّةُ الإطلاق · ح-١٠٠ | آخرُ يومٍ أُهلك قبل الدفاتر — لأصلٍ قائمٍ يُدخَل برصيده الافتتاحيّ. |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync("shipments", "FreightInvoiceId", "BIGINT NULL", ct); | CEEF-232 · ل-١ | ‏ربطُ فاتورة أجرة الشحن — ولاغٍ افتراضاً، فشحناتٌ سابقةٌ |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync("properties", "FixedAssetId", "BIGINT NULL", ct); | CEEF-232 · ع-١ | ‏ربطُ العقار بأصلٍ ثابتٍ يُهلَك — ولاغٍ افتراضاً، فعقاراتٌ |
| BrqoshSchemaUpdater.cs | await DropDuplicateUniqueIndexesAsync(ct); | CEEF-229 · م2-١٠ / D-5 | ‏آخرَ شيء: يُكنَس ما أنشأه المُرقِّعُ سابقاً بأسماءٍ |
| BrqoshSchemaUpdater.cs | await EnsureBatchTrackingAsync(isPg: true, ct); | CEEF-230 · ط1-٣ | ‏النداءُ الثاني — على ما أعادت المرحلةُ السابقة بناءَه. |
| BrqoshSchemaUpdater.cs | await SafePgIndexAsync(""" | CEEF-229 · Z-4 | الفهرس الفريد على عدّاد ZATCA — للقواعد القائمة كذلك. |
| BrqoshSchemaUpdater.cs | await EnsureSqliteColumnAsync("fixed_assets", "DisposalBuyerCustomerId", "INTEGER NULL", c | جاهزيّةُ الإطلاق · ح-١٦ | بيعُ الأصل — انظر النظيرَ في مسار PostgreSQL أعلاه. |
| BrqoshSchemaUpdater.cs | await EnsureSqliteColumnAsync("fixed_assets", "DepreciatedThrough", "TEXT NULL", ct); | جاهزيّةُ الإطلاق · ح-١٠٠ | آخرُ يومٍ أُهلك قبل الدفاتر — انظر النظيرَ في مسار PostgreSQL أعلاه. |
| BrqoshSchemaUpdater.cs | await EnsureBatchTrackingAsync(isPg: false, ct); | CEEF-230 · ط1-٣ | ‏النداءُ الثاني — على ما أعادت المرحلةُ السابقة بناءَه. |
| BrqoshSchemaUpdater.cs | private async Task DropDuplicateUniqueIndexesAsync(CancellationToken ct) | CEEF-229 · م2-١٠ / D-5 | ‏كنسُ قيدِ تفرّدٍ أُنشئ مرّتين. |
| BrqoshSchemaUpdater.cs | private async Task EnsureWorkOrderRecipeSchemaAsync(bool isPg, CancellationToken ct) | جاهزيّةُ الإطلاق · ح-٧٧ · <c>manufacturing#5</c> | وصفةُ أمر الإنتاج — أعمدتُها وجدولُ بنودها. |
| BrqoshSchemaUpdater.cs | internal static readonly (string Column, string PgType, bool Nullable)[] RegulatoryIdentit | CEEF-229 · Z-10 | أعمدة هوية المنشأة النظامية — تُضاف للقواعد القائمة بنفس أسماء |
| BrqoshSchemaUpdater.cs | private async Task EnsureRegulatoryIdentityColumnsAsync(bool isPg, CancellationToken ct) | CEEF-229 · Z-10 | أعمدة هوية المنشأة النظامية على القواعد القائمة. |
| BrqoshSchemaUpdater.cs | await EnsureRegulatoryColumnsOnAsync("customers", BuyerIdentityColumns, isPg, ct); | CEEF-229 · م1-١ب | وطرفُ المشتري يأخذ الشكلَ نفسه — الاسمُ المسجَّل والعنوانُ الوطنيّ. |
| BrqoshSchemaUpdater.cs | internal static readonly (string Column, string PgType, bool Nullable)[] BuyerIdentityColu | CEEF-229 · م1-١ب | أعمدةُ هوية المشتري — الشكلُ نفسه بلا رقم سجلٍّ تجاريّ. |
| BrqoshSchemaUpdater.cs | private async Task AlignRegulatoryColumnNullabilityAsync( | CEEF-229 · Z-10 | مطابقة قابلية العدم على قاعدةٍ قائمة — وإصلاح ما أفسدته نسخةٌ |
| BrqoshSchemaUpdater.cs | } | CEEF-229 · م0-١٣ | التعبئة أُوقفت — كانت خاملةً ثم صارت خطراً. |
| BrqoshSchemaUpdater.cs | if (isPg) | ع-D2، CEEF-204 | أعمدة طابور Outbox على القواعد القائمة . |
| BrqoshSchemaUpdater.cs | private async Task BackfillDocumentTotalsAsync(CancellationToken ct) | CEEF-206 | كان يحمّل كل الفواتير والإشعارات والأوامر بسطورها المملوكة إلى |
| BrqoshSchemaUpdater.cs | private async Task BackfillDocumentTotalsAsync(CancellationToken ct) | CEEF-229 · م2-١١ / D-4 | إعادةُ حساب إجماليات المستندات التي لم تُحفظ إجمالياتُها قطّ — |
| BrqoshSchemaUpdater.cs | private async Task<string?> LedgerDisagreementAsync( | CEEF-229 | ‏يقارن ما أصلحه الإقلاعُ بما يقوله القيدُ المرحَّل. |
| BrqoshSchemaUpdater.cs | await SafePgIndexAsync(""" | CEEF-229 · م2-٩ | سطورُ عرض السعر — كان الأبُ يُنشأ وحده والابنُ منسيّاً. |
| BrqoshSchemaUpdater.cs | await SafePgIndexAsync(""" | CEEF-229 · م2-٩ | سطورُ أمر البيع — الابنُ المنسيّ الثاني. وVatCategory نصٌّ هنا |
| BrqoshSchemaUpdater.cs | await SafePgIndexAsync(""" | CEEF-207 | ‏لأنّ النموذج يُعلن HasConversion() عليه . |
| BrqoshSchemaUpdater.cs | await SafeSqliteIndexAsync(""" | CEEF-229 · م2-٩ | الابنُ المنسيّ — انظر تعليق فرع PostgreSQL. |
| BrqoshSchemaUpdater.cs | await SafeSqliteIndexAsync(""" | CEEF-229 · م2-٩ | الابنُ المنسيّ — انظر تعليق فرع PostgreSQL. |
| BrqoshSchemaUpdater.cs | private async Task EnsureBatchTrackingAsync(bool isPg, CancellationToken ct) | CEEF-230 · ط1-٣ | تتبّعُ الدفعة والصلاحية والرقم التسلسليّ — على قاعدةٍ قائمة. |
| BrqoshSchemaUpdater.cs | string[] lineTables = | CEEF-230 · ط1-٣ | ‏ثمانيةُ جداولٍ لا واحد. SalesInvoiceLine سجلٌّ |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync(table, "DiscountPercent", "NUMERIC(9,4) NOT NULL DEFAULT 0", ct) | CEEF-230 · ط1-٢ | ‏نسبةُ خصم السطر — على الثمانية أنفسِها، فالسجلُّ مشترك. |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync(table, "UnitCode", "character varying(16) NULL", ct); | CEEF-230 · ط1-١ | ‏وحدةُ السطر — على الثمانية أنفسِها للسبب نفسِه. |
| BrqoshSchemaUpdater.cs | if (table == "purchase_invoice_lines") | جاهزيّةُ الإطلاق · ح-١٠٨ | ‏ ‏انتهاءُ الدفعة المشتراة — لفاتورة الشراء وحدَها. |
| BrqoshSchemaUpdater.cs | if (table == "purchase_invoice_lines") | جاهزيّةُ الإطلاق · ح-١٠٨ | ‏ ‏انتهاءُ الدفعة المشتراة — لفاتورة الشراء وحدَها. |
| BrqoshSchemaUpdater.cs | foreach (var table in new[] { "purchase_invoices", "payment_vouchers" }) | CEEF-230 · ط1-٧ | ‏مركزُ تكلفة المستند على فاتورة المشتريات وسندِ الصرف. |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("purchase_invoices", isPg, ct)) | CEEF-230 · ط4-٥ | ‏عملةُ المشتريات والسداد — وكان جانبُ الموردين بلا عملةٍ |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync("payment_vouchers", "SettlementDiscount", | CEEF-231 · ط1-٢ج | ‏خصمُ السداد المكتسَب. |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("receipt_vouchers", isPg, ct)) | CEEF-230 · ط1-٤ | ‏عملةُ سند القبض. |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync("receipt_vouchers", "SettlementDiscount", | CEEF-231 · ط1-٢ج | ‏خصمُ السداد المسموح به. |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("journal_entries", isPg, ct)) | CEEF-231 · O-32 | ‏علامةُ «على مستوى المنشأة» — والافتراضُ false، |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("journal_entry_lines", isPg, ct)) | CEEF-231 · O-34 | ‏مذكرةُ العملة على سطر القيد — لاغيةٌ لكلّ سطرٍ قائم، |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync("sales_invoices", "CurrencyCode", | CEEF-230 · ط1-٤ | ‏عملةُ المستند — والافتراضُ ريالٌ بسعرِ واحد، |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("debit_notes", isPg, ct)) | جاهزيّةُ الإطلاق · ح-٣٩ | ‏وعملةُ الإشعار المدين — مرآةُ ح-٣٥. |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("bills_of_materials", isPg, ct)) | جاهزيّةُ الإطلاق · ح-١٣ | ‏ ‏وتكاليفُ التحويل على قائمة الموادّ. |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("shipments", isPg, ct)) | جاهزيّةُ الإطلاق · ح-١٢ | ‏ ‏ووسمُ أنّ قيدَ تحصيل الدفع عند الاستلام وقع. |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("lease_contracts", isPg, ct)) | جاهزيّةُ الإطلاق · ح-١٠ | ‏ ‏وأسكنيٌّ عقدُ الإيجار أم تجاريّ. |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("credit_notes", isPg, ct)) | جاهزيّةُ الإطلاق · ح-٦ | ‏ ‏وخصمُ المستند على الإشعار الدائن. |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync("credit_notes", "CurrencyCode", | جاهزيّةُ الإطلاق · ح-٣٥ | ‏عملةُ الإشعار — والافتراضُ ريالٌ |
| BrqoshSchemaUpdater.cs | var creditBackfill = isPg | جاهزيّةُ الإطلاق · ح-٣٥ | ‏وتعبئةُ الأجنبيّ من الريال لكلّ |
| BrqoshSchemaUpdater.cs | var backfill = isPg | CEEF-230 · ط1-٤ | ‏وتعبئةُ الأجنبيّ من الريال لكلّ فاتورةٍ قائمة: بسعرِ |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط3-٤ | ‏البوالصُ والتحصيلُ نيابةً عن التجّار. |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط3-٣ب | ‏الغرفُ والحجوزُ وحساباتُ النزلاء. |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط3-٣ | ‏قائمةُ الطعام ووصفاتُها. |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync("menu_items", "SaleProductId", "BIGINT NULL", ct); | CEEF-230 · ط3-٣ج | ‏والقاعدةُ القائمة تُرقَّع: الجدولُ أُنشئ قبل الربط، |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط4-٣ج | ‏مهلةُ السداد — وبها يصير التنبّؤ تنبّؤاً. |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط3-٤ج | ‏أثرُ تتبّع الشحنة — سندُ التسليم. |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط3-١ب و ط3-٣د و ط3-٤ب | ‏الصيانةُ والإكرامياتُ ورحلاتُ التوصيل. |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط3-٢ب | ‏دفعاتُ المطالبات وقراراتُ الشركة عليها. |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط3-٢ | ‏المرضى وشركاتُ التأمين والوثائقُ والزيارات. |
| BrqoshSchemaUpdater.cs | if (await TableExistsAsync("visit_lines", isPg, ct)) | CEEF-231 · ط3-٢ج | ‏رقمُ دفعة الدواء المصروف — على قاعدةٍ قائمةٍ كذلك. |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط3-١ | ‏العقاراتُ والوحداتُ وعقودُ الإيجار. |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط2 | ‏سجلُّ الموظفين ونِسَبُ التأمينات. |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync("payroll_lines", "AdvanceDeduction", | CEEF-230 · ط2-٥ | ‏قسطُ السُّلفة المُستقطَع — وبغيره يقبض الموظّفُ راتبَه |
| BrqoshSchemaUpdater.cs | await EnsurePgColumnAsync("payroll_lines", "AbsenceDeduction", | CEEF-233 · ز-٦ | ‏خصمُ الغياب بلا أجر — ويُنقص المصروف لا الصافيَ وحدَه. |
| BrqoshSchemaUpdater.cs | await SafePgIndexAsync(""" | CEEF-233 · ز-٢ | ‏طلباتُ الإجازة والغياب. |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط1-٦ | ‏الموازنة. |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط1-٥ | ‏خططُ الفوترة الدورية وأثرُ تكراراتها. |
| BrqoshSchemaUpdater.cs | if (isPg) | CEEF-230 · ط1-١ | ‏جدولُ وحدات القياس — يُبذَر للمنشآت الجديدة في التهيئة، |
| ClosedPeriodPostingInterceptor.cs | public sealed class ClosedPeriodPostingInterceptor : SaveChangesInterceptor | جاهزيّةُ الإطلاق · ح-٧٨ · <c>found#1</c> | الدفترُ نفسُه يرفض قيداً يُرحَّل في فترةٍ مقفلة — بتاريخ القيد لا بالتاريخ |
| DatabaseBootstrapService.cs | public async Task<DatabaseIdentity> InspectAsync(CancellationToken ct = default) | CEEF-236 · م-٢٠ | مَن يسكن هذه القاعدة؟ — يُسأل قبل أن يُكتب فيها حرف. |
| DatabaseBootstrapService.cs | private static bool IsMissingSchema(Exception? ex) | CEEF-236 · م-٢٦ | «لا أستطيع القراءة» ليست «لا دفاترَ هنا». |
| DatabaseBootstrapService.cs | public record CompanyBootstrapResult(long CompanyId, string CompanyName, BusinessProfile P | جاهزيّةُ الإطلاق · ح-٦٦ · sector-modules#10 | نظامُ العمل المخزَّنُ في القاعدة — لا المطلوب. |
| DatabaseConfigFile.cs | public static class DatabaseConfigFile | CEEF-236 · ت-٣ | ملفُّ الاتصال %APPDATA%\BRQOSH\database.json — يُكتب من التطبيق لا باليد. |
| DatabaseConfigFile.cs | public static class DatabaseConfigFile | CEEF-206 | صريحة: فالحمايةُ بـDPAPI كانت مقبولةً عند القراءة ولا أداةَ تُنتجها |
| DatabaseConfigFile.cs | public static DatabaseConfigEntry? Load(string path) | CEEF-206 | محميّةً بملفّ تعريفِ مستخدمٍ آخر — لا يُسقَط التشغيلُ بصمتٍ على قاعدةٍ خاطئة . |
| DatabaseConfigFile.cs | throw new InvalidOperationException( | CEEF-236 | ‏والتالفُ غيرُ المُشفَّر بحسابٍ آخر. Unprotect يفكّ Base64 قبل DPAPI، |
| DatabaseConfigFile.cs | public static string BuildPostgresConnectionString(string host, int port, string database, | CEEF-236 | وضعُ التشفير صريحٌ لا متروكٌ لافتراض المُشغِّل. |
| DatabaseConfigFile.cs | public static string ReadPassword(string? connectionString) | CEEF-236 · م-٢٢ | كلمةُ المرور من سلسلةٍ قائمة — لتُحمَل عند إعادة البناء لا لتُعرض. |
| DatabaseConfigFile.cs | return new ConnectionProbe(false, | CEEF-236 | ‏28000 ليس كلمةَ مرورٍ خاطئة بل رفضُ pg_hba.conf — قاعدةٌ لا تسمح |
| DatabaseInitializer.cs | private async Task SeedUnitsOfMeasureAsync(long companyId) | CEEF-230 · ط1-١ | ‏بذرُ وحدات القياس. |
| DatabaseInitializer.cs | private async Task SeedGosiPolicyAsync(long companyId) | CEEF-230 · ط2-٢ | ‏بذرُ نِسَب التأمينات بقيمها المبدئية. |
| DatabaseSchemaService.cs | if (db.Database.IsSqlite()) | ح-2، CEEF-201 | SQLite = تطوير/اختبار فقط، والهجرات مولَّدة لـ PostgreSQL حصراً . |
| DatabaseSchemaService.cs | if (!await Security.LeastPrivilegeDatabaseRole.CanChangeSchemaAsync(db, ct)) | جاهزيّةُ الإطلاق · ح-١٥١ · ‏`security-licensing-tenancy#5` | ‏ ‏دورُ التطبيق لا يُعرّف المخطّط — فلا يُرقَّع ولا يُهاجَر بها؛ |
| DatabaseSchemaService.cs | CrashLog.Write(ex, "schema-migrate-fallback"); | CEEF-237 · ت-١١ | ‏ الابتلاعُ يُسجَّل — وكان صامتاً تماماً. |
| DatabaseSchemaService.cs | private async Task EnsureNotNewerThanThisBuildAsync(CancellationToken ct) | جاهزيّةُ الإطلاق · ح-٥١ · <c>data-upgrade-performance#7</c> | ‏ محطّةٌ بإصدارٍ أقدمَ لا تعمل على |
| DbContextFactory.cs | .AddInterceptors(new AuditLogImmutabilityInterceptor(), new ClosedPeriodPostingInterceptor | CEEF-200 | (المصنع)، لا مسار DI فقط. إغلاق ح-3 . |
| DbContextFactory.cs | .AddInterceptors(new AuditLogImmutabilityInterceptor(), new ClosedPeriodPostingInterceptor | جاهزيّةُ الإطلاق · ح-٧٨ | ‏ ‏وحارسُ الفترة المقفلة عند الحفظ — بالسُّنّة نفسِها: المساران معاً. |
| DbContextFactory.cs | builder.UseNpgsql(connectionString, npgsql => npgsql.ExecutionStrategy(deps => | CEEF-231 · ط5-٧ | ‏إعادةُ المحاولة على الأعطال العابرة — وكانت الآليةُ |
| DbContextFactory.cs | public static string DefaultSqlitePath() => | CEEF-203 | فلا تكتب الاختبارات في قاعدة المستخدم الحقيقية . |
| DocumentNumberGenerator.cs | public static Task<string> NextAsync( | CEEF-229 · م1-١٤ | السياقُ معاملٌ إلزاميّ — لأنّ القفلَ لا يجوز أن يُنسى. |
| DocumentNumberGenerator.cs | var fromDb = await existingNumbers | CEEF-229 · م2-٤أ | الأكبرُ تأخذه القاعدة — انظر الشرح المفصّل في |
| DocumentSequenceLock.cs | internal static class DocumentSequenceLock | CEEF-229 · م1-١٤ / D-1 | قفلٌ استشاريّ على تسلسل المستندات — لكلّ (منشأة · نوع · سنة). |
| DocumentSequenceLock.cs | if (database.CurrentTransaction is null) | CEEF-229 · م1-١٤ | قفلٌ بلا معاملةٍ قفلٌ ميّت — ويُرفض صراحةً لا يُشحن صامتاً. |
| DocumentSequenceLock.cs | await database.ExecuteSqlRawAsync( | CEEF-203 | المعاملاتُ تُمرَّر مصفوفةً صريحة، وإلا ابتلع التحميلُ الزائد رمزَ الإلغاء |
| DocumentSequenceLock.cs | public static class DocumentSequenceLockProbe | CEEF-229 · م1-١٤ | نافذةٌ للاختبار على اشتقاق المفتاح وحدَه — لا على القفل. |
| JournalEntryNumberGenerator.cs | var fromDb = await db.JournalEntries | CEEF-229 · م2-٤أ | الأكبرُ تأخذه القاعدة — لا يُسحب الدفترُ كلُّه إلى الذاكرة. |
| JournalEntryNumberGenerator.cs | await db.Database.ExecuteSqlRawAsync( | CEEF-203 | حرج: يجب تمرير المعاملات كمصفوفة صريحة. التحميل الزائد |
| 20260725114612_AddAuditChain.cs | migrationBuilder.Sql(""" | CEEF-229 | ‏ ترقيمُ الصفوف القائمة قبل الفهرس الفريد. |
| 20260829031810_AddSellerRegulatoryIdentity.cs | public partial class AddSellerRegulatoryIdentity : Migration | CEEF-229 · م2-٩ | إعادةُ سلسلة الهجرات إلى مطابقة النموذج. |
| 20260829172624_AddBuyerRegulatoryIdentity.cs | public partial class AddBuyerRegulatoryIdentity : Migration | CEEF-229 · م1-١ب | هويةُ المشتري النظامية على جدول العملاء. |
| 20260829193357_AddVatExemptionReason.cs | public partial class AddVatExemptionReason : Migration | CEEF-229 · م1-١ب الشقّ الثاني | رمزُ سبب الإعفاء ونصُّه على سطور المستندات. |
| 20260830030241_AddPostedAtUtc.cs | public partial class AddPostedAtUtc : Migration | CEEF-229 · م2-٢٤ | لحظةُ ترحيل الفاتورة والإشعار الدائن — يعلنها رمزُ الطور الأوّل. |
| 20260830141012_AddInventoryCostConsumption.cs | public partial class AddInventoryCostConsumption : Migration | CEEF-229 · م0-٣ | سجلُّ استهلاك طبقات التكلفة — مفتاحُ ترميمها عند الإلغاء. |
| 20260830151747_AddGoodsReceiptJournalLink.cs | public partial class AddGoodsReceiptJournalLink : Migration | CEEF-229 · م1-١٩ | ربطُ إذن الاستلام بقيد استحقاقه — مرساةُ عكسه عند الإلغاء. |
| 20260901184208_AddBatchTracking.cs | public partial class AddBatchTracking : Migration | CEEF-230 · ط1-٣ | تتبّعُ الدفعة والصلاحية والرقم التسلسليّ. |
| 20260901190151_AddLineAndDocumentDiscount.cs | public partial class AddLineAndDocumentDiscount : Migration | CEEF-230 · ط1-٢ | خصمُ السطر وخصمُ المستند. |
| 20260901193917_AddUnitsOfMeasure.cs | public partial class AddUnitsOfMeasure : Migration | CEEF-230 · ط1-١ | وحداتُ القياس وتحويلُها. |
| 20260901200347_AddCostCenterToDocuments.cs | public partial class AddCostCenterToDocuments : Migration | CEEF-230 · ط1-٧ | مركزُ التكلفة على المستندات، وفهرسُه على سطر القيد. |
| 20260901202444_AddRecurringInvoicePlans.cs | public partial class AddRecurringInvoicePlans : Migration | CEEF-230 · ط1-٥ | خططُ الفوترة الدورية وأثرُ تكراراتها. |
| 20260901211414_AddBudgets.cs | public partial class AddBudgets : Migration | CEEF-230 · ط1-٦ | الموازنة وسطورُها. |
| 20260901212707_AddDocumentCurrency.cs | public partial class AddDocumentCurrency : Migration | CEEF-230 · ط1-٤ | عملةُ المستند وسعرُ صرفها ومبالغُها الأجنبية. |
| 20260901214629_AddReceiptVoucherCurrency.cs | public partial class AddReceiptVoucherCurrency : Migration | CEEF-230 · ط1-٤ | عملةُ سند القبض وسعرُ صرفها. |
| 20260901215308_AddReceivableRelieved.cs | public partial class AddReceivableRelieved : Migration | CEEF-230 · ط1-٤ | ما حُرِّر من ذمّة العميل بعملة الدفاتر. |
| 20260901223914_AddEmployeesAndGosi.cs | public partial class AddEmployeesAndGosi : Migration | CEEF-230 · ط2 | سجلُّ الموظفين ونِسَبُ التأمينات، ومعرّفُ الموظّف على سطر الرواتب. |
| 20260902001727_AddRealEstate.cs | public partial class AddRealEstate : Migration | CEEF-230 · ط3-١ | العقاراتُ والوحداتُ وعقودُ الإيجار. |
| 20260902010825_AddHealthcare.cs | public partial class AddHealthcare : Migration | CEEF-230 · ط3-٢ | المرضى وشركاتُ التأمين والوثائقُ والزيارات. |
| 20260902014602_AddHospitalityMenu.cs | public partial class AddHospitalityMenu : Migration | CEEF-230 · ط3-٣ | قائمةُ الطعام ووصفاتُها. |
| 20260902021959_AddHospitalityLodging.cs | public partial class AddHospitalityLodging : Migration | CEEF-230 · ط3-٣ب | الغرفُ والحجوزُ وحساباتُ النزلاء. |
| 20260902031402_AddLogistics.cs | public partial class AddLogistics : Migration | CEEF-230 · ط3-٤ | البوالص والتحصيل نيابةً عن التجّار. |
| 20260902074633_AddPayableCurrency.cs | public partial class AddPayableCurrency : Migration | CEEF-230 · ط4-٥ | عملةُ المشتريات والسداد. |
| 20260902102415_AddMenuSaleProduct.cs | protected override void Up(MigrationBuilder b) | CEEF-230 · ط3-٣ج | ‏ربطُ صنفِ القائمة بصنفِ البيع. |
| 20260902111949_AddInsuranceClaims.cs | protected override void Up(MigrationBuilder b) | CEEF-230 · ط3-٢ب | ‏دفعاتُ المطالبات وقراراتُ الشركة عليها. |
| 20260902134506_AddEmployeeAdvances.cs | protected override void Up(MigrationBuilder b) | CEEF-230 · ط2-٥ | ‏سُلَفُ الموظفين وقسطُها المُستقطَع. |
| 20260902193121_AddVisitLineBatch.cs | protected override void Up(MigrationBuilder b) | CEEF-231 · ط3-٢ج | ‏رقمُ دفعة الدواء المصروف في الزيارة. |
| 20260902202214_AddMaintenanceTipsAndTrips.cs | protected override void Up(MigrationBuilder b) | CEEF-230 | ‏أوامرُ الصيانة (ط3-١ب) وأوعيةُ الإكراميات (ط3-٣د) ورحلاتُ التوصيل (ط3-٤ب). |
| 20260902215416_AddSettlementDiscount.cs | protected override void Up(MigrationBuilder b) | CEEF-231 · ط1-٢ج | ‏خصمُ السداد المبكر على السندين. |
| 20260902232411_AddCostConcurrencyTokens.cs | protected override void Up(MigrationBuilder migrationBuilder) | CEEF-231 · ط5-٨ | ‏رموزُ تزامنٍ على التكلفة والطبقات والدفعات. |
| 20260902235909_AddShipmentTracking.cs | protected override void Up(MigrationBuilder b) | CEEF-230 · ط3-٤ج | ‏أثرُ تتبّع الشحنة — سندُ التسليم. |
| 20260903003301_AddPaymentTerms.cs | protected override void Up(MigrationBuilder b) | CEEF-230 · ط4-٣ج | ‏مهلةُ السداد للعملاء والموردين. |
| 20260903021326_AddJournalCurrencyMemoAndEntityLevel.cs | protected override void Up(MigrationBuilder b) | CEEF-231 · O-34 · O-32 | ‏مذكرةُ العملة على سطر القيد، وعلامةُ «على مستوى المنشأة». |
| 20260903100404_AddShipmentFreightInvoice.cs | protected override void Up(MigrationBuilder b) | CEEF-232 · ل-١ | ‏ربطُ فاتورةِ أجرة الشحن بالبوليصة. |
| 20260903101601_AddPropertyFixedAssetLink.cs | protected override void Up(MigrationBuilder b) | CEEF-232 · ع-١ | ‏ربطُ العقار بأصلٍ ثابتٍ يُهلَك. |
| 20260903120413_AddLeaveRequests.cs | protected override void Up(MigrationBuilder b) | CEEF-233 · ز | ‏جدولُ طلبات الإجازة، وخصمُ الغياب على سطر الراتب. |
| 20260905065349_AddZatcaClearedXmlPath.cs | public partial class AddZatcaClearedXmlPath : Migration | CEEF-237 · ز-٢ | عمودُ المستند المُخلَّص العائد من الهيئة. |
| 20260920161808_AddCreditNoteDocumentDiscount.cs | public partial class AddCreditNoteDocumentDiscount : Migration | جاهزيّةُ الإطلاق · ح-٦ | خصمُ المستند على الإشعار الدائن. |
| 20260920202507_AddLeaseResidentialFlag.cs | public partial class AddLeaseResidentialFlag : Migration | جاهزيّةُ الإطلاق · ح-١٠ | أسكنيٌّ عقدُ الإيجار أم تجاريّ — والفرقُ ضريبةٌ لا وصف. |
| 20260920212504_AddCodCollectionPostedFlag.cs | public partial class AddCodCollectionPostedFlag : Migration | جاهزيّةُ الإطلاق · ح-١٢ | وسمُ أنّ قيدَ تحصيل الدفع عند الاستلام وقع فعلاً. |
| 20260921010734_AddBomConversionCosts.cs | public partial class AddBomConversionCosts : Migration | جاهزيّةُ الإطلاق · ح-١٣ | تكاليفُ التحويل على قائمة الموادّ — الأجرُ المباشرُ |
| 20260921023712_AddFixedAssetDisposalSale.cs | public partial class AddFixedAssetDisposalSale : Migration | جاهزيّةُ الإطلاق · ح-١٦ | بيعُ الأصل الثابت على سجلّ الأصل: المشتري ورقمُ فاتورته |
| 20260921203424_AddCreditNoteCurrency.cs | public partial class AddCreditNoteCurrency : Migration | جاهزيّةُ الإطلاق · ح-٣٥ | عملةُ الإشعار الدائن — نظيرُ أعمدة الفاتورة. |
| 20260922231012_AddDebitNoteCurrency.cs | public partial class AddDebitNoteCurrency : Migration | جاهزيّةُ الإطلاق · ح-٣٩ | عملةُ الإشعار المدين — مرآةُ ح-٣٥ في المشتريات. |
| 20260927114018_AddWorkOrderRecipeSnapshot.cs | public partial class AddWorkOrderRecipeSnapshot : Migration | جاهزيّةُ الإطلاق · ح-٧٧ · manufacturing#5 | وصفةُ أمر الإنتاج المجمَّدة عند إصداره — أجرُ الوحدة ونسبةُ التحميل |
| 20261002235723_AddCustomerCreditLimit.cs | public partial class AddCustomerCreditLimit : Migration | جاهزيّةُ الإطلاق · ح-٩٥ · order-to-cash-procure-to-pay-inventory#13 | حدُّ ائتمان العميل — عمودٌ يقبل الفراغ، وفارغُه بلا حدّ |
| 20261003010109_AddWorkOrderWarehouses.cs | public partial class AddWorkOrderWarehouses : Migration | جاهزيّةُ الإطلاق · ح-٩٧ · manufacturing#7 | مستودعا أمر الإنتاج — عمودان يقبلان الفراغ، وفارغُهما المستودعُ الافتراضيّ |
| 20261003161430_AddFixedAssetDepreciatedThrough.cs | public partial class AddFixedAssetDepreciatedThrough : Migration | جاهزيّةُ الإطلاق · ح-١٠٠ · treasury-payroll-assets#12 | آخرُ يومٍ أُهلك قبل الدفاتر — عمودٌ يقبل الفراغ، وفارغُه أصلٌ يُقتنى |
| 20261009170928_AddPurchaseInvoiceLineExpiry.cs | public partial class AddPurchaseInvoiceLineExpiry : Migration | جاهزيّةُ الإطلاق · ح-١٠٨ · order-to-cash-procure-to-pay-inventory#7 | انتهاءُ الدفعة المشتراة على سطر فاتورة الشراء المباشرة — عمودٌ |
| 20261009180612_AddSalesInvoiceWarehouse.cs | public partial class AddSalesInvoiceWarehouse : Migration | جاهزيّةُ الإطلاق · ح-١١٠ · order-to-cash-procure-to-pay-inventory#12 | مستودعُ فاتورة المبيعات — عمودٌ يقبل الفراغ، وفارغُه المستودعُ |
| 20261009185024_AddPurchaseInvoiceWarehouse.cs | public partial class AddPurchaseInvoiceWarehouse : Migration | جاهزيّةُ الإطلاق · ح-١١١ · order-to-cash-procure-to-pay-inventory#12 | مستودعُ فاتورة المشتريات — عمودٌ يقبل الفراغ، وفارغُه المستودعُ |
| 20261009213258_AddPosShiftAndRecurringPlanWarehouse.cs | public partial class AddPosShiftAndRecurringPlanWarehouse : Migration | جاهزيّةُ الإطلاق · ح-١١٤ · order-to-cash-procure-to-pay-inventory#12 | مستودعُ الوردية والخطة الدورية — عمودان يقبلان الفراغ، وفارغُهما |
| 20261012214357_AddOpeningBalanceItems.cs | public partial class AddOpeningBalanceItems : Migration | جاهزيّةُ الإطلاق · ح-١٣٤ | ‏ الذممُ المفتوحةُ عند بدء التشغيل — مستندٌ يقرؤه الدفترُ المساعد. |
| 20261015141851_AddGosiContributorGroups.cs | public partial class AddGosiContributorGroups : Migration | جاهزيّةُ الإطلاق · ح-١٣٨ | ‏ فئتا المشتركين في نظام التأمينات الجديد (٢٠٢٤) وجدولُ نسبته. |
| 20261016232311_AddPurchaseInvoiceProject.cs | public partial class AddPurchaseInvoiceProject : Migration | جاهزيّةُ الإطلاق · ح-١٤٤ · <c>sector-modules#9</c> | ‏ المشروعُ المُحمَّلُ عليه فاتورةُ المشتريات. |
| 20261017030803_AddEmployeeProject.cs | public partial class AddEmployeeProject : Migration | جاهزيّةُ الإطلاق · ح-١٤٦ · <c>sector-modules#9</c> | ‏ المشروعُ الذي يعمل عليه الموظّف — فتُحمَّل عليه تكلفتُه في الرواتب. |
| 20261017033314_AddWorkOrderActualIssues.cs | public partial class AddWorkOrderActualIssues : Migration | جاهزيّةُ الإطلاق · ح-١٤٧ · <c>manufacturing#4</c> | ‏ الصرفُ الفعليّ لأمر الإنتاج والتالفُ عند إكماله. |
| 20261017042959_AddWorkOrderPartialCompletion.cs | public partial class AddWorkOrderPartialCompletion : Migration | جاهزيّةُ الإطلاق · ح-١٤٨ · <c>manufacturing#4</c> | ‏ دفعاتُ إكمال أمر الإنتاج، وما نُقل من موادّه المصروفة إلى التامّ. |
| 20261017100549_AddBankStatementBalances.cs | public partial class AddBankStatementBalances : Migration | جاهزيّةُ الإطلاق · ح-١٥٤ · <c>treasury-payroll-assets#13</c> | ‏ رصيدا الكشف البنكيّ كما يقولهما البنك. |
| 20261017110217_AddReconciledBankLine.cs | public partial class AddReconciledBankLine : Migration | جاهزيّةُ الإطلاق · ح-١٥٥ · <c>treasury-payroll-assets#13</c> | ‏ التسويةُ البنكيّةُ لكلّ سطرٍ لا لكلّ قيد. |
| 20261017124503_AddFixedAssetCancelledOn.cs | public partial class AddFixedAssetCancelledOn : Migration | جاهزيّةُ الإطلاق · ح-١٥٧ · <c>treasury-payroll-assets#10</c> | ‏ تاريخُ إلغاء اقتناء الأصل — فارغٌ للأصول القائمة. |
| 20261017132244_AddAssetPurchaseInvoice.cs | public partial class AddAssetPurchaseInvoice : Migration | جاهزيّةُ الإطلاق · ح-١٥٨ · <c>treasury-payroll-assets#12</c> | ‏ شراءُ الأصل من مورّدٍ بفاتورته: الفاتورةُ تُسمّي أصلَها، |
| 20261018033344_AddUserRecoveryKey.cs | public partial class AddUserRecoveryKey : Migration | جاهزيّةُ الإطلاق · <c>security-licensing-tenancy#7</c> | ‏ تجزئةُ مفتاح استعادة المدير — فارغةٌ للمستخدمين القائمين |
| 20261018082737_AddManualJournalApprovalThreshold.cs | public partial class AddManualJournalApprovalThreshold : Migration | جاهزيّةُ الإطلاق · <c>accounting-core#6</c> | ‏ حدُّ اعتماد القيد اليدويّ — والمنشآتُ القائمةُ تأخذ افتراضَ المنتج ١٠٬٠٠٠، |
| 20261019225947_AddSalesInvoiceSettlesReservation.cs | public partial class AddSalesInvoiceSettlesReservation : Migration | جاهزيّةُ الإطلاق · <c>sector-modules#6</c> | ‏ الحجزُ الذي تُصفّيه فاتورةُ المغادرة — فارغٌ لكلّ فاتورةٍ قائمة. |
| 20261020214728_AddMaintenanceOrderPurchaseInvoice.cs | public partial class AddMaintenanceOrderPurchaseInvoice : Migration | جاهزيّةُ الإطلاق · <c>sector-modules#11</c> | ‏ فاتورةُ المورّد التي سندُ أمر الصيانة — فارغةٌ لكلّ أمرٍ قائم (دُفع نقداً). |
| 20261020223707_AddPurchaseVatTreatment.cs | public partial class AddPurchaseVatTreatment : Migration | جاهزيّةُ الإطلاق · <c>tax-zatca#12</c> | ‏ معاملةُ ضريبة فاتورة الشراء — والقائمةُ «Domestic» لا سلسلةً فارغةً (والمولِّدُ كتبها |
| 20261020231503_AddFixedAssetDepreciationMethod.cs | public partial class AddFixedAssetDepreciationMethod : Migration | جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c> | ‏ طريقةُ إهلاك الأصل — والقائمُ «StraightLine» لا سلسلةً فارغة (والمولِّدُ |
| 20261020233534_AddZatcaSigningMachine.cs | public partial class AddZatcaSigningMachine : Migration | جاهزيّةُ الإطلاق · <c>tax-zatca#16</c> | ‏ الجهازُ الذي يحمل مفتاحَ الختم — فارغٌ لإعدادٍ قائم (لا يُعرف جهازُه، فلا يُسأل عنه)، |
| OutboxDispatchHook.cs | public static class OutboxDispatchHook | Outbox — ع-D2 / CEEF-204 | خطّاف ما بعد الالتزام لتفريغ طابور الأحداث . |
| OutboxDispatchHook.cs | LastFailure = ex.GetBaseException().Message; | CEEF-231 · O-36 | ‏ لكنّ الابتلاعَ الصامتَ كان يُخفي عطلاً دائماً: |
| ReadOnlyBooksInterceptor.cs | public sealed class ReadOnlyBooksInterceptor : SaveChangesInterceptor | جاهزيّةُ الإطلاق · ح-١٢٠ · ‏`commercial-readiness#6` | ‏ وضعُ القراءة فقط يُفرض عند الحفظ — لا في كلّ شاشة. |
| SchemaSqlGuard.cs | "character varying(16) NOT NULL DEFAULT 'None'", | CEEF-230 · ط1-٣ | ‏نمطُ تتبّع الصنف — تعدادٌ نصّيّ افتراضُه None. |
| SchemaSqlGuard.cs | "character varying(16) NOT NULL DEFAULT 'Standard'", | CEEF-207 | فئة الضريبة لأسطر المستندات. |
| SchemaSqlGuard.cs | "character varying(16) NOT NULL DEFAULT ''", | CEEF-229 · Z-10 | هوية المنشأة النظامية — نصوص غير قابلة للعدم، فارغُها يعني «لم يُدخَل». |
| SchemaSqlGuard.cs | "text NULL", | CEEF-229 · م1-١ب الشقّ الثاني | سببُ الإعفاء — نصٌّ غيرُ محدود الطول كما يُعلنه |
| SchemaSqlGuard.cs | "timestamp with time zone NULL", | CEEF-229 · م2-٢٤ | لحظةُ الترحيل. |
| SchemaSqlGuard.cs | "NUMERIC(9,4) NOT NULL DEFAULT 0", | CEEF-230 · ط1-٢ | ‏نسبةُ خصم السطر — مئةٌ بأربعة أرقامٍ عشرية. |
| SchemaSqlGuard.cs | "NUMERIC(18,6) NOT NULL DEFAULT 1", | CEEF-230 · ط1-٤ | ‏سعرُ الصرف — افتراضُه واحدٌ لا صفر: صفرٌ يُصفّر كلَّ مبلغ. |
| SchemaSqlGuard.cs | "character varying(3) NULL", | CEEF-231 · O-34 | ‏مذكرةُ العملة على سطر القيد — لاغيةٌ بلا افتراض. |
| SchemaSqlGuard.cs | "character varying(16) NOT NULL DEFAULT 'Legacy'", | جاهزيّةُ الإطلاق · ح-١٣٨ | ‏فئةُ مشترك التأمينات وجدولُ النظام الجديد — بافتراضات النطاق لا أصفار: صفرٌ في النسبة |
| SchemaSqlGuard.cs | "NUMERIC(18,4) NOT NULL DEFAULT 0", | جاهزيّةُ الإطلاق · ح-١٤٧ | ‏الكميّةُ التالفةُ لأمر الإنتاج — بدقّة الكميّة المنتَجة، وصفرٌ هو سلوكُ الأمر القائم. |
| SchemaSqlGuard.cs | "TEXT NOT NULL DEFAULT 'None'", | CEEF-230 · ط1-٣ | ‏نمطُ تتبّع الصنف — تعدادٌ نصّيّ افتراضُه None. |
| SchemaSqlGuard.cs | "TEXT NOT NULL DEFAULT '0'", | CEEF-230 · ط1-٢ | ‏نسبةُ خصم السطر ومبلغُ خصم المستند. |
| SchemaSqlGuard.cs | "TEXT NOT NULL DEFAULT '1'", | CEEF-230 · ط1-٤ | ‏سعرُ الصرف الافتراضيّ. |
| SchemaSqlGuard.cs | "TEXT NOT NULL DEFAULT 'Standard'", | CEEF-207 | فئة الضريبة لأسطر المستندات — قيمة ثابتة ضمن قائمة السماح. |
| SchemaSqlGuard.cs | "TEXT NOT NULL DEFAULT 'Legacy'", | جاهزيّةُ الإطلاق · ح-١٣٨ | ‏فئةُ مشترك التأمينات وجدولُ النظام الجديد. |
| SchemaStepFailure.cs | public sealed record SchemaStepFailure(string Step, string ExceptionType, string Message) | CEEF-231 · ط5-١٠ | خطوةُ مخطّطٍ فشلت وابتُلع فشلُها — باسمها وسببها. |
| UnitOfWork.cs | using var ambient = AmbientDbContext.Bind(db); | ع-D2، CEEF-204 | Outbox : ربط السياق يجعل أحداث النطاق تُكتب داخل هذه المعاملة، |
| UnitOfWork.cs | throw new Domain.Accounting.AccountingException( | ع-D1، CEEF-205 | رمز التزامن كشف أن صفاً تغيّر بين القراءة والكتابة — |
| CodeAvailabilityQuery.cs | public class CodeAvailabilityQuery(BrqoshDbContext db) | CEEF-229 · م2-١٠ / D-6 | لماذا يُرفض كودٌ لا يراه المستخدم في أيّ مكان. |
| OutboxDispatcher.cs | public sealed class OutboxDispatcher(IWebhookDispatcher webhooks) | Outbox — ع-D2 / CEEF-204 | مُفرِّغ طابور الأحداث . |
| PersistingDomainEventPublisher.cs | if (AmbientDbContext.Current is { } ambient) | ع-D2، CEEF-204 | Outbox : داخل معاملة جارية — اكتب الحدث في نفس السياق |
| SoftDeletedRowService.cs | public class SoftDeletedRowService(BrqoshDbContext db) | CEEF-229 | الصفُّ المحذوف حذفاً ناعماً: يُرى ويُسترجَع. |
| SoftDeletedRowService.cs | await ClearDeletionAsync(scope, companyId, id, token); | CEEF-229 | ‏ولا حارسَ تصادمٍ هنا — وهو قرارٌ قِيس لا سهو. |
| TenantProvisioningService.cs | public async Task<TenantProvisionResult> ProvisionAsync(TenantProvisionRequest request, Ca | CEEF-229 · م2-١ | غياب EnsureAdministrator هنا مقصود — والتوثيق هو الإصلاح. |
| DebitNoteCancellationService.cs | var inv = new InventoryService(db); | جاهزيّةُ الإطلاق · ح-٣١ | ‏يُعكَس الأصلُ — لا تُرحّل حركةٌ مضادّة. |
| DebitNoteCancellationService.cs | var inv = new InventoryService(db); | CEEF-229 · م0-٣ | ‏والمسارُ الصحيحُ قائمٌ وموثّق ، وستُّ خدماتِ |
| DebitNoteDraftBuilder.cs | public class DebitNoteDraftBuilder(BrqoshDbContext db) | جاهزيّةُ الإطلاق · ح-٣٧ | الإشعارُ المدين يُرجع السطرَ الذي اختاره المستخدم — |
| DebitNoteDraftBuilder.cs | if (invoice.IsForeignCurrency) | جاهزيّةُ الإطلاق · ح-٣٩ | ‏والعملةُ تُورَّث قبل السطور — فسعرُ السطر |
| DebitNoteDraftBuilder.cs | var inv = new InventoryService(db); | جاهزيّةُ الإطلاق · ح-١٠٥ | ‏ ‏ولا يُخمِّن الإشعارُ المدينُ دفعةً من الشاشة. سطرُ فاتورة الشراء لا يحمل دفعة، والدفعاتُ على |
| DebitNotePostingService.cs | if (invoice.VatTreatment != PurchaseVatTreatment.Domestic) | جاهزيّةُ الإطلاق · tax-zatca#12 | ‏ ‏الإشعارُ يعكس ضريبةَ المورّد — وفاتورةُ الاحتساب العكسيّ أو التخليص لا ضريبةَ مورّدٍ |
| DebitNotePostingService.cs | if (note.CurrencyCode != invoice.CurrencyCode \|\| note.ExchangeRate != invoice.ExchangeRate | جاهزيّةُ الإطلاق · ح-١٠٧ · ‏`found#7` | ‏ ‏وعملةُ الإشعار عملةُ فاتورته وسعرُها — وإلّا قورنت قيمةٌ بالدولار بطبقاتٍ بالريال. |
| DebitNotePostingService.cs | var noteNumber = await DocumentNumberGenerator.NextAsync(db, | جاهزيّةُ الإطلاق · ح-٣١ | ‏الرقمُ يُولَد قبل أن يُستعمل مفتاحاً. |
| DebitNotePostingService.cs | var received = await ReceivedBatchesStillHeldAsync(invoice, products, token); | جاهزيّةُ الإطلاق · ح-١٠٥ · ‏`order-to-cash-procure-to-pay-inventory#7` | ‏ ‏المتتبَّعُ يخرج من الدفعات التي دخل بها. |
| DebitNotePostingService.cs | var receiptWarehouses = await ReceiptWarehousesAsync(invoice, token); | جاهزيّةُ الإطلاق · ح-١١١ · ‏`order-to-cash-procure-to-pay-inventory#12` | ‏ ‏المرتجعُ يخرج من المستودع الذي دخلت إليه بضاعةُ الفاتورة — |
| DebitNotePostingService.cs | note.RecalculateTotals(); | جاهزيّةُ الإطلاق · ح-١٠٧ | ‏المرتجعُ المُسعَّر دون التكلفة يحتاج حسابَ الفرق — يُوفَّر |
| DebitNotePostingService.cs | if (inventoryReduction > credited && SecurityContext.CurrentRole != UserRole.Administrator | جاهزيّةُ الإطلاق · found#7 · قرارُ المالك 2026-10-18 | ‏ ‏والتأكيدُ بالخسارة للمدير — وكان يملكه كلُّ مرحِّل، فالحارسُ |
| DebitNotePostingService.cs | if (note.Id == 0) | CEEF-230 · ط2-٠ | ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ JournalPostingService نفسُه. |
| DebitNotePostingService.cs | internal async Task<List<ReceivedBatch>> ReceivedBatchesStillHeldAsync(PurchaseInvoice inv | جاهزيّةُ الإطلاق · ح-١٠٥ | ما استُلم من كلّ دفعةٍ ولا يزال يصلح للإرجاع — من سطور حركات الاستلام نفسِها |
| DebitNotePostingService.cs | private static List<(string? Batch, DateOnly? Expiry, decimal Quantity, long WarehouseId)> | جاهزيّةُ الإطلاق · ح-١٠٥ | يُقسّم كمّيةَ المرتجع على الدفعات المستلَمة بترتيب الاستلام، وينقص ما خرج منها فلا |
| GoodsReceiptCancellationService.cs | if (receipt.JournalEntryId is long accrualEntryId) | CEEF-229 · م1-١٩ | يُعكَس قيدُ الاستحقاق — وإلا بقي التزامُ «بضاعة مستلمة |
| GoodsReceiptCancellationService.cs | await inv.ReverseDocumentMovementAsync(movement); | CEEF-229 · م0-٣ج | يُعكَس الأصلُ — لا تُرحَّل حركةٌ مضادّة. |
| GoodsReceiptPostingService.cs | var baseQty = await new UnitConversionService(db).ToProductUnitAsync( | CEEF-230 · ط1-١ | ‏التحويلُ هنا وحدَه — عند حدود المستند. |
| GoodsReceiptPostingService.cs | if (products.TryGetValue(pid, out var product) && product.IsStockable()) | CEEF-229 · م1-١٩ | القيدُ يُرشَّح بما هو مخزنيّ — والحركةُ لا تُرشَّح. |
| GoodsReceiptPostingService.cs | JournalEntry? accrual = null; | CEEF-229 · م1-١٩ / I-5 | استحقاقُ الاستلام — والقيدُ الذي لم يكن. |
| GoodsReceiptPostingService.cs | if (receipt.Id == 0) | CEEF-230 · ط2-٠ | ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ JournalPostingService نفسُه. |
| PurchaseAccountProvisioning.cs | internal static class PurchaseAccountProvisioning | CEEF-229 · م1-١٩ | حسابا دورة الشراء الجديدان — يُنشآن للمنشآت القائمة أيضاً. |
| PurchaseInvoiceCancellationService.cs | await inv.ReverseDocumentMovementAsync(movement); | CEEF-229 · م0-٣ج | يُعكَس الأصلُ — لا تُرحَّل حركةٌ مضادّة. |
| PurchaseInvoicePostingService.cs | var receiptWarehouse = invoice.WarehouseId is null | جاهزيّةُ الإطلاق · ح-١١١ · ‏`order-to-cash-procure-to-pay-inventory#12` | ‏ ‏الشراءُ المباشر يدخل مستودعَ الفاتورة — لا الافتراضيَّ أبداً. |
| PurchaseInvoicePostingService.cs | if (invoice.VatTreatment == PurchaseVatTreatment.ImportPaidAtCustoms | جاهزيّةُ الإطلاق · tax-zatca#12 | ‏ ‏مستندُ التخليص ضريبةٌ لا بضاعة — فلا أمرَ شراءٍ ولا مشروعَ ولا أصلَ ولا مستودع: |
| PurchaseInvoicePostingService.cs | if (invoice.PurchaseOrderId is null) | جاهزيّةُ الإطلاق · ح-١٤ | ‏ ‏فاتورةٌ بلا أمرِ شراءٍ لبضاعةٍ وصلت بإذنِ استلام |
| PurchaseInvoicePostingService.cs | var receiptLines = new List<(long ProductId, decimal Qty, decimal UnitCost, string? BatchN | جاهزيّةُ الإطلاق · ح-١٠٨ · ‏`order-to-cash-procure-to-pay-inventory#7` | ‏ ‏الدفعةُ وانتهاؤها تسافران مع السطر إلى حركة الاستلام — |
| PurchaseInvoicePostingService.cs | decimal grniRelief = 0m; | CEEF-229 · م1-١٩ | مع إذنِ استلامٍ لا يُمَسّ 1107 إطلاقاً. |
| PurchaseInvoicePostingService.cs | var rate = invoice.ExchangeRate; | جاهزيّةُ الإطلاق · ح-٤ | ‏سطورُ المستند بعملته، والدفترُ بالريال. |
| PurchaseInvoicePostingService.cs | var relief = openAccrual.TryGetValue(pid, out var accrual) | CEEF-229 · م1-٢٠ | ‏الاستهلاكُ بالكمية، والقيمةُ المُحرَّرة تتبعها. |
| PurchaseInvoicePostingService.cs | var asset = invoice.FixedAssetId is long assetId | جاهزيّةُ الإطلاق · ح-١٥٨ · ‏`treasury-payroll-assets#12` | ‏ ‏فاتورةُ أصلٍ تُدين حسابَ الأصل لا المشتريات — وكلُّها له: |
| PurchaseInvoicePostingService.cs | await FinancialPostingSupport.EnsureCostCenterAsync(db, invoice.CompanyId, invoice.CostCen | CEEF-230 · ط1-٧ | ‏مركزُ تكلفة المستند يُختم على سطور المصروف — وقبل |
| PurchaseInvoicePostingService.cs | if (invoice.Id == 0) | CEEF-230 · ط2-٠ | ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ JournalPostingService نفسُه. |
| PurchaseInvoicePostingService.cs | private async Task EnsureNoOpenAccrualForUnlinkedInvoiceAsync( | CEEF-229 · م1-٢٠ / I-8 | الاستحقاقُ المفتوح لكلّ صنف — كمّيةً وقيمةً معاً: |
| PurchaseInvoicePostingService.cs | private async Task EnsureNoOpenAccrualForUnlinkedInvoiceAsync( | جاهزيّةُ الإطلاق · ح-١٤ | يمنع فاتورةً غيرَ مربوطةٍ أن تُدخل بضاعةً دخلت بإذنِ استلام. |
| PurchaseInvoicePostingService.cs | var received = await db.GoodsReceipts | جاهزيّةُ الإطلاق · ح-١٧ | ‏والترتيبُ جزءٌ من الجواب لا زينةٌ في الاستعلام. |
| PurchaseInvoicePostingService.cs | var layers = group | جاهزيّةُ الإطلاق · ح-١٧ | ‏طبقةٌ لكلّ سطرِ إذن — لا متوسّطٌ لكلّ الأذون. |
| PurchaseOrderService.cs | public Task<PurchaseOrder> ApproveAsync(PurchaseOrder order) | CEEF-229 · م1-١٤ | داخل معاملة — فقفلُ التسلسل مربوطٌ بها، وخارجَها يُحرَّر فوراً |
| PurchaseOrderService.cs | var products = await new InventoryService(db).LoadProductsForLinesAsync( | جاهزيّةُ الإطلاق · ح-١٠٦ · ‏`order-to-cash-procure-to-pay-inventory#7` | ‏ ‏الاستلامُ الآليُّ لا يخترع دفعة. سطرُ |
| PurchaseOrderService.cs | var warehouse = await PurchaseInvoicePostingService.ResolveWarehouseAsync( | جاهزيّةُ الإطلاق · ح-١١١ · ‏`order-to-cash-procure-to-pay-inventory#12` | ‏ ‏الاستلامُ الآليُّ يدخل المستودعَ المختار — لا الافتراضيَّ أبداً؛ والموقوفُ يُرفض باسمه. |
| SupplierBalanceQuery.cs | var debited = await DebitNotesForSupplierAsync(companyId, supplierId); | CEEF-229 · م0-٨ | إشعارات المدين تخفّض المستحقّ للمورد. |
| SupplierBalanceQuery.cs | public async Task<IReadOnlyDictionary<long, decimal>> AllBalancesAsync( | CEEF-231 · ط4-٣ب | حتّى تاريخٍ معلوم — وnull يعني الآن. |
| SupplierBalanceQuery.cs | .Select(g => new | CEEF-230 · ط4-٥ | ‏ما حُرِّر من الذمّة لا ما خرج من الصندوق — والدرسُ |
| SupplierBalanceQuery.cs | var debited = (await db.DebitNotes | CEEF-229 · م0-٨ | إشعارات المدين — انظر التعليق في ForSupplierAsync. |
| LeaseRentRecognition.cs | public class LeaseRentRecognition(BrqoshDbContext db) | جاهزيّةُ الإطلاق · ح-٧٩ · <c>sector-modules#7</c> | الإيجارُ المقدَّمُ يُكتسب شهراً بشهر — وكان يُعترف به كلُّه يومَ |
| LeaseService.cs | public class LeaseService(BrqoshDbContext db) | CEEF-230 · ط3-١ | تفعيلُ عقد الإيجار — والربطُ بالفوترة الدورية. |
| LeaseService.cs | var (rentCategory, exemptionCode) = lease.IsResidential | جاهزيّةُ الإطلاق · ح-١٠ | ‏ ‏وفئةُ الإيجار الضريبيّة تُمرَّر — وكانت تُترك |
| LeaseService.cs | await new LeaseRentRecognition(db).WithdrawAfterTerminationAsync( | جاهزيّةُ الإطلاق · ح-٧٩ | ‏ ‏ولا يُكتسب إيجارُ شهرٍ بعد الإنهاء — تُعكس اعترافاتُه بتواريخها، ويبقى غيرُ |
| MaintenanceOrderService.cs | public class MaintenanceOrderService(BrqoshDbContext db) | CEEF-230 · ط3-١ب | يُقيّد تكلفةَ الصيانة على وحدتها — ويُحوّل ما على المستأجر ديناً. |
| MaintenanceOrderService.cs | public async Task<MaintenancePostingResult> PostAsync(long companyId, long orderId, DateOn | جاهزيّةُ الإطلاق · <c>sector-modules#11</c> · قرارُ المالك 2026-10-20 |  وبفاتورة المورّد |
| MaintenanceOrderService.cs | var capitalizing = order.Kind == MaintenanceKind.CapitalImprovement; | CEEF-232 · ع-٢ | ‏والتجديدُ الذي يزيد في العقار يُرسمَل ولا يُصرَف. |
| SecurityDepositService.cs | public class SecurityDepositService(BrqoshDbContext db) | CEEF-230 · ط3-١ | تأمينُ المستأجر — أمانةٌ لا إيراد. |
| SecurityDepositService.cs | lease.RecordDepositReturned(on, forfeited); | CEEF-230 · ط3-١ب | ‏والمصادرةُ لا تتجاوز ما يُدين به المستأجر. |
| BalanceSheetQuery.cs | var pnl = await new IncomeStatementQuery(db) | ع-R1، CEEF-201 | : حصرها في السنة الجارية كان يُسقط أرباح أي سنة سابقة لم يُشغَّل إقفالها |
| BranchProfitabilityQuery.cs | public class BranchProfitabilityQuery(BrqoshDbContext db) | CEEF-231 · ط4-٢ | ربحيّةُ الفروع — والبيانات كانت كلُّها موجودةً ولا يقرؤها تقرير. |
| BudgetVarianceQuery.cs | public class BudgetVarianceQuery(BrqoshDbContext db) | CEEF-230 · ط1-٦ | المُخطَّطُ مقابلَ الفعليّ. |
| CashFlowStatementService.cs | var depreciation = WorkingCapitalEffect( | CEEF-228 | كان invert: true يقلب الإشارة مرّة ثانية. |
| CashFlowStatementService.cs | var depreciation = WorkingCapitalEffect( | CEEF-229 · م2-١٤ | بالبادئة لا بالمساواة — عرفٌ محاسبيّ شائع أن يكون لكل فئة |
| CashFlowStatementService.cs | (SaudiChartOfAccounts.SystemCodes.VatInput, "تغيّر ضريبة القيمة المضافة — مدخلات"), | CEEF-228 | هذه الخمسة كانت غائبة، وكلّها تتحرّك في كلّ عملية بيع أو شراء |
| CashFlowStatementService.cs | var workingCapitalTotal = | CEEF-229 · م2-١٤ | البقيّة — وهي ما كان يسقط صامتاً. |
| CashFlowStatementService.cs | var equityDelta = DeltaForPrefix(startBalances, endBalances, "31", | CEEF-228 | 3103 «الأرباح المبقاة» كانت مشمولةً، وقيدُ الإقفال السنويّ يُدخل فيها |
| CashFlowStatementService.cs | private static decimal WorkingCapitalEffect( | CEEF-229 · م2-١٤ | أثرُ رأس المال العامل النقديّ لكل حسابٍ تحت بادئةٍ ما. |
| CashFlowStatementService.cs | var accountType = type | CEEF-228 | كان النوع يُستنتج من أرصدة بداية المدة وحدها. وحسابٌ لا رصيد |
| CashFlowStatementService.cs | private static decimal DeltaForPrefix( | CEEF-229 · م2-١٤ | الاستثناء صار بالبادئة، والمعامل الميت حُذف. |
| CashFlowStatementService.cs | private static decimal SumCash(Dictionary<string, (AccountType Type, decimal SignedBalance | CEEF-229 · م2-١٤ | النقدية بالبادئة لا بالمساواة التامّة. |
| CompanyBrandingStorage.cs | public static string SaveLogoPng(long companyId, byte[] pngBytes) | CEEF-227 | حفظ شعار مُعالَج (خلفية مفرّغة) — دائماً PNG لأن الشفافية لا تُحفَظ |
| ConsolidatedBalanceSheetQuery.cs | if (ctaPlug != 0 && anyTranslation) | جاهزيّةُ الإطلاق · ح-٤٥ | ‏والفرقُ «احتياطيُ ترجمة» حيث وقعت ترجمةٌ فقط. |
| ConsolidatedBalanceSheetQuery.cs | private async Task<(decimal Earnings, bool Translated)> UnclosedEarningsAsync( | جاهزيّةُ الإطلاق · ح-٤٥ · accounting-core#5 | أرباحُ الشركة غير المقفلة منذ التأسيس — |
| CostCenterStatementQuery.cs | public class CostCenterStatementQuery(BrqoshDbContext db) | CEEF-230 · ط1-٧ | الفعليُّ بمركز التكلفة — وهو التقريرُ الذي لم يكن. |
| DashboardQuery.cs | public record CashTrendPoint(int Year, int Month, decimal Collected, decimal Paid) | CEEF-216 | نقطة شهرية حقيقية في اتجاه النقد . |
| DashboardQuery.cs | decimal RevenueThisMonth = 0m, | CEEF-214 | مؤشرات محاسبية حقيقية من قائمة الدخل — لا من إجماليات المستندات. |
| DashboardQuery.cs | IReadOnlyList<CashTrendPoint>? CashTrend = null); | CEEF-216 | اتجاه نقد حقيقي من القاعدة — كان يُصطنع بضرب رقم الشهر الجاري |
| DashboardQuery.cs | var pnl = await new IncomeStatementQuery(db).ExecuteAsync(companyId, monthStart, today); | CEEF-214 | الإيراد والمصروف والربح تُقرأ من قائمة الدخل — المصدر المحاسبي |
| DataQualityService.cs | await AddSubledgerIdentityAsync(findings, companyId, "AR_SUBLEDGER_DRIFT", | جاهزيّةُ الإطلاق · ح-٢٨ | ‏هويّاتُ الدفاتر المساعدة تُقاس في المنتَج لا في الاختبار وحدَه. |
| DataQualityService.cs | var chain = await new AuditChainVerifier(db).VerifyAsync(companyId, ct); | جاهزيّةُ الإطلاق · ح-٢٩ | ‏سلسلةُ تجزئة سجلّ التدقيق تُفحَص في المنتَج. |
| DataQualityService.cs | private async Task AddSubledgerIdentityAsync( | جاهزيّةُ الإطلاق · ح-٢٨ | يُقارن دفتراً مساعداً بحساب مراقبته، ويُبلّغ الفرقَ |
| DocumentPrintService.Reports.cs | var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == customerId | CEEF-229 · م2-١٨ | قراءةٌ تتجاوز مرشّح الحذف الناعم عمداً — المستند تاريخيّ. |
| DocumentPrintService.Reports.cs | internal async Task<ProgressBillingPdfGenerator.BillingPrintData> ProgressBillingRequestAs | جاهزيّةُ الإطلاق · ح-٥٦ · ui-ux-i18n-printing#1 | ما يُطبع منه المستخلص — ويُقاس قبل أن يُرسَم |
| DocumentPrintService.Reports.cs | ZatcaDocumentType.AssetDisposal => await ExportAssetDisposalAsync(companyId, doc.SourceDoc | جاهزيّةُ الإطلاق · ح-١٦ | ‏وبيعُ الأصل الثابت — مستندٌ ضريبيٌّ يأخذه المشتري. |
| DocumentPrintService.Reports.cs | ZatcaDocumentType.InvoiceCancel or ZatcaDocumentType.BillingCancel or ZatcaDocumentType.De | جاهزيّةُ الإطلاق · ح-١٩ | ‏ومستنداتُ الإلغاء الثلاثة — بأنفسها لا بمصادرها. |
| DocumentPrintService.Reports.cs | public async Task<PrintResult> ExportPayslipAsync( | CEEF-230 · ط6-٢ | قسيمةُ راتبٍ واحدة — والمولِّدُ كان مبنيّاً بلا مستدعٍ واحد. |
| DocumentPrintService.cs | var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == invoice.Cu | CEEF-229 · م2-١٨ | قراءةٌ تتجاوز مرشّح الحذف الناعم عمداً — المستند تاريخيّ. |
| DocumentPrintService.cs | internal sealed record PrintedAmounts( | جاهزيّةُ الإطلاق · ح-٤٢ · tax-zatca#14 | مبالغُ المستند المطبوعة — بعملةٍ واحدة. |
| DocumentPrintService.cs | public async Task<PrintResult> ExportAssetDisposalAsync( | جاهزيّةُ الإطلاق · ح-١٦ | فاتورةُ بيع الأصل الثابت مطبوعةً — يأخذها المشتري. |
| DocumentPrintService.cs | public async Task<PrintResult> ExportZatcaCancellationAsync( | جاهزيّةُ الإطلاق · ح-١٩ | مستندُ الإلغاء يُطبع — وكان يرمي. |
| DocumentPrintService.cs | internal static string? Phase1QrFallback( | CEEF-226 | رمز QR للمرحلة الأولى يُحسب عند الطباعة حين لا يوجد مستند ZATCA. |
| DocumentPrintService.cs | internal static string? Phase1QrFallback( | CEEF-229 · م2-٢٤ |  وطابعُه الزمنيّ صار لحظةَ الترحيل المسجَّلة. |
| FinancialReportPdfGenerator.cs | public static IReadOnlyList<(string Section, string Code, string Name, decimal Amount)> Va | جاهزيّةُ الإطلاق · ح-٤٠ | سطورُ الإقرار المطبوع — دالّةٌ صافيةٌ تُقاس. |
| FinancialReportPdfGenerator.cs | private static void AddBreakdown( | جاهزيّةُ الإطلاق · ح-٤١ · tax-zatca#12 | والمطبوعُ يحمل الفئاتِ كالشاشة — وكان |
| GeneralLedgerQuery.cs | var priorTotals = await db.JournalEntries | CEEF-206 | الأرصدة الافتتاحية تُجمَّع في قاعدة البيانات لا في الذاكرة. |
| GeneralLedgerQuery.cs | var isDebitNature = account.Type.IsDebitNature(); | CEEF-228 | opening موجبٌ إذا كان على الجانب الطبيعيّ للحساب، وسالبٌ إذا |
| IncomeStatementQuery.cs | public async Task<IncomeStatementResult> ExecuteAsync( | ع-R2، CEEF-201 | الافتراضي true: قائمة الدخل تعرض أداء الفترة، لا أثر قيد إقفالها . |
| PayableAgingQuery.cs | public class PayableAgingQuery(BrqoshDbContext db) | CEEF-231 · ط4-٣ب | أعمارُ ديون الموردين — والجانبُ الآخر كان وحدَه مبنياً. |
| PayslipPdfGenerator.cs | decimal AbsenceDeduction = 0m, | جاهزيّةُ الإطلاق · ح-٢٤ | ‏الغيابُ والسلفة — وكانا غائبَين عن القسيمة وحدَها. |
| PayslipPdfGenerator.cs | public decimal GrossPay => | جاهزيّةُ الإطلاق · ح-٢٤ | الاستحقاقُ بعد الغياب — كما يحسبه PayrollLine. |
| PayslipPdfGenerator.cs | public static class PayslipPdfGenerator | CEEF-230 · ط2-٦ | قسيمةُ الراتب. |
| PrintBranding.cs | var address = company.NationalAddress.IsComplete | CEEF-229 · Z-10 | الترويسة تُفضّل العنوان الوطنيّ على النصّ الحرّ متى اكتمل. |
| PrintDocumentKind.cs | SalesDebitNote = 7, | جاهزيّةُ الإطلاق · ح-١٩ | إشعارٌ مدينٌ على جانب المبيعات — يُصدَر لإلغاء |
| PrintText.cs | public static class PrintText | CEEF-226 | عزل النصّ اللاتينيّ/الرقميّ داخل فقرة عربية. |
| PrintedCompliance.cs | internal static class PrintedCompliance | جاهزيّةُ الإطلاق · ح-٥٤ | سطرُ الامتثال المطبوع — واحدٌ لكلّ ورقة. |
| PrintedCompliance.cs | internal static class PrintedCompliance | CEEF-229 · م0-١٠ |  الحالةُ تُطبع بالعربيّة، والمحاكاةُ تُعلَن صراحةً — كان يُطبع اسمُ |
| ProgressBillingPdfGenerator.cs | PrintDocumentKind Kind, | جاهزيّةُ الإطلاق · ح-٥٦ · ui-ux-i18n-printing#1 | ‏المستخلصُ فاتورةٌ ضريبيّة — نوعُها ونسبتُها |
| ProgressBillingPdfGenerator.cs | string? ClearanceNotice = null, | جاهزيّةُ الإطلاق · ح-٥٣ | ‏المستخلصُ لعميلٍ مسجَّلٍ يُخلَّص — وما لم يُعتمد يقول ذلك. |
| ProgressBillingPdfGenerator.cs | internal static string Title(BillingPrintData data) => | جاهزيّةُ الإطلاق · ح-٥٦ · ui-ux-i18n-printing#1 | عنوانُ الورقة عنوانُ الفاتورة — ثمّ «مستخلص |
| ProgressBillingPdfGenerator.cs | private static string? BuildComplianceLine(BillingPrintData data) => | جاهزيّةُ الإطلاق · ح-٥٤ | ‏وكان يطبع اسمَ الحالة البرمجيّ — «SimulatedLocally» و«ReadyToClear» |
| ReceivableAgingQuery.cs | public class ReceivableAgingQuery(BrqoshDbContext db) | CEEF-230 · ط4-٣ | أعمارُ الديون — تقريرٌ يُحيل إليه النظامُ نفسُه ولا وجودَ له. |
| ReceivableAgingQuery.cs | open.AddRange(billings.Where(b => b.CustomerId == customerId && b.BillingDate <= asOf) | CEEF-232 · ج-٣ | ‏والمستخلصُ يدخل بذمّتِه لا بإجماليّه. |
| ReportingJournalEntries.cs | public static IQueryable<JournalEntry> ExcludeYearEndClosing(this IQueryable<JournalEntry> | ع-R2، CEEF-201 | : قيد الإقفال مؤرَّخ داخل السنة نفسها ويصفّر الإيرادات والمصروفات، |
| SalesInvoicePdfGenerator.cs | decimal Discount = 0m); | جاهزيّةُ الإطلاق · ح-٤٤ | ‏خصمُ السطر مبلغاً — Qty × Price − Discount = Total. |
| SalesInvoicePdfGenerator.cs | string? PartyVatNumber = null, | CEEF-227 | الفاتورة الضريبية تلزمها بيانات المشتري لا اسمه وحده. |
| SalesInvoicePdfGenerator.cs | CurrencyDisclosure? Currency = null, | جاهزيّةُ الإطلاق · ح-٤٢ | عدمٌ = مستندٌ بالريال. |
| SalesInvoicePdfGenerator.cs | string? ReferenceDocumentNumber = null, | جاهزيّةُ الإطلاق · ح-٤٣ · ui-ux-i18n-printing#0 | ‏الإشعارُ يُسمّي ما يُصحّحه ولماذا. |
| SalesInvoicePdfGenerator.cs | string? ClearanceNotice = null); | جاهزيّةُ الإطلاق · ح-٥٣ · tax-zatca#9 | ‏مستندُ تخليصٍ لم يُعتمد يقول ذلك على الورقة — |
| SalesInvoicePdfGenerator.cs | public sealed record CurrencyDisclosure( | جاهزيّةُ الإطلاق · ح-٤٢ · tax-zatca#14 | مستندٌ بعملةٍ أجنبيّة: رمزُها وسعرُها |
| SalesInvoicePdfGenerator.cs | decimal DocumentDiscount, | جاهزيّةُ الإطلاق · ح-٤٤ | ‏خصمُ المستند — إلزاميٌّ بلا افتراضيّ، ولا يُستنتَج. |
| SalesInvoicePdfGenerator.cs | var shares = AllocateDocumentDiscount(lines, r.DocumentDiscount); | جاهزيّةُ الإطلاق · ح-٤٤ · ui-ux-i18n-printing#2 | ‏خصمُ المستند يُوزَّع على السطور |
| SalesInvoicePdfGenerator.cs | internal static IReadOnlyList<decimal> AllocateLineVat( | CEEF-227 | توزيع ضريبة الفاتورة على البنود بحصّة كلّ بند من الوعاء. |
| SalesInvoicePdfGenerator.cs | private static string? BuildComplianceLine(InvoicePrintOptions options) => | CEEF-229 · م0-١٠ · ح-٥٤ | ‏الحالةُ بالعربيّة والمحاكاةُ مُعلَنة — من PrintedCompliance وحدَه، |
| TrialBalanceQuery.cs | public async Task<IReadOnlyList<TrialBalanceRow>> ExecuteAsync( | CEEF-230 · ط1-٧ | ‏مركزُ تكلفةٍ بعينه — وnull يعني الكلّ، وهو الأصل. |
| VatReturnMapping.cs | public static class VatReturnMapping | CEEF-229 · م1-١٠ / Z-12 | عبورُ الإقرار من الاستعلام إلى الحدّ — وهو ما كان مقطوعاً. |
| VatReturnMapping.cs | CancelledOutputVat: r.CancelledOutputVat, | جاهزيّةُ الإطلاق · ح-٤٠ | ‏ والحدودُ الأربعةُ تعبر الحدّ — وكانت تُحسب في |
| VatReturnMapping.cs | Map(r.SalesBoxes), | جاهزيّةُ الإطلاق · ح-١٦٥ | ‏ ‏وخاناتُ النموذج تعبر الحدَّ كذلك — وصلةٌ تُقاس هنا لا في المُحوِّل. |
| XlsxWriter.cs | public static class XlsxWriter | CEEF-215 | مولّد ملفات Excel حقيقية (.xlsx) — بلا أي حزمة خارجية. |
| PosReceiptBuilder.cs | public static class PosReceiptBuilder | جاهزيّةُ الإطلاق · ح-١٢٣ · ‏`ui-ux-i18n-printing#8` | ‏ الإيصالُ يُبنى هنا ويُطبع في الشاشة. |
| PosReportQuery.cs | public class PosReportQuery(BrqoshDbContext db) | CEEF-230 · ط6-٨ | ملخّصُ نقطة البيع — وكان يعرض ما لم يمرّ بالدرج. |
| PosSaleService.cs | var posVatCategory = isVatRegistered ? VatCategory.Standard : VatCategory.OutOfScope; | CEEF-228 | كان الوسيط isVatRegistered يُستقبَل ولا يُستعمل ولو مرّة في جسم الدالة، |
| PosSaleService.cs | var posReasonCode = isVatRegistered ? null : VatExemptionReason.OutOfScopeCode; | CEEF-229 · م1-١ب الشقّ الثاني | والفئةُ «خارج النطاق» تقول سببَها: رمزٌ نظاميّ |
| PosSaleService.cs | invoice.SetWarehouse(shift.WarehouseId); | جاهزيّةُ الإطلاق · ح-١١٤ · ‏`order-to-cash-procure-to-pay-inventory#12` | ‏ ومستودعُ الوردية هو مستودعُ الصرف — وفارغُه الافتراضيّ. |
| PosSaleService.cs | await new Sales.SalesInvoicePostingService(db).PostCoreAsync(invoice, token); | CEEF-230 · ط6-٧ | ‏والتوليدُ يقع هنا الآن — داخل PostCoreAsync نفسِه، |
| PosShiftVariancePostingService.cs | public class PosShiftVariancePostingService(BrqoshDbContext db) | CEEF-232 · ن-١ | فرقُ نقدِ الوردية — يُحسب ويُعرض ولا يُقيَّد. |
| CollectableCustomersQuery.cs | public class CollectableCustomersQuery(BrqoshDbContext db) | CEEF-229 · م2-٢٢ | مَن يُمكن تحصيلُه — لا مَن هو نشط. |
| CreditNoteCancellationService.cs | var inv = new InventoryService(db); | CEEF-229 · م0-٣ب | يُعكَس الأصلُ — لا تُرحَّل حركةٌ مضادّة. مرآةُ م0-٣. |
| CreditNoteCancellationService.cs | if (settings?.IsEnabled == true) | CEEF-229 · م1-٦ / Z-4 | التوليدُ داخل المعاملة — والإرسالُ خارجها. |
| CreditNotePostingService.cs | await PostCoreAsync(note, ct); | CEEF-230 · ط6-٧ | ‏التوليدُ نزل إلى PostCoreAsync — انظر الشرحَ هناك. |
| CreditNotePostingService.cs | var saleWarehouses = await SaleWarehousesAsync(invoice, ct); | جاهزيّةُ الإطلاق · ح-١١٠ · ‏`order-to-cash-procure-to-pay-inventory#12` | ‏ ‏المرتجعُ يعود إلى المستودع الذي خرج منه البيع — |
| CreditNotePostingService.cs | var soldOut = await SoldBatchesStillOutAsync(invoice, previousCredits, products, ct); | جاهزيّةُ الإطلاق · ح-١٠٢ · ‏`order-to-cash-procure-to-pay-inventory#7` | ‏ ‏المتتبَّعُ يعود إلى دفعاته. |
| CreditNotePostingService.cs | if (note.Id == 0) | CEEF-230 · ط2-٠ | ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ JournalPostingService نفسُه. |
| CreditNotePostingService.cs | await ZatcaPostingOrchestrator.GenerateInsideTransactionAsync( | CEEF-230 · ط6-٧ | ‏والمستندُ الإلكترونيُّ من المسار المشترك — كما في فاتورة |
| CreditNotePostingService.cs | private static void EnsureReturnedQuantitiesWithinInvoice( | CEEF-229 · م0-٦ | التحقق الكمّي لكل صنف مقابل الفاتورة الأصلية. |
| CreditNotePostingService.cs | private async Task<List<SoldBatch>> SoldBatchesStillOutAsync(SalesInvoice invoice, | جاهزيّةُ الإطلاق · ح-١٠٢ | ما بيع من كلّ دفعةٍ ولم يُرجَع بعد — من سطور حركة البيع نفسِها (وهي تحمل الدفعة |
| CreditNotePostingService.cs | private static List<(string? Batch, DateOnly? Expiry, decimal Quantity, decimal UnitCost,  | جاهزيّةُ الإطلاق · ح-١٠٢ | يُقسّم كمّيّةَ المرتجع على دفعات البيع، وينقص ما خرج منها فلا يُرجَع إلى دفعةٍ |
| CreditNotePostingService.cs | if (method == Domain.Retail.PosPaymentMethod.Card) | CEEF-229 · م2-١٥أ | المرتجع يخرج من الحساب الذي دخلت فيه البيعة. |
| CustomerBalanceQuery.cs | private static IQueryable<CreditNote> ArCreditNotes(BrqoshDbContext db, long companyId, lo | CEEF-229 · م0-٩ | إشعارات الدائن التي تمسّ الذمم — دون إشعارات نقاط البيع. |
| CustomerBalanceQuery.cs | private static decimal ReceivablePortionOf(ProgressBilling billing) => | CEEF-232 · ج-١ | ما بلغ حسابَ الذمم من المستخلص — لا إجماليَّه. |
| CustomerBalanceQuery.cs | private static async Task<decimal> ReleasedRetentionAsync( | CEEF-232 · ج-١ | والمحتجزُ المُفرَج عنه يعود ذمّةً — وهذا هو الطرفُ الذي يُنسى. |
| CustomerBalanceQuery.cs | internal static async Task<IReadOnlyList<RetentionReleaseDocument>> ReleasedRetentionDocum | CEEF-232 · ج-٢ | الإفراجاتُ مفردةً بتواريخها — لا مجموعاً واحداً. |
| CustomerBalanceQuery.cs | var parts = entry.SourceDocument!.Split(':'); | CEEF-232 · ج-٢ | ‏والنوعُ يُقرأ من المصدر لا يُفترض من الحساب. |
| CustomerBalanceQuery.cs | private static async Task<IReadOnlyDictionary<long, decimal>> AppliedDepositsByCustomerAsy | CEEF-232 · ح-٢ | وعربونُ الحجز المُنزَل يُنقص الذمّة — في الدفتر كما في الأستاذ. |
| CustomerBalanceQuery.cs | var received = await db.ReceiptVouchers | CEEF-230 · ط4-٠ | ‏ما حُرِّر من الذمّة لا ما دخل الصندوق — كما في |
| CustomerBalanceQuery.cs | received += (await AppliedDepositsByCustomerAsync(db, companyId, null, customerId)) | CEEF-232 · ح-٢ | ‏ ‏والعربونُ المُنزَل سدادٌ — يُنقص الذمّة كسند القبض. |
| CustomerBalanceQuery.cs | .Select(g => new | CEEF-230 · ط1-٤ | ‏ما حُرِّر من الذمّة لا ما دخل الصندوق. |
| CustomerBalanceQuery.cs | foreach (var release in await ReleasedRetentionDocumentsAsync(db, companyId, asOf: null, c | CEEF-232 · ج-٢ | ‏والمُفرَج عنه سطرٌ في الكشف — وإلّا خالف الرأسُ سطورَه. |
| CustomerBalanceQuery.cs | foreach (var n in await ArCreditNotes(db, companyId, customerId).ToListAsync()) | CEEF-229 · م0-٩ | كشف الحساب يتبع الرصيد: إشعار نقطة البيع لا يمسّ الذمم فلا يظهر فيه. |
| RecurringInvoiceService.cs | public class RecurringInvoiceService(BrqoshDbContext db) | CEEF-230 · ط1-٥ | يُولّد فواتيرَ الخطط المستحقّة حتى تاريخٍ معلوم. |
| RecurringInvoiceService.cs | var issued = new List<long>(); | CEEF-230 · ط6-٧ | ‏ما وُلِّد يُجمع ليُبلَّغ بعد التشغيلة كلِّها — لا داخل |
| RecurringInvoiceService.cs | invoice.SetWarehouse(plan.WarehouseId); | جاهزيّةُ الإطلاق · ح-١١٤ · ‏`order-to-cash-procure-to-pay-inventory#12` | ‏ وفاتورةُ الخطة تصرف من مستودع الخطة — وفارغُه الافتراضيّ. |
| RecurringInvoiceService.cs | await new LeaseRentRecognition(db).DeferAsync(plan, invoice, occurrence, token); | جاهزيّةُ الإطلاق · ح-٧٩ | ‏ ‏وفاتورةُ عقد إيجارٍ تُغطّي أكثرَ من شهرٍ يُؤجَّل ما زاد على شهرها الأوّل — في |
| RejectedInvoiceReissueService.cs | public sealed record CorrectedInvoiceSeed( | جاهزيّةُ الإطلاق · <c>tax-zatca#8</c> | ما تحمله الفاتورةُ المصحَّحة من المرفوضة — العميلُ والعملةُ وسعرُها والمستودعُ وخصمُ |
| RejectedInvoiceReissueService.cs | public class RejectedInvoiceReissueService(BrqoshDbContext db) | جاهزيّةُ الإطلاق · <c>tax-zatca#8</c> | المرفوضُ يُصحَّح ويُعاد إصداره — وكان لا مسارَ له: مستندٌ «مرفوض» في سجلّ الفوترة |
| SalesInvoiceCancellationService.cs | await new LeaseRentRecognition(db).WithdrawForCancelledInvoiceAsync( | جاهزيّةُ الإطلاق · ح-٧٩ | ‏ ‏وفاتورةُ إيجارٍ مؤجَّلٍ يُعكس تأجيلُها واعترافاتُها معها — وإلّا بقي المقبوضُ |
| SalesInvoiceCancellationService.cs | var inv = new InventoryService(db); | CEEF-229 · م0-٣ / I-2 | يُعكَس الأصلُ — لا تُرحَّل حركةٌ مضادّة. |
| SalesInvoiceCancellationService.cs | if (settings?.IsEnabled == true) | CEEF-210 | التوليد داخل المعاملة (يكتب سجلات يجب أن ترتدّ مع الإلغاء)، |
| SalesInvoicePostingService.cs | await PostCoreAsync(invoice, ct); | CEEF-230 · ط6-٧ | ‏والتوليدُ نزل إلى PostCoreAsync فصار كلُّ قناةٍ |
| SalesInvoicePostingService.cs | var invoiceNumber = await NextInvoiceNumberAsync(invoice.CompanyId, invoice.InvoiceDate); | CEEF-229 · م0-٣ | الرقمُ يُخصَّص قبل استهلاك الطبقات — فهو مفتاحُ سجلّ |
| SalesInvoicePostingService.cs | if (invoice.PosShiftId is null \|\| invoice.PosPaymentMethod is null) | جاهزيّةُ الإطلاق · ح-٩٥ · order-to-cash#13 | ‏ حدُّ ائتمان العميل — قبل استهلاك الطبقات، وبعد قفل التسلسل |
| SalesInvoicePostingService.cs | var warehouse = await ResolveWarehouseAsync(inv, invoice, ct); | جاهزيّةُ الإطلاق · ح-١١٠ · ‏`order-to-cash-procure-to-pay-inventory#12` | ‏ ‏البيعُ يُصرف من مستودع الفاتورة — لا الافتراضيِّ أبداً. |
| SalesInvoicePostingService.cs | var qty = await new UnitConversionService(db).ToProductUnitAsync( | CEEF-230 · ط1-١ | ‏يُحوَّل قبل فحص الرصيد: بيعُ كرتونٍ من مئةِ حبّةٍ |
| SalesInvoicePostingService.cs | if (invoice.Id == 0) | CEEF-230 · ط2-٠ | ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ JournalPostingService نفسُه. |
| SalesInvoicePostingService.cs | await new Hospitality.KitchenIssueService(db).IssueForSoldProductsAsync( | CEEF-230 · ط3-٣ج | ‏وبيعُ طبقٍ مرتبطٍ بوصفةٍ يصرف مكوّناتِه — ومن هنا لأنّ |
| SalesInvoicePostingService.cs | await ZatcaPostingOrchestrator.GenerateInsideTransactionAsync( | CEEF-230 · ط6-٧ | ‏والمستندُ الإلكترونيُّ يُولَّد من هنا لا من فوق — فكلُّ |
| SalesInvoicePostingService.cs | private async Task EnsureWithinCreditLimitAsync(SalesInvoice invoice, CancellationToken ct | جاهزيّةُ الإطلاق · ح-٩٥ · <c>order-to-cash-procure-to-pay-inventory#13</c> | البيعُ الآجلُ لا يتجاوز حدَّ ائتمان العميل — المفتوحُ |
| SalesInvoicePostingService.cs | if (method == PosPaymentMethod.Card) | CEEF-206 | الدفع المختلط لا يُرحَّل: لا يوجد في النموذج أي حقل يحمل تقسيم |
| SalesInvoicePostingService.cs | if (method == PosPaymentMethod.Card) | CEEF-229 · م2-١٥ | بيعُ البطاقة يذهب إلى حساب البنك نفسه لا إلى الأب 1102. |
| SalesInvoicePostingService.cs | await DocumentSequenceLock.AcquireAsync(db.Database, companyId, "INV", fiscalYear); | CEEF-229 · م1-١٤ | قبل القراءة — وهذا المسارُ خاصٌّ بالفواتير ولا يمرّ |
| SalesInvoicePostingService.cs | .OrderByDescending(i => i.InvoiceNumber!.Length) | CEEF-206 | الترتيب بالطول أولاً ثم نصياً: التسلسل مصفوف بست خانات، فبعد |
| SalesOrderService.cs | public Task<SalesInvoice> CreateInvoiceFromOrderAsync(long companyId, long orderId, | جاهزيّةُ الإطلاق · ح-٣٣ | الفاتورةُ تُؤرّخ يومَ صدورها لا يومَ الأمر، |
| SalesOrderService.cs | invoice.SetWarehouse(warehouseId); | جاهزيّةُ الإطلاق · ح-١١٠ · ‏`order-to-cash-procure-to-pay-inventory#12` | ‏ ‏الفاتورةُ تُصرف من المستودع المختار (والفارغُ |
| SalesOrderService.cs | var inv = new InventoryService(db); | جاهزيّةُ الإطلاق · ح-١٠٤ · `order-to-cash-procure-to-pay-inventory#7` | ‏ ‏الصنفُ المتتبَّعُ يُخصَّص له الأقدمُ |
| AdminRecoveryKey.cs | public class AdminRecoveryKey(BrqoshDbContext db) | جاهزيّةُ الإطلاق · <c>security-licensing-tenancy#7</c> · قرارُ المالك 2026-10-18 | مفتاحُ استعادة المدير. |
| ApiSecuritySettings.cs | public static UserRole ResolveApiRole(bool isProduction) | CEEF-229 · م2-١ | دور مستدعي API — يضبطه الخادم، لا المستدعي. |
| AuditChainVerifier.cs | UnchainedEntry, | CEEF-229 | صفٌّ غيرُ مربوطٍ ظهر بعد بدء السلسلة — أتى من خارج التطبيق. |
| AuditChainVerifier.cs | public sealed class AuditChainVerifier(BrqoshDbContext db) | O-19، CEEF-212 | التحقق من سلامة سلسلة سجل التدقيق. |
| AuditChainVerifier.cs | var firstChainedId = entries | CEEF-229 | ‏حدُّ الحقبة: أوّلُ صفٍّ مربوطٍ كُتب لهذه المنشأة. |
| AuditChainVerifier.cs | if (string.IsNullOrEmpty(entry.EntryHash)) | CEEF-229 | ‏ صفوفُ ما قبل السلسلة: تجزئتُها فارغة — لا سلسلةَ لها تُفحص. |
| AuditService.cs | await UnitOfWork.ExecuteInTransactionAsync(db, async token => | O-19، CEEF-212 | اربط الإدخال بسلسلة الشركة قبل الحفظ. |
| AuditService.cs | await UnitOfWork.ExecuteInTransactionAsync(db, async token => | CEEF-229 | ‏والمعاملةُ شرطُ حياة القفل، لا تحسيناً. |
| AuditService.cs | entry.ChainTo( | CEEF-229 | ‏ترتيبُ ما قبل السلسلة سالب، و«الأعلى + ١» عليه يُنتج صفراً — |
| AuditService.cs | if (db.Database.CurrentTransaction is null) | CEEF-229 | ‏الشرطُ مُعلَنٌ هنا لا مفترَضاً في المستدعي. |
| LeastPrivilegeDatabaseRole.cs | public static class LeastPrivilegeDatabaseRole | جاهزيّةُ الإطلاق · ح-١٥١ · <c>security-licensing-tenancy#5</c> | حسابُ التطبيق على PostgreSQL بأقلّ صلاحيّة — حدٌّ في الخادم لا في البرنامج. |
| PersistentLoginLockout.cs | public sealed class PersistentLoginLockout(BrqoshDbContext db) | CEEF-237 · ت-٦ | قفلُ الدخول صار يعيش في القاعدة — وكان يُمحى بإعادة تشغيل التطبيق. |
| SecurityContext.cs | public static UserRole? CurrentRole | CEEF-229 · م2-١ | حُذف AutomatedTestBypass — ولم يكن نقصَ اختبارٍ بل تناقضاً مكتوباً. |
| UserAuthService.cs | await new PersistentLoginLockout(db).ThrowIfLockedAsync(companyId, normalized); | CEEF-237 · ت-٦ | ‏ القفلُ يُفرَض هنا لا في النافذة. |
| UserManagementService.cs | public async Task UpdateAsync( | جاهزيّةُ الإطلاق · ح-٥٠ · <c>security-licensing-tenancy#4</c> + <c>#8</c> | ‏ تغييرُ الصلاحيّة |
| UserManagementService.cs | SecurityContext.EnsureAdministrator(); | CEEF-229 · م2-١، S-7 | كانت الدالّة العامّة الوحيدة في هذا الصنف بلا حارس، |
| WhatsAppChannelOptions.cs | AccessToken = LocalSecretProtector.Unprotect(file.AccessToken ?? ""), | CEEF-237 · ت-٧ | ‏ يُفَكُّ ما كان محميّاً، ويمرّ ما كان صريحاً كما هو — |
| WhatsAppChannelOptions.cs | AccessToken = LocalSecretProtector.Protect(options.AccessToken), | CEEF-237 · ت-٧ | ‏ الرمزُ يُحفَظ محميّاً كسائر الأسرار. |
| WhatsAppNotificationChannel.cs | var revealed = Brqosh.Application.Setup.AppEnvironmentConfig.IsProduction ? null : hint; | إغلاق ح-1، CEEF-200 | أمن : لا يُكشف الرمز في الواجهة في الإنتاج — يمنع الاستيلاء |
| WhatsAppNotificationChannel.cs | public sealed class UnavailableOtpNotificationChannel : IWhatsAppNotificationChannel | CEEF-229 · م0-١١ | لا قناة تسليم مهيّأة في الإنتاج — تفشل صراحةً. |
| WhatsAppNotificationChannelFactory.cs | public static class WhatsAppNotificationChannelFactory | CEEF-229 · م0-١١ | اختيار قناة تسليم رمز التحقق. |
| CsvDataMigrationService.cs | public async Task<DataMigrationResult> ImportSuppliersCsvAsync( | جاهزيّةُ الإطلاق · ح-١٣٣ · accounting-core#7 | ‏ الموردون — وكان الترحيلُ لا يعرفهم. كالعملاء: الاسمُ والرقمُ |
| CsvDataMigrationService.cs | public async Task<DataMigrationResult> ImportOpeningTrialBalanceCsvAsync( | جاهزيّةُ الإطلاق · ح-١٣٣ · accounting-core#7 | ‏ الميزانُ الافتتاحيّ — قيدٌ واحدٌ مرحَّلٌ أو لا شيء. |
| CsvDataMigrationService.cs | public async Task<DataMigrationResult> ImportProductsCsvAsync( | جاهزيّةُ الإطلاق · ح-١٣٥ · commercial-readiness#15 | ‏ الأصناف — «Code,NameAr,Unit,Type,SalePrice»؛ والنوعُ |
| CsvDataMigrationService.cs | public async Task<DataMigrationResult> ImportOpeningStockCsvAsync( | جاهزيّةُ الإطلاق · ح-١٣٥ | ‏ المخزونُ الافتتاحيّ — «ProductCode,Quantity,UnitCost» في المستودع الافتراضيّ بتاريخ بدء |
| CsvDataMigrationService.cs | private async Task<DataMigrationResult> ImportOpenItemsAsync( | جاهزيّةُ الإطلاق · ح-١٣٤ · accounting-core#7 | ‏ الذممُ المفتوحةُ لكلّ عميلٍ ومورِّد — ويُفرَغ بها الحسابُ الوسيط. |
| CsvRow.cs | public static class CsvRow | جاهزيّةُ الإطلاق · ح-١٣٥ · <c>data-upgrade-performance#8</c> | ‏ قاسمُ صفّ CSV الواحد — بالحقول المُقتبَسة كما تُصدّرها |
| EulaAcceptance.cs | public static class EulaAcceptance | جاهزيّةُ الإطلاق · <c>installer-deployment-ops#13</c> · قرارُ المالك 2026-10-18 | اتّفاقيّةُ الترخيص تُقبل قبل الاستخدام. |
| JoiningAnExistingCompany.cs | public static class JoiningAnExistingCompany | جاهزيّةُ الإطلاق · ح-١٥٠ · <c>installer-deployment-ops#9</c> | جهازٌ ثانٍ ينضمّ إلى منشأةٍ قائمة — صراحةً. |
| BankAccountService.cs | public async Task<BankAccount> CreateAsync(long companyId, string code, string nameAr, str | CEEF-231 · ط1-٤ب | عملةُ الحساب — وnull تعني الريال. |
| BankAccountService.cs | var availability = await new CodeAvailabilityQuery(db) | CEEF-229 · م2-١٠ / D-6 | ‏الفحصُ يشمل المحذوف. |
| BankGlAccountResolver.cs | public static class BankGlAccountResolver | CEEF-229 · م2-١٥ | حسابُ الأستاذ الذي يمثّل نقدَ البنك — من مكانٍ واحد. |
| BankGlAccountResolver.cs | public static async Task<Account> ResolveCodeAsync( | جاهزيّةُ الإطلاق · ح-٤٨ · <c>sector-modules#8</c> | ‏ نقدُ البنك لمسارٍ يقبل كوداً صريحاً |
| BankReconciliationQuery.cs | public record BankReconciliationStatement( | جاهزيّةُ الإطلاق · ح-١٥٤ · <c>treasury-payroll-assets#13</c> | مذكّرةُ التسوية: رصيدُ البنك المعدَّل مقابلَ رصيد الدفتر المعدَّل. |
| BankReconciliationQuery.cs | var lines = await db.JournalEntries | جاهزيّةُ الإطلاق · ح-٩٣ · treasury-payroll-assets#13 | ‏ سطورُ البنك وحدَها من القاعدة — وكان يُحمّل كلَّ قيدٍ في الفترة بسطوره |
| BankStatementDifferencePostingService.cs | public Task ReverseAsync( | جاهزيّةُ الإطلاق · ح-٨٩ · <c>treasury-payroll-assets#10</c> | عكسُ قيد فرق سطر الكشف — وكان لا يُعكس أبداً، ففرقٌ رُحِّل |
| BankTransferService.cs | public async Task<JournalEntry> ReverseAsync( | جاهزيّةُ الإطلاق · ح-٨٩ · <c>treasury-payroll-assets#10</c> | عكسُ تحويلٍ بنكيّ — وكان لا يُعكس أبداً: العكسُ العامُّ |
| CashFlowForecastService.cs | public sealed class CashFlowForecastService(BrqoshDbContext db) : ICashFlowForecastService | CEEF-230 · ط4-٣ج | التدفّقُ المتوقَّع — والسببُ الأوّلُ لتعثّر منشآتٍ رابحة. |
| ChequePostingService.cs | await new JournalPostingService(db) | جاهزيّةُ الإطلاق · ح-٣٢ | ‏والحارسُ يقيس تاريخَ القيد نفسِه. |
| FxRevaluationService.cs | var lines = await db.JournalEntries | جاهزيّةُ الإطلاق · ح-٢٥ | ‏القيدُ المعكوسُ يبقى في الرصيد مع عكسه — أو يخرجان معاً. |
| FxRevaluationService.cs | var lines = await db.JournalEntries | جاهزيّةُ الإطلاق · ح-٩٣ · treasury-payroll-assets#13 | ‏ وسطورُ البنك وحدَها من القاعدة — وكان يُحمّل كلَّ قيدٍ حتى التاريخ |
| PaymentAllocationService.cs | var receivable = b.NetBillableAmount + b.VatAmount; | CEEF-232 · ج-٤ | ‏والمستخلصُ يُخصَّص عليه بذمّتِه لا بإجماليّه. |
| PaymentAllocationService.cs | await db.SaveChangesAsync(token); | CEEF-229 | ‏الحفظُ داخل القفل، وإلا لم يحمِ القفلُ شيئاً. |
| PaymentAllocationService.cs | var takenInThisBatch = new Dictionary<(PaymentAllocationDocumentType, long), decimal>(); | CEEF-229 · م1-١٦ / A-13 | ما خُصّص في هذه الدفعة يُطرح كذلك. |
| PaymentAllocationService.cs | if (line.Amount > remaining) | CEEF-206 | بلا تفاوت: كل المبالغ مقرّبة لخانتين أصلاً، فالتفاوت 0.01 كان |
| PaymentAllocationService.cs | private static async Task AcquireDocumentLocksAsync( | CEEF-229 | ‏قفلُ التخصيص — لكلّ (منشأة · مستند). |
| PaymentAllocationService.cs | PaymentAllocationDocumentType.ProgressBilling => | CEEF-232 · ج-٤ | ‏والسقفُ هو الذمّةُ لا الإجماليّ — وإلّا قُبِل تخصيصٌ يتجاوز ما على العميل. |
| PaymentAllocationService.cs | private static decimal ReceivablePortionOf(ProgressBilling billing) => | CEEF-232 · ج-٤ | ما بلغ حسابَ الذمم من المستخلص — ويُحسب بعد التحقّق لا داخله. |
| PaymentVoucherPostingService.cs | private async Task<(decimal? Released, Account? Gain, Account? Loss)> ResolveSettlementAsy | CEEF-230 · ط4-٥ | يحسب ما يُحرَّر من ذمّة المورّد، ويُحضر حسابَي فرقِ العملة. |
| PaymentVoucherPostingService.cs | var (releasedPayable, fxGain, fxLoss) = | CEEF-230 · ط4-٥ | ‏قيمةُ الذمّة كما نشأت — بسعرِ يوم كلّ فاتورة، لا |
| PaymentVoucherPostingService.cs | var discountAccount = voucher.SettlementDiscount > 0m | CEEF-231 · ط1-٢ج | ‏حسابُ خصم السداد — ولا يُنشأ إلا حين يوجد خصم، |
| PaymentVoucherPostingService.cs | await FinancialPostingSupport.EnsureCostCenterAsync(db, voucher.CompanyId, voucher.CostCen | CEEF-230 · ط1-٧ | ‏مركزُ تكلفة المستند يُختم على سطور المصروف — وقبل |
| PaymentVoucherPostingService.cs | if (voucher.Id == 0) | CEEF-230 · ط2-٠ | ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ JournalPostingService نفسُه. |
| PaymentVoucherPostingService.cs | if (method == ReceiptMethod.Cash) | CEEF-229 · م2-١٥ | الحلّ من مكانٍ واحد — وكان السقوط هنا يُقيّد على الأب 1102 |
| ReceiptVoucherPostingService.cs | private async Task<(decimal? Released, Account? Gain, Account? Loss)> ResolveSettlementAsy | CEEF-230 · ط1-٤ | يحسب ما يُحرَّر من الذمّة، ويُحضر حسابَي فرقِ العملة. |
| ReceiptVoucherPostingService.cs | var (releasedReceivable, fxGain, fxLoss) = | CEEF-230 · ط1-٤ | ‏قيمةُ الذمّة كما نشأت — بسعرِ يوم كلّ فاتورة، لا بسعر |
| ReceiptVoucherPostingService.cs | var discountAccount = voucher.SettlementDiscount > 0m | CEEF-231 · ط1-٢ج | ‏حسابُ خصم السداد — ولا يُنشأ إلا حين يوجد خصم، |
| ReceiptVoucherPostingService.cs | if (voucher.Id == 0) | CEEF-230 · ط2-٠ | ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ JournalPostingService نفسُه. |
| ReceiptVoucherPostingService.cs | if (method == ReceiptMethod.Cash) | CEEF-229 · م2-١٥ | الحلّ من مكانٍ واحد — وكان السقوط هنا يُقيّد على الأب 1102 |
| PurchaseOrderWorkflowService.cs | public async Task<PurchaseOrder> ApproveAsync(long purchaseOrderId, string? comment = null | CEEF-229 · م1-١٤ | الرقمُ يُخصَّص هنا داخل المعاملة — لا في الشاشة. |
| PurchaseRequestWorkflowService.cs | public async Task<PurchaseRequest> SubmitAsync(PurchaseRequest request, CancellationToken  | CEEF-229 · م1-١٤ | الرقمُ يُخصَّص داخل معاملة الكتابة لا في الشاشة — فقفلُ |
| WorkflowEngine.cs | var instance = await db.WorkflowInstances | CEEF-229 · م2-١ | الرفض يتطلّب ما تتطلّبه الموافقة — وكان سائباً تماماً. |
| ZatcaAutoSubmissionHelper.cs | return await new ZatcaSubmissionService(db).SubmitAsFollowUpToPostingAsync(doc.Id); | CEEF-229 · م2-١ | المسار التلقائيّ لا يُعيد فحص الدور — الإذن مُنح عند |
| ZatcaAutoSubmissionHelper.cs | doc.RecordSubmissionFailure(Describe(ex)); | CEEF-210 | كان الالتقاط مقصوراً على AccountingException، بينما SubmitAsync |
| ZatcaAutoSubmissionHelper.cs | doc.RecordSubmissionFailure(Describe(ex)); | CEEF-229 · م2-١ | لكنّ الابتلاع الصامت كان يُخفي أكثر من انقطاع الشبكة. |
| ZatcaCsidOnboardingService.cs | private static HttpRequestMessage JsonPost(string url, object body) | جاهزيّةُ الإطلاق · ح-١٣٦ | واجهاتُ الهيئة كلُّها تشترط Accept-Version — والتسجيلُ كان بلاها. |
| ZatcaCsidOnboardingService.cs | public ZatcaKeyMaterial GenerateKeyPairAndCsr( | CEEF-237 · ز-١ | ‏CSR بامتدادَي الهيئة الإلزاميّين — وبدونهما يُرفض الطلبُ من أوّل خطوة. |
| ZatcaCsidOnboardingService.cs | public async Task<ZatcaCsidResult> RenewProductionCsidAsync( | جاهزيّةُ الإطلاق · ح-١٣٧ · <c>tax-zatca#10</c> | تجديدُ شهادة الإنتاج قبل انتهائها — دون إعادة التسجيل من أوّله. |
| ZatcaCsidOnboardingService.cs | private static string GetCertDirectory(long companyId) => | جاهزيّةُ الإطلاق · ح-٥ | جذرُ البيانات واحد — وكان هذا الموضعُ يبنيه بيده. |
| ZatcaHandOverCopy.cs | public static class ZatcaHandOverCopy | جاهزيّةُ الإطلاق · ح-٥٣ · <c>tax-zatca#9</c> | ما يُسلَّم للمشتري: ما اعتمدته الهيئة — |
| ZatcaHandOverCopy.cs | public static PrintedMark MarkFor(ZatcaEInvoiceDocument? document, Func<string?> withoutZa | CEEF-229 | محاكاةٌ محلّيّة ⟵ رمزُنا: لم يصل الهيئةَ شيء، والسطرُ المطبوعُ يقول «محاكاة» . |
| ZatcaKeyFile.cs | public static class ZatcaKeyFile | جاهزيّةُ الإطلاق · <c>security-licensing-tenancy#3</c> · قرارُ المالك 2026-10-18 | مفتاحُ ختم الفواتير مشفَّرٌ للجهاز. |
| ZatcaQrGenerator.cs | if (!string.IsNullOrWhiteSpace(publicKeyDerBase64)) | CEEF-229 · Z-9 | الوسم 8 — المفتاح العام لختم التشفير. |
| ZatcaQrGenerator.cs | if (!string.IsNullOrWhiteSpace(certificateSignatureBase64)) | جاهزيّةُ الإطلاق · ح-٧ | ‏ ‏الوسمُ ٩ — توقيعُ جهةِ الإصدار على مفتاحنا العامّ. |
| ZatcaQrGenerator.cs | if (data.Length <= 127) | CEEF-206 | الفرع الطويل كان يكتب 0x81 ثم (byte)Length — فأي طول ≥ 256 |
| ZatcaQrGenerator.cs | public static string GenesisPreviousInvoiceHash() => | CEEF-229 · م2-٢٢ | بصمةُ السابق لأوّل مستندٍ في حياة المنشأة — ثابتٌ تنصّ عليه الهيئة. |
| ZatcaRetryBackoff.cs | public sealed class ZatcaRetryBackoff | جاهزيّةُ الإطلاق · ح-١٥٦ · ‏`tax-zatca#8` | ‏ متى تُعاد محاولةُ إرسال المعلَّق في الخلفيّة. |
| ZatcaSettingsSecrets.cs | public static bool IsSignedOnAnotherMachine(ZatcaSettings settings) => | جاهزيّةُ الإطلاق · <c>tax-zatca#16</c> | هل الختمُ مسجَّلٌ على جهازٍ غيرِ هذا؟ — فالفواتيرُ الإلكترونيّةُ تُصدَر منه وحدَه، |
| ZatcaSubmissionService.cs | if (certOk) | جاهزيّةُ الإطلاق · ح-٥٢ · `tax-zatca#10` | ‏ ‏وصلاحيّةُ الشهادة اليومَ لا وجودُ ملفِّها. |
| ZatcaSubmissionService.cs | var company = await db.Companies.SingleAsync(c => c.Id == companyId); | CEEF-229 · Z-10 | بيانات البائع النظامية — لا الرقم الضريبيّ وحده. |
| ZatcaSubmissionService.cs | public enum HttpFailureKind | جاهزيّةُ الإطلاق · ح-١٢١ · ‏`tax-zatca#8` | ‏ ما يقوله ردٌّ غيرُ ناجحٍ عن المستند. |
| ZatcaSubmissionService.cs | public async Task<ResubmitResult> ResubmitPendingAsync(long companyId, CancellationToken c | جاهزيّةُ الإطلاق · ح-١٢١ · ‏`tax-zatca#8` | ‏ إعادةُ إرسال كلِّ معلَّقٍ دفعةً واحدة — بترتيب السلسلة. |
| ZatcaSubmissionService.cs | public Task<ResubmitResult> ResubmitPendingInBackgroundAsync(long companyId, CancellationT | جاهزيّةُ الإطلاق · ح-١٥٦ · ‏`tax-zatca#8` | ‏ إعادةُ الإرسال من المؤقّت في الخلفيّة — بلا فحص دور، عمداً، |
| ZatcaSubmissionService.cs | internal async Task<ZatcaEInvoiceDocument> SubmitAsFollowUpToPostingAsync(long documentId) | CEEF-229 · م2-١ | إرسالٌ كأثرٍ تابعٍ لترحيلٍ مأذونٍ به — لا يُعيد فحص الدور، عمداً. |
| ZatcaSubmissionService.cs | var gaps = status.ReadyForSubmission ? [] : status.Warnings; | CEEF-229 · م0-١٠ | لا تُعلَّم مقبولةً — لم يرها أحد خارج هذا الجهاز. |
| ZatcaSubmissionService.cs | var kind = ClassifyFailure(response.StatusCode); | جاهزيّةُ الإطلاق · ح-١٢١ · ‏`tax-zatca#8` | ‏ ‏وكان كلُّ ردٍّ غيرِ ناجحٍ «رفضاً» — ٥٠٣ و٤٢٩ و٥٠٠ معاً، |
| ZatcaSubmissionService.cs | var verdict = ReadAuthorityVerdict(body, doc.TransmissionMode); | CEEF-237 · ز-٢ | ‏حالةُ الهيئة تُقرأ، لا تُفترض من رمز HTTP. |
| ZatcaSubmissionService.cs | internal static HttpRequestMessage BuildSubmissionRequest( | جاهزيّةُ الإطلاق · ح-٩ | بناءُ الطلب — مُستخرَجٌ كي يُقاس. |
| ZatcaSubmissionService.cs | request.Headers.Add("Accept-Version", "V2"); | CEEF-206 | واجهات الهيئة تشترط Accept-Version؛ غيابها يرفض الطلب رغم صحة المسار والحمولة. |
| ZatcaUblInvoiceBuilder.cs | decimal DocumentDiscount = 0m, | CEEF-230 · ط1-٢ | خصمُ المستند — يخرج AllowanceCharge لكلّ وعاءٍ ضريبيّ. |
| ZatcaUblInvoiceBuilder.cs | IReadOnlyList<string>? LineUnitCodes = null, | CEEF-230 · ط1-١ | رمزُ UN/ECE لكلّ سطر، بترتيب Lines نفسِه. |
| ZatcaUblInvoiceBuilder.cs | string CurrencyCode = "SAR", | CEEF-230 · ط1-٤ |  عملةُ المستند — والمبالغُ أعلاه بها. |
| ZatcaUblInvoiceBuilder.cs | DateOnly? SupplyDate = null, | جاهزيّةُ الإطلاق · ح-٨ | تاريخُ التوريد — cac:Delivery/cbc:ActualDeliveryDate، |
| ZatcaUblInvoiceBuilder.cs | public sealed record PrepaymentReference( | جاهزيّةُ الإطلاق · <c>sector-modules#6</c> | فاتورةُ دفعةٍ مقدّمةٍ (386) تُسوّى في الفاتورة النهائيّة — كما في معيار |
| ZatcaUblInvoiceBuilder.cs | public sealed record SellerIdentity( | CEEF-229 · Z-10 | هوية البائع النظامية — السجل التجاريّ والاسم المسجَّل والعنوان الوطنيّ. |
| ZatcaUblInvoiceBuilder.cs | public sealed record BuyerIdentity( | CEEF-229 · م1-١ب | هويةُ المشتري النظامية — للفاتورة القياسية وحدها. |
| ZatcaUblInvoiceBuilder.cs | El(Cbc, "DocumentCurrencyCode", data.CurrencyCode), | CEEF-230 · ط1-٤ | ‏عملةُ المستند تتغيّر، وعملةُ الضريبة ريالٌ أبداً — |
| ZatcaUblInvoiceBuilder.cs | if ((data.IsCreditNote \|\| data.IsDebitNote) && | CEEF-229 · Z-6 | ترتيب xsd:sequence في مخطّط UBL 2.1 مُلزِم — لا اصطلاح. |
| ZatcaUblInvoiceBuilder.cs | AddDelivery(root, data); | جاهزيّةُ الإطلاق · ح-٨ | ‏ ‏تاريخُ التوريد — وكان غائباً عن المستند كلِّه. |
| ZatcaUblInvoiceBuilder.cs | var subtotals = LineDocumentTotalsCalculator.ComputeSubtotals( | ع-Z2، CEEF-202 | كان غائباً تماماً فتُرفض الفاتورة في الفحص رغم صحة التوقيع . |
| ZatcaUblInvoiceBuilder.cs | var subtotals = LineDocumentTotalsCalculator.ComputeSubtotals( | CEEF-207 | وعاء لكل فئة ضريبية حاضرة في الفاتورة، لا وعاء واحد بنسبة مسمّرة: |
| ZatcaUblInvoiceBuilder.cs | sub.Category == VatCategory.Standard \|\| sub.ExemptionReasonCode is null | CEEF-229 · م1-١ب | BR-KSA-CL-06: فئةٌ غير أساسية بلا سبب إعفاء تُرفض. |
| ZatcaUblInvoiceBuilder.cs | foreach (var (category, rate, amount) in | CEEF-230 · ط1-٢ | ‏خصمُ المستند: عنصرٌ لكلّ وعاءٍ ضريبيّ لا عنصرٌ واحد. |
| ZatcaUblInvoiceBuilder.cs | if (data.CurrencyCode != "SAR") | CEEF-230 · ط1-٤ | ‏سعرُ الصرف ثمّ ضريبةٌ ثانيةٌ بالريال — ولا يخرجان إلا عند |
| ZatcaUblInvoiceBuilder.cs | var lineRate = line.VatCategory.RateFor(SalesInvoice.SaudiVatRate); | CEEF-207 | نسبة السطر من فئته: الصفريّ والمعفى ينتجان ضريبة صفراً ونسبة 0.00، |
| ZatcaUblInvoiceBuilder.cs | line.DiscountAmount > 0m | CEEF-230 · ط1-٢ | ‏خصمُ السطر — وBaseAmount هو الإجماليُّ قبله، فيُقرأ |
| ZatcaUblInvoiceBuilder.cs | new XElement(Cac + "TaxTotal", | جاهزيّةُ الإطلاق · ح-٨ | ‏ ‏ومبلغُ السطر شاملَ الضريبة — وكان الوعاءُ |
| ZatcaUblInvoiceBuilder.cs | private static void AddDelivery(XElement root, InvoiceData data) | CEEF-229 · Z-5 | سبب إصدار الإشعار الدائن/المدين. |
| ZatcaUblInvoiceBuilder.cs | private static void AddDelivery(XElement root, InvoiceData data) | جاهزيّةُ الإطلاق · ح-٨ | تاريخُ التوريد — cac:Delivery/cbc:ActualDeliveryDate. |
| ZatcaUblInvoiceBuilder.cs | private static void AddIcvRef(XElement root, string icv) | CEEF-229 · Z-6 | العدّاد التسلسليّ يُكتب cbc:UUID مباشرةً تحت المرجع الإضافيّ. |
| ZatcaUblInvoiceBuilder.cs | private static void AddUblSignature(XElement root) | CEEF-229 · Z-6 | cac:Signature في جسد الفاتورة — كان غائباً تماماً. |
| ZatcaUblInvoiceBuilder.cs | private static void AddSupplier(XElement root, string name, string vat, SellerIdentity? se | CEEF-229 · Z-10 | طرف البائع كاملاً: السجل التجاريّ ثم الاسم ثم العنوان الوطنيّ ثم |
| ZatcaUblInvoiceBuilder.cs | private static void AddCustomer(XElement root, string? name, string? vat, BuyerIdentity? b | CEEF-229 · م1-١ب | طرفُ المشتري — وترتيبُ عناصره يتبع المتتالية كما في طرف البائع. |
| ZatcaXmlSigner.cs | var certB64 = Convert.ToBase64String(cert.RawData); | CEEF-229 · Z-2 | يُغرَس الهيكلُ أوّلاً، ثمّ يُقنَّن كلُّ شيءٍ من داخل المستند. |
| ZatcaXmlSigner.cs | var doc = XDocument.Load(new MemoryStream(xmlBytes), LoadOptions.PreserveWhitespace); | CEEF-229 · Z-NEW | ويُقرأ من البايتات لا من نصٍّ مُفكَّك: Encoding.UTF8.GetString |
| ZatcaXmlSigner.cs | var publicKeyDer = cert.PublicKey.ExportSubjectPublicKeyInfo(); | CEEF-229 · Z-9 | المفتاح العام يُستخرج من الشهادة نفسها — لا من مصدرٍ آخر. |
| ZatcaXmlSigner.cs | private static byte[] CertificateSignature(X509Certificate2 cert) | جاهزيّةُ الإطلاق · ح-٧ | توقيعُ جهةِ إصدار الشهادات على ختمنا — الوسم ٩. |
| ZatcaXmlSigner.cs | public static bool CanSign( | CEEF-229 · م2-١٣ب | هل تكفي الإعدادات للتوقيع فعلاً؟ |
| ZatcaXmlSigner.cs | private static string ComputeInvoiceHash(byte[] xmlBytes) | CEEF-229 · Z-1 | الهاش يستثني ثلاثة عقد لا واحدة. |
| ZatcaXmlSigner.cs | new XElement(DsNs + "Transforms", | CEEF-229 · Z-1 | الاستثناءات الثلاثة تُعلَن صراحةً — وإلا لم يستطع أيّ متحقّق |
| ZatcaXmlSigner.cs | private static byte[] CanonicalizeInDocument(XDocument document, string qualifiedName) | CEEF-229 · Z-2 | تقنينٌ شاملٌ لعقدةٍ من داخل مستندها — لا منتزعةً. |
| ZatcaXmlSigner.cs | public record ZatcaSignResult( | CEEF-229 · Z-9 | المفتاح العام بترميز DER (SubjectPublicKeyInfo) — الوسم 8 في رمز |
| ZatcaXmlSigner.cs | public record ZatcaSignResult( | جاهزيّةُ الإطلاق · ح-٧ | توقيعُ جهةِ إصدار الشهادات على المفتاح العامّ — الوسمُ ٩. |
