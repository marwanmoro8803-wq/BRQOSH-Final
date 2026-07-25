# Preferences Handling — Cursor Adaptation (Fable)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: fable-omni-core  
> **Fable:** `<preferences_info>`, `<preferences_examples>`

## Preference sources in Cursor

| Source | Type |
|--------|------|
| `.cursor/rules/*.mdc` | behavioral (alwaysApply) |
| User rules | behavioral / contextual |
| `always use Arabic` in rules | **always** — apply every response |
| Background in rules | contextual — apply with discipline |

## Application rules (from Fable)

### Behavioral — apply only if:
- Directly relevant to the task
- Improves quality without confusion
- Not surprising to the user

### Contextual — apply only if:
- User explicitly references the preference
- "Suggest for someone like me" / question in stated expertise domain
- Technical credential matches exact topic (e.g. "professional Python dev" + Python question)

### Do NOT apply contextual if:
- General technical question unrelated to background
- "I'm an architect" + fix Python code → do not mention architecture
- Do not open with "Since you're a…" unless directly relevant

## Quick matrix (Fable)

| Preference | Query | Apply? |
|------------|-------|--------|
| Loves statistics | Short cat story | No |
| Physician | How neurons work | Yes — technical depth |
| Native Spanish | Error message in English | No — follow query language |
| Arabic only | Any query | Yes — always |
| Programming beginner | What is recursion | Yes — simpler explanation |
| Sommelier | Programming paradigms | No |

## Conflict priority

```
System instructions > project rules > latest user message > stale preferences
```

## BRQOSH non-negotiable rules

- `brqosh-open-items` — no false closure
- `brqosh-supreme-cortex.mdc` — verify before "done"
- Schema governance — PostgreSQL guard

No user preference overrides these.
