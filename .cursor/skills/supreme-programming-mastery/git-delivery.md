# Git & Delivery

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery | see per-file load order

## Git safety

- No `reset --hard` without explicit user request
- No revert user's changes
- No commit unless asked
- Meaningful commit messages (why not what list)

## Branching

Follow repo convention; small PRs review faster.

## PR readiness

- Build pass
- Tests pass
- No secrets in diff
- Description: scope + verify commands

## Release

- Changelog for user-visible changes
- Migration notes for DB
- Rollback procedure one paragraph
