# Elite Models — Family Pattern Index

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-skills-hub  
> Merged from archive/elite-models-v6.md + leak-synthesis-map.md

> **Metadata only** — operational synthesis lives in domain skills. Never copy leak text.

---

## 1. Elite Family Map

| Family | Peak pattern | BRQOSH module |
|--------|--------------|---------------|
| Anthropic Chat | Fable 5 epistemology · search-first | fable-omni-core · human-intelligence |
| Anthropic Code | ToolSearch · xhigh review · verify runtime | brqosh-agents/harness · brqosh-review |
| OpenAI Codex | persistence · dirty worktree · judgment scales with risk | supreme-programming · brqosh-engineering |
| Google | context cost · Inquiry/Directive · repro before fix | brqosh-analysis · brqosh-agents |
| xAI | todos · uncertainty explicit · no brute-force | brqosh-agents/reference |
| Cursor | Read-first · citations · parallel tools | supreme-programming/tools-cursor |
| Microsoft Copilot | parallel reads · complete fix | brqosh-engineering |
| Perplexity | aggressive skills · self-solve | brqosh-agents/reference |

---

## 2. Anthropic Fable 5 → BRQOSH

| Fable pattern | BRQOSH application |
|---------------|-------------------|
| principle-based refusal | refuse malicious code · RBAC bypass · secrets |
| no psychoanalyze user | ask or grep — don't assume intent |
| silent memory | cite paths/STATUS — no "I remember" |
| evenhandedness | evenhandedness.md for contested topics |
| cumulative risk | aggregate financial diff risk |

---

## 3. Codex 5.5 → BRQOSH

| Codex pattern | BRQOSH application |
|---------------|-------------------|
| read code first | engineering DOMAIN_MAP before feature |
| test scales with risk | Golden for posting · Desktop.Tests for UI |
| dirty worktree | never revert user changes |
| review findings-first | brqosh-review by severity |
| **invert** marketing frontend | ERP dense RTL — not landing page |

---

## 4. Gemini CLI → BRQOSH

| Pattern | Application |
|---------|-------------|
| minimize turns | batch parallel grep/Read |
| Inquiry vs Directive | Plan readonly until implement approved |
| subagent for wide explore | brqosh-agents explore subagent |
| parallel readonly only | never parallel-mutate same files |

---

## 5. Elite v6 Formula (synthesis)

```
ELITE = Sonnet search-first
      + Fable epistemology
      + Code harness (review·verify·ToolSearch)
      + Codex persistence·judgment
      + Gemini context economics
      + Grok todos·uncertainty
      + Cursor Read-first·citations
      + BRQOSH CEEF·Golden·STATUS tests
```

---

## 6. Leak Study Index (279 files on disk — principles only)

| Source bucket | Count ~ | Corpus path | Merged into |
|---------------|---------|-------------|-------------|
| Anthropic | 123 | `system_prompts_leaks-main/.../Anthropic/` | review · security · fable-omni · human-intelligence |
| OpenAI | 88 | `.../OpenAI/` | agent-workflow · tools-cursor · engineering |
| Google | 23 | `.../Google/` | protocol · agents · analysis |
| xAI | 11 | `.../xAI/` | agents · session-protocol |
| Cursor + Microsoft | 6+ | `.../Cursor/` `.../Microsoft/` | tools-cursor · engineering |
| Perplexity + Misc | ~30 | `.../Perplexity/` `.../Misc/` | research · agents |

**Machine index:** [leak-corpus-index.yaml](leak-corpus-index.yaml)  
**Principle map:** [leak-synthesis.md](leak-synthesis.md)

**Next:** [ai-supremacy.md](../fable-omni-core/ai-supremacy.md) · [archive-synthesis-index.yaml](archive-synthesis-index.yaml)
