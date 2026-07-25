# BRQOSH — خارطة التنفيذ V2 (ما بعد M1–M12)

> **الإصدار:** 1.1 · **تاريخ الاعتماد:** 2026-07-09  
> **آخر تحديث:** 2026-07-09 — **N1–N5 موثّقة بالكامل**  
> **الحالة:** **الوثيقة المتبعة بعد إغلاق** [`EXECUTION_ROADMAP.md`](EXECUTION_ROADMAP.md) (M1–M12)  
> **المنهج:** توثيق المرحلة → تنفيذ → إغلاق CEEF → التالية  
> **المراجع:** [`docs/مهم`](مهم) · [`OPEN_ITEMS.md`](OPEN_ITEMS.md) · [`LIFECYCLE-ROADMAP-100.md`](../design-system/LIFECYCLE-ROADMAP-100.md)

---

## السياق

| V1 (M1–M12) | V2 (N1–N5) |
|-------------|------------|
| Financial Truth · ZATCA · Workflow domain · Treasury · Close · API | **إكمال الفجوات** · Workflow UI · Use Cases · Luxury UI · بوابات |

---

## خريطة المراحل V2 — **مكتملة**

| المرحلة | الاسم | الحالة | التقرير |
|---------|-------|--------|---------|
| **N1** | فروقات بنكية → قيد JE | ✅ مغلق (أدنى) | CEEF-050 |
| **N2** | Workflow UI — طلب شراء | ✅ مغلق (أدنى) | CEEF-051 |
| **N3** | Desktop → Use Cases | ✅ مغلق (أدنى) | CEEF-052 |
| **N4** | Luxury UI — دفعة مشتريات | ✅ مغلق (أدنى) | CEEF-053 |
| **N5** | بوابات خارجية O-01…O-05 | ⏭ موثّق | CEEF-054 |

---

## N1 — فروقات بنكية ✅

**تقرير:** `تقارير/CEEF-050_Bank_Difference_JE.md` · LIFECYCLE-68

| # | البند | الحالة |
|---|-------|--------|
| N1.1–N1.6 | JE · Use Case · UI · tests | ✅ |

**إغلاق:** 2026-07-09 · 275 PASS

---

## N2 — Workflow UI ✅

**تقرير:** `تقارير/CEEF-051_Workflow_PurchaseRequest_UI.md` · M3.5 · LIFECYCLE 22·51–52

| # | البند | الحالة |
|---|-------|--------|
| N2.1 | `PurchaseRequestsPage` | ✅ |
| N2.2 | شريط pipeline StatusBadge | ✅ |
| N2.3 | Submit / Approve / Reject / Convert PO | ✅ |
| N2.4 | Navigation + Dialog + markup test | ✅ |
| N2.5 | CEEF-051 | ✅ |

**إغلاق:** 2026-07-09 · 277 PASS

---

## N3 — Desktop Use Cases ✅

**تقرير:** `تقارير/CEEF-052_Desktop_UseCases_Facade.md` · M6.4

| # | البند | الحالة |
|---|-------|--------|
| N3.1 | Bank recon → Use Cases | ✅ |
| N3.2 | `IMonthEndCloseUseCase` | ✅ |
| N3.3 | `AppServices.CreateInfrastructureScope()` | ✅ |
| N3.4 | CEEF-052 | ✅ |

**⏭ لاحقاً:** باقي الصفحات (~50) — خارج نطاق V2

---

## N4 — Luxury UI ✅ (دفعة 1)

**تقرير:** `تقارير/CEEF-053_Luxury_UI_Purchases_Batch.md` · M7.5 جزئي

| # | البند | الحالة |
|---|-------|--------|
| N4.1 | Purchases — PR page + pipeline | ✅ |
| N4.2 | Projects batch | ⏭ V3 |
| N4.3 | Payroll batch | ⏭ V3 |
| N4.4 | Markup tests (5) | ✅ |
| N4.5 | CEEF-053 | ✅ |

---

## N5 — بوابات خارجية ⏭

**تقرير:** `تقارير/CEEF-054_External_Gates_V2_Closure.md`

| # | البند | حكم |
|---|-------|-----|
| N5.1–N5.5 | O-01…O-05 | ⏭ تشغيل ميداني |

**لا كود إضافي** — مراجع: `docs/ops/WAKE_UP_CHECKLIST.md`

---

## ✅ ملخص نهاية V2

| فئة | المراحل |
|-----|---------|
| **مغلق بالكود** | N1 · N2 · N3 · N4 (دفعة 1) |
| **⏭ خارجي** | N5 |

**المتابعة:** [`EXECUTION_ROADMAP_V3.md`](EXECUTION_ROADMAP_V3.md) — V3-1…V3-5 ✅

**277 PASS** · coverlet ≥75%

---

## سجل القياس V2

| التاريخ | اختبارات | ملاحظة |
|---------|----------|--------|
| 2026-07-09 (baseline) | 273 | قبل V2 |
| 2026-07-09 (N1) | 275 | CEEF-050 |
| 2026-07-09 (N2–N4) | **277** | CEEF-051–053 · N5 ⏭ CEEF-054 |

---

## روابط

| وثيقة | دور |
|-------|-----|
| [`EXECUTION_ROADMAP.md`](EXECUTION_ROADMAP.md) | V1 M1–M12 |
| **هذا الملف** | V2 N1–N5 (مغلق) |
| `docs/مهم` | استراتيجية مجلس |
