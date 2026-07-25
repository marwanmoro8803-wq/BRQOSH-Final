# Shura Heartbeat Core — القلب النابض (Dynamo v1)

> **Constitution:** [shura-constitution.md](shura-constitution.md) · **Registry:** [shura-members-registry.yaml](shura-members-registry.yaml)  
> **Engine:** `scripts/ops/shura-dynamo/` · **Integration:** `CognitiveQa.Core/ShuraDynamoIntegration.cs`

---

## 1. What is the heartbeat?

The council **decides** — Dynamo **executes**. Heartbeat = operational pulse that proves council members are alive in the repo.

```
Council JSON (exit 0) → Resolve-ShuraDispatch → F00 queue → Invoke-ShuraPulse × N → pulse evidence
```

| NOT | BUT |
|-----|-----|
| Background daemon 24/7 | On-demand + post-consensus dispatch |
| LLM agents debating | Deterministic ops scripts per seat |
| Theatre | `dist/evidence/shura-dynamo/pulses/*.json` with exit codes |

---

## 2. Pulse anatomy (one heartbeat cycle)

Each member pulse runs through phases logged by `Write-PulseHeartbeat`:

| Phase | Action |
|-------|--------|
| **START** | Seat ID · role · mode (on_demand / shura_dispatch / maintain / manual) |
| **OPS** | Primary `opsScript` from registry (e.g. `run-prompt-audit-engine.ps1`) |
| **DOTNET** | Fallback `dotnetCommand` (e.g. `test CognitiveQa.sln`) |
| **NPM** | Fallback `npmCommand` for web stacks |
| **SCAN** | Marker presence when no ops configured |
| **END** | Evidence JSON written · exit code |

**Evidence path:** `dist/evidence/shura-dynamo/pulses/pulse-{seat}-{timestamp}.json`

---

## 3. F00 — orchestrator (heart pacemaker)

F00 is the **dispatch authority** — not a voting seat.

| Function | Tool |
|----------|------|
| Read latest council | `Resolve-ShuraDispatch.ps1` → parses `council-*.json` |
| Build pulse queue | Priority bands: security(100) > quality(90) > platform(80) > … |
| Run closure pulse | `merge-council-chronicle.ps1` — unify AiCouncil + Shura timeline |
| CognitiveQa trigger | `ShuraDynamoIntegration.RunAsync(mode)` |

**Registry:** `scripts/ops/shura-dynamo/shura-dynamo-registry.json` — 41 members.

---

## 4. Priority bands (dispatch order)

| Band | Priority | Members (examples) |
|------|----------|-------------------|
| security | 100 | S04 Pen · D13 Security |
| quality | 90 | S02 Manual · S12 Report · D17 QA |
| platform | 80 | S08 DevOps · D11 CI |
| superintelligence | 75 | S11 Super · D19 AI · D28 Prompt |
| automation | 60 | S01 QA Auto |
| advisory | 40 | S07 L10n · S09 Compatibility |
| discipline | 30 | D01–D28 (marker/ops mix) |
| facilitator | 10 | F00 chronicle |

---

## 5. Modes

| Mode | When | Trigger |
|------|------|---------|
| `shura_dispatch` | After council exit 0 | Auto from `Resolve-ShuraDispatch` |
| `on_demand` | Agent or ops manual | `Invoke-ShuraPulse.ps1 -SeatId S01` |
| `maintain` | Periodic hygiene | Future CI hook — not default |
| `manual` | Debug | `-DryRun` preview |

```powershell
# Full dispatch after council
.\scripts\ops\run-shura-dynamo.ps1 -Mode Auto

# Single seat pulse
.\scripts\ops\shura-dynamo\Invoke-ShuraPulse.ps1 -SeatId S11 -Mode on_demand
```

---

## 6. Member → pulse mapping (core 12)

| Seat | Pulse action | Proves |
|------|--------------|--------|
| S01 | `run-prompt-audit-engine.ps1` | PAE engine alive |
| S02 | `run-cognitive-audit.ps1` | Exploratory scan |
| S03 | `run-cognitive-perception.ps1` | UX/vision path |
| S04 | `validate-prompt-governance.ps1` | Security governance |
| S05 | `run-cognitive-live-smoke.ps1` | FlaUI beta path |
| S06 | `dotnet test` | Performance baseline |
| S07 | marker scan | RTL/i18n markers |
| S08 | `run-shura-council.ps1` | DevOps integration |
| S09 | `run-cognitive-multi-target.ps1` | Compatibility matrix |
| S10 | `merge-council-chronicle.ps1` | Report chronicle |
| S11 | `run-omni-qa-gates.ps1` | Superintelligence gates |
| S12 | `sync-cognitive-status.ps1` | STATUS alignment |
| F00 | `merge-council-chronicle.ps1` | Post-council synthesis |

Disciplines D01–D28: stack-specific ops or marker scan — see registry JSON.

---

## 7. Omega cell probe (structural health)

`OmegaCellHealth` in CognitiveQa probes **GovernCell** = `ShuraCouncil/` + `PromptAuditEngine/` presence.

This is **structural** health (files exist) — distinct from Dynamo operational pulses.

---

## 8. Anti-patterns

| DO | DON'T |
|----|-------|
| Dispatch after exit 0 council | Run Dynamo without SAVE |
| Read pulse JSON for verify | Assume pulse passed from chat |
| F00 chronicle merge | Skip MEM after decision |
| Marker scan for absent stacks | Fake pulse success without evidence |

---

## 9. Verify heartbeat

```powershell
.\scripts\ops\shura-dynamo\Invoke-ShuraPulse.ps1 -SeatId F00 -Mode on_demand
ls dist/evidence/shura-dynamo/pulses/
```

**Related:** System 92 CEEF · `dist/evidence/prompt-governance/CEEF-SYSTEM-92-*`
