---
name: brqosh-engineering
description: >-
  BRQOSH .NET 9 / C# / WPF / EF engineering: DOMAIN_MAP, DI, RBAC,
  AppServices.CreateDb, tests, git. Use with supreme-programming-mastery for
  universal discipline; this skill for BRQOSH-specific depth.
---

# BRQOSH Engineering

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)

> **Parent:** `supreme-programming-mastery` (Fable-class workflow + 12 axes + languages)

## Explore loop

```
grep/glob -> entry points -> Application interfaces -> Infrastructure -> Desktop bindings -> tests
```

## Must follow

- Local patterns win; read before write
- Bind SecurityContext before admin DB access
- SQLite + PostgreSQL: SchemaSqlGuard for new columns
- PowerShell: use `;` not always `&&`

## References

- [engineering.md](engineering.md) — architecture, EF, WPF, tests
- [programming.md](programming.md) — 12 closure axes
- [polyglot.md](polyglot.md) — language pitfalls in diffs
- [tools.md](tools.md) — grep, MCP, parallel tools

---

## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** P-BRQOSH-CODE · **Parent:** [supreme-programming/SKILL.md](../supreme-programming-mastery/SKILL.md#engineering-roadmap)

## Default path

```
SKILL → engineering.md → [programming|polyglot|tools] → tests → STATUS.yaml
```

---

### `SKILL.md` · L0 · 1/5

| | |
|--|--|
| **TRIGGER** | Any BRQOSH .NET / WPF / EF code |
| **NEXT** | engineering.md |
| **EXIT** | Explore loop + RBAC awareness |
| **TIME** | 20s |

---

### `engineering.md` · L1 · 2/5 — foundational

| | |
|--|--|
| **TRIGGER** | **First** read — DOMAIN_MAP · EF · WPF |
| **EXIT** | Entry points + BindSecurityContext understood |
| **ANTI** | Code before DOMAIN_MAP |
| **TIME** | 90s |

---

### `programming.md` · L2 · 3/5

| | |
|--|--|
| **TRIGGER** | BRQOSH 12-axis closure on change |
| **TIME** | 45s |

---

### `polyglot.md` · L2 · 3b/5

| | |
|--|--|
| **TRIGGER** | SQL / PS / scripts in diff |
| **TIME** | 30s |

---

### `tools.md` · L2 · 2b/5

| | |
|--|--|
| **TRIGGER** | DISCOVER phase · parallel grep · MCP |
| **TIME** | 30s |

## Scenarios

| Task | Path |
|------|------|
| New feature | engineering → programming → supreme/testing |
| PG migration | engineering → SchemaSqlGuard → accounting if financial |
