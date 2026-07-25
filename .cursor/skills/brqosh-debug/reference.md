# Rapid Diagnosis — Supreme Reference (v15)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-debug  
> **Synthesis:** Gemini repro-before-fix · Codex review stance · Cursor Read-first · Anthropic evidence  
> **Corpus:** [leak-corpus-index.yaml](../brqosh-skills-hub/leak-corpus-index.yaml)

> symptoms → hypotheses → evidence → fix — before the first edit  
> **With:** [routing.md](../brqosh-agent-protocol/routing.md) · C01 CODE + C11 QA

---

## 1. Evidence-First (non-negotiable)

```
No fix before:
  ① repro or stack trace or failing test name
  ② likely file/line (grep-backed)
  ③ hypothesis A (most likely) + B (alternative)
```

**Gemini mandate:** for behavioral bugs, **reproduce with test or script before applying fix** when feasible.

**Evidence tools (order):**
1. Error message / symptom verbatim (user or CI)
2. grep / logs / test output
3. git diff — what changed recently?
4. Minimal repro test

---

## 2. Hypothesis Tree (<3 steps)

```
SYMPTOM
  ├─ reproducible? ─ no → collect logs/steps · ask one precise question
  │                  yes ↓
  ├─ recent change? ─ yes → git diff first (~90% root cause)
  │                    no ↓
  └─ nearest layer: config · dep · logic · data · env · thread
       → hypothesis A · B
       → quickest falsify test for A
```

**90-second rule:** if layer unclear → read **one** entrypoint only (handler · test · main).

---

## 3. Symptom Matrix (expanded)

| Symptom | First hypothesis | Quick verify |
|---------|------------------|--------------|
| build fail | syntax · missing ref · TFM mismatch | compiler line 1 |
| test fail | assertion · fixture data · order | test name + line |
| null ref | uninitialized · lifecycle · async | stack frame 0 |
| UI not showing | binding · visibility · dispatcher | XAML + code-behind |
| UI wrong RTL | FlowDirection · mirrored layout | LocaleManager |
| slow | N+1 · loop · sync IO | log timing / SQL |
| flaky | race · time · shared static | run 3–5× |
| memory climb | event leak · cache unbounded | profiler snapshot |
| auth fail | SecurityContext · token expiry | grep BindSecurity |
| ERP imbalance | posting · rounding · branch filter | Golden test |
| schema error PG | missing column · guard | SchemaSqlGuard |
| 401/403 API | RBAC · branch scope | test with role |
| migration fail | SQLite vs PG divergence | guard + diff SQL |

---

## 4. Layer Isolation Protocol

```
Presentation → Application → Domain → Infrastructure → External
```

| Layer signal | Check |
|--------------|-------|
| XAML only | bindings · resources · styles |
| ViewModel | INotifyPropertyChanged · commands |
| Service | business rule · transaction |
| Repository | query · filter · BranchId |
| DB | schema · migration · constraint |

**Stop descending** once evidence pins layer — don't read entire solution.

---

## 5. Reproduction Discipline (Gemini)

```
BEFORE fix:
  1. Write or locate failing test
  2. Confirm RED
  3. Apply minimal fix
  4. Confirm GREEN
  5. Add regression if none existed
```

If repro impossible (hardware · OTP · production-only):
→ document ⏭ · collect max local evidence · propose staged verify plan.

---

## 6. Fix vs Workaround

| Choose FIX when | TEMPORARY workaround when |
|-----------------|---------------------------|
| clear root · low risk | production down · fix needs days |
| test covers | external blocker (vendor/OTP) |
| small diff | — document ⏭ + ticket |

No permanent workaround without code comment + follow-up.

---

## 7. Dirty Worktree During Debug (Codex)

- unrelated user edits in **your** files → read · integrate
- unrelated elsewhere → ignore
- never `git checkout --` / `reset --hard` to "clean" for debug

---

## 8. Rollback Safety

```
Before risky change:
  ① one-step revert possible?
  ② test protection exists?
  ③ user forbade touching file X?

After fix:
  → build + affected tests
  → if fail: revert · re-diagnose (don't stack fixes blindly)
```

**BRQOSH:** no PostgreSQL column without SchemaSqlGuard review.

---

## 9. Review Stance When Debugging via Review (Codex)

If user says "review" while investigating:

```
Findings FIRST — severity order — file/line grounded
Then open questions
Then change summary (secondary)
If no issues: say so + note residual risk / test gaps
```

---

## 10. Bisection (when diff unknown)

```
1. git bisect or manual midpoint on timeline
2. run fastest failing test each step
3. stop at introducing commit
4. minimal fix on that commit's intent
```

---

## 11. Logging Archaeology

| Source | When |
|--------|------|
| test stdout | first |
| application logs | repro captured |
| CI artifact | flaky / env-specific |
| SQL profiler | ERP performance |

grep error codes · exception types · correlation ids.

---

## 12. BRQOSH-Specific Debug Paths

| Area | First move |
|------|------------|
| Golden posting | read test name · expected debit/credit |
| BranchId | grep BranchId filter in repository |
| ZATCA | distinguish stub vs live OTP (O-01 external) |
| Desktop UI | static Desktop.Tests · BrqoshTheme keys |
| API | bind SecurityContext · check 403 |

```powershell
$env:BRQOSH_SKIP_STRESS='1'; $env:BRQOSH_TEST='1'
dotnet test Brqosh.sln -c Release --filter "FullyQualifiedName~<TestName>"
```

---

## 13. Report Protocol

```
1. Root cause (one sentence)
2. Evidence (log/test/diff line)
3. Fix (what changed — file level)
4. Verify (command + PASS)
5. ⏭ if still external
```

---

## 14. Anti-Slowness

| Avoid | Instead |
|-------|---------|
| read 20 files | grep → Read excerpt |
| refactor during debug | minimal fix |
| assume "should work" | read actual code |
| multi-edit without test | one step + verify |
| long explain before fix | result first |
| repeat failed command 5× | change approach (Grok) |

---

## 15. Council Integration

| Phase | Council |
|-------|---------|
| triage | C11 QA |
| root cause | C01 CODE |
| security suspect | C07 SECURITY |
| accounting | C06 ACCOUNTING |
| BRQOSH close | C08 OPS |

**Next:** [routing.md](../brqosh-agent-protocol/routing.md) · [agent-workflow.md](../supreme-programming-mastery/agent-workflow.md)
