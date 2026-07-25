# CEEF-127 — O-04 Load 1M (حد خارجي · prep in-repo)

| المستوى | الحالة |
|---------|--------|
| ✅ كود | JournalStress tests · run-stress-benchmark.ps1 |
| ✅ اختبار | CI stress 2k–5k PASS (~47s) |
| ⏭ حي | `-FullMillion` على VM مخصص (ساعات) |
| ⏭ خارجي | أجهزة 1M قيد |

**Prep evidence:** `dist/evidence/O-04-load/stress-benchmark-ci.log`  
**Runbook:** `docs/ops/O-04_STRESS_LOAD_RUNBOOK.md`

```powershell
.\scripts\ops\run-stress-benchmark.ps1 -FullMillion
# أرشفة الناتج في dist/evidence/O-04-load/
```

**لا يُغلق من الكود** — يتطلب VM مخصص.
