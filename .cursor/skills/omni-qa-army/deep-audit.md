# Deep Audit — Performance, Security & Compliance

> **Source:** raw prompt L145–176 (ideas only)  
> **Leaks:** Anthropic security-review (3-phase) · verify.md (runtime observation)  
> **Roles:** Pen Tester (S04) · Perf Tester (S06) · L10n (S07)  
> **Related:** [roles/role-pentest.md](roles/role-pentest.md) · [roles/role-performance.md](roles/role-performance.md) · [brqosh-review/security.md](../brqosh-review/security.md)

---

## Purpose

Beyond UI — ensure app is **fast, secure, globally accessible, and compliant**. Simulate real-world stress with measurable evidence.

---

## Performance stress and memory (Perf Tester)

| Probe | Method | Flag |
|-------|--------|------|
| UI responsiveness | rapid click/scroll loops | thread freeze > 200ms |
| CPU/memory | process monitor baseline | monotonic memory after 100× same action |
| Race conditions | double Submit before response | duplicate/corrupt state |
| Network mid-transfer | kill/slow proxy | hang without recovery |

**Deliverable:** `PERFORMANCE_AND_MEMORY_ANALYSIS_REPORT.md` with time-series metrics.

---

## Network and API interception (Pen Tester)

| MUST when app uses HTTP(S) | Evidence |
|----------------------------|----------|
| Intercept traffic | proxy capture log |
| Check auth tokens | missing on protected calls |
| Cleartext sensitive data | Critical finding |
| Malformed server responses | client handling graded |
| Invalid certificates | pinning/validation behavior |

**Confidence filter:** security findings ≥80% or drop — [security.md](../brqosh-review/security.md).

---

## Global accessibility and i18n (UX + L10n)

| Standard | Check |
|----------|-------|
| WCAG 2.1 AA | contrast · keyboard · focus |
| Screen reader | accessibility tree roles |
| RTL injection | layout break · truncation |
| Locale formats | `12345` · `12,345.67` · `12.345,67` |

Cross-ref [arabic-qa.md](arabic-qa.md) for Arabic-specific probes.

---

## Dependency and CVE scan (Pen + DevOps)

| Input | Tool (future code) | Output |
|-------|-------------------|--------|
| package.json / requirements.txt / Cargo.toml | audit CLI | CVE list |
| Source tree | secret regex | hardcoded keys |

| Severity | CVE |
|----------|-----|
| Critical/High | block release recommendation |
| Medium | track in bug tracker |

---

## Business logic oracles (Superintelligence overlap)

| Step | Action |
|------|--------|
| 1 Infer | rules from UI + code (e.g. Total = sum prices) |
| 2 Test | input combinations |
| 3 Flag | output contradicts inferred rule |

Apply to tax · dates · inventory · permissions.

---

## Review order (deep audit session)

```
SECURITY → PERF → COMPLIANCE → LOGIC ORACLES
```

---

## Forbidden

| FORBIDDEN | DO INSTEAD |
|-----------|------------|
| mitm claims without capture | evidence path |
| perf numbers without baseline | before/after |
| Skip i18n on Arabic-target apps | mandatory RTL pass |

---

## Future code owners (when authorized)

`performance_profiler.py` · `network_interceptor.py` · `dependency_scanner.py` · `l10n_i18n_tester.py` · `pentest_engine.py`

---

## Verify

```powershell
Test-Path .cursor/skills/omni-qa-army/deep-audit.md
rg "deep-audit" .cursor/skills/omni-qa-army/SKILL.md
```
