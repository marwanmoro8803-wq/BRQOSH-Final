---
name: brqosh-ui
description: >-
  BRQOSH WPF UI: RTL, dense ERP layout, DataGrid, empty states, luxury
  operational UX (not marketing). Use for screens, forms, XAML, bindings,
  or desktop visual consistency.
---

# BRQOSH UI

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)

## Principles

- RTL native, not mirrored blindly
- Dense operational ERP; KPI cards; clear hierarchy
- Reuse existing styles; apply `brqosh-ui-design-system` skill when styling

## Checklist

User context -> hierarchy -> RTL -> density -> empty/loading/error -> a11y basics

## Reference

[reference.md](reference.md)

---

## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** P-BRQOSH-UI · **With:** brqosh-ui-design-system

## Default path

```
SKILL → reference.md → design-system → XAML → Desktop.Tests
```

---

### `SKILL.md` · L0 · 1/2

| | |
|--|--|
| **TRIGGER** | Screen · form · DataGrid · RTL · WPF |
| **EXIT** | UI checklist: density · RTL · not marketing landing |
| **ANTI** | Marketing landing patterns in ERP |
| **TIME** | 25s |

---

### `reference.md` · L3 · 2/2

| | |
|--|--|
| **TRIGGER** | WPF patterns · empty states · KPI cards |
| **EXIT** | XAML matches design system |
| **VERIFY** | Desktop.Tests or static UI check |
| **TIME** | 90s |
