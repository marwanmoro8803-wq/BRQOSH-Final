<?php
/**
 * About page (slug: company).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
get_template_part(
	'template-parts/page-hero',
	null,
	array(
		'eyebrow' => 'من نحن',
		'title'   => brqosh_setting( 'company_name' ),
		'lead'    => 'نطوّر برقوش ERP في الرياض لنمنح المنشآت السعودية والخليجية برنامج محاسبة عربياً حقيقياً: يتبع الأنظمة المحلية، ويحفظ البيانات على أجهزة المنشأة، ويُختبر كل رقم فيه قبل أن يصل إلى العميل.',
	)
);
$brqosh_values = array(
	array( 'icon' => 'ledger', 'title' => 'الدقة قبل السرعة', 'text' => 'لا يُرحَّل قيد غير متوازن، ولا تُعدَّل فترة مقفلة، وكل تغيير مسجّل في سجل التدقيق.' ),
	array( 'icon' => 'lock', 'title' => 'البيانات ملك المنشأة', 'text' => 'قاعدة البيانات على أجهزتكم، لا على خوادمنا، وتصدير بياناتكم متاح دائماً.' ),
	array( 'icon' => 'shield', 'title' => 'صراحة في الادعاءات', 'text' => 'نقول ما يفعله البرنامج فعلاً، ونوثّق ما بقي، ولا نعد بما لم نختبره.' ),
	array( 'icon' => 'globe', 'title' => 'عربي أولاً', 'text' => 'الواجهة والمصطلحات والتقارير مصمّمة بالعربية من اليمين إلى اليسار، والإنجليزية لغة ثانية كاملة.' ),
);
?>
<section class="section" aria-labelledby="vals">
	<div class="container">
		<div class="section-head"><h2 id="vals">مبادئنا</h2></div>
		<?php get_template_part( 'template-parts/feature-cards', null, array( 'items' => $brqosh_values, 'cols' => 4 ) ); ?>
	</div>
</section>
<section class="section section-sand" aria-labelledby="eng">
	<div class="container split">
		<div>
			<p class="eyebrow">كيف نبني</p>
			<h2 id="eng">برنامج يُختبر كما تُدقَّق الدفاتر</h2>
			<p>كل إصدار يمر بآلاف الاختبارات الآلية، منها سنة مالية كاملة من أول قيد حتى الإقفال تُطابَق قوائمها مع أرقام محسوبة يدوياً، قبل أن يصل إلى أي عميل.</p>
		</div>
		<div>
			<dl class="release-meta">
				<div><dt>المنصة</dt><dd>‎.NET 10 · Windows</dd></div>
				<div><dt>قاعدة البيانات</dt><dd>PostgreSQL</dd></div>
				<div><dt>المقر</dt><dd>الرياض</dd></div>
			</dl>
		</div>
	</div>
</section>
<?php
get_template_part( 'template-parts/cta' );
get_footer();
