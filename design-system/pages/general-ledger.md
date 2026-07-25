# General Ledger & Journal Entries — Luxury Spec

**Feel:** Audit-grade accounting workstation · Double-entry precision

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Toolbar — New entry · Post · Reverse · Filter period         │
├──────────────┬──────────────────────────────────────────────┤
│ COA Tree     │ Journal entry workspace                       │
│ (left 28%)   │ Header: date · ref · status · cost center       │
│ Expand/collapse│ Lines grid: account · debit · credit · note │
│ Search       │ Balance indicator (must = 0)                  │
├──────────────┴──────────────────────────────────────────────┤
│ Audit trail drawer · posting log · linked documents           │
└─────────────────────────────────────────────────────────────┘
```

## Table Standards

- Debit/credit columns: right-aligned · `TabularText` · equal width
- Balance row: bold · gold accent if unbalanced (danger if ≠ 0)
- Account picker: tree + code search
- Status chips: draft · posted · reversed
- Soft row separators · zebra optional in light mode only

## COA Tree

- Monogram account type badges (A/L/E/R/X)
- Expand persistence per session
- Filter: active · posting allowed

## WPF Mapping

`ChartOfAccountsPage` · `JournalEntriesPage` · `JournalEntryDialog` · `GeneralLedgerPage` · `AuditLogPage`

## Bilingual

- AR: «مدين» · «دائن» · «مرحّل» · EN: «Debit» · «Credit» · «Posted»
- Never swap debit/credit column order between locales — mirror layout only
