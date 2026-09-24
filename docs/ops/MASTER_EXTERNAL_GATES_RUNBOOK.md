# دليل تنفيذ البوابات الخارجية A→Z — O-01…O-05

**الغرض:** مسار تشغيل موحّد من التحضير داخل المستودع حتى الأدلة الحية.  
**قاعدة:** لا يُعتبر البند مغلقاً دون دليل ميداني موثّق.

---

## المرحلة 0 — التحقق الداخلي (قبل أي خطوة خارجية)

```powershell
cd C:\Users\qc\Projects\BRQOSH
.\scripts\ops\run-external-gates.ps1
```

| الفحص | المتوقع |
|--------|---------|
| Security static | PASS |
| Tests `BRQOSH_SKIP_STRESS=1` | 0 فشل |
| FullScenarioAudit | 13/13 |
| Coverlet مجمّع | ≥ 75% |

---

## المرحلة 1 — O-01 ZATCA Production OTP

**المرجع التفصيلي:** `docs/ops/O-01_ZATCA_PRODUCTION_RUNBOOK.md`  
**السكربت:** `docs/ZATCA_LIVE_CSID.md`

### الخطوات (ملخّص)

1. تجهيز منشأة بيانات VAT صحيح 15 رقماً
2. فتح بوابة فاتورة والحصول على OTP (صلاحية قصيرة)
3. Desktop → إعدادات ZATCA → معالج CSID
4. طلب شهادة امتثال ثم إنتاج
5. إصدار **10 فواتير إنتاج** مقبولة — حفظ UUID + لقطات
6. أرشفة الأدلة في `dist/evidence/O-01-zatca/`

**الحكم:** ⏭ خارجي حتى 10 فواتير + OTP هيئة

---

## المرحلة 2 — O-02 مراجعة SOCPA

**المرجع:** `docs/ops/O-02_SOCPA_REVIEWER_PACKAGE.md`

1. تسليم الحزمة للمراجع المستقل
2. تشغيل `FullScenarioAudit` على بيئة المراجع
3. جلسة محاسبية P0/P1 (سيناريوهات CEEF-023)
4. تقرير موقّع PDF → `dist/evidence/O-02-socpa/`

**الحكم:** ⏭ خارجي حتى تقرير موقّع

---

## المرحلة 3 — O-03 MSI على VM نظيفة

**السكربت الداخلي:** `scripts/ops/verify-msi-build.ps1`  
**المرجع الميداني:** `docs/ops/O-03_MSI_VM_RUNBOOK.md`

1. بناء MSI في CI أو محلياً
2. VM Windows نظيف (بدون .NET SDK)
3. تثبيت MSI + تشغيل البرنامج
4. `FullScenarioAudit` + لقطة شاشة QA-AUDIT
5. (اختياري) توقيع ميداني للمثبت

**الحكم:** ⏭ خارجي حتى لقطة VM

---

## المرحلة 4 — O-04 Load 1M قيد

**السكربت:** `scripts/ops/run-stress-benchmark.ps1`

```powershell
# CI-safe (2k–5k)
.\scripts\ops\run-stress-benchmark.ps1

# هدف 1M — VM مخصّصة، على خادم PostgreSQL للاختبار (ح-٥٩)
.\scripts\ops\run-stress-benchmark.ps1 -FullMillion -Postgres
```

أرشفة: زمن التنفيذ، CPU/RAM، سجلات PostgreSQL → `dist/evidence/O-04-stress/`

**الحكم:** ⏭ خارجي حتى تشغيل 1M موثّق

---

## المرحلة 5 — O-05 OWASP ZAP

**الثابت (داخل المستودع):** `scripts/security-static-scan.ps1`  
**الحي:** `scripts/ops/run-zap-baseline.ps1`

```powershell
$env:BRQOSH_ZAP_TARGET_URL = "https://staging-api.example.com"
$env:BRQOSH_API_KEY = "..."
.\scripts\ops\run-zap-baseline.ps1
```

**الحكم:** ⏭ خارجي حتى تقرير ZAP baseline/Pen signed

---

## جدول الأدلة المطلوبة

| البند | مجلد الأدلة | دليل القبول |
|-------|-------------|-------------|
| O-01 | `dist/evidence/O-01-zatca/` | 10 UUID + OTP log redacted |
| O-02 | `dist/evidence/O-02-socpa/` | تقرير SOCPA PDF |
| O-03 | `dist/evidence/O-03-msi-vm/` | لقطات VM + install log |
| O-04 | `dist/evidence/O-04-stress/` | 1M benchmark log |
| O-05 | `dist/evidence/O-05-zap/` | HTML/PDF ZAP report |

---

## بعد الإغلاق

حدّث `docs/OPEN_ITEMS.md` بروابط الأدلة و CEEF تقرير إغلاق لكل بند.
