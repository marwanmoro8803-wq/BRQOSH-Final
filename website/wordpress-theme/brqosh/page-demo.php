<?php
/**
 * Free trial request (slug: demo).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
$brqosh_days = brqosh_setting( 'trial_days' );
get_template_part(
	'template-parts/page-hero',
	null,
	array(
		'eyebrow' => 'تجربة مجانية',
		'title'   => 'جرّب برقوش ERP مجاناً لمدة ' . $brqosh_days . ' أيام',
		'lead'    => 'نسخة كاملة بكل الأقسام على جهازك. سجّل بياناتك ونرسل لك رابط التحميل ونساعدك في الإعداد.',
	)
);
?>
<section class="section">
	<div class="container form-layout">
		<aside>
			<h2>ماذا يحدث بعد التسجيل؟</h2>
			<ul class="ticks" role="list">
				<li><?php echo brqosh_icon( 'check' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>نتواصل معك خلال يوم عمل برابط التحميل.</li>
				<li><?php echo brqosh_icon( 'check' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>نساعدك في التنصيب وتجهيز دليل الحسابات والأرصدة الافتتاحية.</li>
				<li><?php echo brqosh_icon( 'check' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>تبدأ التجربة تلقائياً عند أول تشغيل، بلا بطاقة دفع.</li>
				<li><?php echo brqosh_icon( 'check' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>بياناتك تبقى كما هي عند التفعيل، فلا تعيد الإدخال.</li>
			</ul>
		</aside>
		<div class="form-card">
			<h2>سجّل للتجربة</h2>
			<?php get_template_part( 'template-parts/lead-form', null, array( 'type' => 'demo', 'submit' => 'أرسل طلب التجربة' ) ); ?>
		</div>
	</div>
</section>
<?php
get_footer();
