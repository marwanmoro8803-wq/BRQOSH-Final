# CEEF-042 — إغلاق المرحلة 2: ZATCA ذرّي + Golden Path

| الحقل | القيمة |
|-------|--------|
| **النطاق** | ACC-002 · `ZatcaPostingOrchestrator` · Golden Path بيع→ZATCA→تحصيل |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **المرحلة 2 مغلقة داخل المستودع** |

---

## 1. ما أُغلق

### 2.1 — `ZatcaPostingOrchestrator`

| السلوك | التنفيذ |
|--------|---------|
| محاكاة | توليد + إرسال **داخل** معاملة DB مع GL |
| إنتاج | توليد داخل TX · HTTP submit **بعد** commit فقط |

**الملف:** `src/Brqosh.Infrastructure/Zatca/ZatcaPostingOrchestrator.cs`

### 2.2 — خدمات الترحيل المحدّثة

- `SalesInvoicePostingService`
- `CreditNotePostingService`
- `ProgressBillingPostingService`
- `PosSaleService`

### 2.3 — اختبار Golden Path

**الملف:** `tests/Brqosh.Application.Tests/Sales/GoldenPathSellZatcaCollectTests.cs`

| الاختبار | يثبت |
|----------|------|
| `GoldenPath_Sell_ZatcaSimulation_Collect_ReducesReceivable` | فاتورة → ZATCA Cleared → سند قبض يخفّض الذمم |
| `ZatcaOrchestrator_Simulation_SubmitInsideSameTransactionAsGl` | ذرّية المحاكاة مع GL |

### 2.4 — تصحيح LIFECYCLE-100

- خطوة 2: `LoginLockoutTracker` → lockout ✅
- خطوة 47: `ZatcaPostingOrchestrator` → محاكاة داخل TX ✅

---

## 2. أدلة تشغيل

| الفحص | النتيجة |
|--------|---------|
| Domain | **58/58 PASS** |
| Application | **166/166 PASS** (+2 Golden Path) |
| Api | **23/23 PASS** |
| Desktop | **9/9 PASS** (إصلاح `LocaleManagerTests`) |
| **المجموع** | **256/256 PASS** |
| Golden Path filter | **2/2 PASS** |

---

## 3. ما لم يُغلق (صريح)

| البند | السبب |
|-------|--------|
| ZATCA OTP / Production حي | ⏭ خارجي — `OPEN_ITEMS` O-01 |
| SOCPA · MSI VM | ⏭ خارجي |
| Workflow | المرحلة 3 |

---

## 4. الحكم

| الادعاء | الحكم |
|---------|--------|
| «ZATCA محاكاة ذرّية مع GL» | ✅ |
| «مسار بيع→ZATCA→تحصيل قابل للاختبار» | ✅ |
| «ZATCA production حي» | ❌ ⏭ |

**التالي:** المرحلة 3 — Workflow Engine (CEEF-043).
