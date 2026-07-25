# Testing

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery | see per-file load order

## Pyramid

```
        E2E (few)
      Integration (some)
    Unit (many)
```

## Every bug fix

1. Repro test (fails before fix)
2. Fix minimal
3. Test passes
4. Related regression suite

## Test quality

| Good | Bad |
|------|-----|
| Asserts behavior | Asserts implementation detail |
| Deterministic | Time/random dependent |
| Fast unit | Requires network unless integration |
| Named by scenario | test1, test2 |

## Coverage philosophy

Critical paths > line % worship. Money, auth, migrations — must have tests.

## Commands (detect from repo)

| Stack | Typical |
|-------|---------|
| .NET | `dotnet test` |
| Node | `npm test` / `pnpm test` |
| Python | `pytest` |
| Rust | `cargo test` |
| Go | `go test ./...` |

## BRQOSH

`BRQOSH_TEST=1` `BRQOSH_SKIP_STRESS=1` — see STATUS.yaml in brqosh-skills-hub.
