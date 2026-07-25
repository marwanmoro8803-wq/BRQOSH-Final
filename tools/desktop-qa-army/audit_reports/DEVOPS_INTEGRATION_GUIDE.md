# DevOps Integration

```powershell
cd tools/desktop-qa-army
python -m venv .venv
.\.venv\Scripts\pip install -r requirements.txt
.\run_audit.ps1
```

GitHub Actions: `.github/workflows/qa-audit.yml`
Azure: `azure-pipelines.yml`

Artifacts: `audit_reports/` uploaded on CI.
