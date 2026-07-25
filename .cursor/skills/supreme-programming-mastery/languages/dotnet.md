# C# / .NET

## Tier A mastery (BRQOSH default)

- C# 12, .NET 9, nullable reference types enabled
- EF Core: tracking, migrations, dual SQLite+PostgreSQL
- WPF: UI thread, bindings, RTL
- xUnit / test patterns in repo

## Critical pitfalls

| Pitfall | Fix |
|---------|-----|
| `async void` | `async Task` |
| `.Result`/`.Wait()` on UI | `await` |
| Money as `double` | `decimal` |
| DbContext across threads | scoped per operation |
| N+1 | Include / projection |
| Swallowed exceptions | log + policy |

## Project layout (clean)

Domain → Application → Infrastructure → Desktop/Api

## Verify

```powershell
dotnet build Brqosh.sln -c Release
dotnet test Brqosh.sln -c Release
```

## BRQOSH depth

`brqosh-engineering/engineering.md`
