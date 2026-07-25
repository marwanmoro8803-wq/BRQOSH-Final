"""Apply ToolbarCard / ContentCard / SummaryCard styles to XAML views."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1] / "src" / "Brqosh.Desktop" / "Views"

REPLACEMENTS = [
    (
        re.compile(
            r'<Border Grid\.Row="0" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="16,12" Margin="0,0,0,16"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="0" Style="{StaticResource ToolbarCard}">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="0" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="16,12" Margin="0,0,0,12"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="0" Style="{StaticResource ToolbarCard}" Margin="0,0,0,12">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="1" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="16,12" Margin="0,0,0,12"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="1" Style="{StaticResource ToolbarCard}" Margin="0,0,0,12">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="1" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="4"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="1" Style="{StaticResource ContentCard}">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="1" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="4" Margin="0,0,0,12"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1"([^>]*)>',
            re.MULTILINE,
        ),
        r'<Border Grid.Row="1" Style="{StaticResource ContentCard}" Margin="0,0,0,12"\1>',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="1" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="8"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="1" Style="{StaticResource ContentCard}" Padding="8">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="1" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="20"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="1" Style="{StaticResource ContentCard}" Padding="20">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="2" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="4"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="2" Style="{StaticResource ContentCard}">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="2" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="4"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1"([^>]*)>',
            re.MULTILINE,
        ),
        r'<Border Grid.Row="2" Style="{StaticResource ContentCard}"\1>',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="3" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="4"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="3" Style="{StaticResource ContentCard}">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="2" Background="\{StaticResource SidebarBrush\}" CornerRadius="10" Padding="16,10" Margin="0,12,0,0">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="2" Style="{StaticResource SummaryCard}">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="4" Background="\{StaticResource SurfaceBrush\}" CornerRadius="10" Padding="16,10" Margin="0,12,0,0"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="4" Style="{StaticResource TotalsCard}">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="3" Background="\{StaticResource SurfaceBrush\}" CornerRadius="10" Padding="16,10" Margin="0,12,0,0"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="3" Style="{StaticResource TotalsCard}">',
    ),
    (
        re.compile(
            r'<Border Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="4"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Style="{StaticResource ContentCard}">',
    ),
    (
        re.compile(
            r'<Border Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="24"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Style="{StaticResource ContentCard}" Padding="24">',
    ),
    (
        re.compile(
            r'<Border Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="32" MaxWidth="640"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Style="{StaticResource ContentCard}" Padding="32" MaxWidth="640">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="3" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="16,12" Margin="0,12,0,0"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="3" Style="{StaticResource ToolbarCard}" Margin="0,12,0,0">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="2" Background="\{StaticResource SurfaceBrush\}" CornerRadius="12" Padding="16,12" Margin="0,12,0,0"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="2" Style="{StaticResource ToolbarCard}" Margin="0,12,0,0">',
    ),
    (
        re.compile(
            r'<Border Grid\.Row="1" Background="\{StaticResource SurfaceBrush\}" CornerRadius="8" Padding="4"\s+'
            r'BorderBrush="\{StaticResource BorderBrush\}" BorderThickness="1">',
            re.MULTILINE,
        ),
        '<Border Grid.Row="1" Style="{StaticResource ContentCard}">',
    ),
]

def main() -> None:
    changed = 0
    for path in sorted(ROOT.rglob("*.xaml")):
        text = path.read_text(encoding="utf-8")
        original = text
        for pattern, repl in REPLACEMENTS:
            text = pattern.sub(repl, text)
        if text != original:
            path.write_text(text, encoding="utf-8", newline="\n")
            changed += 1
            print(f"updated: {path.relative_to(ROOT)}")

    remaining = []
    for path in sorted(ROOT.rglob("*.xaml")):
        if 'Background="{StaticResource SurfaceBrush}" CornerRadius' in path.read_text(encoding="utf-8"):
            remaining.append(str(path.relative_to(ROOT)))

    print(f"\n{changed} files updated.")
    if remaining:
        print("Still manual:")
        for r in remaining:
            print(f"  - {r}")

if __name__ == "__main__":
    main()
