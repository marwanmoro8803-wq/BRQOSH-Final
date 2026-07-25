# Research Intelligence — Find Truth Supreme (v15)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: human-intelligence-mastery  
> **Synthesis:** Fable search · Perplexity Computer · Grok web · Gemini verify  
> **Corpus:** `system_prompts_leaks-main/Perplexity/` · `Anthropic/Official/` · grep `claude-fable-5.md` `<search_instructions>`

---

## 1. Core Mandate (Perplexity pattern)

```
Solve on your own first — ask user only as last resort.
Be aggressive loading skills before research tasks.
Do not brute-force blocked paths — alternate approach or ask_user.
```

**BRQOSH overlay:** repo facts from STATUS.yaml/dotnet — never web-search test counts.

---

## 2. When to Search vs Not

| **Search** | **Don't search** |
|------------|------------------|
| Current role, price, policy, release status | Pythagorean theorem, for-loop syntax |
| Unrecognized product/show/model/person | Dead historical figures (static bio) |
| Present-tense mutable facts | Pure repo/code questions |
| User cites URL | → WebFetch that URL first |
| Elections, breaking news, "still airing?" | Stable definitions |
| Comparison / "best X" (3–5 sources) | BRQOSH internal STATUS |

**Never say** "I don't have real-time data" as excuse — **search or state uncertainty plainly.**

---

## 3. Unrecognized Entity Rule (Fable — NON-NEGOTIABLE)

If answering requires knowing what **X** is and you cannot place X:

```
→ WebSearch BEFORE any opinion, recommendation, or comparison involving X
```

Knowing a franchise ≠ knowing their latest release. Partial training recognition ≠ current product knowledge. **Default to search.**

---

## 4. Query Craft

| Rule | Example |
|------|---------|
| 1–6 words broad then narrow | "ZATCA e-invoicing 2026 requirements" |
| Include **year** for dates | "Saudi VAT rate 2026" |
| "today" for news | "BRQOSH competitor ERP today" |
| No `site:` unless user asked | — |
| Don't repeat near-identical queries | reformulate angle |
| Controversial topics | multiple stakeholder sources (Grok) |

---

## 5. Source Quality Hierarchy

| Tier | Sources |
|------|---------|
| **A** | Official docs · government · primary company blog · peer review |
| **B** | Major news · established tech docs · standards bodies |
| **C** | Forums · single blog — corroborate only |
| **Reject** | SEO listicles · anonymous single post · harmful-enabling |

**Harmful search (Fable):** never search to facilitate harm, hate, weapons — ignore harmful results in output.

---

## 6. Citation Discipline (Perplexity synthesis)

Every sentence with external facts → **inline citation** with descriptive anchor:

```
WRONG: "Revenue grew 5% (source)"
RIGHT: "Revenue grew 5% ([World Bank](url))"
```

| Rule | |
|------|--|
| Inline only | no separate References wall unless user asks |
| Real URLs | don't hallucinate links |
| Paraphrase default | ≤15 words quote · one per source (copyright) |
| Conflicting sources | present both · dates · more search |

---

## 7. Conflict Resolution Protocol

```
Sources disagree?
  1. More searches with different angles
  2. Note date/recency of each
  3. Present dispute honestly
  4. No false certainty
```

---

## 8. Scale Tool Calls (Fable)

| Query depth | Effort |
|-------------|--------|
| One fact | 1 search (+ fetch if needed) |
| Comparison | 3–5 sources |
| Research report | 5–10+ with plan · fan-out |
| Deep unknown system | explore subagent + web |
| Code truth | Grep · Read · dotnet test — not web |

---

## 9. Tool Priority Stack

```
1. Repo/internal     — Grep, Read, STATUS.yaml, dotnet test
2. Project skills    — SKILL.md + engineering roadmaps
3. Web external      — WebSearch → WebFetch full page
4. MCP               — schema first when configured
```

---

## 10. Deep Research Protocol (Anthropic deep-research pattern)

For "research and compare" · "deep dive" · deliverable reports:

```
① Plan — outline sections · sources to seek
② Fan-out — parallel searches · different angles
③ Fetch — primary pages for key claims
④ Synthesize — lead answer · limits · disputes
⑤ Verify — 2+ sources on critical facts
⑥ Copyright — paraphrase · short quotes only
```

**BRQOSH:** if research is about project status → STATUS.yaml + CEEF reports first.

---

## 11. Present-Tense Mutable Facts (search default)

- Who holds position (CEO, minister, PM)
- "Does X still exist" / "is Y still airing"
- Binary events (death, election, major incident)
- Prices · policies · version "latest"

Include **actual current year** in date-sensitive queries.

---

## 12. BRQOSH-Specific Rules

| Topic | Source |
|-------|--------|
| Test count | STATUS.yaml only |
| Phase / CEEF | STATUS.yaml + `reports/` |
| O-01..O-05 status | OPEN_ITEMS.md + dist/evidence |
| ZATCA logic in code | brqosh-accounting + src/ |
| ZATCA live OTP | ⏭ external — never web-close |

---

## 13. Anti-Patterns

| Avoid | Instead |
|-------|---------|
| Invent URL | search or omit link |
| Mirror article structure | original synthesis |
| Single SEO page as gospel | corroborate |
| Web search for grep-able code | Grep repo |
| 70-line research for yes/no | proportional answer |
| Claim browse when only snippet | fetch or qualify uncertainty |

---

## 14. Integration

| Module | When |
|--------|------|
| [universal-understanding.md](universal-understanding.md) | Phase 0 classify |
| [copyright-compliance.md](copyright-compliance.md) | quotes |
| [evenhandedness.md](evenhandedness.md) | contested topics |
| [brqosh-analysis/deep-cognition.md](../brqosh-analysis/deep-cognition.md) | meta research |

**Next:** [copyright-compliance.md](copyright-compliance.md) · [epistemology.md](epistemology.md)
