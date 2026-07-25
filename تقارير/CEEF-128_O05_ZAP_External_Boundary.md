# CEEF-128 — O-05 OWASP ZAP (حد خارجي · prep in-repo)

| المستوى | الحالة |
|---------|--------|
| ✅ كود | security-static-scan.ps1 · run-zap-baseline.ps1 |
| ✅ اختبار | static scan PASS (0 warnings) |
| ⏭ حي | ZAP HTML ضد staging |
| ⏭ خارجي | Docker + `BRQOSH_ZAP_TARGET_URL` |

**Prep evidence:** `dist/evidence/O-05-zap/static-scan.log`  
**Runbook:** `docs/ops/O-05_ZAP_BASELINE_RUNBOOK.md`

```powershell
$env:BRQOSH_ZAP_TARGET_URL = "https://staging.example.com"
.\scripts\ops\run-zap-baseline.ps1
```

**لا يُغلق من الكود** — يتطلب URL staging حي.
