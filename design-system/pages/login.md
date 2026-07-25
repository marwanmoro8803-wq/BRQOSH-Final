# Page Override — Login (تسجيل الدخول)

**Pattern:** Split hero + AuthCard (Luxury design system)

## Layout

- Hero gradient: `HeroPanelGradient` · gold accent divider 4×80px
- Auth panel: `AuthCard` + `ElevatedShadow` · width 460px
- Tabs: `AuthTabButton` — **تسجيل الدخول** | **إنشاء حساب**

## Login form

| Element | Style / behavior |
|---------|------------------|
| Username | `AppTextBox` |
| Password | `AppPasswordBox` + `PasswordRevealButton` (eye toggle) |
| Remember me | `CheckBox` — persists username in `%AppData%/BRQOSH/login-prefs.json` |
| Forgot password | `LinkButton` → `ForgotPasswordWindow` |
| MFA | Collapsed until required |
| Submit | `PrimaryButton` full-width |
| Error | `DangerBrush` 13px |

## Register form (طلب حساب مستخدم — **ليس** إنشاء منشأة)

> **G-69:** هذا التبويب يُنشئ **مستخدماً** داخل المنشأة الحالية فقط. إنشاء منشأة جديدة عبر `SetupWizardWindow` (أول تشغيل) أو `OrganizationProvisionerWindow` (مدير — 🔜 G-69).

- Display name · username · WhatsApp mobile · password + confirm (eye toggles)
- **إرسال رمز التحقق على واتساب** → `WhatsAppOtpService` (local outbox in dev)
- OTP 6 digits · creates inactive `Viewer` pending admin activation
- Label مقترح: **«طلب حساب مستخدم»** بدل «إنشاء حساب» لتجنب اللبس

## Password recovery (`ForgotPasswordWindow`)

1. Username → send WhatsApp OTP to registered `MobilePhoneE164`
2. OTP + new password → `WhatsAppOtpService.ResetPasswordAsync`
3. ⏭ Live WhatsApp Business API = external gate

## Contrast

- Hero white on dark teal: AA
- Form labels `TextSecondary` on `Surface`: AA

## Lifecycle

- Step 2 (Identity): login · MFA · lockout · **remember me · recovery · self-register**
