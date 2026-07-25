# Smart Mind v7 — Polyglot Pitfalls Encyclopedia

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-engineering | see per-file load order

> From Claude Code xhigh angle D — **expanded for every common language** · what humans miss

---

## 1. C / C++

| Pitfall | Example | Fix |
|---------|---------|-----|
| UB | signed overflow | fixed width · checks |
| use-after-free | dangling ptr | RAII · smart pointers |
| buffer overflow | strcpy | bounds · snprintf |

---

## 2. Rust

| Pitfall | Fix |
|---------|-----|
| borrow after move | ownership flow |
| `unwrap()` in prod | `?` · proper error |
| `Send`/`Sync` violations | Arc · channels |

---

## 3. Go

| Pitfall | Fix |
|---------|-----|
| nil map write | make() first |
| goroutine leak | context cancel |
| range var capture | copy loop var |
| error ignored | explicit handle |

---

## 4. Java / Kotlin

| Pitfall | Fix |
|---------|-----|
| null NPE | Optional · Kotlin null safety |
| equals without hashCode | pair both |
| thread pool exhaustion | bounded · timeout |

---

## 5. C# / .NET (BRQOSH Tier A)

| Pitfall | Fix |
|---------|-----|
| `async void` | async Task |
| `.Result`/`.Wait()` UI | await |
| `double` for money | **decimal** |
| DbContext threading | scope per operation |
| N+1 queries | Include/projection |
| nullable ref ignored | enable · fix warnings |
| `DateTime` TZ | UTC store · local display |
| catch swallow | log + rethrow policy |
| forgotten `CompanyId` | always filter |
| EF tracking stale | AsNoTracking reads |

---

## 6. TypeScript / JavaScript

| Pitfall | Fix |
|---------|-----|
| `==` coercion | `===` |
| falsy zero | `??` · explicit checks |
| closure in loop | let/block scope |
| unhandled promise | await/top-level catch |
| any creep | strict types |

---

## 7. Python

| Pitfall | Fix |
|---------|-----|
| mutable default args | None sentinel |
| late binding closure | default arg trick |
| GIL CPU bound | multiprocessing |
| import side effects | `if __name__` |

---

## 8. SQL

| Pitfall | Fix |
|---------|-----|
| injection | parameterized |
| NULL logic | IS NULL explicit |
| implicit conversion | cast explicit |
| missing index | explain plans |
| lock escalation | transaction scope |
| SQLite≠PG types | SchemaSqlGuard |

---

## 9. XAML / WPF

| Pitfall | Fix |
|---------|-----|
| binding path typo | design-time · tests |
| dispatcher cross-thread | Invoke |
| memory leak events | weak events · unsubscribe |
| RTL column star | DataGridRtlHelper |
| converter exception | fallback values |

---

## 10. PowerShell / Bash

| Pitfall | Fix |
|---------|-----|
| word splitting | quotes |
| `$null` vs empty | explicit test |
| `&&` on PS old | `;` · `$LASTEXITCODE` |
| path spaces | quotes · `-LiteralPath` |

---

## 11. Regex

| Pitfall | Fix |
|---------|-----|
| ReDoS | timeout · simple patterns |
| unescaped metachars | escape user input |
| greedy vs lazy | explicit groups |

---

## 12. Concurrency (General)

| Pitfall | Fix |
|---------|-----|
| TOCTOU | lock · idempotency key |
| double post | transaction · unique constraint |
| lost update | optimistic concurrency |

---

## 13. Review Protocol (Humans Skip This)

For every diff:
```
① Identify languages in diff
② Open each language section above
③ Record finding even if "maybe"
④ xhigh angle D = this file
```

**Links:** [code-review.md](code-review.md) · [integration](../brqosh-skills-hub/integration.md)
