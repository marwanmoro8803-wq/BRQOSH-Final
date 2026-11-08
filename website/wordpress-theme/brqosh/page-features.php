<?php
/**
 * Features page (slug: features).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
get_template_part(
	'template-parts/page-hero',
	null,
	array(
		'eyebrow' => 'المميزات',
		'title'   => 'كل ما تحتاجه المنشأة في برنامج محاسبة واحد',
		'lead'    => 'الحسابات والفوترة والمخزون والرواتب والتقارير في قاعدة بيانات واحدة على أجهزتك، بواجهة عربية من اليمين إلى اليسار وإنجليزية عند الحاجة.',
	)
);
?>
<section class="section" aria-labelledby="mods">
	<div class="container">
		<div class="section-head">
			<h2 id="mods">الأقسام</h2>
			<p>تُفعَّل حسب نشاطك وترخيصك. الحسابات العامة هي النواة، ولا تُعطَّل أبداً.</p>
		</div>
		<?php get_template_part( 'template-parts/feature-cards', null, array( 'items' => brqosh_modules(), 'numbered' => true, 'cols' => 3 ) ); ?>
	</div>
</section>

<section class="section section-sand" aria-labelledby="einv">
	<div class="container split">
		<div>
			<p class="eyebrow">الفوترة الإلكترونية</p>
			<h2 id="einv">فاتورة موقّعة برمز QR من أول يوم</h2>
			<p>يُنتج البرنامج فواتير إلكترونية وفق متطلبات المرحلة الثانية (الربط والتكامل): ملف UBL 2.1 موقّع، ورمز QR، وتسلسل لا ينقطع حتى مع عدة محطات.</p>
			<a class="link-arrow" href="<?php echo brqosh_page_url( 'e-invoicing' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">تفاصيل الفوترة الإلكترونية <?php echo brqosh_icon( 'arrow' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></a>
		</div>
		<?php get_template_part( 'template-parts/feature-cards', null, array( 'items' => array_slice( brqosh_einvoice_points(), 0, 4 ), 'cols' => 2 ) ); ?>
	</div>
</section>

<section class="section" aria-labelledby="sec">
	<div class="container">
		<div class="section-head">
			<h2 id="sec">الأمان والتشغيل</h2>
		</div>
		<?php get_template_part( 'template-parts/feature-cards', null, array( 'items' => brqosh_trust_points(), 'cols' => 3 ) ); ?>
	</div>
</section>

<section class="section section-sand" aria-labelledby="deploy">
	<div class="container">
		<div class="section-head">
			<h2 id="deploy">طريقة التشغيل التي تناسبك</h2>
		</div>
		<ul class="cards cols-2" role="list">
			<li class="card">
				<div class="card-top"><span class="card-icon"><?php echo brqosh_icon( 'windows' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></span></div>
				<h3>جهاز واحد</h3>
				<p>تنصيب على جهاز ويندوز واحد بقاعدة بيانات محلية — مناسب للمنشآت الصغيرة والمكاتب.</p>
			</li>
			<li class="card">
				<div class="card-top"><span class="card-icon"><?php echo brqosh_icon( 'server' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></span></div>
				<h3>شبكة المنشأة</h3>
				<p>قاعدة PostgreSQL على خادم المنشأة يتصل بها عدة مستخدمين وفروع، مع صلاحيات لكل مستخدم ونسخ احتياطي مركزي.</p>
			</li>
		</ul>
	</div>
</section>
<?php
get_template_part( 'template-parts/cta' );
get_footer();
