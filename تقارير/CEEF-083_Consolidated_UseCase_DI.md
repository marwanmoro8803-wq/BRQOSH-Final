# CEEF-083 — V8-4: Use Cases — موجة 7 (Consolidated)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | IConsolidatedTrialBalanceQuery · IConsolidatedBalanceSheetQuery · IConsolidatedIncomeStatementQuery |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **V8-4 ✅** |

## التغيير
- `ConsolidatedTrialBalancePage` → DI `IConsolidatedTrialBalanceQuery`
- `ConsolidatedBalanceSheetPage` → DI `IConsolidatedBalanceSheetQuery`
- `ConsolidatedIncomeStatementPage` → DI `IConsolidatedIncomeStatementQuery`

## الاختبارات
321 PASS إجمالي (58 Domain · 179 Application · 26 Api · 58 Desktop)
