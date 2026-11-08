<?php
/**
 * Site footer: product/company/legal links, contact data, legal identity.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

$brqosh_email   = brqosh_setting( 'email' );
$brqosh_phone   = brqosh_setting( 'phone' );
$brqosh_cr      = brqosh_setting( 'cr_number' );
$brqosh_vat     = brqosh_setting( 'vat_number' );
$brqosh_columns = array(
	'البرنامج' => array(
		'features'    => 'المميزات',
		'sectors'     => 'القطاعات',
		'e-invoicing' => 'الفوترة الإلكترونية',
		'pricing'     => 'الأسعار',
		'downloads'   => 'التحميل والتحديثات',
	),
	'الشركة'   => array(
		'company'    => 'من نحن',
		'contact'    => 'تواصل معنا',
		'demo'       => 'جرّبه مجاناً',
		'activation' => 'التفعيل',
	),
	'قانوني'   => array(
		'privacy' => 'سياسة الخصوصية',
		'terms'   => 'الشروط والأحكام',
		'eula'    => 'اتفاقية الترخيص',
	),
);
?>
</main>
<footer class="site-footer">
	<div class="container footer-grid">
		<div class="footer-brand">
			<a class="brand brand-light" href="<?php echo esc_url( home_url( '/' ) ); ?>" aria-label="برقوش ERP">
				<span class="brand-logo"><?php echo brqosh_svg_file( 'logo' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></span><span class="brand-tag">ERP</span>
			</a>
			<p>برنامج محاسبة عربي متكامل للمنشآت في السعودية والخليج. يُطوَّر في الرياض.</p>
			<ul class="footer-contact">
				<li><?php echo brqosh_icon( 'mail' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><a href="<?php echo esc_url( 'mailto:' . $brqosh_email ); ?>"><?php echo esc_html( $brqosh_email ); ?></a></li>
				<li><?php echo brqosh_icon( 'phone' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><a dir="ltr" href="<?php echo esc_url( 'tel:' . $brqosh_phone ); ?>"><?php echo esc_html( brqosh_phone_display( $brqosh_phone ) ); ?></a></li>
				<li><?php echo brqosh_icon( 'pin' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><span><?php echo esc_html( brqosh_setting( 'address' ) ); ?></span></li>
			</ul>
		</div>
		<?php foreach ( $brqosh_columns as $brqosh_heading => $brqosh_links ) : ?>
			<div class="footer-col">
				<h2><?php echo esc_html( $brqosh_heading ); ?></h2>
				<ul>
					<?php foreach ( $brqosh_links as $brqosh_slug => $brqosh_label ) : ?>
						<li><a href="<?php echo brqosh_page_url( $brqosh_slug ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>"><?php echo esc_html( $brqosh_label ); ?></a></li>
					<?php endforeach; ?>
				</ul>
			</div>
		<?php endforeach; ?>
	</div>
	<div class="container footer-bottom">
		<p>© <?php echo esc_html( gmdate( 'Y' ) ); ?> <?php echo esc_html( brqosh_setting( 'company_name' ) ); ?>. جميع الحقوق محفوظة.</p>
		<?php if ( $brqosh_cr || $brqosh_vat ) : ?>
			<p class="footer-ids">
				<?php if ( $brqosh_cr ) : ?><span>السجل التجاري: <bdi><?php echo esc_html( $brqosh_cr ); ?></bdi></span><?php endif; ?>
				<?php if ( $brqosh_vat ) : ?><span>الرقم الضريبي: <bdi><?php echo esc_html( $brqosh_vat ); ?></bdi></span><?php endif; ?>
			</p>
		<?php endif; ?>
	</div>
</footer>
<a class="wa-float" href="<?php echo brqosh_whatsapp_url( 'السلام عليكم، أرغب بالاستفسار عن برقوش ERP' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>" target="_blank" rel="noopener" aria-label="تواصل عبر واتساب">
	<?php echo brqosh_icon( 'whatsapp' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>
</a>
<?php wp_footer(); ?>
</body>
</html>
