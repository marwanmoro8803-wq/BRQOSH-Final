# CEEF-152 — مساعد البرنامج الذكي (Program AI Assistant)

| الحقل | القيمة |
|-------|--------|
| **التاريخ** | 2026-07-11 |
| **السلطة** | طلب المستخدم · CEEF-150/151 · Fable epistemic humility |
| **النطاق** | in-repo فقط — لا LLM خارجي (⏭ O-12) |

---

## 1. المشكلة

المساعد السابق (`FinancialAssistantService`) يوجّه أسئلة KPI مالية فقط — لا يربط بالشاشة الحالية ولا بمعرفة البرنامج (ترحيل · ZATCA · RBAC).

---

## 2. الحل (✅ in-repo)

| طبقة | الملف | الدور |
|------|-------|-------|
| عقود | `ProgramAssistantContracts.cs` | `IProgramAssistantService` + `ProgramAssistantContext` |
| معرفة | `ProgramKnowledge.cs` | دليل الشاشات · ترحيل · ZATCA (O-01 ⏭) · RBAC |
| تنفيذ | `ProgramAssistantService.cs` | توجيه حسب الموضوع + تفويض مالي |
| DI | `InfrastructureServiceCollectionExtensions.cs` | تسجيل الخدمة |
| UI | `MainWindow.xaml.cs` | سياق `_activePageKey` · بطاقة شاشة · «مساعد البرنامج الذكي» |

### مسارات الرد

1. **الشاشة الحالية** — دليل من `ProgramKnowledge.Pages`
2. **ترحيل / ZATCA / صلاحيات / مساعدة** — أدلة ثابتة in-repo
3. **مالي** — `FinancialAssistantService` (KPIs حية)
4. **هجين** — دليل الشاشة + KPI عند عدم تطابق الكلمات المفتاحية

---

## 3. الاختبارات

| المقياس | قبل | بعد |
|---------|-----|-----|
| الإجمالي | 488 | **491** (+3) |
| Application | 268 | **271** (+3) |

```powershell
dotnet test Brqosh.sln -c Release --nologo  # 491/491 PASS
```

اختبارات جديدة: `ProgramAssistantTests` — شاشة · ZATCA/O-01 · تفويض مالي.

---

## 4. ⏭ خارج النطاق

| البند | السبب |
|-------|-------|
| LLM / ChatGPT / Claude API | O-12 roadmap |
| OTP ZATCA حي | O-01 EXTERNAL |
| تغطية كل nav keys (~80+) | موجة توسعة لاحقة |

---

## 5. الملفات

- `src/Brqosh.Application/Intelligence/ProgramAssistantContracts.cs`
- `src/Brqosh.Application/Intelligence/ProgramKnowledge.cs`
- `src/Brqosh.Infrastructure/Intelligence/ProgramAssistantService.cs`
- `src/Brqosh.Infrastructure/InfrastructureServiceCollectionExtensions.cs`
- `src/Brqosh.Desktop/MainWindow.xaml.cs`
- `tests/Brqosh.Application.Tests/Intelligence/ProgramAssistantTests.cs`
