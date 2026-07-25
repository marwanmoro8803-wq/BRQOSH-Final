# ShuraCouncil — Evidence-Gated Consensus Engine v5

> **Constitution:** `.cursor/skills/brqosh-analysis/shura-constitution.md`  
> **Members:** `shura-members-registry.yaml` · **Heartbeat:** `scripts/ops/shura-dynamo/`

## Quick run

```powershell
.\scripts\ops\run-shura-council.ps1 -Source "dist/evidence/prompt-governance/CEEF-*.md"
.\scripts\ops\validate-shura-constitution.ps1
```

## Architecture

| Component | File |
|-----------|------|
| Entry | `Program.cs` |
| Evidence | `EvidenceLoader.cs` |
| Seats BRQOSH | `SeatIntelligenceRegistry.cs` + `SeatEvaluators.cs` |
| Seats PD | `PromptSeatIntelligenceRegistry.cs` + `PromptGovernanceSeatEvaluators.cs` |
| Consensus | `ConsensusEngine.cs` / `PromptConsensusEngine.cs` |
| Adversarial | `AdversarialVerifier.cs` |
| Supreme gates | `SupremeGateEngine.cs` (7 gates) |
| Disciplines | `ProgrammingDisciplineRegistry.cs` (D01–D28) |
| F00 | `FacilitatorEngine.cs` |

## Outputs

- `dist/evidence/shura-council/council-*.md`
- `dist/evidence/shura-council/council-*.json`
- exit `0` = consensus approved

## Heartbeat (Dynamo)

After council exit 0: `.\scripts\ops\run-shura-dynamo.ps1 -Mode Auto`
