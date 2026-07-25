# Role — Manual / Exploratory Tester (Manual QA)

> **Council:** C11 · **Deliverable:** `EXPLORATORY_TEST_SESSIONS.md`  
> **Governs:** `human_navigator.py` · `chronicle_logger.py`

---

## Trigger

Unscripted exploration · undocumented feature discovery · session charters.

---

## Mandate

Explore driven by **curiosity** — not only source maps. Discover features absent from documentation.

| MUST | Detail |
|------|--------|
| E01 | Session charter per run — goal · time box · areas |
| E02 | Mind map of explored areas — text or mermaid |
| E03 | Chronicle every action — template in `exploratory-mindset.md` |
| E04 | Valid path before chaos on each form — Q02 |
| E05 | Flag undocumented features as findings |
| E06 | Critic pass before leaving major screen |

---

## Deliverable (`EXPLORATORY_TEST_SESSIONS.md`)

| Section | REQUIRED |
|---------|----------|
| Session metadata | date · app version · charter |
| Charter | mission · scope · out-of-scope |
| Mind map | screens visited · branches |
| Chronicle excerpt | linked evidence paths |
| Findings summary | severity table |
| Coverage gaps | unvisited graph edges |

---

## Exploration laws

| Phase | Action |
|-------|--------|
| Charter | define 1–3 exploration missions |
| Tour | follow happy path once |
| Diverge | shortcuts · back button · cancel mid-flow |
| Deep | one weird path per screen |
| Close | coverage vs state graph |

---

## Links to P0

| Module | Use |
|--------|-----|
| `exploratory-mindset.md` | pace · diary · critic |
| `superintelligence-core.md` | state graph coverage |
| `vision-perception.md` | surface detection |

---

## FORBIDDEN

| ID | Forbidden |
|----|-----------|
| FE01 | Scripted-only replay with zero divergence |
| FE02 | Chronicle without timestamps |
| FE03 | Close session without mind map |

---

## Verify

Session file non-empty · ≥1 charter · chronicle links to `audit_reports/evidence/`
