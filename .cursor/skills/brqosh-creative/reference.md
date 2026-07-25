# Creative Media Council — Full Reference

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-creative | merged from archive/creative-media-council-v10.md

> Human-first · honest limits · checklists — C04 Games + C05 Media  
> **With:** [human-cognition.md](../brqosh-communication/human-cognition.md) · [routing.md](../brqosh-agent-protocol/routing.md)

---

## 1. Honest Limits (mandatory)

| Delivers | Does not replace |
|----------|------------------|
| Creative direction · brief · critique | 4K video render · professional mix |
| Song structure · draft lyrics | DAW · VST · mastering |
| Game design doc · feel notes | Unity build on your machine |
| UI mockup direction · tokens | Final Figma export |
| Pipeline steps | Blender · After Effects · GPU farm |

**Strength = method + critique + guided iteration** — not full studio production.

---

## 2. Human-Centered Process

```
BRIEF (who watches/plays/listens?)
  → CONSTRAINTS (engine · time · style · platform)
  → CONCEPT (3 directions max)
  → PROTOTYPE (cheapest version)
  → CRITIQUE (honest — what fails)
  → ITERATE (guided improvement)
```

Every creative decision ties to **human experience** — not theory-only aesthetics.

---

## 3. UI/UX Direction Checklist

- [ ] User and usage context defined
- [ ] Visual hierarchy — one focal point
- [ ] RTL if Arabic — not blind mirror
- [ ] ERP density (not marketing landing)
- [ ] empty · loading · error states
- [ ] a11y: contrast · keyboard · screen reader basics
- [ ] consistency with existing design system
- [ ] human microcopy — no robot-speak

**BRQOSH:** brqosh-ui-design-system · luxury ERP · RTL DataGrid

---

## 4. Game Feel Checklist

- [ ] Core loop in one sentence
- [ ] Input latency acceptable — responsiveness first
- [ ] Immediate feedback (sound · particles · camera — moderate)
- [ ] Fair difficulty curve
- [ ] Physics/network: engine limits explicit
- [ ] Performance budget (draw calls · memory)
- [ ] Save/load · progression clear
- [ ] 5-minute playtest before polish

**Engines:** Unity (C#) · Unreal · Godot — no engine switch without cause.

---

## 5. Narrative Checklist

- [ ] Protagonist want vs need
- [ ] Conflict clear from minute one
- [ ] Show don't tell where medium fits
- [ ] Consistent tone
- [ ] Culture/language — respect not caricature
- [ ] Endings tied to choices if interactive

---

## 6. Audio / Image / Video

**Audio:** mood board in words · structure (intro/verse/chorus) · speakable lyrics · SFX layers · mix notes for human engineer

**Image/Video:** composition · lighting · 3–5 color palette · aspect ratio · 6–12 frame storyboard · shot list · **GenerateImage only on explicit request**

---

## 7. Technical Pipelines

| Medium | Real tools | Agent delivers |
|--------|------------|----------------|
| UI | Figma · WPF · React | spec · tokens · RTL |
| 3D | Blender · Maya | blocking · export notes |
| Game | Unity · Unreal | scripts · architecture |
| Audio | DAW · FMOD | structure · direction |
| Video | Premiere · DaVinci | edit plan · pacing |

---

## 8. Anti–Break-Workflow

- No full ERP redesign in a bug task
- No engine migration in same task as gameplay tweak
- No 50 variations — 3 strong then choose
- Drafts in designated files — do not pollute `src/`

---

## 9. Council Integration

| Task | Councils |
|------|----------|
| New screen | C03 UI + C05 + C10 |
| Indie game | C04 + C05 + C01 |
| Song | C05 + C10 |
| Promo video | C05 + C12 PRODUCT |

**Next:** [human-cognition.md](../brqosh-communication/human-cognition.md)

---

## v15c — BRQOSH creative boundary

ERP work stays **operational density** — creative council applies to:
- Empty state copy · onboarding microcopy
- Icon/metaphor direction (not full illustration pipeline)
- Training video **script** outline — not render

| BRQOSH task | Creative role | Primary skill |
|-------------|---------------|---------------|
| Invoice screen layout | hierarchy · RTL | brqosh-ui-design-system |
| Dashboard empty state | human microcopy | this file §3 |
| Marketing website | **out of scope** | decline or separate project |
| Game / song / film | full checklists §4–6 | this file |

**GenerateImage:** only on explicit user request — never auto for ERP screens.
