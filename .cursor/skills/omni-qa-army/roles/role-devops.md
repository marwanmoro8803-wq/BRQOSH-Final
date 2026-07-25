# Role — CI/CD & DevOps Engineer (DevOps)

> **Council:** C08 · **Deliverable:** `DEVOPS_INTEGRATION_GUIDE.md`  
> **Governs:** `audit_orchestrator.py` CI mode · `dependency_scanner.py`

---

## Trigger

Pipeline embedding · headless config · Docker/shell entry · exit codes · artifacts.

---

## Mandate

Package framework for **repeatable CI execution** — GitHub Actions · Azure Pipelines · Jenkins.

| MUST | Detail |
|------|--------|
| D01 | `run_all.ps1` and/or `run_all.sh` — single entry |
| D02 | Config file — app path · output dir · severity gate |
| D03 | Headless — no interactive prompts |
| D04 | Artifact upload — reports · evidence · BUG_INDEX |
| D05 | Exit code contract — document mapping |
| D06 | Optional Dockerfile — documented if used |

---

## Deliverable (`DEVOPS_INTEGRATION_GUIDE.md`)

| Section | REQUIRED |
|---------|----------|
| Prerequisites | Python version · OS agents |
| Quick start | one command local |
| Config reference | every key |
| GitHub Actions | sample workflow YAML |
| Azure Pipelines | sample stage |
| Jenkins | sample pipeline snippet |
| Artifacts | paths CI must publish |
| Exit codes | table |

---

## Exit code contract (default)

| Code | Meaning |
|------|---------|
| 0 | pass — no Critical/High open |
| 1 | Critical findings |
| 2 | High findings (configurable) |
| 3 | framework error |

---

## Anthropic doctor pattern

| Check | Action |
|-------|--------|
| Stale evidence | fail if report older than build |
| Missing config | fail fast with message |
| Partial run | non-zero if orchestrator aborted |

---

## FORBIDDEN

| ID | Forbidden |
|----|-----------|
| FD01 | CI requires manual launcher click |
| FD02 | Undocumented exit codes |
| FD03 | Secrets in committed config |

---

## Verify

Sample workflow snippet in skill spec · config template when code wave authorized
