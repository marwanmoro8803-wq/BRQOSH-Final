# CEEF-043 — إغلاق المرحلة 3: Workflow أدنى + طلب شراء

| الحقل | القيمة |
|-------|--------|
| **النطاق** | `IWorkflowEngine` · `PurchaseRequest` · سلسلة Manager→Finance → PO |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **المرحلة 3 مغلقة (أدنى)** — UI pipeline مؤجّل للمرحلة 7 |

---

## 1. ما أُغلق

### 3.1 — محرك سير العمل

| الملف | الدور |
|-------|-------|
| `IWorkflowEngine.cs` | عقد Submit · Approve · Reject |
| `WorkflowEngine.cs` | سلسلة افتراضية · ربط الأدوار |
| `WorkflowInstance.cs` | حالة Pending/Approved/Rejected · فصل المهام |

**سلسلة طلب الشراء:** Manager (محاسب/مدير) → Finance (مدير نظام فقط)

### 3.2 — طلب الشراء

| الملف | الدور |
|-------|-------|
| `PurchaseRequest.cs` | Draft→Submitted→Approved→ConvertedToPo |
| `PurchaseRequestWorkflowService.cs` | إرسال · اعتماد · رفض · تحويل لـ PO |

### 3.3 — اختبارات

**الملف:** `tests/Brqosh.Application.Tests/Workflow/PurchaseRequestWorkflowTests.cs`

| الاختبار | يثبت |
|----------|------|
| `FullChain_Submit_Manager_Finance_ConvertToPo` | مسار كامل حتى أمر شراء |
| `Reject_StopsChain` | الرفض يوقف السلسلة |
| `SelfApproval_IsBlocked` | فصل المهام |

---

## 2. أدلة تشغيل

| الفحص | النتيجة |
|--------|---------|
| Domain | **58/58 PASS** |
| Application | **169/169 PASS** (+3 Workflow) |
| Api | **23/23 PASS** |
| Desktop | **9/9 PASS** (عند عدم قفل DLL) |
| **المجموع** | **259/259 PASS** |

---

## 3. ما لم يُغلق (صريح)

| البند | المرحلة |
|-------|---------|
| UI pipeline موافقات في Desktop | 7 |
| Delegation · Escalation | لاحق |
| Payment chain بعد PO | 4 |

---

## 4. الحكم

| الادعاء | الحكم |
|---------|--------|
| «Workflow Engine أدنى موجود ومختبر» | ✅ |
| «طلب شراء → اعتماد → PO» | ✅ |
| «واجهة موافقات فاخرة» | 🔶 المرحلة 7 |

**التالي:** المرحلة 4 — الخزينة (استيراد كشف + مطابقة).
