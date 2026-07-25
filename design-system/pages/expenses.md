# Expenses & Reimbursements — Luxury Spec

**Feel:** Receipt capture tech · Clean approval chain

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Capture zone — receipt upload simulation · OCR preview strip  │
├─────────────────────────────────────────────────────────────┤
│ Expense list — category · amount · policy · approval status   │
├──────────────────────────┬──────────────────────────────────┤
│ Approval workflow        │ Expense analytics · by category   │
│ Requester → Manager → FI │ Trend bar chart                  │
└──────────────────────────┴──────────────────────────────────┘
```

## Receipt Capture (UI simulation)

- Drop zone `GlassPanel` with scan animation (micro 120ms)
- OCR feel: extracted fields highlight sequentially
- Policy check: green pass · red violation with rule cite

## Approval States

| State | Badge |
|-------|-------|
| Draft | Muted |
| Submitted | Info |
| Approved | Success |
| Reimbursed | Gold accent |
| Rejected | Danger |

## WPF Mapping

Extend from payment/voucher patterns · future dedicated `ExpensesPage`

## Bilingual

- AR: «مطالبة مالية» · «اعتماد» · EN: «Reimbursement» · «Approval»
