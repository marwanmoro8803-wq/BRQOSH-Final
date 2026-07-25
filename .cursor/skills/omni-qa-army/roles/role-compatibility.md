# Role — Compatibility / Hardware Tester (Compatibility QA)

> **Council:** C03 · **Deliverable:** `COMPATIBILITY_TEST_MATRIX.md`  
> **Governs:** `vision_brain.py` multi-DPI · resolution matrix

---

## Trigger

Resolution · DPI · touch vs mouse · low memory · OS variants.

---

## Mandate

Simulate **system configuration diversity** — pass/fail per cell in compatibility matrix.

| MUST | Detail |
|------|--------|
| C01 | Resolutions — 1366×768 through 3840×2160 (sample 4+) |
| C02 | DPI — 100% · 125% · 150% · 200% |
| C03 | Input mode — mouse vs touch if applicable |
| C04 | Low memory — document if simulated |
| C05 | Screenshot per failing cell |
| C06 | `detect_new_surface()` still works at each DPI |

---

## Deliverable (`COMPATIBILITY_TEST_MATRIX.md`)

| Column | REQUIRED |
|--------|----------|
| OS | version |
| Resolution | WxH |
| DPI | % |
| Input | mouse/touch |
| Memory | normal/constrained |
| Status | PASS/FAIL/BLOCKED |
| Notes | defects · BUG refs |
| Evidence | screenshot path |

---

## Minimum matrix size

| Dimension | Minimum samples |
|-----------|-----------------|
| Resolution | 4 |
| DPI | 4 |
| OS | 1 (document target) |

---

## Links to P0

| Module | Use |
|--------|-----|
| `vision-perception.md` | DPI section · capture |
| `report-excellence.md` | severity per clip/overlap |

---

## FORBIDDEN

| ID | Forbidden |
|----|-----------|
| FC01 | Test only developer default 100% DPI |
| FC02 | PASS without screenshot spot-check |
| FC03 | Ignore unclickable controls at high DPI |

---

## Verify

Matrix ≥16 cells (4 res × 4 DPI) or documented BLOCKED with reason
