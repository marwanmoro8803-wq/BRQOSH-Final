<?php
/**
 * Card grid. Args: items (icon,title,text[,points]), numbered (bool), cols (2|3|4).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

$brqosh_items    = $args['items'] ?? array();
$brqosh_numbered = ! empty( $args['numbered'] );
$brqosh_cols     = in_array( (int) ( $args['cols'] ?? 3 ), array( 2, 3, 4 ), true ) ? (int) $args['cols'] : 3;
?>
<ul class="cards cols-<?php echo esc_attr( (string) $brqosh_cols ); ?>" role="list">
	<?php foreach ( $brqosh_items as $brqosh_i => $brqosh_item ) : ?>
		<li class="card reveal">
			<div class="card-top">
				<span class="card-icon"><?php echo brqosh_icon( $brqosh_item['icon'] ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></span>
				<?php if ( $brqosh_numbered ) : ?>
					<span class="card-num"><?php echo esc_html( str_pad( (string) ( $brqosh_i + 1 ), 2, '0', STR_PAD_LEFT ) ); ?></span>
				<?php endif; ?>
			</div>
			<h3><?php echo esc_html( $brqosh_item['title'] ); ?></h3>
			<p><?php echo esc_html( $brqosh_item['text'] ); ?></p>
			<?php if ( ! empty( $brqosh_item['points'] ) ) : ?>
				<ul class="ticks" role="list">
					<?php foreach ( $brqosh_item['points'] as $brqosh_point ) : ?>
						<li><?php echo brqosh_icon( 'check' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><?php echo esc_html( $brqosh_point ); ?></li>
					<?php endforeach; ?>
				</ul>
			<?php endif; ?>
		</li>
	<?php endforeach; ?>
</ul>
