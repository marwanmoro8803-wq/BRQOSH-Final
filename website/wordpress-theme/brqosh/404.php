<?php
/**
 * Not found.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
?>
<section class="page-hero page-404">
	<div class="container">
		<p class="eyebrow">404</p>
		<h1>هذه الصفحة غير موجودة</h1>
		<p class="lead">ربما نُقلت أو كُتب الرابط بشكل مختلف.</p>
		<div class="hero-actions">
			<a class="btn btn-primary" href="<?php echo esc_url( home_url( '/' ) ); ?>">الرئيسية</a>
			<a class="btn btn-ghost" href="<?php echo brqosh_page_url( 'contact' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">تواصل معنا</a>
		</div>
	</div>
</section>
<?php
get_footer();
