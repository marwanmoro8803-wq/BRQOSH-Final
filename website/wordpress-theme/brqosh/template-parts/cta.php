<?php
/**
 * Closing call to action shared by marketing pages.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;
?>
<section class="cta-band" aria-labelledby="cta-title">
	<div class="container cta-inner">
		<div>
			<h2 id="cta-title">جرّب برقوش ERP على أجهزتك</h2>
			<p>نسخة تجريبية كاملة لمدة <?php echo esc_html( brqosh_setting( 'trial_days' ) ); ?> أيام بكل الأقسام، ونساعدك في التنصيب وتجهيز دليل الحسابات.</p>
		</div>
		<div class="cta-actions">
			<a class="btn btn-light" href="<?php echo brqosh_page_url( 'demo' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">ابدأ التجربة المجانية</a>
			<a class="btn btn-outline-light" href="<?php echo brqosh_whatsapp_url( 'السلام عليكم، أرغب بتجربة برقوش ERP' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>" target="_blank" rel="noopener"><?php echo brqosh_icon( 'whatsapp' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?> تحدث معنا عبر واتساب</a>
		</div>
	</div>
</section>
