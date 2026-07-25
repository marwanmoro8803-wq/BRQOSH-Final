---
name: universal-coding
description: >-
  Stack detection and verify for any repo (Node, Rust, Go, Python, .NET).
  Use when project is not BRQOSH or stack is unknown. Do not assume ZATCA/CEEF.
---

# Universal Coding

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)

## Golden rule

Discover repo first -> apply method -> verify with project tool -> no done without proof

## Stack detect

| Signal | Stack |
|--------|-------|
| *.csproj | .NET |
| package.json | Node |
| Cargo.toml | Rust |
| go.mod | Go |
| pyproject.toml | Python |

## If BRQOSH

Switch to brqosh-* skills + STATUS.yaml

## Reference

[reference.md](reference.md)

---

## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** P-UNIVERSAL-CODE · **Not BRQOSH**

## Default path

```
SKILL → reference.md → supreme/languages/* → project verify
```

---

### `SKILL.md` · L0 · 1/2

| | |
|--|--|
| **TRIGGER** | Non-BRQOSH repo · unknown stack |
| **EXIT** | Stack manifest detected |
| **WARNING** | If BRQOSH paths → switch to brqosh-* |
| **TIME** | 20s |

---

### `reference.md` · L3 · 2/2

| | |
|--|--|
| **EXIT** | Build/test PASS · no CEEF |
| **VERIFY** | Project-native test command |
| **TIME** | 60s |
