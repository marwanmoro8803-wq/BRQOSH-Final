# Page Override — List / CRUD pages (Empty State)

**Pattern:** ToolbarCard + ContentCard DataGrid + EmptyState overlay

## Behavior (CEEF-039)

When `PublishGridCount(grid, 0)` runs:

1. `EmptyStateHelper.Sync` wraps grid host
2. Shows `EmptyStatePanel` with monogram + title + subtitle
3. Hides empty DataGrid

## Catalog

See `EmptyStateCatalog.cs` for grid-specific Arabic copy.

## Integration

All list pages call `UiTestAutomation.PublishGridCount` after `ItemsSource` assignment.
Applied via `scripts/apply-empty-state-grid-count.py`.
