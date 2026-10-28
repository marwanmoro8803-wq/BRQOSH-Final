namespace Brqosh.Application.Intelligence;

/// <summary>أدلة تشغيلية مفصّلة للشاشات غير المغطاة في <see cref="ProgramKnowledge"/> الرئيسي.</summary>
public static class ProgramPageGuideTemplates
{
    private static readonly Dictionary<string, (string Ar, string En)> Templates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["cost_centers"] = (
                "أنشئ/عدّل مركز تكلفة واربطه بالمستندات والقيود. التقارير تُفلتر حسب المركز بعد الترحيل.",
                "Create/edit cost centers and link to documents and entries. Reports filter by center after posting."),
            ["fiscal_periods"] = (
                "افتح فترة مالية قبل الترحيل · أغلقها بعد الإقفال. لا ترحيل في فترة مغلقة.",
                "Open fiscal period before posting · close after month-end. No posting in closed periods."),
            ["contracts"] = (
                "عقد مشروع: ربط بمشروع مقاولات · قيمة · مراحل. يغذي المستخلصات والمحتجزات.",
                "Project contract: link to construction project · value · milestones. Feeds billings and retentions."),
            ["boq"] = (
                "جدول كميات (BOQ): أضف بنود الكميات للمشروع · الوحدة · السعر · الكمية المخططة.",
                "BOQ: add line items · unit · rate · planned quantity for the project."),
            ["progress_billings"] = (
                "مستخلص: نسبة إنجاز أو كميات منفّذة → ترحيل يُولّد إيراد مقاولات وذمم.",
                "Progress billing: % complete or executed qty → posting generates construction revenue and AR."),
            ["subcontractors"] = (
                "مقاول باطن: بيانات المورد + عقد فرعي · ربط بمشروع · محتجزات عند الصرف.",
                "Subcontractor: vendor data + sub-contract · project link · retentions on payment."),
            ["retentions"] = (
                "محتجزات: نسبة محجوزة من مستخلص/مقاول · تُحرّر لاحقاً بقيد عكسي.",
                "Retentions: withheld % from billing/subcontract · released later via reversing entry."),
            ["project_profitability"] = (
                "تقرير ربحية: إيرادات المشروع − تكاليف مباشرة وغير مباشرة. يعتمد على مستندات مرحّلة.",
                "Profitability: project revenue minus direct/indirect costs. Requires posted documents."),
            ["wip"] = (
                "أعمال تحت التشغيل: أوامر الإنتاج الصادرة غير المكتملة — كمّيّاتٌ لا أرصدة؛ الموادّ تُصرف وتُكلَّف عند الإكمال.",
                "WIP: released work orders not yet completed — quantities, not balances; materials are issued and costed on completion."),
            ["pos_shifts"] = (
                "وردية POS: افتح قبل البيع · أغلق في نهاية اليوم · راجع الفروقات النقدية.",
                "POS shift: open before sales · close end of day · review cash variances."),
            ["pos_reports"] = (
                "تقارير POS: مبيعات الوردية · طرق الدفع · ZATCA للفواتير المرحّلة.",
                "POS reports: shift sales · payment methods · ZATCA for posted invoices."),
            ["customer_statement"] = (
                "كشف عميل: اختر العميل والفترة · يعرض فواتير · قبض · رصيد.",
                "Customer statement: pick customer and period · invoices · receipts · balance."),
            ["sales_quotations"] = (
                "عرض سعر: مسودة → إرسال للعميل → تحويل لأمر بيع/فاتورة عند القبول.",
                "Quotation: draft → send → convert to SO/invoice when accepted."),
            ["sales_orders"] = (
                "أمر بيع: حجز كميات · تحويل لفاتورة عند التسليم · لا ترحيل محاسبي حتى الفاتورة.",
                "Sales order: reserve qty · invoice on delivery · no GL until invoice posted."),
            ["sales_returns"] = (
                "مردود مبيعات: إشعار دائن من فاتورة مرحّلة · يعكس الإيراد والضريبة والذمم.",
                "Sales return: credit note from posted invoice · reverses revenue, VAT, AR."),
            ["crm_opportunities"] = (
                "فرصة بيع: مرحلة · قيمة متوقعة · ربط بعميل محتمل · تحويل لفاتورة لاحقاً.",
                "Opportunity: stage · expected value · lead link · convert to invoice later."),
            ["crm_activities"] = (
                "أنشطة CRM: مكالمات · زيارات · متابعات — لا ترحيل محاسبي.",
                "CRM activities: calls · visits · follow-ups — no accounting posting."),
            ["supplier_statement"] = (
                "كشف مورد: فواتير مشتريات · مدفوعات · رصيد مستحق.",
                "Supplier statement: purchase invoices · payments · outstanding balance."),
            ["purchase_requests"] = (
                "طلب شراء: يمرّ بالموافقات · ويتحوّل إلى أمر شراء عند الاعتماد.",
                "Purchase request: goes through approvals · converts to a purchase order once approved."),
            ["goods_receipts"] = (
                "إذن استلام: استلام بضاعة مقابل أمر شراء · يزيد المخزون عند الترحيل.",
                "Goods receipt: receive against PO · increases stock on posting."),
            ["debit_notes"] = (
                "إشعار مدين (مشتريات): تصحيح فاتورة مورد مرحّلة · يخفّض الذمم الدائنة.",
                "Debit note (purchases): correct posted supplier invoice · reduces AP."),
            ["warehouses"] = (
                "مستودعات: تعريف مواقع التخزين · ربط بحركات المخزون والجرد.",
                "Warehouses: storage locations · linked to movements and stock count."),
                ["product_categories"] = ("فئات الأصناف: تجميع الأصناف · حسابات الإيراد والمخزون والتكلفة الافتراضية لكل فئة.", "Product categories: group items · default revenue, inventory and COGS accounts per category."),
                ["posting_accounts"] = ("حسابات الترحيل: اختيار الحساب لكل دور في القيود الآلية · يسري على القيود الجديدة فقط.", "Posting accounts: choose the account for each automatic-entry role · applies to new entries only."),
            ["stock_movements"] = (
                "حركة مخزون: تحويل بين مستودعات أو تسوية · ترحيل يحدّث الأرصدة.",
                "Stock movement: transfer between warehouses or adjustment · posting updates balances."),
            ["stock_count"] = (
                "جرد: عدّ فعلي مقابل نظام · فروقات تُرحّل كتسوية مخزون.",
                "Stock count: physical vs system · variances post as inventory adjustment."),
            ["landed_cost"] = (
                "تكلفة إضافية: شحن/جمارك تُوزّع على أصناف الاستلام · ترفع تكلفة المخزون.",
                "Landed cost: freight/duty allocated to receipt lines · raises inventory cost."),
            ["bank_accounts"] = (
                "حساب بنكي: ربط بدفتر الحسابات · يُستخدم في سندات القبض/الصرف.",
                "Bank account: GL link · used in receipt/payment vouchers."),
            ["bank_transfers"] = (
                "تحويل بنكي: من حساب لآخر · قيد داخلي بين نقدية/بنوك.",
                "Bank transfer: between accounts · internal cash/bank journal."),
            ["exchange_rates"] = (
                "أسعار صرف: إدخال يدوي أو مزامنة · تُستخدم في إعادة التقييم والفواتير الأجنبية.",
                "Exchange rates: manual or sync · used in revaluation and foreign invoices."),
            ["cheques"] = (
                "شيكات: تحت التحصيل/الصرف · حالة (مودع · محصل · مرتد) تؤثر على الخزينة.",
                "Cheques: receivable/payable · status (deposited · cleared · bounced) affects treasury."),
            ["consolidated_trial_balance"] = (
                "ميزان موحّد: تجميع شركات المجموعة · تحقق من التوازن قبل الإقفال.",
                "Consolidated TB: group companies rollup · verify balance before close."),
            ["consolidated_income_statement"] = (
                "قائمة دخل موحّدة: إيرادات ومصروفات المجموعة بعد التحويلات.",
                "Consolidated P&L: group revenue/expense after translation."),
            ["consolidated_balance_sheet"] = (
                "مركز مالي موحّد: أصول والتزامات المجموعة في تاريخ واحد.",
                "Consolidated balance sheet: group assets/liabilities at one date."),
            ["intercompany_elimination"] = (
                "إزالة بين الشركات: قيود لإلغاء معاملات داخل المجموعة قبل التقارير الموحّدة.",
                "IC elimination: entries to remove intercompany transactions before consolidated reports."),
            ["general_ledger_report"] = (
                "دفتر الأستاذ: حركات حساب محدد لفترة · مدين/دائن/رصيد.",
                "General ledger: account movements for period · debit/credit/balance."),
            ["fixed_assets"] = (
                "أصول ثابتة: تسجيل · إهلاك دوري · استبعاد · قيود تلقائية عند الترحيل.",
                "Fixed assets: register · periodic depreciation · disposal · auto entries on post."),
            ["payroll"] = (
                "رواتب: دورة · استحقاقات · استقطاعات · ترحيل يُولّد قيود الرواتب.",
                "Payroll: cycle · earnings · deductions · posting generates payroll entries."),
            ["organizations"] = (
                "منشآت: هيكل المستأجر/الشركة الأم · يغذي التوحيد والفروع.",
                "Organizations: tenant/parent structure · feeds consolidation and branches."),
            ["company_groups"] = (
                "مجموعات شركات: تجميع للتقارير الموحّدة والإزالة بين الشركات.",
                "Company groups: rollup for consolidated reporting and IC elimination."),
            ["branches"] = (
                "فروع: يُنسب كل مستند إلى فرع · ويُختار الفرع الجاري من أعلى الشاشة.",
                "Branches: every document is tied to a branch · the current branch is chosen at the top of the screen."),
            ["users"] = (
                "مستخدمون: حساب ودور وسياسة كلمة المرور · والدور يحدد الشاشات التي تظهر له.",
                "Users: account, role and password policy · the role decides which screens the user sees."),
            ["audit_log"] = (
                "سجل تدقيق: كل ترحيل أو تعديل حرج مسجّل بمن فعله ومتى · لا يُحذف ولا يُعدَّل.",
                "Audit log: every critical post or change with who and when · it cannot be deleted or edited."),
            ["data_quality"] = (
                "جودة البيانات: فحوصات ذمم · فترات · أرصدة · تنبيهات قبل الإقفال.",
                "Data quality: AR/period/balance checks · alerts before close."),
            ["platform_events"] = (
                "أحداث المنصة: سجل أحداث النظام والتكامل · للمراقبة التشغيلية.",
                "Platform events: system/integration event log for operations monitoring."),
            ["branch_profitability"] = (
                "اختر الفترة ليظهر لكل فرع إيراده ومصروفه وربحه. مجموع الفروع يساوي صافي قائمة الدخل، فإن اختلف فراجع قيوداً غير منسوبة لفرع.",
                "Pick a period to see revenue, expenses and profit per branch. The branches add up to the income statement's net result; if they do not, look for entries not tied to a branch."),
            ["budget_variance"] = (
                "قارن المخطط بالفعلي لكل بند في الفترة. الانحراف الموجب صالح للمنشأة في الحالتين: إيراد أعلى من المخطط أو مصروف أقل منه.",
                "Compare planned against actual per line for the period. A positive variance is favourable either way: revenue above plan or expense below it."),
            ["cost_center_statement"] = (
                "إيراد ومصروف كل مركز تكلفة في الفترة. ما لم يُنسب إلى مركز يظهر في سطر «غير مصنف» ولا يُخفى.",
                "Revenue and expense per cost center for the period. Anything not assigned to a center appears as 'unclassified' rather than being hidden."),
            ["production_cost_sheet"] = (
                "لكل أمر إنتاج مكتمل: المواد والأجر والتحميل وتكلفة الوحدة، مأخوذة من قيد إكمال الأمر نفسه. اختر الفترة واضغط عرض.",
                "For each completed work order: materials, labour, overhead and unit cost, taken from the order's own completion entry. Pick the period and press View."),
            ["receivable_aging"] = (
                "من عليه وكم ومنذ متى. العمر يُحسب من تاريخ المستند. ابدأ بالتحصيل من الشرائح الأقدم.",
                "Who owes, how much and since when. Age is counted from the document date. Chase the oldest buckets first."),
            ["recurring_plans"] = (
                "خطط لفواتير متكررة (إيجار، اشتراك، عقد صيانة). التوليد دفعة يطلقها المستخدم بزر «توليد المستحق» ولا يجري وحده، ويمكن إيقاف خطة مؤقتاً.",
                "Plans for repeating invoices (rent, subscription, maintenance contract). Generation is a batch you start with 'Generate due'; it does not run by itself, and a plan can be paused."),
            ["payable_aging"] = (
                "لمن هو عليك ومنذ متى. العمر يُحسب من تاريخ المستند. رتّب السداد بحسب الاستحقاق والعمر.",
                "Whom you owe and since when. Age is counted from the document date. Plan payments by due date and age."),
            ["stock_batches"] = (
                "أرصدة كل دفعة وتاريخ انتهائها. رشّح بين: كل الدفعات، وما يقارب الانتهاء (٩٠ يوماً)، وما انتهت صلاحيته.",
                "Balance and expiry of every batch. Filter between all batches, those nearing expiry (90 days) and those already expired."),
            ["units_of_measure"] = (
                "عرّف الوحدات (كرتون، حبة، كيلو). رمز UN/ECE للوحدة هو ما يذهب إلى الفاتورة الإلكترونية، فتأكد منه.",
                "Define units (carton, piece, kilo). The unit's UN/ECE code is what goes onto the e-invoice, so check it."),
            ["fx_revaluation"] = (
                "رصيد بعملة أجنبية يبقى بسعر يوم إيداعه ما لم يُعَد تقييمه. هنا تعيد تقييم الحسابات البنكية بعملة تخالف عملة الدفاتر بسعر الصرف المسجّل.",
                "A foreign-currency balance stays at its deposit-day rate unless revalued. Here you revalue bank accounts held in a currency other than the books' currency, at the recorded exchange rate."),
            ["monetary_revaluation"] = (
                "البنود النقدية بعملة أجنبية تُترجم بسعر الإقفال، ويُعكس القيد في اليوم التالي فلا يتراكم.",
                "Foreign-currency monetary items are translated at the closing rate, and the entry reverses the next day so it does not accumulate."),
            ["bank_statements"] = (
                "استورد كشف البنك، ثم طابقه آلياً مع القيود، ثم رحّل الفروق. ما لم يُطابَق يبقى ظاهراً حتى تعالجه.",
                "Import the bank statement, match it automatically against the entries, then post the differences. Unmatched lines stay visible until you handle them."),
            ["cash_position"] = (
                "داخل وخارج لكل حساب نقدي أو بنكي، والمركز الموحد في العنوان.",
                "Inflows and outflows per cash or bank account, with the combined position in the header."),
            ["cash_flow_forecast"] = (
                "ثلاثة عشر أسبوعاً مبنية على الذمم المستحقة فعلاً لا على النية: التحصيل المتوقع من فواتير العملاء، والسداد من فواتير الموردين.",
                "Thirteen weeks built from receivables and payables actually due, not intentions: expected collections from customer invoices and payments from supplier invoices."),
            ["properties"] = (
                "سجّل العقار ثم وحداته. حالة الإشغال قيد تحكمه عقود الإيجار لا وصف يُكتب يدوياً.",
                "Register the property, then its units. Occupancy is a constraint driven by lease contracts, not a free-text description."),
            ["leases"] = (
                "تفعيل العقد ينشئ خطة فوترة دورية. مبلغ التأمين أمانة تُرد للمستأجر وليس إيراداً.",
                "Activating a lease creates a recurring billing plan. The security deposit is held in trust for the tenant, not revenue."),
            ["maintenance_orders"] = (
                "تُسجَّل تكلفة الصيانة على وحدتها. تحميل التلف على المستأجر هو سند مصادرة التأمين.",
                "Maintenance cost is recorded against its unit. Charging damage to the tenant is the basis for forfeiting the deposit."),
            ["patients"] = (
                "رقم الملف لا يتكرر. ملفان لشخص واحد يفرّقان تاريخه المرضي، فابحث عن المريض قبل فتح ملف جديد.",
                "A file number is never repeated. Two files for one person split their history, so search for the patient before opening a new file."),
            ["visits"] = (
                "زيارة واحدة تنتج ذمتين: على المريض وعلى شركة التأمين. راجع بيانات التأمين ثم اضغط «فوترة الزيارة».",
                "One visit produces two receivables: one on the patient and one on the insurer. Check the insurance details, then press 'Bill visit'."),
            ["insurance_payers"] = (
                "شركة التأمين عميل له ذمة، ومهلة سدادها تختلف عن مهلة المريض. اضبط المهلة لكل شركة.",
                "An insurer is a customer with its own receivable, and its payment term differs from the patient's. Set the term per insurer."),
            ["insurance_claims"] = (
                "المطالبة المرفوضة تخرج من ذمة الشركة بإشعار دائن: إما تحميلاً على المريض أو شطباً.",
                "A rejected claim leaves the insurer's receivable through a credit note: either charged to the patient or written off."),
            ["tip_pools"] = (
                "الإكرامية مال العامل لا إيراد المطعم: التزام يخرج بالصرف إلى مستحقيه.",
                "A tip belongs to the staff, not the restaurant's revenue: a liability that leaves when paid out to its recipients."),
            ["menu_items"] = (
                "بيع الطبق يصرف مكوناته من المخزون إن كانت له وصفة؛ وبغير وصفة يبقى المخزون كما اشتُري.",
                "Selling a dish consumes its ingredients from stock if it has a recipe; without a recipe, stock stays as purchased."),
            ["rooms"] = (
                "حالة الإشغال يقودها الحجز ولا تُضبط يدوياً.",
                "Occupancy status is driven by the reservation and is not set by hand."),
            ["reservations"] = (
                "إيراد الغرفة يُستحق ليلة بليلة لا عند الوصول: أنزل النزيل، وقيّد الليالي، واقبض العربون عند الحاجة، ثم «مغادرة وتصفية» لإقفال حسابه.",
                "Room revenue is earned night by night, not on arrival: check the guest in, post the nights, take a deposit if needed, then 'Check-out and settle' to close the folio."),
            ["delivery_trips"] = (
                "عهدة السائق أصل لا مصروف، والمسار الخاسر يظهر أولاً لتراجعه.",
                "The driver's advance is an asset, not an expense, and the loss-making route is listed first so you can review it."),
            ["shipments"] = (
                "ما يُحصَّل عند الاستلام أمانة للتاجر لا إيراد للشركة: يُثبَت التحصيل ثم يُورَّد للتاجر، أما أجرة الشحن فتُفوتَر وحدها.",
                "Cash collected on delivery is held in trust for the merchant, not company revenue: record the collection, then remit it to the merchant; only the shipping fee is invoiced."),
            ["employees"] = (
                "هوية وجنسية وأجر وآيبان. الجنسية تحكم اشتراك التأمينات الاجتماعية، فأدخلها بدقة.",
                "Identity, nationality, pay and IBAN. Nationality decides social-insurance enrolment, so enter it accurately."),
            ["gosi_policy"] = (
                "النسب تتغير بقرار الهيئة، والمنشأة تملكها لا البرنامج: راجعها عند أي قرار جديد قبل ترحيل رواتب.",
                "The rates change by the authority's decision and are owned by the company, not the software: review them whenever a new decision is issued, before posting payroll."),
            ["payslips"] = (
                "تُبنى القسيمة من الدورة المرحّلة لا من سجل الموظفين الحالي، فتبقى كما صُرفت مهما تغيّر الأجر بعدها.",
                "A payslip is built from the posted run, not from the current employee record, so it stays as paid however the salary changes later."),
            ["employee_advances"] = (
                "السلفة ذمة على الموظف لا مصروف راتب، وتُسدد بالاستقطاع الشهري من الراتب.",
                "An advance is a receivable from the employee, not a salary expense, and is repaid through monthly payroll deductions."),
            ["leave_requests"] = (
                "رصيد الإجازات التزام يتراكم كل يوم ويُدفع نقداً عند انتهاء الخدمة. سجّل الإجازة والغياب أولاً بأول.",
                "The leave balance is a liability that builds up daily and is paid in cash at end of service. Record leave and absence as they happen."),
            ["wage_protection"] = (
                "إلزامي نظاماً: اختر دورة الرواتب ثم «تصدير الملف» وارفعه إلى البنك ليودع الرواتب. ويُنبَّه إلى موظفين لم يدخلوا الملف مع سبب كلٍّ منهم.",
                "Mandatory by regulation: pick the payroll run, press 'Export file' and submit it to the bank, which then credits salaries. Employees left out of the file are flagged with the reason for each."),
            ["soft_deleted_rows"] = (
                "الحذف في برقوش إخفاء لا إعدام. اختر النوع ثم السجل واضغط «استعادة المحدد» لإرجاعه.",
                "Deleting in BRQOSH hides a record rather than destroying it. Pick the type, then the record, and press 'Restore selected'."),
            ["data_migration"] = (
                "نقل الدليل والعملاء والموردين والأصناف والأرصدة الافتتاحية من نظام سابق بملف CSV. الصف الأول عناوين، وما تعذّر يُعرض بسطره وسببه. ابدأ بالدليل ثم الميزان الافتتاحي ثم الذمم.",
                "Move the chart, customers, suppliers, items and opening balances from a previous system via CSV. The first row is headers and anything that fails is shown with its line and reason. Start with the chart, then the opening trial balance, then the open items."),
            ["backup"] = (
                "خذ نسخة كل يوم عمل واحفظ نسخة على قرص آخر. النسخة التي لا تُجرَّب ليست نسخة. الاستعادة تمحو ما في القاعدة الحالية ولا رجعة فيها، فأغلق البرنامج على بقية الأجهزة أولاً.",
                "Take a backup every working day and keep a copy on another disk. A backup that has never been tested is not a backup. Restore wipes the current database and cannot be undone, so close the program on other machines first."),
            ["diagnostics"] = (
                "ما يسأل عنه الدعم في أول مكالمة: الإصدار والقاعدة والترخيص والنسخ. استعمل «نسخ التقرير» أو «حزمة الدعم» عند الإبلاغ عن مشكلة، و«فحص التحديث» لمعرفة الإصدار الأحدث.",
                "What support asks first: version, database, licence and backups. Use 'Copy report' or 'Support bundle' when reporting a problem, and 'Check for update' to see the latest version."),
        };

    public static bool TryGet(string navKey, out (string Ar, string En) guide) =>
        Templates.TryGetValue(navKey.Trim(), out guide!);

    public static int Count => Templates.Count;
}
