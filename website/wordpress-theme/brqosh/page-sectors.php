<?php
/**
 * Sectors page (slug: sectors).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
get_template_part(
	'template-parts/page-hero',
	null,
	array(
		'eyebrow' => 'القطاعات',
		'title'   => 'برنامج واحد يتكلم لغة نشاطك',
		'lead'    => 'عند إنشاء المنشأة تختار نوع النشاط، فيفعّل برقوش ERP الأقسام والحسابات المناسبة له فوق النواة المحاسبية نفسها — فتبقى القوائم المالية موحّدة مهما اختلف النشاط.',
	)
);
?>
<section class="section" aria-label="القطاعات المدعومة">
	<div class="container">
		<?php get_template_part( 'template-parts/feature-cards', null, array( 'items' => brqosh_sectors(), 'cols' => 4 ) ); ?>
	</div>
</section>

<section class="section section-sand" aria-labelledby="mix">
	<div class="container split">
		<div>
			<p class="eyebrow">أكثر من نشاط؟</p>
			<h2 id="mix">منشأة تبيع وتصنّع وتقاول</h2>
			<p>تُفعَّل أقسام أكثر من قطاع في المنشأة نفسها، أو تُفصل الأنشطة في شركات وفروع ثم تُجمَّع في قوائم موحّدة.</p>
		</div>
		<div>
			<a class="btn btn-primary" href="<?php echo brqosh_page_url( 'demo' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">جرّبه على نشاطك</a>
		</div>
	</div>
</section>
<?php
get_template_part( 'template-parts/cta' );
get_footer();
