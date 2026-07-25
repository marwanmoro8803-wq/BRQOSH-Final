# نظام تصميم BRQOSH — دليل واجهة المستخدم وتجربته

> الإصدار **2.0** — يوليو 2026 (UI UX Pro Max)  
> منصة: WPF Desktop · RTL عربي · ويندوز 10/11  
> **الحالة:** مطبّق + طبقة Pro Max (Monogram nav · KPI Bento · Focus ring · design-system/)  
> **مرجع AI:** `design-system/MASTER.md` · فحص: `scripts/ui-ux-pro-max-audit.ps1`

---

## 1. مبادئ التصميم الأساسية

| المبدأ | التطبيق |
|--------|---------|
| **الوضوح أولاً** | كل عنصر له وظيفة واحدة واضحة. لا ضجيج بصري. |
| **التسلسل الهرمي** | أهمية العنصر تُترجم مباشرة لحجمه ووزنه وموضعه. |
| **الاتساق** | نفس المكون يبدو ويتصرف بنفس الطريقة في كل مكان. |
| **RTL عربي أصيل** | التصميم مبني أصلاً للعربية، لا ترجمة معكوسة. |
| **كثافة مناسبة للـ ERP** | معلومات كافية في الشاشة، مع مساحة تنفس لا إهدار. |
| **المظهر الداكن ممكّن** | كل ألوان واجهة المستخدم مبنية على متغيرات Brush قابلة للتبديل. |
| **إمكانية الوصول (a11y)** | AutomationProperties، تباين ≥ 4.5:1، تنقل لوحة المفاتيح. |

---

## 2. ألوان العلامة التجارية

### الأساسية

| الاسم | القيمة | الاستخدام |
|-------|--------|-----------|
| **Primary** | `#0D9488` (Teal 600) | الأزرار الرئيسية، الروابط، التمييز |
| **Primary Dark** | `#0F766E` | التمرير فوق الزر الرئيسي |
| **Primary Light** | `#14B8A6` | مؤشر العنصر النشط في الشريط الجانبي |
| **Primary Soft** | `#CCFBF1` | خلفية الشارات، الصف المحدد |
| **Accent (Gold)** | `#D4A853` | العناصر المميزة، الشارات، الفواصل البصرية |
| **Sidebar** | `#0F172A` (Slate 900) | خلفية الشريط الجانبي |
| **Background** | `#F1F5F9` (Slate 100) | خلفية الصفحة |
| **Surface** | `#FFFFFF` | البطاقات، الحقول، النوافذ |

### الدلالية

| الاسم | القيمة | الاستخدام |
|-------|--------|-----------|
| **Success** | `#10B981` | عمليات ناجحة، حالة مرحّلة |
| **Danger** | `#EF4444` | الحذف، الإلغاء، الأخطاء |
| **Warning** | `#F59E0B` | تنبيهات، قيم متأخرة |
| **Info** | `#3B82F6` | ملاحظات إعلامية |

---

## 3. مقياس المسافات

| المستوى | القيمة | الاستخدام |
|---------|--------|-----------|
| xs | 4px  | مسافة داخلية صغيرة جداً |
| sm | 8px  | مسافة بين العناصر المتلاصقة |
| md | 12px | مسافة داخلية الحقول الثانوية |
| lg | 16px | مسافة بين المجموعات |
| xl | 24px | هامش القسم |
| 2xl | 32px | هامش البطاقات الكبيرة |
| 3xl | 48px | هامش الصفحة الرئيسي |

**القاعدة:** كل مسافة مضاعف للـ 4px.

---

## 4. الطباعة

```
AppFont = "Segoe UI Variable, Segoe UI, Tahoma, Arial"
```

| الدور | الحجم | الوزن | الاستخدام |
|-------|-------|-------|-----------|
| Page Title | 22px | Bold | عنوان الصفحة في الترويسة |
| Section Title | 17px | SemiBold | عنوان قسم داخل الصفحة |
| Body | 14px | Regular | نص الجداول والحقول |
| Label | 13px | SemiBold | تسميات الحقول |
| Caption | 12px | Regular | النص الثانوي والملاحظات |
| Micro | 11px | Regular | شريط الحالة، التذييلات |

---

## 5. قواعد الأزرار (Button Grammar)

### التسلسل الهرمي

```
PrimaryButton → إجراء واحد فقط لكل شاشة/حوار
SecondaryButton → الإجراءات الثانوية
GhostButton → الإجراءات الخفيفة (تصفية، خيارات)
DangerButton → حدود حمراء — للإجراءات التدميرية المحتملة
SoftDangerButton → خلفية حمراء ناعمة — للإلغاء والحذف الفعلي
SuccessButton → تأكيد الترحيل والإنهاء
IconGhostButton → أزرار الأيقونات (مربعة 36×36)
```

### ترتيب الشريط RTL (من اليمين إلى اليسار)

```
[ Primary: إجراء رئيسي ] | [ ثانوي 1 ] [ ثانوي 2 ] | [ خطر: إلغاء/حذف ]
                                                         ← ← ←
              العداد والمعلومات على اليسار (يمين الشاشة)
```

**القواعد الصارمة:**
- زر `Primary` واحد فقط في كل شريط أدوات أو حوار.
- أزرار الخطر **دائماً** منفصلة بـ `ToolbarSpacer` عن الأزرار العادية.
- لا يجمع `Primary` و `Danger` في نفس المجموعة.

---

## 6. شريط الأدوات (Toolbar)

```xml
<!-- النمط المرجعي RTL -->
<Border Style="{StaticResource ToolbarCard}">
    <DockPanel LastChildFill="False">
        <!-- يمين: Primary أولاً -->
        <StackPanel DockPanel.Dock="Right" Style="{StaticResource ToolbarPrimaryGroup}">
            <Button Content="إجراء رئيسي" Style="{StaticResource PrimaryButton}"/>
        </StackPanel>
        <Border DockPanel.Dock="Right" Style="{StaticResource ToolbarSpacer}"/>
        <!-- ثانوي -->
        <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
            <Button Content="عرض"/>
            <Button Content="طباعة"/>
        </StackPanel>
        <Border DockPanel.Dock="Right" Style="{StaticResource ToolbarSpacer}"/>
        <!-- خطر -->
        <Button DockPanel.Dock="Right" Content="إلغاء" Style="{StaticResource SoftDangerButton}"/>
        <!-- يسار: عداد / معلومات -->
        <TextBlock DockPanel.Dock="Left" x:Name="CountText" .../>
    </DockPanel>
</Border>
```

---

## 7. كثافة الجداول (DataGrid)

| المعامل | القيمة |
|---------|--------|
| RowHeight | 44px |
| HeaderHeight | ~42px (Padding 16,13) |
| FontSize | 14px |
| Cell Padding | 16,0 |
| Alternating Row | `#FAFBFC` |
| Selected Row | `PrimarySoftBrush (#CCFBF1)` |
| Hover Row | `#EFF6FF` |
| Grid Lines | Horizontal فقط (`BorderLightBrush`) |

**ملاحظة:** لا أعمدة تلقائية (`AutoGenerateColumns="False"` دائماً).

---

## 8. نماذج الإدخال (Forms)

```
FormSectionTitle  ← عنوان القسم
    ↓
FieldLabel        ← تسمية الحقل (SemiBold, 13px, مسافة سفلية 6px)
    ↓
AppTextBox / AppPasswordBox / AppComboBox
    ↓
TextBlock (caption)  ← ملاحظة أو قيد (12px, TextMuted)
```

**التباعد الأفقي بين الحقول:** 16px.  
**مجموعة الحقول داخل بطاقة:** `ContentCard` بـ Padding 28,24.

---

## 9. الحالات الفارغة (Empty States)

```xml
<Border Style="{StaticResource ContentCard}" Padding="48">
    <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
        <TextBlock Text="🔍" FontSize="48" HorizontalAlignment="Center"/>
        <TextBlock Text="لا توجد نتائج" FontSize="20" FontWeight="Bold"
                   HorizontalAlignment="Center" Margin="0,12,0,0"/>
        <TextBlock Text="جرّب تغيير فلاتر البحث أو أضف عنصراً جديداً."
                   FontSize="14" Foreground="{StaticResource TextSecondaryBrush}"
                   HorizontalAlignment="Center" Margin="0,8,0,0"/>
    </StackPanel>
</Border>
```

---

## 10. قواعد الحوارات (Dialogs)

| المعامل | القيمة |
|---------|--------|
| عرض الحوار الصغير | 480–560px |
| عرض الحوار المتوسط | 680–760px |
| عرض الحوار الكبير | 900–1000px |
| بطاقة الحوار | `AuthCard` (CornerRadius 20, Padding 40,36) |
| زر الإجراء الرئيسي | `HorizontalAlignment="Stretch"` — كامل العرض في الحوارات الصغيرة |
| `IsDefault="True"` | دائماً على زر التأكيد |
| `IsCancel="True"` | دائماً على زر الإغلاق |

---

## 11. شريط التنقل الجانبي

```
┌─────────────────────────────┐  ← SidebarGradientBrush (#0F172A)
│  BRQOSH          [خط ذهبي] │
│  نظام ERP                   │
│  [اسم المنشأة]              │
│  [شارة النمط: ذهبية]        │
├─────────────────────────────┤
│  SECTION HEADER             │  ← NavGroupHeader (10.5px Bold, #475569)
│  ▶ عنصر التنقل              │  ← NavItem (13.5px, #94A3B8)
│  ▶ عنصر نشط ══ ◾           │  ← NavItemActive (Bold, White, مؤشر تيل)
├─────────────────────────────┤
│  [اسم المستخدم]   [دائرة]  │  ← بطاقة المستخدم (#1E293B)
└─────────────────────────────┘
```

**المؤشر النشط:** شريط عمودي 3px (`PrimaryLightBrush`) على الجانب الأيمن (وجه المحتوى في RTL).

---

## 12. الوضع الداكن (Dark Mode)

يدعم النظام الوضع الداكن عبر `ThemeManager.Toggle()`. جميع الألوان مبنية على متغيرات `*Brush` بحيث يمكن تغييرها وقت التشغيل.

| المفتاح | الفاتح | الداكن |
|--------|--------|--------|
| `BackgroundBrush` | `#F1F5F9` | `#0D1117` |
| `SurfaceBrush` | `#FFFFFF` | `#161B22` |
| `BorderBrush` | `#E2E8F0` | `#21262D` |
| `TextPrimaryBrush` | `#0F172A` | `#E6EDF3` |
| `TextSecondaryBrush` | `#64748B` | `#8B949E` |

**قاعدة:** لا ألوان مكتوبة مباشرة (`#RRGGBB`) في صفحات XAML — استخدم `{StaticResource *Brush}` دائماً.

---

## 13. إمكانية الوصول (Accessibility)

- **تباين الألوان:** الأزرار الرئيسية #0D9488 على أبيض = 4.56:1 ✅
- **AutomationProperties.Name:** مطلوب على كل حقل إدخال وزر غير واضح.
- **AutomationProperties.AutomationId:** على `PageHost` و `PageTitle` لدعم الاختبار الآلي.
- **FocusVisualStyle:** مُعطّل على الخلايا، مفعّل على الحقول والأزرار.
- **IsDefault / IsCancel:** على أزرار الحوارات.
- **FlowDirection:** `RightToLeft` على جميع النوافذ والعناصر المستقلة.

---

## 14. شريط الحالة السفلي

```
[● متصل] | قاعدة البيانات: متصل   |   الجلسة نشطة          BRQOSH ERP
  ↑ StatusDbText                      ↑ StatusSessionText
```

- الخلفية: `StatusBarBrush (#0F172A)`.
- النص: `StatusBarTextBrush (#64748B)`.
- ارتفاع: ~32px.

---

## 15. مراجع المكونات

| المفتاح | النوع | الدور |
|--------|-------|-------|
| `PrimaryButton` | Button | إجراء رئيسي، واحد لكل شاشة |
| `SecondaryButton` | Button | إجراءات ثانوية، حدود |
| `GhostButton` | Button | إجراءات خفيفة بدون حدود |
| `DangerButton` | Button | تدميري محتمل، حدود حمراء |
| `SoftDangerButton` | Button | إلغاء/حذف، خلفية حمراء ناعمة |
| `SuccessButton` | Button | ترحيل، تأكيد نهائي |
| `IconGhostButton` | Button | أيقونة مربعة 36×36 |
| `AppTextBox` | TextBox | حقل نص مع تركيز تيل |
| `AppPasswordBox` | PasswordBox | كلمة مرور |
| `AppComboBox` | ComboBox | قائمة منسدلة |
| `FieldLabel` | TextBlock | تسمية الحقل |
| `PageTitleText` | TextBlock | عنوان الصفحة الكبير |
| `PageSubtitleText` | TextBlock | وصف فرعي |
| `FormSectionTitle` | TextBlock | عنوان القسم في النموذج |
| `ToolbarCard` | Border | بطاقة شريط الأدوات |
| `ContentCard` | Border | بطاقة المحتوى |
| `SummaryCard` | Border | ملخص داكن |
| `TotalsCard` | Border | ملخص الإجماليات |
| `AuthCard` | Border | نافذة المصادقة/الإعداد |
| `ToolbarPrimaryGroup` | StackPanel | مجموعة الأزرار الرئيسية |
| `ToolbarSpacer` | Border | فاصل رأسي بين المجموعات |
| `NavItem` | Button | عنصر تنقل غير نشط |
| `NavItemActive` | Button | عنصر تنقل نشط |
| `NavGroupHeader` | TextBlock | رأس مجموعة التنقل |
| `ProfileCard` | RadioButton | بطاقة اختيار نمط العمل |
| `StatusBadge` | Border | شارة الحالة |
| `AppFocusVisual` | Style | حلقة تركيز لوحة المفاتيح (Primary 2px) |
| `NavMonogramBadge` | Border | شارة اختصار التنقل |
| `KpiMetricCard` | Border | بطاقة مؤشر أداء (Bento) |
| `KpiMonogramBadge` | Border | شارة اختصار KPI |
| `LoadingStatePanel` | Border | حالة التحميل |
| `EmptyStatePanel` | Border | حالة فارغة |

---

## 12. UI UX Pro Max — طبقة 2026-07

| البند | التطبيق |
|-------|---------|
| **Design System MASTER** | `design-system/MASTER.md` — Financial ERP / Swiss Modernism |
| **Page overrides** | `design-system/pages/{dashboard,shell,login}.md` |
| **No emoji icons** | `NavIconFactory` + `KpiMetricCardBuilder` monograms |
| **KPI Bento grid** | `DashboardPage` — WrapPanel 280px min cards |
| **Theme toggle** | نص «داكن»/«فاتح» بدل رموز تعبيرية |
| **Focus a11y** | `AppFocusVisual` على أزرار/حقول/تنقل |
| **Pre-delivery audit** | `.\scripts\ui-ux-pro-max-audit.ps1` |

### قائمة تحقق Pro Max (WPF)

- [x] لا emoji كأيقونات تنقل/KPI
- [x] Cursor=Hand على عناصر تفاعلية (موجود في Theme)
- [x] Hover على NavItem والأزرار
- [x] تباين نص ≥ 4.5:1 (Teal/Slate palette)
- [x] AutomationId على KpiGrid و PageHost
- [x] Tab order shell header (Branch · Theme)
- [x] KeyboardNavigation Local على UserControl
- [ ] Tab order toolbar لكل صفحة (~80) — اختياري تدريجي
