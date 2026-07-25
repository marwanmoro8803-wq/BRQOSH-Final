# Bug Tracker Spec — Internal Tickets

> **Prompt:** raw QA prompt · Internal Bug Tracker section  
> **Leak:** Anthropic code-review severity ordering

---

## Directory layout

```
audit_reports/bug_tracker/
  BUG_INDEX.md
  BUG-0001.md
  BUG-0002.md
  ...
```

---

## BUG-XXXX.md schema (MANDATORY)

| Field | Required | Values |
|-------|----------|--------|
| Bug ID | yes | `BUG-0001` zero-padded |
| Title | yes | concise |
| Severity | yes | Critical / High / Medium / Low |
| Status | yes | Open · In Progress · Resolved |
| Detected By | yes | QA role name |
| Date & Time | yes | ISO-8601 |
| Description | yes | detail |
| Steps to Reproduce | yes | numbered |
| Expected | yes | |
| Actual | yes | |
| Evidence | yes | paths to screenshots/logs |
| Source Reference | no | file:line |
| Suggested Fix | no | from report-excellence |

---

## BUG_INDEX.md (auto-regenerated)

| Section | Content |
|---------|---------|
| Summary table | ID · Title · Severity · Status · Role |
| Statistics | total · open · by severity · by role |
| Session | audit timestamp · target app path |

**MUST** regenerate index after each new ticket.

---

## ID assignment

- Auto-increment · zero-pad 4 digits
- **FORBIDDEN** reuse IDs across sessions (append only)

---

## Severity → exit code (CI P1)

| Condition | Exit |
|-----------|------|
| Any Critical open | non-zero |
| High only | configurable · default non-zero |
| Medium/Low only | zero with warnings |

---

## Launcher integration (P3)

| Feature | Law |
|---------|-----|
| Read BUG_INDEX | parse markdown table |
| Filter | severity · status · role |
| Status update | writes back to BUG-XXXX.md |
| CSV export | same fields |

**P1 code owner:** `bug_tracker.py` · Jinja2 template

---

## Link to report-excellence

Every Critical/High finding in `AUDIT_REPORT.md` **MUST** have matching BUG file.
