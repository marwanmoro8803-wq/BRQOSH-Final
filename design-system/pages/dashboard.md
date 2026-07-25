# Page Override — Dashboard (لوحة التحكم)

**Pattern:** KPI Bento Grid (3 columns desktop, wrap on narrow)

## Structure

1. **ToolbarCard** — Page title + subtitle (data freshness note)
2. **KpiGrid** — WrapPanel of KpiMetricCard items (min width 280px)
3. **Error state** — ContentCard with DangerBrush message

## KPI Card Anatomy

```
┌─────────────────────────────┐
│ [AB]  Title (Caption)    │▌│  ← monogram badge + accent bar
│ 1,234,567.89 ر.س            │  ← value (Bold 26px) + currency suffix
└─────────────────────────────┘
```

## Monogram Map (no emoji)

| KPI | Monogram | Accent |
|-----|----------|--------|
| مبيعات الشهر | MS | #2563EB |
| مشتريات الشهر | MC | #7C3AED |
| تحصيلات الشهر | TH | #059669 |
| مدفوعات الشهر | MD | #DC2626 |
| ذمم العملاء | DA | #0891B2 |
| ذمم الموردين | DD | #EA580C |
| صافي ضريبة | VAT | #4F46E5 |
| قيمة المخزون | ST | #0D9488 |
| فترات مفتوحة | OP | #16A34A |
| فترات مقفلة | CL | #64748B |

## Loading

Show `LoadingStatePanel` with "جاري تحميل لوحة التحكم..." until query completes.

## a11y

- `AutomationProperties.AutomationId="KpiGrid"` on grid host
- Each card: `AutomationProperties.Name` = KPI title
