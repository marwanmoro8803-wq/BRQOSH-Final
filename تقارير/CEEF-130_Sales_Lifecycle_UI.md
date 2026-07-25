# CEEF-130 — G-08 + G-09 Sales Lifecycle UI

| الحقل | القيمة |
|-------|--------|
| **النطاق** | V13b/c · G-08 عروض أسعار · G-09 أوامر بيع |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **✅ مغلق (كود + اختبار)** |

## المخرجات

### G-08 — عروض الأسعار

| مكوّن | المسار |
|--------|--------|
| قائمة | `SalesQuotationsPage.xaml(.cs)` |
| حوار إنشاء | `SalesQuotationDialog.xaml(.cs)` |
| خدمة | `SalesQuotationService` + DI |
| تنقل | `NavigationCatalog` → `sales_quotations` |
| MainWindow | `case "sales_quotations"` |

### G-09 — أوامر البيع

| مكوّن | المسار |
|--------|--------|
| قائمة | `SalesOrdersPage.xaml(.cs)` |
| حوار إنشاء | `SalesOrderDialog.xaml(.cs)` |
| فوترة من أمر | `SalesOrderService.CreateInvoiceFromOrderAsync` |
| تنقل | `NavigationCatalog` → `sales_orders` |

## الاختبارات

| نوع | الملف |
|-----|-------|
| Application | `LifecycleV12Tests.SalesQuotation_CreateAndSend` |
| Application | `SalesOrderTests` |
| Desktop nav | `V13_lifecycle_pages_registered_in_catalog` |
| Desktop UI | `SalesQuotationsPage_has_toolbar_and_grid` |
| Desktop UI | `SalesOrdersPage_has_invoice_and_new_buttons` |

## الدليل

| المستوى | الحالة | الدليل |
|---------|--------|--------|
| ✅ كود | تم | صفحتان + حواران + nav + DI |
| ✅ اختبار | PASS | Lifecycle + SalesOrder + Desktop markup |
| ✅ حي | — | — |
| ⏭ خارجي | حجز مخزون متقدم · CRM | G-10 |

**أدلة التشغيل:** `dist/evidence/CEEF-130-v13/dotnet-test.log` · `external-gates.log` · `agent-skill-regression.log`

## ملاحظة

G-09 في MARKET_LAUNCH ذكر «لا كود» — **قديم**. الكود والـ UI موجودان منذ W5/LIFECYCLE؛ هذا التقرير يُوثّق الإغلاق الرسمي.
