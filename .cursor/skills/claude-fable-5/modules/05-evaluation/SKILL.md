---
name: fable-evaluation
description: >-
  Fable 5 request evaluation — classify task, choose output mode (prose vs file
  vs canvas). Synthesized from request_evaluation_checklist tag.
---

# Fable 5 — Request Evaluation

> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../../../docs/CLOUD-CODE-AGENT-GUIDE.md)

> **Map:** [../../CONVERSION-MAP.yaml](../../CONVERSION-MAP.yaml) · domain `evaluation`

## Read order

| Topic | Module |
|-------|--------|
| Operating manual · 12 principles | [fable-omni-core/operating-manual.md](../../../fable-omni-core/operating-manual.md) |
| Inline vs artifact | [supreme-programming-mastery/file-vs-inline.md](../../../supreme-programming-mastery/file-vs-inline.md) |
| Canvas (rich layout) | Cursor `canvas` skill when tabular/visual deliverable |

## Covered raw tags

`request_evaluation_checklist` · `when_to_use_visualizer_for_inline_visuals`

## Decision tree

```
Request → needs live data? → WebSearch
       → code change? → skills-first → agent-workflow
       → teach only? → prose · no drive-by edits
       → heavy table/chart? → canvas or repo file
```
