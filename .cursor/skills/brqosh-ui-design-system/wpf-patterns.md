# WPF Patterns (v18 Strict)

> **Binding:** [enforcement.md](enforcement.md) · **With:** `brqosh-ui/reference.md` · `BrqoshTheme.xaml`

---

## Resource resolution (MUST)

```xml
<!-- REQUIRED -->
<Button Background="{DynamicResource PrimaryBrush}" />

<!-- FORBIDDEN on UserControl — breaks theme -->
<Button Background="{Binding Resources[PrimaryBrush]}" />
```

Use `UiResources` where codebase convention requires.

---

## Page host (standard)

```xml
<UserControl x:Class="...">
  <Grid Margin="{StaticResource PageMargin}">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
    </Grid.RowDefinitions>
    <local:ToolbarCard Grid.Row="0"/>
    <local:ContentCard Grid.Row="1"/>
  </Grid>
</UserControl>
```

---

## DataGrid (financial — locked defaults)

| Property | Value |
|----------|-------|
| `RowHeight` | **44** |
| `HeadersVisibility` | Column |
| `GridLinesVisibility` | Horizontal |
| `CanUserAddRows` | false (default) |
| Amount columns | right-aligned · culture format |
| Status | `DataTemplate` pill |

```xml
<DataGridTextColumn Binding="{Binding Amount, StringFormat=N2}"
                    ElementStyle="{StaticResource RightAlignedTabularText}"/>
```

---

## RTL (mandatory)

```xml
FlowDirection="{Binding Source={x:Static local:LocaleManager.Current}, Path=FlowDirection}"
```

- MUST test star column order in AR
- MUST mirror directional icons (chevrons) in RTL

---

## Theme keys (use only these patterns)

| Key | Use |
|-----|-----|
| `PrimaryBrush` | CTA · active nav |
| `SidebarBackgroundBrush` | shell |
| `PageBackgroundBrush` | canvas |
| `SurfaceBrush` | cards |
| `TextPrimaryBrush` | body |
| `CardShadow` | ContentCard |
| `PrimaryButton` / `GhostButton` | actions |
| `ContentCard` / `ToolbarCard` | layout |

New colors → add to `BrqoshTheme.xaml` — **never** inline hex.

---

## KPI (code-first)

```csharp
var card = KpiMetricCardBuilder.Create(
    label: LocaleManager.T(ar: "Total sales", en: "Total sales"), // first arg: Arabic product copy per M04
    value: formatted,
    delta: delta,
    monogram: "SA");
```

---

## Navigation icons

```csharp
var icon = NavIconFactory.Create(NavIconKind.Sales);
// MUST NOT use emoji
```

---

## Empty state

```xml
<local:EmptyState
    Title="{Binding EmptyTitle}"
    Description="{Binding EmptyDescription}"
    ActionCommand="{Binding CreateCommand}"
    ActionText="{Binding CreateLabel}"/>
```

**MUST** show when grid/list empty.

---

## Loading

- DataGrid: disable grid + overlay `ProgressBar` in ContentCard
- KPI: skeleton — **MUST** respect reduced motion

---

## Dialogs

```csharp
var dlg = new InvoiceDialog(viewModel) { Owner = Application.Current.MainWindow };
if (dlg.ShowDialog() == true) Refresh();
```

Width 480–720px · default button = primary · Escape = cancel.

---

## Security

```csharp
BindSecurityContext(button, "Sales.Invoice.Post");
```

**MUST** bind RBAC before showing destructive/post actions.

---

## Focus and input (mandatory)

- `FocusVisualStyle` on all interactives
- `Cursor="Hand"` on Button · Hyperlink · CheckBox
- Logical tab order · RTL mirror

---

## Verify gate

```
□ ThemeManager: dark + light
□ LocaleManager: AR + EN
□ Desktop.Tests if page covered
□ ui-ux-pro-max-audit.ps1
```

---

## File map

| Concern | Path |
|---------|------|
| Theme | `src/Brqosh.Desktop/Themes/BrqoshTheme.xaml` |
| Nav | `Navigation/NavIconFactory.cs` |
| KPI | `Ui/KpiMetricCardBuilder.cs` |
| Locale | `Localization/LocaleManager.cs` |
| Empty | `Ui/EmptyState.xaml` |
