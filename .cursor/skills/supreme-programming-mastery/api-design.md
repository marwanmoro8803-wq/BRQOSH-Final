# API Design

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery | see per-file load order

## REST

- Nouns for resources; HTTP verbs semantic
- Version in path or header — be consistent
- Pagination: cursor preferred for large sets
- Idempotency keys for POST that create money/state

## Errors

```json
{ "error": "code", "message": "human", "details": [] }
```

- 4xx client, 5xx server — don't leak stack in prod

## GraphQL / gRPC

- When appropriate: typed contracts, schema evolution rules
- N+1: DataLoader / batching

## Internal APIs

- Interface in Application layer; DTOs at boundary
- Don't expose EF entities at API surface

## Documentation

OpenAPI/Swagger when project uses it — match reality.
