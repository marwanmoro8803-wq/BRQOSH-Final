# Debug — evidence first

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery | see per-file load order

## Before fix

1. Exact error / symptom
2. `git diff` if recent change
3. grep stack trace symbol
4. Repro test or minimal repro steps

## Tree

```
FAIL -> reproducible? -> recent diff? -> layer: config|dep|logic|data|env
```

## Fix vs workaround

| Fix | Temp workaround |
|-----|-----------------|
| Root clear, test exists | Production down, external blocker |
| Small diff | Document + ticket |

## After fix

Run **affected** tests only first, then broader suite if risky.

## Flaky

Run 3×; fix race or isolate shared state.

## Tools

Logs → grep → read entrypoint → read failing test → read implementation.

Full BRQOSH matrix: `brqosh-debug/reference.md`
