<?php
/**
 * Fallback template (posts index, archives, search).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
get_template_part( 'template-parts/page-hero', null, array( 'title' => is_search() ? 'نتائج البحث' : wp_strip_all_tags( get_the_archive_title() ? get_the_archive_title() : 'المقالات' ) ) );
?>
<section class="section">
	<div class="container container-narrow">
		<?php if ( have_posts() ) : ?>
			<ul class="post-list" role="list">
				<?php
				while ( have_posts() ) :
					the_post();
					?>
					<li><a href="<?php the_permalink(); ?>"><h2><?php the_title(); ?></h2></a><p><?php echo esc_html( wp_trim_words( get_the_excerpt(), 30 ) ); ?></p></li>
				<?php endwhile; ?>
			</ul>
			<?php the_posts_pagination(); ?>
		<?php else : ?>
			<p>لا يوجد محتوى.</p>
		<?php endif; ?>
	</div>
</section>
<?php
get_footer();
