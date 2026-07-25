# Standards × Spec Review — Supreme (v15)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-review  
> **Synthesis:** Anthropic Claude Code `review` · Codex findings-first · parallel subagents  
> **Corpus:** `Anthropic/Claude Code/` review skill patterns

> **Two axes that must not hide each other**

---

## 1. Why Two Axes?

| Situation | Standards | Spec |
|-----------|-----------|------|
| Clean code · wrong feature | ✅ | ❌ |
| Correct feature · breaks conventions | ❌ | ✅ |
| Both wrong | ❌ | ❌ |
| Both right | ✅ | ✅ |

**Never merge axes** in one vague "looks good."

---

## 2. Standards Axis — Repository Law

### BRQOSH authoritative sources

| Source | Governs |
|--------|---------|
| `.cursor/rules/brqosh-open-items.mdc` | closure · O-items |
| `.cursor/rules/brqosh-schema-governance.mdc` | PostgreSQL |
| `.cursor/rules/brqosh-ui-design-system.mdc` | Desktop UI |
| `.cursor/rules/brqosh-supreme-cortex.mdc` | agent behavior (supersedes legacy supreme-intelligence-hub redirect) |
| `docs/DOMAIN_MAP.md` | module boundaries |
| `docs/SCHEMA_GOVERNANCE.md` | schema |
| `docs/OPEN_ITEMS.md` | external ⏭ |
| `brqosh-skills-hub/STATUS.yaml` | test counts |
| Adjacent code in same module | local convention |

### Inspection protocol

```
git diff <base>...HEAD
For each hunk:
  → violates documented standard?
  → cite: rule name + file:line
  → classify: hard violation | judgment call
```

### Common BRQOSH standard violations

| Violation | Severity |
|-----------|----------|
| Domain imports Infrastructure | P0 architecture |
| Query without CompanyId | P0 isolation |
| Raw SQL without SchemaSqlGuard | P0 schema |
| UI action without PermissionService | P1 RBAC |
| `Resources["Key"]` on UserControl | P2 UI |
| ✅ claim in comment without test | P1 process |
| Marketing UI patterns in ERP | P2 product |
| Partial O-01 closure language | P0 honesty |

---

## 3. Spec Axis — Intent Law

### Spec source priority

```
1. User message (explicit latest)
2. `docs/strategic/` (strategic docs) · active `.cursor/plans/`
3. design-system/pages/<screen>.md
4. CEEF report for wave
5. OPEN_ITEMS scope boundaries
```

### Inspection protocol

```
Read spec excerpt → read diff
(a) required capability missing?
(b) scope creep not requested?
(c) implemented but wrong semantics?
(d) wrong module per DOMAIN_MAP?
```

**Every spec finding quotes a spec line or user quote.**

---

## 4. Parallel Execution (readonly)

```
Sub-agent A: Standards only — diff + rules list
Sub-agent B: Spec only — plan + diff
Parent: merge without cross-contamination
```

**Forbidden:** one agent doing both — bias hides one axis.

---

## 5. Report Format (Codex findings-first adapted)

```markdown
## Security (if run)
- [P0] ...

## Spec
- [P0] Required X — missing — "quote from spec"

## Standards
- [P1] `path:line` — rule — description

## Bugs (from code-review if run)
- [P0] ...

## Summary
Spec: N · Standards: M · worst issue: ...
Residual risk: ...
```

**Display order:** Security P0 → Spec P0 → Financial P0 → Bugs → Standards → summary

---

## 6. Financial Path Overlay

When diff touches `Posting*` · `Journal*` · `Invoice*` · `VAT*`:

| Extra spec check | Extra standard |
|------------------|----------------|
| Golden scenario exists/updated | PostingPolicy enforced |
| Audit on post | decimal not double |
| reversal path if cancel | CompanyId on queries |

Merge **code-review angle I** into Spec axis for money.

---

## 7. Effort Scaling

| Diff size | Standards×Spec depth |
|-----------|------------------------|
| <50 lines · non-financial | quick pass |
| Single feature | full both axes |
| CEEF wave | full + security + xhigh |
| Schema migration | schema governance mandatory |

---

## 8. Integration with Full Review Stack

```
diff
 ├─ security-review (exploit >80%)
 ├─ standards ║ spec (this module · parallel)
 ├─ code-review xhigh 10 angles
 ├─ fix
 ├─ simplify 4 angles
 └─ closure 4 levels if wave
```

---

## 9. Anti-Patterns

| Avoid | Instead |
|-------|---------|
| "LGTM" without axes | both tables |
| Style nit as P0 | proportion severity |
| Spec drift ignored | quote missing requirement |
| Standards from memory | cite rule file |
| Mix axes in one bullet | separate sections |

**Next:** [code-review.md](code-review.md) · [simplify.md](simplify.md) · [security.md](security.md)
