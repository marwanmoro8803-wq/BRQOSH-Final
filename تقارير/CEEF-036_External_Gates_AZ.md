# CEEF-036 — بوابات خارجية A→Z (O-01…O-05)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | دليل رئيسي · 5 runbooks · 4 سكربتات ops |
| **التاريخ** | 2026-07-09 |
| **الحكم** | تحضير كامل — الإغلاق ⏭ حتى الأدلة الحية |

## الملفات

| البند | Runbook | سكربت |
|-------|---------|--------|
| O-01 | `docs/ops/O-01_ZATCA_PRODUCTION_RUNBOOK.md` | `docs/ZATCA_LIVE_CSID.md` |
| O-02 | `docs/ops/O-02_SOCPA_REVIEWER_PACKAGE.md` | — |
| O-03 | `docs/ops/O-03_MSI_VM_RUNBOOK.md` | `scripts/ops/verify-msi-build.ps1` |
| O-04 | `docs/ops/O-04_STRESS_LOAD_RUNBOOK.md` | `scripts/ops/run-stress-benchmark.ps1` |
| O-05 | `docs/SECURITY_CHECKLIST.md` | `scripts/ops/run-zap-baseline.ps1` |
| الكل | `docs/ops/MASTER_EXTERNAL_GATES_RUNBOOK.md` | `scripts/ops/run-external-gates.ps1` |

## أدلة داخلية

- `run-external-gates.ps1` محدّث: coverage per-assembly + publish MSI
- CI: `BRQOSH_SKIP_STRESS=1` · `BRQOSH_TEST=1`

## الحكم

| الادعاء | الحكم |
|---------|--------|
| مسار A→Z موثّق ومؤتمت قدر الإمكان | ✅ |
| O-01…O-05 مغلقة | ❌ خارجية |
