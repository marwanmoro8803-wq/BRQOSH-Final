# SQL & Data

## Principles

- Parameterized queries always
- Migrations versioned and reversible when possible
- Index for query patterns; avoid SELECT *
- Transactions for multi-step invariants

## Dual DB (BRQOSH)

SQLite dev + PostgreSQL prod — SchemaSqlGuard on new columns

## Pitfalls

| Issue | Fix |
|-------|-----|
| N+1 in ORM | joins/projections |
| Lost update | optimistic concurrency |
| Migration without backup plan | rollback script |

## Analytics

Separate read models / reporting DB for heavy queries when needed.
