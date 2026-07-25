# Program Design Mastery (v18 Strict)

> **Binding:** [enforcement.md](enforcement.md) · **Corpus:** grep-only from `system_prompts_leaks-main/`  
> **Product:** BRQOSH — WPF financial ERP · RTL Arabic + English · Saudi compliance

---

## 1. Definition

Software design is **not decoration**. It is the translation of business intent into a surface that can be operated with confidence.

| Dimension | BRQOSH meaning |
|-----------|----------------|
| **Purpose** | One clear job per screen: post · compare · close · comply |
| **Audience** | Accountant working 8 hours/day — scan, not marketing read |
| **Environment** | WPF desktop · branch · fiscal year · RBAC · ZATCA |
| **Nature** | **Operational tool** — not landing page · not gallery |
| **Vitality** | Complete states · immediate feedback — zero empty decoration |

**Gold rule:** If it does not serve a financial or operational decision, it is noise. Remove it.

---

## 2. Ten properties of correct software UI

| Property | Operational test |
|----------|------------------|
| **Clear** | Screen job known in 3 seconds |
| **Dense** | High information · low noise |
| **Honest** | No fake data · real empty states |
| **Consistent** | Same visual language across 100 lifecycle steps |
| **Predictable** | Post always in same hierarchy · same grid pattern |
| **Forgiving** | Fixable errors · confirm on irreversible |
| **Context-complete** | Company · branch · period · permission visible |
| **Responsive** | Instant feedback — not decorative motion |
| **Deep** | Drill-down to document and audit |
| **Purposeful** | Every element earns its place |

---

## 3. Product spectrum (where BRQOSH lives)

```
Marketing site  ←——→  Operational tool  ←——→  Game / creative
      ⛔ FORBIDDEN           ✅ BRQOSH              ⛔ FORBIDDEN
```

| Type | Surface goal | Density | Motion |
|------|--------------|---------|--------|
| Editorial | persuade · story | sparse | expressive |
| **Operational** | complete work | **dense** | restrained |
| Document | read · report | medium | minimal |
| Game | play · explore | varied | playful |

**BRQOSH = Operational + Document (reports).** Editorial patterns are **forbidden** unless user explicitly requests marketing assets outside ERP shell.

---

## 4. Information architecture

```
Platform
 └── Company (tenant)
      └── Branch
           └── Module (GL · Sales · …)
                └── Entity list (DataGrid)
                     └── Document (invoice · journal)
                          └── Lines + posting
```

**Navigation laws:**

- MUST reach any recurring entity in **≤3 clicks** from shell
- MUST show return path (active nav or breadcrumb)
- MUST NOT hide critical context in dialogs (closed period · wrong branch)

Map every screen to `LIFECYCLE-ROADMAP-100.md` — UI + domain together.

---

## 5. Authority order

```
1. User explicit words
2. BrqoshTheme.xaml + MASTER.md + pages/*.md
3. This skill stack (enforcement → modules)
4. Agent judgment (subject-grounded only)
```

**MUST NOT** override an existing design system for generic AI aesthetics.

---

## 6. Treatment calibration

| Output type | ERP treatment |
|-------------|---------------|
| Report / document | typographic hierarchy · tables · tabular numbers |
| Tool / dashboard | information design · summary before detail · state encoding |
| Dialog / form | complete fields · validation · fixable errors |
| Marketing / landing | **FORBIDDEN** — replace with immediate work screen |

When uncertain: a well-composed work screen beats a bold visual identity.

---

## 7. Universal fundamentals

### 7.1 Ground in subject

- MUST define: one subject · one audience · one screen job
- MUST use real content — MUST NOT show mock as live
- Accounting world: entries · invoices · tax · branch · who posted when

### 7.2 Typography

- MUST pair display + body + utility (tabular data)
- MUST use tabular figures in all amount columns
- MUST keep fixed type scale — MUST NOT scale type with viewport width
- MUST use tighter headings inside compact panels (Codex)

### 7.3 Color

- MUST choose neutrals deliberately — not default grey
- MUST separate brand accent from semantic status colors
- MUST NOT use AI-slop palettes (purple gradient · cream/terracotta template)
- Charts: color follows **job** — see `visual-effects.md`

### 7.4 Layout

- MUST use Grid/StackPanel with theme gaps — MUST NOT scatter manual margins
- Wide tables: local horizontal scroll — MUST NOT scroll entire page sideways
- MUST keep stable toolbar/card dimensions during load/hover
- Pattern: **shell → module → document** — MUST NOT nest cards

### 7.5 Anti AI-slop

| Generic AI pattern | BRQOSH replacement |
|--------------------|-------------------|
| cream + serif + terracotta | teal trust + slate shell + gold accent |
| purple-blue gradient hero | toolbar + DataGrid |
| emoji section markers | monogram nav/KPI |
| universal `rounded-lg` | BrqoshTheme radius 8–14px |
| centered everything | RTL flow · financial alignment |
| card-in-card | one ContentCard + grid |

### 7.6 Copy as design material

- MUST use user-facing words ("Notifications") not system words ("webhook config")
- Buttons MUST use active verbs ("Post" → toast "Posted")
- Errors MUST state what happened + how to fix — MUST NOT apologize vaguely
- MUST NOT embed in-app manuals explaining the product (Codex)

### 7.7 Structure carries information

- Numbered steps (01/02/03) ONLY when order is real information (period close)
- Dividers and labels MUST reflect content — MUST NOT decorate

---

## 8. Operational UI ≠ document

When the surface is a **tool**, not a memo:

| Principle | ERP application |
|-----------|-----------------|
| Scanned and operated | DataGrid first · KPI then drill-down |
| Summary before detail | period status · balances · then lines |
| State in form and number | pill/chip/stripe for draft/posted/overdue |
| Semantic ≠ brand color | teal identity · red danger — never mixed |
| Clickable looks clickable | Hand cursor · hover · focus ring |
| No filler | every KPI justified or removed · honest empty |

---

## 9. Codex empathy — ERP inversion

| Generic SaaS (Codex) | BRQOSH (Saudi ERP) |
|----------------------|-------------------|
| quiet utilitarian | **operational density** |
| avoid hero | **immediate work screen** |
| scannable sparse | **KPI bento + drill lists** |
| predictable nav | **monogram sidebar + RBAC** |
| feature-complete controls | **posting states + audit trail** |

User MUST feel precision and richness — not marketing emptiness.

---

## 10. Control vocabulary

| Data / intent | Control |
|---------------|---------|
| Clear command | text button or icon+text |
| Repeated tool | toolbar icon + tooltip |
| Mode / view | tabs or segmented control |
| Binary | toggle or checkbox |
| Numeric | NumericUpDown or slider |
| Option set | ComboBox or menu |
| State / severity | pill · chip · stripe |
| Dense entities | **DataGrid (default)** |
| Overview | KPI row + chart + drill table |

Use icon instead of text button when symbol is industry-standard (save · undo · export).

---

## 11. Environment matrix

| Layer | Governs |
|-------|---------|
| Platform | WPF · .NET 9 native — no web tropes |
| Locale | AR RTL + EN LTR · `LocaleManager.T` |
| Theme | light pearl · dark cosmic · `BrqoshTheme.xaml` |
| Security | RBAC hide/disable + message |
| Compliance | ZATCA · VAT · fiscal period |
| Data | no AI KPI without query |
| Lifecycle | 100 steps login→close |

Deep dive: [interaction-environment.md](interaction-environment.md)

---

## 12. Real data law

- User asks for "my invoices" → **real query or honest empty** — never fake rows
- Demo mode → **explicit badge**
- API integration → clear setup — MUST NOT build ad-hoc secret-entry UI in product screens

---

## 13. Bold design exception (inverted)

| Context | Rule |
|---------|------|
| Greenfield marketing site | bold visuals allowed |
| **Existing BRQOSH ERP** | **MUST preserve MASTER/BrqoshTheme** |
| New page in ERP | MUST follow `pages/*.md` first |

---

## 14. Design process (before XAML)

```
1. CLASSIFY — new | edit | KPI | report
2. READ    — enforcement · page spec · MASTER · lifecycle step
3. PLAN    — 4–6 colors · 2–3 type roles · one layout sentence
4. CHECK   — reject generic template look
5. BUILD   — smallest correct diff
6. VERIFY  — 12-axis · audit · AR/EN/dark
```

**Surgical edit rule:** change only what was requested — MUST NOT "improve" surrounding layout.

---

## 15. Corpus deepen index

| Topic | Corpus file | Module |
|-------|-------------|--------|
| Fundamentals | `Anthropic/.../artifact-design.md` | this file |
| Workflow | `Anthropic/claude-design.md` | this file |
| Charts | `Anthropic/.../dataviz/` | visual-effects |
| Frontend | `OpenAI/Codex/gpt-5.5.md` | this file |
| Real data | `Google/ai-studio-build.md` | this file |
| Views | `Notion/notion-ai.md` | modules |
| Visual core | `Anthropic/visualize.md` | design-foundations |
| Interaction | `dataviz/references/interaction.md` | interaction-environment |

**Next:** [design-foundations.md](design-foundations.md) · [enforcement.md](enforcement.md)
