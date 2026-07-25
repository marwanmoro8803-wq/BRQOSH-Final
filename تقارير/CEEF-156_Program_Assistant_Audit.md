# CEEF-156 — تحليل المساعد الذكي (تقرير شامل)

| الحقل | القيمة |
|-------|--------|
| **التاريخ** | 2026-07-11 |
| **السلطة** | طلب المستخدم · Fable CLASSIFY+VERIFY · CEEF-155 |
| **النطاق** | تحليل in-repo فقط — لا ادعاء O-12 LLM |
| **المراجع** | `ProgramAssistantService v2` · `ProgramKnowledge` · Shura D1–D7 |

---

## 1. الحكم التنفيذي

| السؤال | الحكم | الدليل |
|--------|-------|--------|
| هل هو كامل؟ | **جزئياً in-repo** | 27 دليل مفصّل / ~70 شاشة · 11 مسار · لا LLM · لا إجراءات |
| هل هو متكامل؟ | **Desktop فقط** | `MainWindow.xaml.cs` · لا API assistant endpoint |
| نقاط ضعف؟ | **نعم** | keyword routing · تكرار · RBAC وصفي |
| عيوب تصميمية؟ | **نعم** | لا ذاكرة · لا تنفيذ · لا مستند مفتوح |
| نقص؟ | **نعم** | O-12 · 43+ شاشة بأدلة رقيقة · 0 Desktop UI test |

**الدرجات:**

| المستوى | الدرجة |
|---------|--------|
| مساعد تشغيلي in-repo (قواعد + KPIs) | **B+** |
| مساعد «خبير شامل» لكل ERP | **C** |
| AI CFO / سوق (O-12) | **F** (⏭ EXTERNAL) |

---

## 2. البنية (ما يعمل)

```
Desktop UI → IProgramAssistantService v2
  → Classify · QuestionMatcher · WorkflowKnowledge
  → FinancialAssistantService (KPIs حية)
  → ProgramReplyComposer (سياق + جواب + مسار + اقتراحات)
```

| المكوّن | الملف | الدور |
|---------|-------|-------|
| Orchestrator | `ProgramAssistantService.cs` | توجيه + دمج |
| معرفة شاشات | `ProgramKnowledge.cs` + `ProgramNavCatalog.cs` | 27 مفصّل + 70 عنوان |
| مسارات | `ProgramWorkflowKnowledge.cs` | 11 دورة |
| مطابقة سؤال | `ProgramQuestionMatcher.cs` | 19 مفتاح (قبل CEEF-157) |
| تكوين رد | `ProgramReplyComposer.cs` | طبقات خبيرة |
| مالي حي | `FinancialAssistantService` + `RulesEngine-v1` | KPIs + تنبيهات |

**تحقق:** 501/501 PASS · `ProgramAssistantTests` 11 · CEEF-155 · EngineId `brqosh-program-assistant-v2`

---

## 3. مصفوفة الاكتمال

| المحور | التغطية | الحالة |
|--------|---------|--------|
| شاشات التنقل | 70 مفتاح `ProgramNavCatalog` | عناوين + قالب عام |
| أدلة مفصّلة | 27 `ProgramKnowledge.Pages` | باقي الشاشات ضحلة |
| مسارات عمل | 11 دورة ثابتة | نص in-repo |
| مطابقة سؤال→شاشة | 19 مفتاح (قبل التوسعة) | ~73% بلا مرادفات |
| KPIs حية | DashboardQuery + RulesEngine | ✅ |
| سياق جلسة | صفحة · دور · ملف | لا فرع · لا مستند |
| UI | لوحة جانبية + رؤى | لا scroll · لا اختصارات |
| API / LLM | — | ⏭ غير موجود |

---

## 4. نقاط القوة

1. دمج متعدد الطبقات v2 في كل رد
2. صدق ZATCA/O-01 (لا إغلاق كاذب)
3. KPIs من بيانات الشركة الحقيقية
4. محرك قواعد قابل للشرح (`RulesEngine-v1`)
5. تكامل DI Desktop مع `_activePageKey`
6. 11 اختبار وحدة للمسارات الحرجة

---

## 5. نقاط الضعف

| # | الضعف | مثال |
|---|-------|------|
| W1 | توجيه keyword | «كيف أرحّل؟» → HowTo قبل Posting |
| W2 | تكرار محتوى | inferred + core + workflow |
| W3 | 43 شاشة بأدلة رقيقة | `contracts` · `goods_receipts` |
| W4 | RBAC وصفي | لا قراءة صلاحيات فعلية |
| W5 | ملف عمل سطحي | Construction/Retail في الهيدر فقط |
| W6 | تداخل ZATCA/مالي | مصنّفان منفصلان |
| W7 | لا ذاكرة محادثة | كل سؤال معزول |
| W8 | اختبار ضيق | 11/70 · 0 UI |

---

## 6. العيوب التصميمية

1. لا تنفيذ (تنقل · ترحيل · فتح نموذج)
2. لا وعي بالمستند المفتوح (مسودة/مرحّل)
3. لا ربط برسائل RBAC الفعلية
4. قناة Desktop فقط
5. ليس LLM — أسئلة غامضة → KPI عام
6. لا تعلم من التفاعل

---

## 7. النقص

**معرفة:** مقاولات فرعية · تقارير موحّدة · أمان · رواتب · أصول  
**تقنية:** O-12 · API endpoint · FlaUI للوحة AI  
**UX:** scroll · أزرار انتقل · اقتراحات لـ 5 شاشات فقط

---

## 8. Adversarial 3b (خصومة ادعاءات)

| الادعاء | الحكم | الدليل |
|---------|-------|--------|
| «المساعد يفهم كل البرنامج» | **مرفوض** | 27/70 دليل عميق · keyword routing |
| «المساعد ذكي / LLM» | **مرفوض** | `RulesEngine-v1` · لا API نموذج |
| «يربط بكل صفحة حية» | **جزئي** | `_activePageKey` ✅ · لا مستند مفتوح |
| «جاهز AI CFO» | **مرفوض** | O-12 OPEN · Shura D2 |
| «in-repo مساعد تدريب تشغيلي» | **مقبول** | CEEF-155 · 501 PASS · B+ |

---

## 9. مواءمة الشورى

| قرار | الحكم |
|------|-------|
| D1 تجريب in-repo | متوافق — مرشد تشغيلي |
| D2 رفض إطلاق سوق | متوافق — لا AI CFO |
| D4 O-01 | متوافق — صريح ⏭ |
| D5 FlaUI | فجوة — لوحة AI غير مُختبرة |
| O-12 | ⏭ EXTERNAL — ليس عيباً مخفياً |

---

## 10. توصيات وموجة CEEF-157 (P1)

- توسيع أدلة 70 شاشة (`ProgramPageGuideTemplates`)
- `ProgramQuestionMatcher` لكل المفاتيح
- `ProgramProfileHints` حسب ملف العمل
- تقليل تكرار `ComposeExpert`
- Desktop.Tests smoke للوحة AI
- scroll تلقائي في المحادثة

---

## 11. الخلاصة

المساعد **جيد in-repo كمرشد قواعد + KPIs (B+)** — **ليس منتج ذكاء شامل (C)** و**ليس AI CFO (F/O-12)**. يُوصى بموجة CEEF-157 قبل أي تسويق «مساعد ذكي متكامل».
