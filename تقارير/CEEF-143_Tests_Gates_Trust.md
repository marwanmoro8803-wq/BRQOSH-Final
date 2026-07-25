# CEEF-143 — اختبارات · بوابات · ثقة زائفة

| الحقل | القيمة |
|-------|--------|
| **النطاق** | محور J + L |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **اختبارات قوية للنواة** · **ثقة زائفة على API/ops** |

## قياس حي (هذه الجلسة)

| Assembly | PASS | ملاحظة |
|----------|------|--------|
| Domain | 64 | `dotnet-test-final.log` |
| Desktop | 112 | +2 عن CEEF-137 |
| Api | 27 | |
| Application | 267 | +9 عن CEEF-137 |
| **المجموع** | **470** | قياس حي CEEF-138 |

## فجوات تغطية مُثبتة

| Gap | دليل |
|-----|------|
| لا `Brqosh.Infrastructure.Tests` | grep: 0 project |
| Desktop = markup لا سلوك | grep FlaUI/WinAppDriver: 0 |
| Payroll cycle tests | مضمّن في ManufacturingTests فقط |
| API role denial | لا أدوار = لا اختبار denial |
| Print PDF | موجود لكن خارج external-gates |

## ما تتحققه البوابات vs لا

| يتحقق | لا يتحقق |
|-------|----------|
| dotnet test (SKIP_STRESS=1) | 1M load |
| security-static-scan | ZAP live |
| coverage ≥75% | Desktop WPF LOC |
| FullScenarioAudit | في CI workflow |
| PG gate (if database.json) | PG إلزامي في CI |
| i18n PageTitlesEn | XAML ratio gate |
| MSI publish | VM install |

## CI vs run-external-gates

`.github/workflows/ci.yml`: build · security · ui-ux · test+coverage — **لا** stress · **لا** FullScenario · **لا** ZAP.

## AUDIT-GATE-001 (P1)

`run-external-gates.ps1` PASS **لا يعني** O-01…O-05 مغلقة — يطبع checklist فقط.

## درجة المحور: **B+** tests · **C** gate honesty
