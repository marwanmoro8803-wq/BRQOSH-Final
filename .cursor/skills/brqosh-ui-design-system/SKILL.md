---
name: brqosh-ui-design-system
description: >-
  BRQOSH Luxury ERP design system router: tokens, WPF patterns, platform and
  lifecycle roadmaps. Use when editing Desktop UI, XAML, Themes, or
  design-system docs. Financial Truth First — dense RTL operational ERP.
---

# BRQOSH UI Design System (v18 Strict)

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)  
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)
> **Law:** [enforcement.md](enforcement.md) — read first, always  
> **Canonical specs:** `design-system/` at repo root  
> **Corpus:** grep-only · [leak-source-index.yaml](leak-source-index.yaml)  
> **User strings:** bilingual via `LocaleManager.T` in app code only — not in skill bodies  
> **CognitiveQa shell:** [CLOUD-CODE-SHELL.md](../../../CognitiveQa/design-system/CLOUD-CODE-SHELL.md) · [cloud-code-desktop.md](../omni-qa-army/cloud-code-desktop.md)

## Immutable strategic rules

| Rule | Enforcement |
|------|-------------|
| **Financial Truth First** | audit · posting · tabular · who/when/why |
| **No expansion before trust** | no fake KPI/AI without data |
| **No intelligence before data** | rules-based UI before LLM |
| **Dense RTL ERP** | not marketing · monogram nav · DataGrid 44px |
| **Strict English skills** | all modules English · MUST/MUST NOT language |

---

## Codex SaaS → BRQOSH ERP Inversion Table

| Codex says (SaaS/marketing) | BRQOSH does (ERP) |
|-----------------------------|-------------------|
| quiet utilitarian | **operational density** · fast scan |
| avoid hero sections | **immediate work screen** |
| no card-in-card marketing | toolbar band + DataGrid |
| stable dimensions | RTL Star columns · no layout shift |
| no landing page | **shell → module → document** |
| scannable but sparse | **KPI bento + drill-down lists** |
| Playwright for web | **Desktop.exe + Desktop.Tests** |

**Win vs human designer:** repeat checklist every screen · never forget RTL/a11y · cite MASTER tokens.

---

## Token Quick Reference (from MASTER)

| Token | Value | WPF |
|-------|-------|-----|
| Primary | #0D9488 Teal | actions · active nav |
| Secondary shell | #0F172A Slate 900 | sidebar |
| Accent | #D4A853 Gold | highlights |
| Background | #F1F5F9 | page canvas |
| Surface | #FFFFFF | cards |
| row-height | 44px | DataGrid |
| radius-lg | 14px | ContentCard |
| motion-normal | 200ms | hover |

Full table: [MASTER.md](../../../design-system/MASTER.md)

---

## Page Spec Index (`design-system/pages/`)

| Screen | Spec file |
|--------|-----------|
| Shell / nav | shell.md |
| Login | login.md |
| Dashboard | dashboard.md · dashboard-executive.md |
| GL | general-ledger.md |
| Sales/invoicing | invoicing-sales.md |
| Purchases | purchases-suppliers.md |
| Inventory | inventory.md |
| Bank/cash | bank-cash.md |
| Payroll | payroll-hr.md |
| Fixed assets | fixed-assets.md |
| Reports | reports-analytics.md |
| Settings | settings-multi-company.md |
| Compliance | compliance-tax.md |
| AI assistant | ai-assistant.md |
| Org provisioning | organization-provisioning.md |

**Rule:** read page spec **before** XAML for that screen.

---

## Synthesis modules (strict read order)

| # | Module | Scope |
|---|--------|-------|
| 0 | [enforcement.md](enforcement.md) | **Binding law** — MUST/MUST NOT |
| 1 | [program-design-mastery.md](program-design-mastery.md) | nature · IA · spectrum |
| 2 | [design-foundations.md](design-foundations.md) | hierarchy · color jobs · spacing |
| 3 | [interaction-environment.md](interaction-environment.md) | session · filters · desktop |
| 4 | [tokens.md](tokens.md) | WPF token values |
| 5 | [modules.md](modules.md) | screen recipes |
| 6 | [visual-effects.md](visual-effects.md) | KPI · charts |
| 7 | [human-interface.md](human-interface.md) | copy · errors |
| 8 | [wpf-patterns.md](wpf-patterns.md) | XAML implementation |
| 9 | [print-document-system.md](print-document-system.md) | **PDF · print · ZATCA · vouchers** |

## Read order (UI task)

| Step | Source |
|------|--------|
| 0 | [enforcement.md](enforcement.md) — **always first** |
| 1 | [program-design-mastery.md](program-design-mastery.md) — new screen or design decision |
| 2 | [design-foundations.md](design-foundations.md) + [interaction-environment.md](interaction-environment.md) — if IA/filter/session |
| 3 | [MASTER.md](../../../design-system/MASTER.md) |
| 4 | [pages/<screen>.md](../../../design-system/pages/) |
| 5 | [modules.md](modules.md) + [tokens.md](tokens.md) |
| 6 | [brqosh-ui/reference.md](../brqosh-ui/reference.md) + [wpf-patterns.md](wpf-patterns.md) |
| 7 | `BrqoshTheme.xaml` · `NavIconFactory` · `KpiMetricCardBuilder` |

## Roadmaps & vision

| Doc | Purpose |
|-----|---------|
| [PLATFORM-ROADMAP.md](../../../design-system/PLATFORM-ROADMAP.md) | Phase → UI bridge |
| [LIFECYCLE-ROADMAP-100.md](../../../design-system/LIFECYCLE-ROADMAP-100.md) | 100 steps login → close |
| [ECOSYSTEM-VISION.md](../../../design-system/ECOSYSTEM-VISION.md) | Luxury ecosystem |
| [MASTER-LUXURY-ECOSYSTEM.md](../../../design-system/MASTER-LUXURY-ECOSYSTEM.md) | Executive shell |
| [docs/strategic/](../../../docs/strategic/) | Strategic v4.0 |

---

## WPF Non-Negotiables

| Rule | Why |
|------|-----|
| `FindResource` / `UiResources` | UserControl `Resources["Key"]` breaks theme |
| `LocaleManager.T(ar, en)` | bilingual product |
| `BrqoshTheme.xaml` keys | single palette |
| Monogram nav/KPI — **no emoji** | professional ERP |
| DataGrid 44px rows | dense finance tables |
| Tabular numbers | accounting scan |
| Test dark + AR/EN | ThemeManager · LocaleManager |
| `Cursor=Hand` on clickables | polish |
| FocusVisualStyle | keyboard a11y |

---

## Anti-Patterns (MASTER + Codex)

| Avoid | Why |
|-------|-----|
| AI purple gradients | not crypto app |
| glassmorphism overload | readability |
| marketing hero in ERP | wrong product |
| emoji KPI badges | unprofessional |
| low-contrast gray tables | WCAG fail |
| card-in-card sections | visual noise |
| neon/crypto aesthetic | trust loss for finance |

---

## Empty · Loading · Error (every screen)

```
â–، Empty state — what to do next (Arabic)
â–، Loading — skeleton or progress in DataGrid area
â–، Error — actionable message · no stack trace to user
â–، Permission denied — RBAC message not blank screen
```

---

## Verify

```powershell
.\scripts\ui-ux-pro-max-audit.ps1
```

Pair: `brqosh-ui` · Desktop.Tests for static UI where exists.

---

## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** P-BRQOSH-DESIGN · **With:** brqosh-ui

## Default path

```
enforcement → program-design-mastery → design-foundations → MASTER → pages/<screen>
  → modules/tokens → wpf-patterns → XAML → 12-axis verify → audit
```

## Leak deepen (grep only)

```powershell
# Example — chart anti-patterns
rg -n "dual-axis" system_prompts_leaks-main/system_prompts_leaks-main/Anthropic/Claude` Code/bundled-skills/dataviz/
```

Extract principle → update module → never paste raw into SKILL.md. Index: [leak-source-index.yaml](leak-source-index.yaml).

---

### `SKILL.md` · L0 · 1/3

| | |
|--|--|
| **TRIGGER** | Desktop UI · XAML · Themes · design-system docs |
| **SKIP** | Pure backend · no visual change |
| **EXIT** | MASTER + page spec identified |
| **ANTI** | Marketing landing · emoji KPI · neon gradients |
| **TIME** | 30s |

---

### `design-system/MASTER.md` · L1 · 2/3

| | |
|--|--|
| **TRIGGER** | Any styling decision · new screen |
| **EXIT** | Tokens · shell · anti-patterns applied |
| **TIME** | 60s |

---

### `brqosh-ui/reference.md` · L2 · 3/3

| | |
|--|--|
| **TRIGGER** | WPF implementation · bindings · empty states |
| **EXIT** | XAML matches design system |
| **VERIFY** | ui-ux-pro-max-audit.ps1 or Desktop.Tests |
| **TIME** | 90s |
