# Launcher Architecture — Standalone QA App (Skill Spec Only)

> **Source:** raw prompt L446–472 · L205–211 (ideas only)  
> **Status:** Design law only — **no** PySide6/React code until user release  
> **Roles:** DevOps (S08) · QA Auto (S01)  
> **Related:** [orchestrator-law.md](orchestrator-law.md) · [bug-tracker-spec.md](bug-tracker-spec.md)

---

## Purpose

Define the **professional standalone launcher** that hosts the Cognitive QA Army — the face of the framework when code is authorized.

---

## Technology stack (binding when implemented)

| Layer | Choice | Rationale |
|-------|--------|-----------|
| Backend | Python 3.11+ | imports audit framework in-process |
| Shell | PySide6 + QWebEngineView | native desktop + modern web UI |
| Frontend | React + TypeScript + TailwindCSS | split panels · tabs · themes |
| Bridge | QWebChannel | Python ↔ React bidirectional |
| Terminal | xterm.js + Python pty | live logs · pip install on first run |

**FORBIDDEN until release:** child-process-only launcher without in-process orchestrator.

---

## UI regions (MUST)

| Region | Function |
|--------|----------|
| Drag-drop zone | select target `.exe` / `.app` — glowing border animation |
| File picker | glass-morphism fallback button |
| Start Cognitive Audit | primary CTA — gradient · pulse |
| Deep Code Analysis | runs `code_deep_analyzer.py` on source dir |
| Self-Heal | runs `self_healer.py` or Perfection Loop |
| Perfection Loop toggle | audit → analyze → fix → re-audit until score 100 |
| Terminal panel | chronicle output · syntax highlighting · auto-scroll |
| Progress dashboard | elapsed · steps · bugs by severity · active role avatar |
| Live Reports tab | streaming findings via QWebChannel |
| Bug Tracker tab | `BUG_INDEX.md` table · filter · status edit · CSV export |
| Language toggle | EN/AR — UI + reports + terminal |

---

## Bug Tracker tab (launcher integration)

| Feature | Spec |
|---------|------|
| Read | `audit_reports/bug_tracker/BUG_INDEX.md` |
| Filter | severity · status · detecting role |
| Edit status | Open → In Progress → Resolved · writes `BUG-XXXX.md` |
| Detail panel | full ticket on row click |
| Export | CSV for external tools |
| Real-time | QWebChannel signal on new ticket from `bug_tracker.py` |

---

## Export and completion

| Action | Output |
|--------|--------|
| Audit complete | executive summary card + subtle celebration |
| Perfection achieved | special celebration when loop finishes score 100 |
| Export PDF | full report + bug list from UI |

---

## Packaging law

| Platform | Tool |
|----------|------|
| Windows | PyInstaller + PySide6/QWebEngine hooks |
| macOS | py2app |
| Deliverable | single portable `.exe` / `.app` — no Python/Node required on target |

Document in `build_standalone.md` + README when code exists.

---

## Accessibility (launcher itself)

| MUST | Detail |
|------|--------|
| WCAG AA contrast | launcher leads by example |
| Keyboard navigation | all controls reachable |
| Screen reader | semantic React structure |

Cross-ref [empathetic-ux.md](empathetic-ux.md) · [roles/role-accessibility.md](roles/role-accessibility.md).

---

## Verify (skills phase)

```powershell
Test-Path .cursor/skills/omni-qa-army/launcher-architecture.md
rg "launcher-architecture" .cursor/skills/omni-qa-army/SKILL.md
Test-Path cognitive-qa/launcher   # MUST be False until release
```
