<?php
/**
 * Pricing page (slug: pricing).
 *
 * Plans mirror the live site; licence terms mirror LicensePlan.cs
 * (annual 365 days or lifetime). Final price is set by written quote.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
$brqosh_price = brqosh_setting( 'price_from' );
$brqosh_plans = array(
	array(
		'name'     => 'الأساسية',
		'note'     => 'تبدأ من',
		'price'    => $brqosh_price,
		'featured' => false,
		'points'   => array( 'مستخدمان', 'فرع واحد', 'الحسابات والمبيعات والمشتريات', 'الفوترة الإلكترونية وضريبة 15%', 'التقارير المالية' ),
		'cta'      => array( 'demo', 'ابدأ التجربة' ),
	),
	array(
		'name'     => 'المتقدمة',
		'note'     => 'حسب المنشأة',
		'price'    => '',
		'featured' => true,
		'points'   => array( 'كل ما في الأساسية', '5 مستخدمين', 'حتى 3 فروع', 'المخزون والمستودعات', 'الخزينة والتسوية البنكية' ),
		'cta'      => array( 'contact', 'اطلب عرض سعر' ),
	),
	array(
		'name'     => 'المؤسسات',
		'note'     => 'حسب الطلب',
		'price'    => '',
		'featured' => false,
		'points'   => array( 'مستخدمون وفروع حسب الحاجة', 'نقاط بيع ورواتب وتصنيع', 'أقسام القطاعات', 'تنصيب على خادم المنشأة', 'دعم مخصص' ),
		'cta'      => array( 'contact', 'تواصل معنا' ),
	),
);
get_template_part(
	'template-parts/page-hero',
	null,
	array(
		'eyebrow' => 'الأسعار',
		'title'   => 'الأسعار تبدأ من ' . $brqosh_price . ' ريال حسب المنشأة',
		'lead'    => 'يتحدد السعر بحجم منشأتك وعدد المستخدمين والفروع والأقسام التي تحتاجها. الأسعار لا تشمل ضريبة القيمة المضافة 15%. ابدأ بتجربة مجانية كاملة.',
	)
);
?>
<section class="section" aria-label="الباقات">
	<div class="container">
		<ul class="plans" role="list">
			<?php foreach ( $brqosh_plans as $brqosh_plan ) : ?>
				<li class="plan<?php echo $brqosh_plan['featured'] ? ' is-featured' : ''; ?>">
					<?php if ( $brqosh_plan['featured'] ) : ?><span class="plan-flag">الأكثر طلباً</span><?php endif; ?>
					<h2><?php echo esc_html( $brqosh_plan['name'] ); ?></h2>
					<p class="plan-price">
						<small><?php echo esc_html( $brqosh_plan['note'] ); ?></small>
						<?php if ( '' !== $brqosh_plan['price'] ) : ?>
							<strong><bdi><?php echo esc_html( $brqosh_plan['price'] ); ?></bdi></strong> <span>ر.س</span>
						<?php endif; ?>
					</p>
					<ul class="ticks" role="list">
						<?php foreach ( $brqosh_plan['points'] as $brqosh_point ) : ?>
							<li><?php echo brqosh_icon( 'check' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><?php echo esc_html( $brqosh_point ); ?></li>
						<?php endforeach; ?>
					</ul>
					<a class="btn <?php echo $brqosh_plan['featured'] ? 'btn-primary' : 'btn-ghost'; ?> btn-block" href="<?php echo brqosh_page_url( $brqosh_plan['cta'][0] ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>"><?php echo esc_html( $brqosh_plan['cta'][1] ); ?></a>
				</li>
			<?php endforeach; ?>
		</ul>
		<p class="note">الأسعار المعروضة استرشادية وقد تتغير؛ السعر النهائي يُثبت في عرض السعر والاتفاقية المكتوبة.</p>
	</div>
</section>

<section class="section section-sand" aria-labelledby="lic">
	<div class="container">
		<div class="section-head"><h2 id="lic">نوع الترخيص</h2></div>
		<ul class="cards cols-3" role="list">
			<li class="card"><div class="card-top"><span class="card-icon"><?php echo brqosh_icon( 'clock' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></span></div><h3>تجربة <?php echo esc_html( brqosh_setting( 'trial_days' ) ); ?> أيام</h3><p>نسخة كاملة بكل الأقسام تبدأ تلقائياً عند أول تشغيل، وبياناتك تبقى بعد التفعيل.</p></li>
			<li class="card"><div class="card-top"><span class="card-icon"><?php echo brqosh_icon( 'backup' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></span></div><h3>سنوي</h3><p>ترخيص لمدة سنة يُجدَّد، ويُنبّهك البرنامج قبل انتهائه بثلاثين يوماً.</p></li>
			<li class="card"><div class="card-top"><span class="card-icon"><?php echo brqosh_icon( 'key' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></span></div><h3>دائم</h3><p>ترخيص لا ينتهي، مربوط بجهازك ومنشأتك، ويمكن تفعيله دون إنترنت.</p></li>
		</ul>
	</div>
</section>
<?php
get_template_part( 'template-parts/faq' );
get_template_part( 'template-parts/cta' );
get_footer();
