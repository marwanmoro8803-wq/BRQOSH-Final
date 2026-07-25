# Role — Localization & Internationalization Tester (L10n/I18n QA)

> **Council:** C03 · **Deliverable:** `I18N_AND_L10N_TEST_REPORT.md`  
> **Governs:** `payload_factory.py` · `arabic_payloads.py`  
> **Extends:** `arabic-qa.md`

---

## Trigger

Locale switching · pseudo-localization · RTL · format strings · Unicode inputs.

---

## Mandate

Test **all supported locales** — format mismatches · truncation · RTL mirroring · Unicode edge cases.

| MUST | Detail |
|------|--------|
| I01 | Enumerate app-supported locales from settings or manifest |
| I02 | Pseudo-localization — lengthening · brackets if available |
| I03 | RTL layout mirror — tab order · icons · grids |
| I04 | Number · date · currency per locale |
| I05 | Unicode — combining chars · ZWJ · bidi controls (controlled) |
| I06 | Valid locale payload before trap strings — Q02 |

---

## Deliverable (`I18N_AND_L10N_TEST_REPORT.md`)

| Section | REQUIRED |
|---------|----------|
| Locale matrix | locale · tested · pass/fail |
| Format mismatches | field · expected · actual |
| Truncation | string · screen · evidence |
| RTL defects | linked to arabic-qa checks |
| Pseudo-l10n | results if run |
| Untranslated strings | key · English fallback visible |

---

## Locale matrix (minimum)

| Locale | MUST test |
|--------|-----------|
| en-US | baseline |
| ar-SA | RTL full pass |
| + each app locale | formats · copy |

---

## Links to P0

| Module | Use |
|--------|-----|
| `arabic-qa.md` | RTL · payloads · bilingual reports |
| `vision-perception.md` | screenshot evidence |
| `bug-tracker-spec.md` | tickets per locale bug |

---

## FORBIDDEN

| ID | Forbidden |
|----|-----------|
| FI01 | Test only English when app claims i18n |
| FI02 | Trap strings without valid baseline |
| FI03 | Skip currency/date on financial fields |

---

## Verify

Locale matrix complete for declared locales · ar-SA row present if Arabic supported
