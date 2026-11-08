<?php
/**
 * Generic page (legal pages and any page without a dedicated template).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
while ( have_posts() ) :
	the_post();
	get_template_part( 'template-parts/page-hero', null, array( 'title' => get_the_title() ) );
	?>
	<section class="section">
		<article class="container container-narrow prose">
			<?php the_content(); ?>
			<p class="updated">آخر تحديث: <time datetime="<?php echo esc_attr( get_the_modified_date( 'c' ) ); ?>"><?php echo esc_html( get_the_modified_date( 'Y-m-d' ) ); ?></time></p>
		</article>
	</section>
	<?php
endwhile;
get_footer();
