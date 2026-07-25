---
name: brqosh-review
description: >-
  Code and security review for BRQOSH: 8-angle review, financial angle,
  confidence filter, standards/spec, simplify cleanup. Use for PR review,
  pre-merge audit, or security-sensitive changes.
---

# BRQOSH Review

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)

## Display order

Security -> Spec -> Bugs -> Standards -> Cleanup

## Security

Confidence >= 80% before reporting; no noise

## BRQOSH

Financial posting angle mandatory for money-related diffs

## References

- [code-review.md](code-review.md)
- [security.md](security.md)
- [standards.md](standards.md)
- [simplify.md](simplify.md)

---

## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** P-BRQOSH-REVIEW

## Default path

```
SKILL → security.md → code-review.md → standards.md → simplify.md
```

---

### `security.md` · L1 · 1/4 — first on money/auth paths

| | |
|--|--|
| **TRIGGER** | PR · auth · secrets · financial code |
| **EXIT** | Security findings before style |
| **TIME** | 60s |

---

### `code-review.md` · L2 · 2/4

| | |
|--|--|
| **TRIGGER** | Any code review |
| **EXIT** | Findings by severity |
| **TIME** | 45s |

## Scenarios

| Request | Path |
|---------|------|
| Financial PR | security → code-review → accounting angle |
