<?php
/**
 * Inner-page hero. Args: eyebrow, title, lead (all plain text).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;
?>
<section class="page-hero">
	<div class="container">
		<?php if ( ! empty( $args['eyebrow'] ) ) : ?>
			<p class="eyebrow"><?php echo esc_html( $args['eyebrow'] ); ?></p>
		<?php endif; ?>
		<h1><?php echo esc_html( $args['title'] ?? get_the_title() ); ?></h1>
		<?php if ( ! empty( $args['lead'] ) ) : ?>
			<p class="lead"><?php echo esc_html( $args['lead'] ); ?></p>
		<?php endif; ?>
	</div>
</section>
