<?php
/**
 * Contact page (slug: contact).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

get_header();
$brqosh_email = brqosh_setting( 'email' );
$brqosh_phone = brqosh_setting( 'phone' );
get_template_part(
	'template-parts/page-hero',
	null,
	array(
		'eyebrow' => 'تواصل معنا',
		'title'   => 'نسعد بسؤالك',
		'lead'    => 'اكتب رسالتك وسنرد عليك في أقرب وقت خلال أوقات العمل.',
	)
);
?>
<section class="section">
	<div class="container form-layout">
		<aside>
			<h2>بيانات التواصل</h2>
			<ul class="contact-list" role="list">
				<li><?php echo brqosh_icon( 'whatsapp' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><span><b>واتساب</b><a href="<?php echo brqosh_whatsapp_url(); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>" target="_blank" rel="noopener" dir="ltr"><?php echo esc_html( brqosh_phone_display( $brqosh_phone ) ); ?></a></span></li>
				<li><?php echo brqosh_icon( 'phone' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><span><b>الجوال</b><a href="<?php echo esc_url( 'tel:' . $brqosh_phone ); ?>" dir="ltr"><?php echo esc_html( brqosh_phone_display( $brqosh_phone ) ); ?></a></span></li>
				<li><?php echo brqosh_icon( 'mail' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><span><b>البريد</b><a href="<?php echo esc_url( 'mailto:' . $brqosh_email ); ?>"><?php echo esc_html( $brqosh_email ); ?></a></span></li>
				<li><?php echo brqosh_icon( 'pin' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?><span><b>العنوان</b><?php echo esc_html( brqosh_setting( 'address' ) ); ?></span></li>
			</ul>
		</aside>
		<div class="form-card">
			<h2>أرسل رسالة</h2>
			<?php get_template_part( 'template-parts/lead-form', null, array( 'type' => 'contact', 'submit' => 'أرسل الرسالة' ) ); ?>
		</div>
	</div>
</section>
<?php
get_footer();
