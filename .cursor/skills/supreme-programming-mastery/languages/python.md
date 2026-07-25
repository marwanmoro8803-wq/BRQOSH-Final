# Python

## Style

- PEP 8; type hints on public APIs
- venv/poetry per project — check before install
- Explicit `if x is None`

## Pitfalls

| Issue | Fix |
|-------|-----|
| Mutable default args | None + init |
| Import side effects | lazy imports |
| GIL + CPU bound | multiprocessing / rust extension |
| Silent except | log + re-raise |

## Verify

`pytest` or `python -m unittest`
