# O-05 — OWASP ZAP Baseline Pen Test (Live)

| الحقل | القيمة |
|-------|--------|
| **البند** | O-05 / G-05 |
| **الحالة in-repo** | ✅ سكربت + static scan |
| **الحالة حي** | ⏭ خارجي — Docker + staging URL |

## المتطلبات

1. Docker Desktop يعمل
2. API أو staging URL متاح عبر HTTPS
3. متغير `BRQOSH_ZAP_TARGET_URL` (مثال: `https://staging.example.com`)

## التشغيل

```powershell
cd C:\Users\qc\Projects\BRQOSH
$env:BRQOSH_ZAP_TARGET_URL = 'https://your-staging-host'
.\scripts\ops\run-zap-baseline.ps1
```

## الأدلة المطلوبة (⏭ حي)

ضع في `dist/evidence/O-05-zap/`:

| ملف | الوصف |
|-----|--------|
| `zap-report.html` | تقرير ZAP baseline |
| `zap-baseline.log` | سجل التشغيل الكامل |
| `screenshot-staging.png` | لقطة للبيئة المستهدفة |

## فحص ثابت (in-repo — بديل CI)

```powershell
.\scripts\security-static-scan.ps1
```

## المراجع

- `docs/ops/MASTER_EXTERNAL_GATES_RUNBOOK.md` §5
- `dist/evidence/O-05-zap/BLOCKERS.md`
- `تقارير/CEEF-119_O01_O05_Execution_Wave.md`
