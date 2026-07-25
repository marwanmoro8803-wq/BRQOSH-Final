namespace Brqosh.Domain.Accounting;

/// <summary>تعريف حساب في القالب الجاهز — يُحوَّل إلى Account عند إنشاء الشركة.</summary>
public record SeedAccount(
    string Code,
    string NameAr,
    string NameEn,
    AccountType Type,
    string? ParentCode,
    bool IsGroup,
    bool IsSystem = false);

/// <summary>
/// دليل الحسابات السعودي الجاهز — يُزرع تلقائياً عند إنشاء شركة جديدة.
/// الترقيم هرمي: 1 أصول، 2 التزامات، 3 حقوق ملكية، 4 إيرادات، 5 مصروفات.
/// الحسابات النظامية (VAT وغيرها) محمية من الحذف لأنها مربوطة بالترحيل الآلي.
/// </summary>
public static class SaudiChartOfAccounts
{
    public static IReadOnlyList<SeedAccount> Template { get; } =
    [
        // ============ 1 — الأصول ============
        new("1", "الأصول", "Assets", AccountType.Asset, null, IsGroup: true),

        new("11", "الأصول المتداولة", "Current Assets", AccountType.Asset, "1", IsGroup: true),
        new("1101", "النقدية بالصندوق", "Cash on Hand", AccountType.Asset, "11", IsGroup: false, IsSystem: true),
        new("1102", "النقدية لدى البنوك", "Cash at Banks", AccountType.Asset, "11", IsGroup: false, IsSystem: true),
        new("1103", "شيكات برسم التحصيل", "Cheques Under Collection", AccountType.Asset, "11", IsGroup: false, IsSystem: true),
        new("1104", "العملاء (المدينون التجاريون)", "Accounts Receivable - Trade", AccountType.Asset, "11", IsGroup: false, IsSystem: true),
        new("1105", "أوراق القبض", "Notes Receivable", AccountType.Asset, "11", IsGroup: false),
        new("1106", "مخصص الديون المشكوك في تحصيلها", "Allowance for Doubtful Debts", AccountType.Asset, "11", IsGroup: false),
        new("1107", "المخزون", "Inventory", AccountType.Asset, "11", IsGroup: false, IsSystem: true),
        new("1108", "بضاعة بالطريق", "Goods in Transit", AccountType.Asset, "11", IsGroup: false),
        new("1109", "مصروفات مدفوعة مقدماً", "Prepaid Expenses", AccountType.Asset, "11", IsGroup: false),
        new("1110", "دفعات مقدمة للموردين", "Advances to Suppliers", AccountType.Asset, "11", IsGroup: false),
        new("1111", "ضريبة القيمة المضافة — مدخلات", "VAT Input (Recoverable)", AccountType.Asset, "11", IsGroup: false, IsSystem: true),
        new("1112", "عهد وسلف الموظفين", "Employee Advances & Custodies", AccountType.Asset, "11", IsGroup: false),
        new("1113", "أعمال تحت التنفيذ (مستخلصات غير مفوترة)", "Work in Progress - Unbilled", AccountType.Asset, "11", IsGroup: false, IsSystem: true),
        new("1114", "محتجزات لدى العملاء", "Retention Receivable", AccountType.Asset, "11", IsGroup: false, IsSystem: true),

        new("12", "الأصول غير المتداولة", "Non-Current Assets", AccountType.Asset, "1", IsGroup: true),
        new("1201", "الأراضي", "Land", AccountType.Asset, "12", IsGroup: false),
        new("1202", "المباني والإنشاءات", "Buildings & Constructions", AccountType.Asset, "12", IsGroup: false),
        new("1203", "الآلات والمعدات", "Machinery & Equipment", AccountType.Asset, "12", IsGroup: false),
        new("1204", "السيارات ووسائل النقل", "Vehicles", AccountType.Asset, "12", IsGroup: false),
        new("1205", "الأثاث والتجهيزات المكتبية", "Furniture & Office Fixtures", AccountType.Asset, "12", IsGroup: false),
        new("1206", "أجهزة الحاسب والأنظمة", "Computers & IT Systems", AccountType.Asset, "12", IsGroup: false),
        new("1207", "مجمع إهلاك الأصول الثابتة", "Accumulated Depreciation", AccountType.Asset, "12", IsGroup: false, IsSystem: true),
        new("1208", "الأصول غير الملموسة", "Intangible Assets", AccountType.Asset, "12", IsGroup: false),

        // ============ 2 — الالتزامات ============
        new("2", "الالتزامات", "Liabilities", AccountType.Liability, null, IsGroup: true),

        new("21", "الالتزامات المتداولة", "Current Liabilities", AccountType.Liability, "2", IsGroup: true),
        new("2101", "الموردون (الدائنون التجاريون)", "Accounts Payable - Trade", AccountType.Liability, "21", IsGroup: false, IsSystem: true),
        new("2102", "أوراق الدفع", "Notes Payable", AccountType.Liability, "21", IsGroup: false),
        new("2103", "شيكات برسم الدفع", "Cheques Payable", AccountType.Liability, "21", IsGroup: false, IsSystem: true),
        new("2104", "مصروفات مستحقة", "Accrued Expenses", AccountType.Liability, "21", IsGroup: false),
        new("2105", "دفعات مقدمة من العملاء", "Advances from Customers", AccountType.Liability, "21", IsGroup: false, IsSystem: true),
        new("2106", "ضريبة القيمة المضافة — مخرجات", "VAT Output (Payable)", AccountType.Liability, "21", IsGroup: false, IsSystem: true),
        new("2107", "صافي ضريبة القيمة المضافة المستحقة", "VAT Net Payable", AccountType.Liability, "21", IsGroup: false, IsSystem: true),
        new("2108", "رواتب وأجور مستحقة", "Salaries & Wages Payable", AccountType.Liability, "21", IsGroup: false, IsSystem: true),
        new("2109", "التأمينات الاجتماعية المستحقة", "GOSI Payable", AccountType.Liability, "21", IsGroup: false),
        new("2110", "محتجزات مستحقة لمقاولي الباطن", "Retention Payable - Subcontractors", AccountType.Liability, "21", IsGroup: false, IsSystem: true),
        new("2111", "الزكاة وضريبة الدخل المستحقة", "Zakat & Income Tax Payable", AccountType.Liability, "21", IsGroup: false),

        new("22", "الالتزامات غير المتداولة", "Non-Current Liabilities", AccountType.Liability, "2", IsGroup: true),
        new("2201", "قروض طويلة الأجل", "Long-Term Loans", AccountType.Liability, "22", IsGroup: false),
        new("2202", "مخصص مكافأة نهاية الخدمة", "End of Service Benefits Provision", AccountType.Liability, "22", IsGroup: false),

        // ============ 3 — حقوق الملكية ============
        new("3", "حقوق الملكية", "Equity", AccountType.Equity, null, IsGroup: true),
        new("3101", "رأس المال", "Share Capital", AccountType.Equity, "3", IsGroup: false, IsSystem: true),
        new("3102", "الاحتياطي النظامي", "Statutory Reserve", AccountType.Equity, "3", IsGroup: false),
        new("3103", "الأرباح المبقاة (المرحّلة)", "Retained Earnings", AccountType.Equity, "3", IsGroup: false, IsSystem: true),
        new("3104", "أرباح/خسائر العام الحالي", "Current Year Profit/Loss", AccountType.Equity, "3", IsGroup: false, IsSystem: true),
        new("3105", "جاري الشركاء", "Partners' Current Account", AccountType.Equity, "3", IsGroup: false),
        new("3106", "احتياطي ترجمة العملات", "Currency Translation Reserve (CTA)", AccountType.Equity, "3", IsGroup: false, IsSystem: true),

        // ============ 4 — الإيرادات ============
        new("4", "الإيرادات", "Revenue", AccountType.Revenue, null, IsGroup: true),
        new("4101", "إيرادات المبيعات", "Sales Revenue", AccountType.Revenue, "4", IsGroup: false, IsSystem: true),
        new("4102", "مردودات ومسموحات المبيعات", "Sales Returns & Allowances", AccountType.Revenue, "4", IsGroup: false, IsSystem: true),
        new("4103", "خصم مسموح به", "Sales Discounts", AccountType.Revenue, "4", IsGroup: false),
        new("4104", "إيرادات عقود المقاولات (المستخلصات)", "Contract Revenue - Progress Billings", AccountType.Revenue, "4", IsGroup: false, IsSystem: true),
        new("4105", "إيرادات الخدمات", "Service Revenue", AccountType.Revenue, "4", IsGroup: false),
        new("4106", "إيرادات أخرى", "Other Income", AccountType.Revenue, "4", IsGroup: false, IsSystem: true),
        new("4107", "أرباح فروق عملة غير محققة", "Unrealized FX Gain", AccountType.Revenue, "4", IsGroup: false, IsSystem: true),

        // ============ 5 — المصروفات ============
        new("5", "المصروفات", "Expenses", AccountType.Expense, null, IsGroup: true),

        new("51", "تكلفة الإيرادات", "Cost of Revenue", AccountType.Expense, "5", IsGroup: true),
        new("5101", "تكلفة البضاعة المباعة", "Cost of Goods Sold", AccountType.Expense, "51", IsGroup: false, IsSystem: true),
        new("5102", "مشتريات", "Purchases", AccountType.Expense, "51", IsGroup: false),
        new("5103", "مردودات ومسموحات المشتريات", "Purchase Returns & Allowances", AccountType.Expense, "51", IsGroup: false),
        new("5104", "تكاليف مقاولي الباطن", "Subcontractor Costs", AccountType.Expense, "51", IsGroup: false, IsSystem: true),
        new("5105", "مواد ومهمات المشاريع", "Project Materials & Supplies", AccountType.Expense, "51", IsGroup: false),
        new("5106", "أجور عمالة مباشرة", "Direct Labor", AccountType.Expense, "51", IsGroup: false),
        new("5107", "تكاليف صناعية غير مباشرة", "Manufacturing Overhead", AccountType.Expense, "51", IsGroup: false),

        new("52", "المصروفات التشغيلية والإدارية", "Operating & Administrative Expenses", AccountType.Expense, "5", IsGroup: true),
        new("5201", "الرواتب والأجور", "Salaries & Wages", AccountType.Expense, "52", IsGroup: false, IsSystem: true),
        new("5202", "بدلات السكن والانتقال", "Housing & Transport Allowances", AccountType.Expense, "52", IsGroup: false),
        new("5203", "التأمينات الاجتماعية", "GOSI Contributions", AccountType.Expense, "52", IsGroup: false),
        new("5204", "الإيجارات", "Rent Expense", AccountType.Expense, "52", IsGroup: false),
        new("5205", "الكهرباء والمياه والاتصالات", "Utilities & Communications", AccountType.Expense, "52", IsGroup: false),
        new("5206", "الصيانة والإصلاحات", "Maintenance & Repairs", AccountType.Expense, "52", IsGroup: false),
        new("5207", "القرطاسية والمطبوعات", "Stationery & Printing", AccountType.Expense, "52", IsGroup: false),
        new("5208", "الدعاية والإعلان", "Advertising & Marketing", AccountType.Expense, "52", IsGroup: false),
        new("5209", "الرسوم الحكومية والاشتراكات", "Government Fees & Subscriptions", AccountType.Expense, "52", IsGroup: false),
        new("5210", "أتعاب مهنية واستشارية", "Professional & Consulting Fees", AccountType.Expense, "52", IsGroup: false),
        new("5211", "مصروف إهلاك الأصول الثابتة", "Depreciation Expense", AccountType.Expense, "52", IsGroup: false, IsSystem: true),
        new("5212", "ديون معدومة", "Bad Debts Expense", AccountType.Expense, "52", IsGroup: false),
        new("5213", "عمولات ورسوم بنكية", "Bank Charges & Commissions", AccountType.Expense, "52", IsGroup: false, IsSystem: true),
        new("5214", "خصم مكتسب", "Purchase Discounts Earned", AccountType.Expense, "52", IsGroup: false),
        new("5215", "مصروفات متنوعة", "Miscellaneous Expenses", AccountType.Expense, "52", IsGroup: false),
        new("5216", "مصروف الزكاة", "Zakat Expense", AccountType.Expense, "52", IsGroup: false),
        new("5217", "خسائر فروق عملة غير محققة", "Unrealized FX Loss", AccountType.Expense, "52", IsGroup: false, IsSystem: true),
    ];

    /// <summary>أكواد الحسابات النظامية التي يعتمد عليها الترحيل الآلي — مرجعية ثابتة للوحدات.</summary>
    public static class SystemCodes
    {
        public const string CashOnHand = "1101";
        public const string CashAtBanks = "1102";
        public const string ChequesUnderCollection = "1103";
        public const string AccountsReceivable = "1104";
        public const string Inventory = "1107";
        public const string VatInput = "1111";
        public const string AccountsPayable = "2101";
        public const string ChequesPayable = "2103";
        public const string VatOutput = "2106";
        public const string VatNetPayable = "2107";
        public const string SalesRevenue = "4101";
        public const string SalesReturns = "4102";
        public const string CostOfGoodsSold = "5101";
        public const string SalariesExpense = "5201";
        public const string RetainedEarnings = "3103";
        public const string CurrentYearProfit = "3104";
        public const string TranslationReserve = "3106";
        public const string RetentionReceivable = "1114";
        public const string RetentionPayableSubcontractors = "2110";
        public const string ContractRevenue = "4104";
        public const string SubcontractorCosts = "5104";
        public const string AdvancesFromCustomers = "2105";
        public const string WorkInProgress = "1113";
        public const string UnrealizedFxGain = "4107";
        public const string UnrealizedFxLoss = "5217";
        public const string BankCharges = "5213";
        public const string OtherIncome = "4106";
    }

    /// <summary>
    /// التحقق من سلامة القالب: أكواد فريدة، آباء موجودون، نوع الابن يطابق نوع الأب.
    /// يُستدعى في الاختبارات لضمان عدم كسر القالب مستقبلاً.
    /// </summary>
    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var byCode = new Dictionary<string, SeedAccount>();

        foreach (var account in Template)
        {
            if (!byCode.TryAdd(account.Code, account))
                errors.Add($"كود مكرر: {account.Code}");
        }

        foreach (var account in Template)
        {
            if (account.ParentCode is null)
                continue;
            if (!byCode.TryGetValue(account.ParentCode, out var parent))
            {
                errors.Add($"الحساب {account.Code} يشير إلى أب غير موجود: {account.ParentCode}");
                continue;
            }
            if (!parent.IsGroup)
                errors.Add($"الحساب {account.Code} أبوه {parent.Code} ليس حساباً تجميعياً.");
            if (parent.Type != account.Type)
                errors.Add($"الحساب {account.Code} نوعه {account.Type} يخالف نوع أبيه {parent.Type}.");
            if (!account.Code.StartsWith(parent.Code, StringComparison.Ordinal))
                errors.Add($"كود الحساب {account.Code} لا يبدأ بكود أبيه {parent.Code}.");
        }

        return errors;
    }
}
