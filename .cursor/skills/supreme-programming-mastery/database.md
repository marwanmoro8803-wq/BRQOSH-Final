# Database & Persistence

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery | see per-file load order

## Modeling

- Normalize until proven denormalization need
- Audit columns: who/when on financial mutations
- Soft delete vs hard delete — domain decision

## Migrations

- Forward + rollback plan
- Test on PostgreSQL if prod is PG (BRQOSH: SchemaSqlGuard)
- Never destructive migration without backup path

## ORM

- Explicit loading strategy
- AsNoTracking for read-only reports
- Concurrency tokens on contested rows

## Caching

- Cache invalidation strategy documented
- Don't cache auth-sensitive without TTL + keys
