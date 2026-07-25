# BRQOSH Memory Detail — `docs/memory/`

**Index:** [../PROJECT-MEMORY.yaml](../PROJECT-MEMORY.yaml) · [../../.cursor/LEARNING-INDEX.yaml](../../.cursor/LEARNING-INDEX.yaml) · [../../.cursor/skills/brqosh-skills-hub/skill-lessons.yaml](../../.cursor/skills/brqosh-skills-hub/skill-lessons.yaml)  
**Rule:** [.cursor/rules/brqosh-deep-memory.mdc](../../.cursor/rules/brqosh-deep-memory.mdc) · [brqosh-continuous-learning.mdc](../../.cursor/rules/brqosh-continuous-learning.mdc)

## When to create a file here

| Situation | Action |
|-----------|--------|
| Small fix · one module | YAML row in `PROJECT-MEMORY.yaml` only |
| 3+ modules · architectural fork | `MEM-NNN-short-slug.md` + YAML `links.detail` |
| Full wave | CEEF in `تقارير/` + YAML row with `links.ceef` |

## Naming

```
MEM-{same-id-as-yaml}-{kebab-slug}.md
Example: MEM-002-ceef120-closure.md
```

## Template

Copy [_TEMPLATE.md](_TEMPLATE.md) when creating a detail file.

## Search

```powershell
rg -n "keyword|MEM-" docs/PROJECT-MEMORY.yaml docs/memory/
.\scripts\ops\validate-project-memory.ps1
```
