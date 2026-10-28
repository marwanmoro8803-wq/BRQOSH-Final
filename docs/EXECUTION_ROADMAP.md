# BRQOSH — خارطة التنفيذ الرسمية (EXECUTION ROADMAP)

> **الإصدار:** 1.2 · **تاريخ الاعتماد:** 2026-07-09  
> **آخر تحديث:** 2026-07-09 — **M1–M12 موثّقة بالكامل** · M1–M7·M9·M10·M11 ✅/مرجع · M8·M12 ⏭  
> **الحالة:** **الوثيقة الوحيدة المتبعة للتنفيذ** — كل مرحلة تُوثَّق هنا قبل الانتقال للتالية  
> **المراجع:** [`docs/مهم`](مهم) · [`LIFECYCLE-ROADMAP-100.md`](../design-system/LIFECYCLE-ROADMAP-100.md) · [`OPEN_ITEMS.md`](OPEN_ITEMS.md)

---

## قواعد الحوكمة

| قاعدة | المعنى |
|-------|--------|
| **لا ✅ بدون دليل** | اختبار · build · تقرير CEEF · تشغيل |
| **ترتيب إلزامي** | توثيق المرحلة → تنفيذ → توثيق الإغلاق → التالية |
| **Financial Truth First** | لا UI فاخر قبل سد الثغرات المالية/الأمنية |
| **⏭ خارجي** | OTP ZATCA · SOCPA · MSI VM · 1M load · ZAP — ليس «نقص كود» |

### معيار حالة الخطوة (100-step)

| الرمز | المعيى |
|------|--------|
| ✅ | سلوك + كود + اختبار (أو تشغيل موثّق) |
| 🔶 | جزئي — شاشة أو سلوك ناقص |
| 🔜 | غير منفّذ |
| ⏭ | بوابة خارجية |

---

## خريطة المراحل

| المرحلة | الاسم | الحالة | التقرير |
|---------|-------|--------|---------|
| **1** | التدقيق المستقل والمتطلبات | ✅ مغلق | CEEF-041 |
| **2** | سد ثقة P0 — ZATCA ذرّي + Golden Path | ✅ مغلق | CEEF-042 |
| **3** | P1 — Workflow ودورة المستند | ✅ مغلق (أدنى) | CEEF-043 |
| **4** | الخزينة — استيراد كشف + مطابقة | ✅ مغلق (أدنى) | CEEF-044 |
| **5** | الإقفال — شهري/سنوي/أرشفة | ✅ مغلق | CEEF-045 |
| **6** | معمارية Application — Use Cases | ✅ مغلق (أدنى) | CEEF-046 |
| **7** | UI — Golden Paths فاخرة | ✅ مغلق (أدنى) | CEEF-047 |
| **8** | بوابات السوق السعودي (O-01…) | ⏭ | OPEN_ITEMS |
| **9** | نضج Enterprise — API/Webhooks | ✅ مغلق (أدنى) | CEEF-049 |
| **10** | Data Intelligence | ✅ مرجع | CEEF-021 |
| **11** | AI Decision Support (قواعد) | ✅ مرجع | CEEF-022 |
| **12** | Global Platform SaaS | ⏭ موثّق | CEEF-035 |

---

## المرحلة 1 — التدقيق المستقل ✅

**الهدف:** فهم كامل · عدم الثقة الأعمى بالوثائق · تحديد الفجوات الحقيقية.

### المدخلات
- `docs/مهم` v4.0
- `design-system/LIFECYCLE-ROADMAP-100.md`
- `design-system/PLATFORM-ROADMAP.md` + ECOSYSTEM-VISION
- `docs/ROADMAP.md` v3.0
- `docs/OPEN_ITEMS.md`
- مقارنة بالكود الفعلي

### المخرجات الرئيسية
1. نموذج نظام: Desktop monolith · Infrastructure-heavy · Application شبه فارغة
2. **254 اختبار PASS** (Domain 58 · Application 164 · Api 23 · Desktop 9)
3. تناقضات موثّقة (Workflow P1 vs DOMAIN_MAP · ROADMAP قديم جزئياً)
4. فجوات حرجة: ZATCA خارج TX · Workflow غائب · Service Locator
5. تقدير جاهزية صادق: **~50–58%** (ليس 80% Saudi Production)

### قرارات المجلس
- اعتماد هذه الوثيقة `EXECUTION_ROADMAP.md` كمسار تنفيذ
- البدء بالمرحلة 2 قبل أي توسع UI فاخر شامل
- تصحيح حالات LIFECYCLE-100 عند كل مرحلة

### الدليل
- تقرير: `تقارير/CEEF-041_Independent_Board_Audit.md`
- تاريخ الإغلاق: **2026-07-09**

---

## المرحلة 2 — سد ثقة P0 ✅

**الهدف:** إغلاق ACC-002 (اتساق ZATCA/GL) · Golden Path قابل للاختبار · دقة LIFECYCLE.

### نطاق العمل
| # | البند | معيار الإغلاق | الحالة |
|---|-------|----------------|--------|
| 2.1 | `ZatcaPostingOrchestrator` | محاكاة داخل TX · إنتاج بعد commit | ✅ |
| 2.2 | تحديث خدمات الترحيل | Sales · Credit · ProgressBilling · POS | ✅ |
| 2.3 | اختبار Golden Path | بيع → ZATCA → تحصيل | ✅ |
| 2.4 | تصحيح LIFECYCLE-100 | lockout 🔶→✅ · خطوة 47 | ✅ |
| 2.5 | تقرير CEEF-042 | اختبارات PASS | ✅ |

### خطوات LIFECYCLE المستهدفة
- 35 · 40 · 41–48 · 63 — **Golden Path مغطى بالاختبار**

### حالة الإغلاق
- **تاريخ:** 2026-07-09
- **تقرير:** `تقارير/CEEF-042_P0_ZATCA_GoldenPath.md`
- **اختبارات:** 256 PASS (قبل M3) · +2 Golden Path

---

## المرحلة 3 — P1 Workflow ✅ (أدنى)

**الهدف:** محرك موافقات أدنى · حالات مستند Draft→Approved→Posted لمسار الشراء.

### نطاق العمل
| # | البند | معيار الإغلاق | الحالة |
|---|-------|----------------|--------|
| 3.1 | `IWorkflowEngine` + `WorkflowEngine` | سلسلة Manager→Finance | ✅ |
| 3.2 | `PurchaseRequest` + تحويل PO | طلب→اعتماد→أمر شراء | ✅ |
| 3.3 | فصل المهام | لا اعتماد ذاتي | ✅ |
| 3.4 | اختبارات Workflow | 3 اختبارات PASS | ✅ |
| 3.5 | UI pipeline | ✅ V2-N2 CEEF-051 |
| 3.6 | تقرير CEEF-043 | اختبارات PASS | ✅ |

### خطوات LIFECYCLE
- 22 🔶→🔶 (Posted موجود · pipeline شراء جزئي)
- 51 · 52 — طلب شراء + اعتماد ✅ (Domain+خدمة)

### شروط البدء للمرحلة 4
- المرحلة 3 ✅

### حالة الإغلاق
- **تاريخ:** 2026-07-09
- **تقرير:** `تقارير/CEEF-043_Workflow_PurchaseRequest.md`
- **اختبارات:** 259 PASS (256 + 3 Workflow)

---

## المرحلة 4 — الخزينة ✅ (أدنى)

**الهدف:** استيراد كشف بنكي CSV · مطابقة تلقائية مع دفتر الأستاذ · تقرير حركة نقد.

### نطاق العمل
| # | البند | الحالة |
|---|-------|--------|
| 4.1 | `BankStatement` + CSV parser | ✅ |
| 4.2 | `BankStatementImportService` | ✅ |
| 4.3 | `BankStatementMatcherService` | ✅ |
| 4.4 | `CashMovementReportService` | ✅ |
| 4.5 | اختبارات (4) | ✅ |
| 4.6 | UI استيراد/مطابقة | ✅ `BankReconciliationPage` |
| 4.7 | فروقات بنكية JE | ✅ V2-N1 CEEF-050 |

### LIFECYCLE: 67–70

### الإغلاق
- **تاريخ:** 2026-07-09 · **CEEF-044**

---

## المرحلة 5 — الإقفال ✅

**الهدف:** إقفال شهري بفحص ميزان · إقفال سنوي SOCPA-style.

| # | البند | الحالة |
|---|-------|--------|
| 5.1 | `MonthEndCloseOrchestrator` | ✅ |
| 5.2 | `FiscalPeriodService` | ✅ |
| 5.3 | `YearEndCloseService` | ✅ |
| 5.4 | اختبارات | ✅ |

### LIFECYCLE: 91–100 (97·98–100 ✅ · wizard 🔶 M7)

### الإغلاق
- **تاريخ:** 2026-07-09 · **CEEF-045**

---

## المرحلة 6 — Application Layer ✅ (أدنى)

**الهدف:** Use Case contracts + DI · خطوة نحو إزالة Service Locator.

| # | البند | الحالة |
|---|-------|--------|
| 6.1 | `IMonthEndCloseUseCase` | ✅ |
| 6.2 | `IBankStatement*UseCase` · `ICashMovementReportUseCase` | ✅ |
| 6.3 | Adapters في `AddBrqoshInfrastructure` | ✅ |
| 6.4 | Desktop → Use Cases (كامل) | 🔶 V2-N3 أدنى · CEEF-052 |

### الإغلاق
- **تاريخ:** 2026-07-09 · **CEEF-046**

---

## المرحلة 7 — UI Golden Paths ✅ (أدنى)

**الهدف:** design-system على المسارات الذهبية الأساسية.

| # | البند | الحالة |
|---|-------|--------|
| 7.1 | Dashboard executive (KPI · charts · intel) | ✅ |
| 7.2 | Invoice split + ZATCA side panel | ✅ |
| 7.3 | Bank recon split-panel matcher | ✅ |
| 7.4 | `GoldenPathUiMarkupTests` (3) | ✅ |
| 7.5 | Luxury لكل الـ 13 module | 🔶 V3 دفعات 1–3 · CEEF-053·055–057 |

### الإغلاق
- **تاريخ:** 2026-07-09 · **CEEF-047**
- **Build:** Desktop Release 0 errors

---

## المرحلة 8 — بوابات السوق ⏭ (موثّقة — لا كود)

**الهدف:** إغلاق O-01…O-05 خارج المستودع — **ليس نقص تنفيذ**.

| # | البند | O-ID | السبب | مرجع تشغيل |
|---|-------|------|--------|------------|
| 8.1 | ZATCA Production OTP | O-01 | OTP هيئة الزكاة | `docs/ZATCA_LIVE_CSID.md` |
| 8.2 | مراجعة SOCPA | O-02 | مراجع مستقل | `docs/OPEN_ITEMS.md` |
| 8.3 | MSI على VM نظيف | O-03 | جهاز ميداني | `installer/README.md` |
| 8.4 | Load 1M قيد | O-04 | أجهزة/ساعات | Stress 2k–5k داخل CI |
| 8.5 | OWASP ZAP حي | O-05 | شبكة + ماسح | `scripts/security-static-scan.ps1` فقط |

### معيار الإغلاق (⏭)
- لقطة OTP / تقرير SOCPA / لقطة MSI / تقرير load / تقرير ZAP — **خارج git**

### حالة الإغلاق
- **تاريخ التوثيق:** 2026-07-09
- **الحكم:** ⏭ — الكود جاهز للبوابات؛ الاعتماد خارجي
- **لا يمنع:** M1–M7 · M9 · M10–M11

---

## المرحلة 9 — Enterprise API ✅ (أدنى)

**الهدف:** REST ناضج · Webhooks · Idempotency — تكامل خارجي Enterprise.

| # | البند | معيار الإغلاق | الحالة |
|---|-------|----------------|--------|
| 9.1 | REST `/api/v1/*` | health · reports · intelligence · tenant | ✅ |
| 9.2 | Webhook subscriptions | GET/POST register | ✅ |
| 9.3 | Event dispatch HTTP | `PersistingDomainEventPublisher` → `WebhookDispatcher` | ✅ |
| 9.4 | Idempotency keys | `Idempotency-Key` middleware | ✅ |
| 9.5 | HMAC / retry queue | SaaS multi-instance | ⏭ M12 |

### الكود
- `Brqosh.Infrastructure/Webhooks/*`
- `Brqosh.Api/Idempotency/IdempotencyStore.cs`
- `IWebhookDispatcher` في Application

### الاختبارات
- `WebhookDispatcherTests` (3) · `WebhookSubscriptionApiTests` (2) · `IdempotencyApiTests` (1)
- **Api:** 26 PASS

### حالة الإغلاق
- **تاريخ:** 2026-07-09
- **تقرير:** `تقارير/CEEF-049_Enterprise_API_Webhooks.md`
- **سابق (stub):** CEEF-048

---

## المرحلة 10 — Data Intelligence ✅ (مرجع — إعادة تحقق)

**الهدف:** Observability · snapshots · data quality — **مغلق سابقاً CEEF-021**.

| # | البند | الحالة |
|---|-------|--------|
| 10.1 | `IEventTrackingQuery` | ✅ |
| 10.2 | `IReportSnapshotService` | ✅ |
| 10.3 | `IDataQualityService` | ✅ |
| 10.4 | API `/api/v1/report-snapshots` · `/intelligence/financial` | ✅ |

### إعادة التحقق 2026-07-09
- Api tests تغطي snapshots + intelligence — **PASS ضمن 26**
- لا فجوة كود جديدة في M10

### الإغلاق
- **مرجع:** `تقارير/CEEF-021_Data_Intelligence_Observability.md`
- **حكم الخارطة:** ✅ مرجع — لا عمل إضافي داخل المستودع

---

## المرحلة 11 — AI Decision Support ✅ (مرجع — إعادة تحقق)

**الهدف:** `IFinancialIntelligenceService` — **قواعد فقط · ليس LLM**.

| # | البند | الحالة |
|---|-------|--------|
| 11.1 | تحليل مالي rule-based | ✅ |
| 11.2 | Desktop + API exposure | ✅ |
| 11.3 | LLM / AI CFO | ⏭ O-06 استراتيجي |

### إعادة التحقق 2026-07-09
- `GET /api/v1/intelligence/financial` — PASS
- Dashboard M7 يستهلك intel — ✅

### الإغلاق
- **مرجع:** `تقارير/CEEF-022_AI_Financial_Intelligence.md`
- **حكم الخارطة:** ✅ مرجع

---

## المرحلة 12 — Global Platform SaaS ⏭ (موثّقة — استراتيجي)

**الهدف:** Multi-tenant · billing · HA · provisioning — **إعادة تصميم منفصل**.

| # | البند | الحالة | ملاحظة |
|---|-------|--------|--------|
| 12.1 | Tenant provisioning stubs | 🔶 | CEEF-035 |
| 12.2 | Billing catalog | 🔶 stub | |
| 12.3 | Cloud HA / DR | ⏭ | O-07 |
| 12.4 | Webhook DB + HMAC | ⏭ | يتبع M9 |
| 12.5 | BI warehouse / Power BI | ⏭ | منصة منفصلة |

### معيار البدء (مستقبلي)
- قرار مجلس: SaaS vs on-prem enterprise فقط
- O-01…O-05 مغلقة أو مقبولة كمخاطرة

### حالة الإغلاق
- **تاريخ التوثيق:** 2026-07-09
- **مرجع:** `تقارير/CEEF-035_SaaS_Provisioning_Stubs.md`
- **حكم الخارطة:** ⏭ — **لا ✅ عالمي** قبل طبقة البيانات والمراقبة (قاعدة الحوكمة)

---

**الخارطة V2 (N1–N5) مكتملة.** المتابعة في [`EXECUTION_ROADMAP_V3.md`](archive/roadmaps/EXECUTION_ROADMAP_V3.md) … [`EXECUTION_ROADMAP_V11.md`](EXECUTION_ROADMAP_V11.md) (**إغلاق نهائي V11** · CEEF-099).

---

## ✅ ملخص نهاية الخارطة (M1–M12)

| فئة | المراحل |
|-----|---------|
| **مغلق بالكود + اختبار** | M1 · M2 · M3 · M4 · M5 · M6 · M7 · M9 |
| **مرجع مغلق سابقاً** | M10 · M11 |
| **⏭ خارجي / استراتيجي** | M8 · M12 |
| **🔜 اختياري لاحقاً** | M7.5 luxury لكل الـ 13 module · M3.5 workflow UI |

**الخارطة الرسمية مكتملة التوثيق.** التنفيذ التالي يبدأ من O-01…O-05 (M8) أو قرار M12 SaaS — **ليس** مرحلة M13 ضمن هذه الوثيقة.

---

## Golden Paths (معيار القبول النهائي)

| المسار | الخطوات | الحالة |
|--------|---------|--------|
| بيع → ZATCA → تحصيل | 35→41–48→63→40 | **M2** ✅ |
| شراء → اعتماد → PO | 51→52→53 | **M3** ✅ |
| استيراد كشف → مطابقة | 67→68 | **M4** ✅ |
| إقفال شهري | 91→97 | **M5** ✅ |
| إقفال سنوي | 98→100 | **M5** ✅ |

---

## سجل القياس

| التاريخ | اختبارات | Coverlet | ملاحظة |
|---------|----------|----------|--------|
| 2026-07-09 (M1) | 254 PASS | ≥75% | CEEF-041 |
| 2026-07-09 (M2) | 256 PASS | ≥75% | CEEF-042 |
| 2026-07-09 (M3) | 259 PASS | ≥75% | CEEF-043 |
| 2026-07-09 (M4–M6) | 264 PASS | ≥75% | CEEF-044–046 |
| 2026-07-09 (M7–M9 stub) | 269 PASS | ≥75% | CEEF-047–048 |
| 2026-07-09 (M9 إغلاق) | **273 PASS** | ≥75% | CEEF-049 · M1–M12 موثّقة |

---

## روابط سريعة

| وثيقة | دور |
|-------|-----|
| **هذا الملف** | مسار التنفيذ الرسمي |
| `docs/مهم` | استراتيجية مجلس v4.0 |
| `design-system/LIFECYCLE-ROADMAP-100.md` | 100 خطوة تفصيلية |
| `docs/EXECUTION_ROADMAP.md` | **مسار التنفيذ الرسمي** (توثيق→تنفيذ→إغلاق) |
| `docs/OPEN_ITEMS.md` | بوابات خارجية |
| `تقارير/CEEF-*` | أدلة الإغلاق |
