# Executive Dashboard — Main Intelligent Dashboard

**Pattern:** Executive command center · Real-time financial pulse · AI-augmented

## Layout Zones (top → bottom)

1. **Hero Header** — `GlassPanel` · bilingual title + subtitle · last sync hint
2. **Smart Alert Strip** — cash-flow / compliance / anomaly · cyan edge
3. **KPI Bento Grid** — 5×2 UniformGrid inside `ContentCard`
4. **Charts Column (1.55*)** — interactive charts + performance summary
5. **Intelligence Column (*)** — AI insights · quick actions · activity feed

## KPI Cards (10 live + expansion path)

| KPI | Monogram | Accent | Data source |
|-----|----------|--------|-------------|
| Revenue | RV | `#22D3EE` | `SalesThisMonth` |
| Expenses | EX | `#F87171` | Purchases + Paid |
| Net Profit | NP | `#D4AF37` | Sales − Purchases |
| Cash Flow | CF | `#34D399` | Collected − Paid |
| AR | AR | `#0891B2` | `Receivables` |
| AP | AP | `#EA580C` | `Payables` |
| Net VAT | VT | `#4F46E5` | `NetVatThisMonth` |
| Inventory | ST | `#0D9488` | `InventoryValue` |
| Open Periods | OP | `#16A34A` | `OpenPeriods` |
| Closed Periods | CL | `#64748B` | `ClosedPeriods` |

**Phase 2 KPIs** (add cards when backend ready): MRR · Burn Rate · Working Capital · Forecast Accuracy.

## Charts (animated)

| Chart | Type | Zone |
|-------|------|------|
| Cash Trend | Line / Area | ChartsColumn |
| Revenue vs Cost | Bar comparison | ChartsColumn |
| Performance Summary | 2×2 metric grid | ChartsColumn |
| Composition (future) | Pie | ChartsColumn |
| Liquidity (future) | Gauge | ChartsColumn |
| Variance (future) | Waterfall | ChartsColumn |
| Scorecard (future) | Radar | IntelColumn |

Requirements: tooltips · clean legend · 600ms ease-out enter · theme brushes only.

## Intelligence Column

- **AI Insights** — 3 cards: AR aging · VAT filing · inventory slow movers
- **Quick Actions** — SI · JE · RC · VT chips
- **Recent Activity** — collections · payments · sales · purchases feed

## WPF Implementation

| Artifact | Path |
|----------|------|
| XAML hosts | `Views/Pages/DashboardPage.xaml` |
| Data bind | `DashboardPage.xaml.cs` → `DashboardQuery` |
| Builders | `ExecutiveDashboardBuilder.cs` · `KpiMetricCardBuilder.cs` · `SparklineBuilder.cs` |
| Test hook | `UiTestAutomation.PublishKpiPanel(KpiGrid, count)` |

## Bilingual

All copy via `LocaleManager.T`. Hero example:
- AR: «لوحة القيادة التنفيذية»
- EN: «Executive Command Center»
