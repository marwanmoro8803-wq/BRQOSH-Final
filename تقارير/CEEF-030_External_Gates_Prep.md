# CEEF-030 — تحضير بوابات O-01…O-05 (اعتمادات سوقية)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | سكربتات تشغيل · قائمة تحقق · تحقق داخل المستودع قبل التنفيذ الحي |
| **التاريخ** | 2026-07-08 |
| **الحكم** | **تحضير مُثبت** — O-01…O-05 **تبقى ⏭ خارجية** حتى الأدلة الحية |

---

## 1. ما أُنجز

| المكوّن | الملف |
|---------|-------|
| قائمة تحقق O-01…O-05 مع نقاط الدخول | `scripts/ops/external-gates-checklist.ps1` |
| مشغّل موحّد (قائمة + أمن + اختبارات + FullScenario) | `scripts/ops/run-external-gates.ps1` |

### محتوى القائمة (ملخّص)

| البند | نقطة الدخول في المستودع |
|-------|-------------------------|
| O-01 ZATCA OTP حي | `docs/ZATCA_LIVE_CSID.md` · CSID Wizard |
| O-02 SOCPA | `docs/SECURITY_CHECKLIST.md` + مراجع مستقل |
| O-03 MSI VM نظيف | `installer/README.md` · `build-msi.ps1` |
| O-04 Load 1M | `JournalStressLoadTests` (`BRQOSH_SKIP_STRESS=0`) |
| O-05 ZAP حي | `security-static-scan.ps1` (ثابت) + ZAP على staging |

---

## 2. أدلة تشغيل

تشغيل:

```powershell
.\scripts\ops\run-external-gates.ps1
```

| الفحص | النتيجة |
|--------|---------|
| `external-gates-checklist.ps1` | يطبع القائمة |
| Security static | **PASS** |
| Tests (`BRQOSH_SKIP_STRESS=1`) | **228/228 PASS** |
| FullScenarioAudit | **13/13 PASS** |

---

## 3. ما لم يُغلق — صريح

| البند | ما يلزم للإغلاق |
|-------|-----------------|
| O-01 | OTP هيئة + 10 فواتير إنتاج مقبولة |
| O-02 | تقرير مراجع SOCPA موقّع |
| O-03 | MSI على VM نظيف + لقطة |
| O-04 | تشغيل stress 1M على أجهزة مخصّصة |
| O-05 | تقرير ZAP baseline على API staging |

---

## 4. الحكم

| الادعاء | الحكم |
|---------|--------|
| مسار تشغيل موحّد لتحضير O-01…O-05 | ✅ داخل المستودع |
| O-01…O-05 مغلقة | ❌ — خارجية |

**التالي:** تنفيذ O-01…O-05 حسب الأولوية السوقية (خارج نطاق الكود وحده).
