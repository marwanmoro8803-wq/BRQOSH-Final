# Payroll & HR — Luxury Spec

**Feel:** Human + professional · Privacy-aware · Clear workflow

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Cycle status banner — draft · calculated · posted · reversed  │
├──────────────────────────┬──────────────────────────────────┤
│ Employee directory       │ Payslip preview / line breakdown  │
│ Card or grid view        │ Allowances · deductions · net     │
├──────────────────────────┴──────────────────────────────────┤
│ HR analytics mini — headcount · gross · net · leave summary   │
└─────────────────────────────────────────────────────────────┘
```

## Employee Card

- Monogram avatar badge (initials — no photos required)
- Role · department · salary band (permission-gated)
- Attendance integration status chip

## Payroll Cycle

- Stepper: Open → Calculate → Review → Post
- Payslip generation batch progress
- Leave tracking sidebar (future)

## WPF Mapping

`PayrollPage` · `PayrollDialog` · `UsersPage`

## Bilingual

- AR: «مسير رواتب» · «صافي الراتب» · EN: «Payroll Run» · «Net Pay»
- Mask sensitive amounts when user lacks permission
