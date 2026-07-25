# CEEF-095 — V11-1: تحقق داخلي (External Gates Harness)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | `run-external-gates.ps1` + إصلاحات سكربت |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **V11-1 ✅** |

## الدليل
| الفحص | النتيجة |
|-------|---------|
| Security static | PASS |
| Tests Release `BRQOSH_SKIP_STRESS=1` | **333 PASS** |
| FullScenarioAudit | **13/13** |
| Coverlet مجمّع | **80.64%** ≥ 75% |
| `installer/publish.ps1` | PASS → `dist/BRQOSH` |

## إصلاحات
- `external-gates-checklist.ps1` — ترميز ASCII
- `run-external-gates.ps1` — `-c Release` + بوابة تغطية مجمّعة
- `installer/publish.ps1` — نسخ `.bat` بدون مسار عربي ثابت
