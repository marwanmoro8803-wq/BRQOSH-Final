namespace UiAudit;

internal enum AuditAction
{
  /// <summary>فتح حوار إنشاء ثم إلغاء.</summary>
  OpenDialogCancel,
  /// <summary>فتح حوار إنشاء، إدخال بيانات، حفظ/ترحيل.</summary>
  OpenDialogSave,
  /// <summary>تشغيل تقرير أو عرض كشف.</summary>
  RunReport,
  /// <summary>تصدير PDF — يقبل تنبيه «اعرض أولاً».</summary>
  ExportPdf,
  /// <summary>نقرة بدون تحديد — يجب ظهور تنبيه.</summary>
  RequireSelectionWarning,
  /// <summary>تحديد أول صف ثم تنفيذ الإجراء.</summary>
  SelectFirstThenAct,
  /// <summary>نقرة مباشرة (إعدادات، فحص، تحديث).</summary>
  ClickSafe,
  /// <summary>فتح معالج ثم إغلاق.</summary>
  OpenWizardCancel,
  /// <summary>تحقق من الجدول/المحتوى فقط.</summary>
  VerifyGridOnly,
}

internal sealed record ButtonSpec(string LabelContains, AuditAction Action, string? DialogKind = null);

internal sealed record PageSpec(string NavTitle, ButtonSpec[] Buttons, int? MinGridRows = null);

internal static class PageAuditCatalog
{
    public static IReadOnlyList<PageSpec> All { get; } =
    [
        new("لوحة التحكم", [new ButtonSpec("KPI", AuditAction.VerifyGridOnly)], MinGridRows: 1),

        new("دليل الحسابات",
        [
            new("حساب جديد", AuditAction.OpenDialogCancel, "account"),
            new("تعديل", AuditAction.RequireSelectionWarning),
            new("إيقاف", AuditAction.RequireSelectionWarning),
        ]),

        new("القيود اليومية",
        [
            new("قيد جديد", AuditAction.OpenDialogCancel, "journal"),
            new("عرض", AuditAction.SelectFirstThenAct),
            new("عكس", AuditAction.RequireSelectionWarning),
        ], MinGridRows: 1),

        new("مراكز التكلفة",
        [
            new("مركز تكلفة", AuditAction.OpenDialogSave, "cost_center"),
            new("إيقاف", AuditAction.RequireSelectionWarning),
        ]),

        new("الفترات المالية",
        [
            new("إقفال", AuditAction.RequireSelectionWarning),
            new("إعادة فتح", AuditAction.RequireSelectionWarning),
        ], MinGridRows: 1),

        new("المشاريع",
        [
            new("مشروع جديد", AuditAction.OpenDialogCancel, "project"),
            new("إيقاف", AuditAction.RequireSelectionWarning),
        ]),

        new("العقود",
        [
            new("عقد جديد", AuditAction.OpenDialogCancel, "contract"),
            new("استكمال", AuditAction.RequireSelectionWarning),
            new("إلغاء", AuditAction.RequireSelectionWarning),
        ]),

        new("جداول الكميات (BOQ)",
        [
            new("بند جديد", AuditAction.RequireSelectionWarning),
            new("تعديل", AuditAction.RequireSelectionWarning),
            new("حذف", AuditAction.RequireSelectionWarning),
        ]),

        new("المستخلصات",
        [
            new("مستخلص جديد", AuditAction.OpenDialogCancel, "progress_billing"),
            new("عرض", AuditAction.SelectFirstThenAct),
            new("عرض القيد", AuditAction.SelectFirstThenAct),
            new("إلغاء", AuditAction.RequireSelectionWarning),
        ]),

        new("مقاولو الباطن",
        [
            new("عقد باطن", AuditAction.OpenDialogCancel, "subcontract"),
            new("محتجز", AuditAction.RequireSelectionWarning),
            new("استكمال", AuditAction.RequireSelectionWarning),
            new("إلغاء", AuditAction.RequireSelectionWarning),
        ]),

        new("المحتجزات",
        [
            new("إفراج محتجز عميل", AuditAction.RequireSelectionWarning),
            new("إفراج محتجز باطن", AuditAction.RequireSelectionWarning),
        ]),

        new("ربحية المشاريع", [], MinGridRows: 0),

        new("قوائم المواد (BOM)",
        [
            new("BOM جديد", AuditAction.OpenDialogCancel, "bom"),
            new("تعديل", AuditAction.RequireSelectionWarning),
            new("تعديل الاسم", AuditAction.RequireSelectionWarning),
            new("إيقاف", AuditAction.RequireSelectionWarning),
        ]),

        new("أوامر الإنتاج",
        [
            new("أمر إنتاج", AuditAction.OpenDialogCancel, "work_order"),
            new("إصدار", AuditAction.RequireSelectionWarning),
            new("إكمال", AuditAction.RequireSelectionWarning),
            new("عرض قيد", AuditAction.SelectFirstThenAct),
            new("إلغاء", AuditAction.RequireSelectionWarning),
        ]),

        new("أعمال تحت التشغيل", [], MinGridRows: 0),

        new("شاشة البيع",
        [
            new("دفع", AuditAction.RequireSelectionWarning),
            new("إفراغ", AuditAction.ClickSafe),
            new("طباعة", AuditAction.RequireSelectionWarning),
        ]),

        new("الورديات",
        [
            new("فتح وردية", AuditAction.OpenDialogCancel, "pos_shift"),
            new("إغلاق", AuditAction.RequireSelectionWarning),
        ]),

        new("تقارير نقاط البيع", [], MinGridRows: 0),

        new("العملاء",
        [
            new("عميل جديد", AuditAction.OpenDialogSave, "customer"),
            new("إيقاف", AuditAction.RequireSelectionWarning),
        ], MinGridRows: 1),

        new("كشف حساب عميل",
        [
            new("عرض الكشف", AuditAction.RunReport),
            new("تصدير PDF", AuditAction.ExportPdf),
        ], MinGridRows: 1),

        new("عروض الأسعار",
        [
            new("عرض سعر", AuditAction.OpenDialogCancel, "sales_quotation"),
        ], MinGridRows: 0),

        new("أوامر البيع",
        [
            new("أمر بيع", AuditAction.OpenDialogCancel, "sales_order"),
        ], MinGridRows: 0),

        new("فواتير المبيعات",
        [
            new("فاتورة جديدة", AuditAction.OpenDialogCancel, "sales_invoice"),
            new("عرض", AuditAction.SelectFirstThenAct),
            new("عرض القيد", AuditAction.SelectFirstThenAct),
            new("إلغاء", AuditAction.RequireSelectionWarning),
            new("طباعة PDF", AuditAction.SelectFirstThenAct),
        ], MinGridRows: 1),

        new("مردودات المبيعات",
        [
            new("إشعار دائن", AuditAction.OpenDialogCancel, "credit_note"),
            new("عرض", AuditAction.SelectFirstThenAct),
            new("عرض القيد", AuditAction.SelectFirstThenAct),
            new("طباعة PDF", AuditAction.SelectFirstThenAct),
            new("إلغاء", AuditAction.RequireSelectionWarning),
        ]),

        new("العملاء المحتملون",
        [
            new("عميل محتمل", AuditAction.OpenDialogCancel, "crm_lead"),
        ], MinGridRows: 0),

        new("فرص البيع",
        [
            new("فرصة", AuditAction.OpenDialogCancel, "crm_opportunity"),
        ], MinGridRows: 0),

        new("أنشطة CRM",
        [
            new("نشاط", AuditAction.OpenDialogCancel, "crm_activity"),
        ], MinGridRows: 0),

        new("الموردون",
        [
            new("مورد جديد", AuditAction.OpenDialogSave, "supplier"),
            new("إيقاف", AuditAction.RequireSelectionWarning),
        ], MinGridRows: 1),

        new("كشف حساب مورد",
        [
            new("عرض الكشف", AuditAction.RunReport),
            new("تصدير PDF", AuditAction.ExportPdf),
        ], MinGridRows: 1),

        new("طلبات الشراء",
        [
            new("طلب جديد", AuditAction.OpenDialogCancel, "purchase_request"),
        ], MinGridRows: 0),

        new("أوامر الشراء",
        [
            new("أمر شراء", AuditAction.OpenDialogCancel, "purchase_order"),
            new("عرض", AuditAction.SelectFirstThenAct),
            new("إلغاء", AuditAction.RequireSelectionWarning),
            new("إنشاء فاتورة", AuditAction.RequireSelectionWarning),
        ]),

        new("فواتير المشتريات",
        [
            new("فاتورة مشتريات", AuditAction.OpenDialogCancel, "purchase_invoice"),
            new("عرض", AuditAction.SelectFirstThenAct),
            new("عرض القيد", AuditAction.SelectFirstThenAct),
            new("إلغاء", AuditAction.RequireSelectionWarning),
            new("طباعة PDF", AuditAction.SelectFirstThenAct),
        ], MinGridRows: 1),

        new("أذونات استلام البضاعة", [], MinGridRows: 0),

        new("إشعارات المدين (مشتريات)", [], MinGridRows: 0),

        new("الأصناف والمنتجات",
        [
            new("صنف جديد", AuditAction.OpenDialogSave, "product"),
            new("إيقاف", AuditAction.RequireSelectionWarning),
        ], MinGridRows: 1),

        new("المستودعات",
        [
            new("مستودع جديد", AuditAction.OpenDialogSave, "warehouse"),
            new("إيقاف", AuditAction.RequireSelectionWarning),
        ], MinGridRows: 1),

        new("حركات المخزون",
        [
            new("حركة مخزنية", AuditAction.OpenDialogCancel, "stock_movement"),
            new("عرض", AuditAction.SelectFirstThenAct),
            new("عرض القيد", AuditAction.SelectFirstThenAct),
            new("إلغاء", AuditAction.RequireSelectionWarning),
        ], MinGridRows: 1),

        new("الجرد",
        [
            new("تحديث", AuditAction.RunReport),
            new("ترحيل فروقات", AuditAction.RequireSelectionWarning),
        ]),

        new("التكلفة الإضافية", [], MinGridRows: 0),

        new("سندات القبض",
        [
            new("سند قبض", AuditAction.OpenDialogCancel, "receipt_voucher"),
            new("عرض القيد", AuditAction.SelectFirstThenAct),
            new("طباعة PDF", AuditAction.SelectFirstThenAct),
            new("عكس", AuditAction.RequireSelectionWarning),
        ], MinGridRows: 1),

        new("سندات الصرف",
        [
            new("سند صرف", AuditAction.OpenDialogCancel, "payment_voucher"),
            new("عرض القيد", AuditAction.SelectFirstThenAct),
            new("طباعة PDF", AuditAction.SelectFirstThenAct),
            new("عكس", AuditAction.RequireSelectionWarning),
        ]),

        new("الحسابات البنكية",
        [
            new("حساب بنكي", AuditAction.OpenDialogSave, "bank_account"),
            new("إيقاف", AuditAction.RequireSelectionWarning),
        ], MinGridRows: 1),

        new("التحويلات البنكية",
        [
            new("تحويل بنكي", AuditAction.OpenDialogCancel, "bank_transfer"),
            new("عرض القيد", AuditAction.RequireSelectionWarning),
        ], MinGridRows: 0),

        new("أسعار الصرف", [], MinGridRows: 0),

        new("الشيكات",
        [
            new("شيك وارد", AuditAction.OpenDialogCancel, "cheque"),
            new("شيك صادر", AuditAction.OpenDialogCancel, "cheque"),
            new("إيداع", AuditAction.RequireSelectionWarning),
            new("تحصيل", AuditAction.RequireSelectionWarning),
            new("ارتجاع", AuditAction.RequireSelectionWarning),
            new("إلغاء", AuditAction.RequireSelectionWarning),
            new("عرض قيود", AuditAction.RequireSelectionWarning),
        ]),

        new("التسوية البنكية",
        [
            new("عرض", AuditAction.RunReport),
        ]),

        new("سجل الفواتير الإلكترونية",
        [
            new("إرسال", AuditAction.RequireSelectionWarning),
            new("عرض XML", AuditAction.SelectFirstThenAct),
            new("فتح المستند", AuditAction.SelectFirstThenAct),
        ]),

        new("إعدادات هيئة الزكاة",
        [
            new("حفظ", AuditAction.ClickSafe),
            new("فحص الجاهزية", AuditAction.ClickSafe),
            new("معالج CSID", AuditAction.OpenWizardCancel),
        ]),

        new("ميزان المراجعة",
        [
            new("عرض", AuditAction.RunReport),
            new("PDF", AuditAction.ExportPdf),
        ], MinGridRows: 1),

        new("قائمة الدخل",
        [
            new("عرض", AuditAction.RunReport),
            new("تصدير PDF", AuditAction.ExportPdf),
        ], MinGridRows: 1),

        new("قائمة المركز المالي",
        [
            new("عرض", AuditAction.RunReport),
            new("تصدير PDF", AuditAction.ExportPdf),
        ], MinGridRows: 1),

        new("قائمة التدفقات النقدية",
        [
            new("عرض", AuditAction.RunReport),
            new("تصدير PDF", AuditAction.ExportPdf),
        ], MinGridRows: 1),

        new("ميزان مراجعة موحّد",
        [
            new("عرض", AuditAction.RunReport),
            new("PDF", AuditAction.ExportPdf),
        ], MinGridRows: 0),

        new("قائمة دخل موحّدة",
        [
            new("عرض", AuditAction.RunReport),
            new("تصدير PDF", AuditAction.ExportPdf),
        ], MinGridRows: 0),

        new("مركز مالي موحّد",
        [
            new("عرض", AuditAction.RunReport),
            new("تصدير PDF", AuditAction.ExportPdf),
        ], MinGridRows: 0),

        new("إزالة المعاملات بين الشركات",
        [
            new("عرض", AuditAction.RunReport),
        ], MinGridRows: 0),

        new("إقرار ضريبة القيمة المضافة",
        [
            new("عرض الإقرار", AuditAction.RunReport),
            new("تصدير PDF", AuditAction.ExportPdf),
        ]),

        new("دفتر الأستاذ العام",
        [
            new("عرض", AuditAction.RunReport),
            new("تصدير PDF", AuditAction.ExportPdf),
        ], MinGridRows: 1),

        new("سجل الأصول",
        [
            new("أصل جديد", AuditAction.OpenDialogCancel, "fixed_asset"),
            new("عرض قيد الاقتناء", AuditAction.SelectFirstThenAct),
            new("عرض قيد الاستبعاد", AuditAction.RequireSelectionWarning),
            new("استبعاد", AuditAction.RequireSelectionWarning),
            new("إهلاك", AuditAction.ClickSafe),
        ]),

        new("دورات الرواتب",
        [
            new("دورة رواتب", AuditAction.OpenDialogCancel, "payroll"),
            new("ترحيل", AuditAction.RequireSelectionWarning),
            new("عرض القيد", AuditAction.SelectFirstThenAct),
            new("عكس", AuditAction.RequireSelectionWarning),
        ]),

        new("إعدادات المنشأة",
        [
            new("حفظ", AuditAction.ClickSafe),
            new("تفعيل الترخيص", AuditAction.OpenWizardCancel),
        ]),

        new("المنشآت",
        [
            new("منشأة جديدة", AuditAction.OpenDialogCancel, "organization"),
        ], MinGridRows: 0),

        new("مجموعات الشركات", [], MinGridRows: 0),

        new("الفروع", [], MinGridRows: 0),

        new("المستخدمون",
        [
            new("مستخدم جديد", AuditAction.OpenDialogCancel, "user"),
        ], MinGridRows: 1),

        new("سجل التدقيق", [], MinGridRows: 1),

        new("جودة البيانات", [], MinGridRows: 0),

        new("أحداث المنصة", [], MinGridRows: 0),

        new("دعم العملاء", [], MinGridRows: 0),

    ];
}
