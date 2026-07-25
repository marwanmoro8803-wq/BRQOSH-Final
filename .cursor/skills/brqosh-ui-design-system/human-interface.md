# Human Interface (v18 Strict)

> **Binding:** [enforcement.md](enforcement.md) · **Locale:** `LocaleManager.T(ar, en)` for all visible strings

---

## Voice

| Attribute | Requirement |
|-----------|-------------|
| Register | professional · direct · respectful |
| Person | neutral preferred ("Saved") — avoid condescension |
| Bilingual | AR + EN parity — AR primary for Saudi accountants |
| Jargon | correct accounting terms — official ZATCA/SOCPA labels |

**FORBIDDEN:** marketing hype · chatty AI tone on posting screens · apology without fix path.

---

## Labels and buttons

### Naming

```
[Object] + [Action]  → "New invoice" / localized equivalent
[Action] only        → when context clear: "Save"
[Danger verb]        → "Delete" not soft euphemism
```

### Hierarchy

| Level | Style | Examples |
|-------|-------|----------|
| Primary | `PrimaryButton` | Save · Post · Submit |
| Secondary | `GhostButton` | Cancel · Back |
| Tertiary | hyperlink | View audit |
| Danger | isolated + confirm | Delete period |

**MUST** use active voice: "Post" → toast "Posted" — not "Posting operation completed successfully".

---

## Errors (actionable — mandatory structure)

Every error MUST include:

```
1. What happened (specific)
2. Why (if known)
3. What to do next
```

| FORBIDDEN | REQUIRED |
|-----------|----------|
| "An error occurred" | "Cannot post: period is closed. Open period in Fiscal Year settings." |
| "Error 500" | "Server unreachable. Check network and retry." |
| "Sorry!" | (omit — provide fix path only) |

**MUST NOT** show stack trace · blame user · fake success.

---

## Empty states

```
[Monogram optional — never emoji]
Title:    No invoices yet
Body:     Create your first invoice to start sales.
[Button: New invoice]  — if RBAC permits
```

- MUST be honest when no data
- MUST offer next action when logical
- **MUST NOT** show sample rows as live data

---

## Tooltips

- MUST tooltip unfamiliar toolbar icons (AR/EN)
- MUST tooltip disabled controls with reason (RBAC · closed period)
- MUST NOT use in-app manual paragraphs (Codex)

---

## Confirmation dialogs

**MUST** confirm: delete · post · period close · ZATCA submit · irreversible actions.

```
Title:   Post this entry?
Body:    Journal #… amount … — cannot undo after approval.
Actions: [Post] [Cancel]
```

Audit-sensitive confirms MUST show acting user from session.

---

## Toasts

| Event | Duration | Content |
|-------|----------|---------|
| Success | 3s | past-tense result |
| Warning | 5s | partial failure |
| Error | sticky | until dismissed + fix link |

Position: non-blocking — MUST NOT cover active DataGrid selection.

---

## Forms

- MUST show inline field error below control
- MUST use logical tab order · RTL mirror
- MUST mark required fields consistently — not color alone
- MUST parse/format amounts with culture · display tabular

---

## Accessibility copy

- MUST set `AutomationProperties.Name` on KPI tiles
- MUST pair icon + text for status (posted/pending/draft)
- MUST NOT rely on color alone

---

## Microcopy forbidden

| FORBIDDEN | USE |
|-----------|-----|
| "Click here" | verb on button |
| "Successfully completed operation" | "Saved" |
| Lorem as live data | empty or labeled demo |
| Internal IDs in UI | human labels |
| Fake step numbers | plain sections |

---

## Surgical edit discipline

Small request → change **only** that string/color/control.  
**MUST NOT** redesign surrounding layout.
