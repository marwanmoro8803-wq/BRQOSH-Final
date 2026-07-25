# Impact Before Change — Pre-Flight & Memory Writeback (v1)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-agent-protocol  
> **Rule:** [brqosh-deep-memory.mdc](../../rules/brqosh-deep-memory.mdc)  
> **Memory index:** [docs/PROJECT-MEMORY.yaml](../../../docs/PROJECT-MEMORY.yaml)

---

## When to read this file

| Trigger | Action |
|---------|--------|
| Any `Write` / `StrReplace` / delete in `src/` · `tests/` · schema | Full checklist §1–§6 |
| New or edit `.cursor/rules/*.mdc` | Checklist + MEM `type: rule` after verify |
| User asks "where/how" architecture | Checklist in analyze mode — no edits |
| Wave close | Checklist already done → CEEF + MEM |

**Skip checklist:** Inquiry-only · read-only review · grep/explain with no mutation.

---

## §1 — CLASSIFY the change

```
fix        → repro test · root cause · minimal diff
feature    → DOMAIN_MAP home · DI registration · tests
removal    → grep ALL references · migration of callers
rule       → which layer (.mdc / skill / docs) · conflict with cortex?
decision   → ADR or MEM detail if 3+ modules
```

**Risk tier:**

| Tier | Signals | Mode |
|------|---------|------|
| L0 BLITZ | 1 file · obvious test red | checklist lite (§2+§6 only) |
| L1 STANDARD | 2–4 files · one module | full §1–§6 |
| L2 DEEP | posting · schema · RBAC · 5+ files | full + Plan · 2 councils |
| L3 WAVE | CEEF scope | zero-gap-matrix · gates |

---

## §2 — SCOPE

Answer in one line before editing:

1. Which **DOMAIN_MAP** module? (`docs/DOMAIN_MAP.md`)
2. Which projects: Domain · Application · Infrastructure · Desktop · Api · Tests?
3. Is this inside current user scope or drive-by?

**Stop** if module boundary crossed without user scope — ask or document in MEM.

---

## §3 — LINKS

Parallel grep (mandatory for L1+):

```powershell
# Replace Symbol with type/method/service name
rg -n "Symbol" src/ tests/ --glob "*.cs"
rg -n "Symbol" src/Brqosh.Desktop --glob "*.xaml*"
```

Record:

| Direction | Question |
|-----------|----------|
| Callers | Who breaks if signature/behavior changes? |
| Callees | What assumptions does this code depend on? |
| DI | `AppServices` · `Program.cs` · Desktop registration |
| Events | `IDomainEventPublisher` · workflow hooks |
| UI | ViewModel · navigation · `LocaleManager.T` keys |

---

## §4 — SHARED STATE

| Shared | Check |
|--------|-------|
| Schema | SQLite **and** PG patches · SchemaSqlGuard |
| BranchId | session scope · queries |
| RBAC | `SecurityContext` · visibility |
| Posting | `PostingOrchestrator` · Golden · audit trail |
| i18n | nav keys · RTL layout |
| Config | `appsettings` · feature flags |

**Schema touch:** read `brqosh-schema-governance.mdc` — PG gate mandatory before ✅.

---

## §5 — IMPACT & CONFLICTS

Score each **High / Medium / Low / None**:

| Axis | High if… |
|------|----------|
| Accounting | touches journal · VAT · ZATCA · period close |
| Security | auth · secrets · SQL · RBAC bypass risk |
| Workflow | PR/PO/JE approval chain |
| UX | new screen · grid behavior · empty state |
| Performance | full-table load · sync over async |
| External | O-01..O-05 · claims live closure |

**Conflicts to grep:**

- `docs/OPEN_ITEMS.md` — deferred work
- `docs/PROJECT-MEMORY.yaml` — prior decision on same area
- `reports/CEEF-*` — closed wave assumptions
- In-flight git diff — dirty worktree integration

**Removal rule:** list every caller; migrate or test-prove zero references before delete.

---

## §6 — VERIFY PLAN

Define **before** edit:

```powershell
# Narrow fix example
dotnet build Brqosh.sln -c Release --nologo
dotnet test Brqosh.sln -c Release --nologo --filter "FullyQualifiedName~Golden"

# Schema wave
.\scripts\ops\schema-gate-pg.ps1

# Major wave
.\scripts\ops\run-external-gates.ps1
```

| Change type | Minimum verify |
|-------------|----------------|
| Domain posting | Golden + affected Domain tests |
| Desktop UI | Desktop.Tests if static/VM pattern |
| API | Api.Tests |
| Rule/skill only | agent-skill-regression if skills touched |
| Wave | build + full test count from STATUS.yaml |

**No "done" without running planned verify** (or explicit ⏭ with evidence path).

---

## §7 — EXECUTE (after checklist)

```
Read target file → StrReplace/Write → re-read file
dotnet test/build per §6
```

Smallest complete diff — no drive-by refactor.

---

## §8 — MEMORY WRITEBACK (after verify)

1. Open `docs/PROJECT-MEMORY.yaml`
2. Increment `next_id` / use next `MEM-NNN`
3. Append entry (bilingual `problem`/`how` OK — keep fields concise)
4. If 3+ modules or fork: add `docs/memory/MEM-NNN-slug.md` from `_TEMPLATE.md`
5. Wave: CEEF in `reports/` + link in `links.ceef`
6. New rule: link `links.rule` to `.mdc` path
7. Optional: one-line lesson in `leak-synthesis.md` on CEEF wave only

**Report to user:** MEM id · verify result · ⏭ if any.

---

## §9 — Plan Mode contract (L2+)

When entering Plan, output:

```markdown
## Impact summary
- Scope: …
- Links: … (grep evidence)
- Shared: …
- Risks: …
- Conflicts: …
- Verify: …
- MEM/CEEF: …
```

**No file mutations** until user approves plan (unless user said implement/fix now).

---

## Anti-patterns

| Avoid | Instead |
|-------|---------|
| Edit then grep | grep then edit |
| MEM before test | verify then MEM |
| Assume DOMAIN_MAP | Read map |
| SQLite-only schema ✅ | PG gate |
| Chat history as spec | PROJECT-MEMORY + CEEF |

**Next:** [protocol.md](protocol.md) · [constitution.md](constitution.md) · [context-continuity.md](../fable-omni-core/context-continuity.md)
