<?php
/**
 * FAQ accordion built on <details> (works without JavaScript).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;
?>
<section class="section" aria-labelledby="faq-title">
	<div class="container container-narrow">
		<div class="section-head">
			<p class="eyebrow">أسئلة شائعة</p>
			<h2 id="faq-title">قبل أن تبدأ</h2>
		</div>
		<div class="faq">
			<?php foreach ( brqosh_faqs() as $brqosh_faq ) : ?>
				<details>
					<summary><?php echo esc_html( $brqosh_faq['q'] ); ?><?php echo brqosh_icon( 'plus', 'faq-icon' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></summary>
					<p><?php echo esc_html( $brqosh_faq['a'] ); ?></p>
				</details>
			<?php endforeach; ?>
		</div>
	</div>
</section>
