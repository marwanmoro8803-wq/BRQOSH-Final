<?php
/**
 * Licence activation request (slug: activation).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
get_template_part(
	'template-parts/page-hero',
	null,
	array(
		'eyebrow' => 'التفعيل',
		'title'   => 'تفعيل ترخيص برقوش ERP',
		'lead'    => 'التفعيل مربوط بجهازك ومنشأتك، ويتم دون إنترنت برمز تفعيل موقّع نرسله لك.',
	)
);
?>
<section class="section">
	<div class="container form-layout">
		<aside>
			<ol class="steps steps-compact" role="list">
				<li class="step"><span class="step-num">1</span><h3>افتح شاشة التفعيل</h3><p>من البرنامج: الإعدادات ← التفعيل، وانسخ «رقم الطلب» بزر النسخ.</p></li>
				<li class="step"><span class="step-num">2</span><h3>أرسل الطلب</h3><p>املأ النموذج برقم الطلب والرقم التسلسلي للترخيص.</p></li>
				<li class="step"><span class="step-num">3</span><h3>أدخل رمز التفعيل</h3><p>نرسل لك رمز التفعيل الموقّع (يبدأ بـ <bdi dir="ltr">BRQA1</bdi>)، الصقه في الشاشة نفسها.</p></li>
			</ol>
			<p class="note"><?php echo brqosh_icon( 'lock' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?> لا تشارك الرقم التسلسلي إلا عبر هذا النموذج أو مع فريق برقوش مباشرة. رمز التفعيل يصلح لجهاز واحد ومرة واحدة.</p>
		</aside>
		<div class="form-card">
			<h2>طلب رمز التفعيل</h2>
			<?php get_template_part( 'template-parts/lead-form', null, array( 'type' => 'activation', 'submit' => 'أرسل طلب التفعيل' ) ); ?>
		</div>
	</div>
</section>
<?php
get_footer();
