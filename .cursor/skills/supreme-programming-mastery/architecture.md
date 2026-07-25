# Architecture

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery | see per-file load order

## Layers (default pattern)

```
Presentation → Application → Domain → Infrastructure
```

Dependencies point **inward**. Domain has no framework imports.

## Design decisions

| Need | Pattern |
|------|---------|
| Swap implementations | Interface + DI |
| Cross-cutting | Middleware, decorators, aspects |
| Events | Domain events + handlers (idempotent) |
| Read-heavy | CQRS consideration |
| Scale-out | Stateless services; externalize session |

## Before new architecture

- Does existing pattern in repo suffice?
- Smallest change that fits?
- Rollback plan?

## Documentation

5-line flow diagram before large feature — not 50-page ADR unless asked.

## Microservices vs monolith

Default monolith/module boundaries until proven need to split.

## BRQOSH

DOMAIN_MAP in `brqosh-engineering/engineering.md` — follow layer rules strictly.
