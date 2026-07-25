# DevOps & CI

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery | see per-file load order

## Pipeline minimum

```
lint → build → test → (optional integration) → artifact
```

## Infrastructure as code

Versioned; reviewed like application code.

## Observability

- Structured logs
- Metrics on SLOs (latency, errors)
- Alerts actionable — no alert fatigue

## BRQOSH

`scripts/ops/run-external-gates.ps1` on major waves

## Containers

Multi-stage builds; non-root user; pin base image digests when security-critical.
