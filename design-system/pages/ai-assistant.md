# AI Assistant Panel — Luxury Spec

**Feel:** Embedded intelligence · Glass + subtle glow · Not a bolt-on chatbot

## Layout (300px column)

```
┌─────────────────────────┐
│ AI Header — context chip │
│ Current page · company   │
├─────────────────────────┤
│ Chat thread              │
│ User / assistant bubbles │
├─────────────────────────┤
│ Predictive cards stack   │
│ Anomaly · opportunity    │
├─────────────────────────┤
│ Quick explain buttons    │
│ Next-best-action chips   │
├─────────────────────────┤
│ Input bar + send         │
└─────────────────────────┘
```

## Visual Treatment

- Panel: `GlassPanel` + 1px `ElectricCyanBrush` edge at 40% opacity
- Assistant bubbles: `AiInsightCard`
- User bubbles: `SurfaceBrush` subtle
- Glow: only on panel edge — never full-screen neon
- Context chips: page key · period · branch

## Capabilities (UI shell)

| Feature | UI element |
|---------|------------|
| Chat | Scrollable thread |
| Predictions | Stacked insight cards |
| Anomalies | `AlertStrip` inline in thread |
| Quick explain | Ghost buttons on selected grid row |
| Next-best-action | `QuickActionChip` |

## WPF Mapping

`MainWindow.xaml` — `AiColumn` · `BuildAiPanelShell()` in `MainWindow.xaml.cs`

Toggle: header AI button · column width 0 ↔ 300px · 320ms panel motion

## Bilingual

- AR: «المساعد الذكي» · EN: «AI Assistant»
- Responses via `LocaleManager.T` when rule-based (no LLM in baseline)

## Anti-patterns

- Purple gradient chat UI
- Generic ChatGPT clone layout
- AI panel blocking primary workflow
