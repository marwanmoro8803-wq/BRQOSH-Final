# Arabic QA — RTL · Payloads · Bilingual Reports

> **Prompt:** raw QA prompt · Multilingual Intelligence section  
> **Platform:** Program Developer · Saudi/Gulf desktop apps  
> **Skill language:** English · **Report/runtime:** Arabic allowed

---

## Scope

First-class QA for Arabic UI — not translation afterthought.

| MUST test | Detail |
|-----------|--------|
| RTL layout | alignment · order · scroll |
| Arabic numerals | U+0661–0663 vs ASCII 123 display/input |
| Hijri dates | if app claims support |
| Arabic comma | U+060C in copy and parsers |
| Mixed AR/EN | labels · errors · URLs |
| Bidi overrides | Unicode edge strings (controlled) |

---

## Payload categories (P1 `arabic_payloads.py`)

| Category | Examples |
|----------|----------|
| Names | realistic triple names |
| Addresses | GCC formats |
| Phone | +966 patterns |
| Lorem | Arabic paragraphs |
| Traps | Tatweel · bidi control chars · ZWJ |

**MUST** run valid Arabic path before trap strings on same field.

---

## Vision / navigation (RTL)

| Check | Pass criteria |
|-------|---------------|
| Tab order | logical for RTL reading |
| Icons | not mirrored incorrectly |
| DataGrid | columns align · no clip |
| Error messages | Arabic grammar · no mojibake |

Link: `vision-perception.md` a11y first.

---

## Bilingual reporting law

| Setting | Behavior |
|---------|----------|
| `report_locale: en` | English report body |
| `report_locale: ar` | Arabic report body (runtime) |
| Finding in Arabic UI | title/description matches UI language |

**FORBIDDEN:** Arabic paragraphs inside `.cursor/skills/*.md` bodies.

---

## Locale formats

| Locale | Test |
|--------|------|
| ar-SA | dates · currency SAR |
| en-US | baseline compare |
| pseudo-l10n | lengthening · P1 |

---

## Severity boost

| Issue | Severity |
|-------|----------|
| RTL layout break on primary workflow | High minimum |
| Arabic error shows English stack trace | Critical UX |
| Wrong decimal in money field | Critical (accounting apps) |

---

## Related

- `brqosh-ui` · `LocaleManager.T` patterns for app under test
- P1: `l10n_i18n_tester.py` · `multilingual_report_builder.py`
