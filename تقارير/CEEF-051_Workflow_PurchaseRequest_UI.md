# CEEF-051 — V2/N2: Workflow UI — طلبات الشراء

| الحقل | القيمة |
|-------|--------|
| **النطاق** | M3.5 · LIFECYCLE 22·51–52 UI |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **N2 ✅ مغلق (أدنى)** |

---

## 1. ما أُغلق

| # | البند | الدليل |
|---|-------|--------|
| N2.1 | `PurchaseRequestsPage` | قائمة + أزرار workflow |
| N2.2 | شريط pipeline | StatusBadge: مسودة→مُرسَل→مدير→مالية→PO |
| N2.3 | Submit / Approve / Reject / Convert PO | `PurchaseRequestWorkflowService` |
| N2.4 | Navigation + dialog | `purchase_requests` · `PurchaseRequestDialog` |
| N2.5 | Markup test | `PurchaseRequestsPage_has_workflow_pipeline_strip` |

### الملفات
- `Views/Pages/PurchaseRequestsPage.xaml(.cs)`
- `Views/Pages/PurchaseRequestDialog.xaml(.cs)`
- `NavigationCatalog.cs` · `MainWindow.xaml.cs`

---

## 2. سلوك UI

1. **مسودة** — حفظ من Dialog
2. **إرسال** — PR-{year}-{seq} + WorkflowEngine
3. **اعتماد** — Manager ثم Finance (فصل مهام)
4. **رفض** — سبب إلزامي
5. **تحويل PO** — PO-{year}-{seq}

---

## 3. القياس

**277 PASS** (Desktop 14 · +2 markup)

---

## 4. ⏭ لاحقاً

- إشعارات workflow · delegation UI · attachments
