# Chronicle Protocol — Hyper-Detailed Audit Diary

> **Source:** raw prompt L53–59 · L74–79 (ideas only)  
> **Leaks:** Anthropic code-review (severity-first findings)  
> **Roles:** Manual QA (S02) · Beta Tester (S05) · Report Auditor (S12)  
> **Related:** [exploratory-mindset.md](exploratory-mindset.md) · [report-excellence.md](report-excellence.md)

---

## Purpose

Every action produces an **instantaneous micro-log** — the Chronological Audit Diary. Human-readable, engaging, evidence-grade.

---

## Mandatory template (per action)

```markdown
[TIMESTAMP] **Observation:** (labels · colors · states visible)
[REASONING] **Cognitive Analysis:** (why interact · expectation · likely handler)
[ACTION] **Execution:** (control id · text typed · coords if needed)
[RESULT] **System Response:** (delta · match/mismatch · anomaly flag)
```

**MUST** flag expectation mismatch as `learning anomaly` and update state model.

---

## Voice and tone (Human Mental Agility)

| DO | DON'T |
|----|-------|
| Lively internal monologue | Robotic "Clicked X. Saw Y." |
| Express surprise/frustration | Dry telemetry only |
| One critic sentence per major screen | Skip reasoning |

**Example tone:** "Alright, let's see what this 'Sync' button does... clicked... whole table flickered and lost selection. Classic state mismanagement!"

---

## Session structure

| Section | Content |
|---------|---------|
| Header | app name · version · session id · roles active |
| Body | chronological entries (template above) |
| Footer | screens visited · anomalies count · interrupts triggered |

---

## Dual-path documentation (forms)

| Path | Chronicle requirement |
|------|----------------------|
| Valid compliant data | separate entry block |
| Return malicious/weird | separate entry block — same form |

**MUST NOT** merge valid and fuzz paths into one vague entry.

---

## Integration with reports

| Artifact | Chronicle use |
|----------|---------------|
| `AUDIT_REPORT.md` | full unedited diary section |
| `EXPLORATORY_TEST_SESSIONS.md` | session charters + diary excerpts |
| `BUG-XXXX.md` | repro steps derived from chronicle lines |
| Beta narrative | first-person quotes from diary |

---

## Severity link (report-excellence)

When chronicle flags anomaly → promote to finding with:

- One-sentence summary
- Exact repro command
- Severity (Critical/High/Medium/Low)

---

## Bilingual output law (Q05)

| Layer | Language |
|-------|----------|
| Skill spec (this file) | English |
| Runtime diary (when authorized) | English or Arabic per launcher config |
| Report matching UI language | same as tested app locale |

---

## Forbidden

| FORBIDDEN | DO INSTEAD |
|-----------|------------|
| Batch log without per-action detail | one template block per action |
| Chronicle without timestamp | ISO or elapsed ms |
| Delete diary on crash | append recovery entries |

---

## Future code owners (when authorized)

`chronicle_logger.py` · Launcher live log panel · `multilingual_report_builder.py`

---

## Verify

```powershell
Test-Path .cursor/skills/omni-qa-army/chronicle-protocol.md
rg "chronicle-protocol" .cursor/skills/omni-qa-army/exploratory-mindset.md
```
