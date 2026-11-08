<?php
/**
 * Lead form. Args: type (demo|contact|activation), submit (button label).
 *
 * Field names match brqosh_validate_lead(); errors from the JSON response are
 * attached by assets/js/main.js to the element with id "err-<field>".
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

$brqosh_type   = $args['type'] ?? 'contact';
$brqosh_submit = $args['submit'] ?? 'إرسال';

/**
 * Prints one labelled input with its error slot.
 *
 * @param string $name  Field name.
 * @param string $label Label.
 * @param array  $attrs Extra attributes (type, autocomplete, inputmode, dir, placeholder, required, pattern).
 */
$brqosh_field = static function ( string $name, string $label, array $attrs = array() ): void {
	$attrs = array_merge( array( 'type' => 'text', 'required' => true ), $attrs );
	$html  = '';
	foreach ( $attrs as $k => $v ) {
		if ( true === $v ) {
			$html .= ' ' . esc_attr( $k );
		} elseif ( false !== $v ) {
			$html .= ' ' . esc_attr( $k ) . '="' . esc_attr( (string) $v ) . '"';
		}
	}
	$id = 'f-' . $name;
	echo '<div class="field"><label for="' . esc_attr( $id ) . '">' . esc_html( $label ) . ( ! empty( $attrs['required'] ) ? ' <span class="req" aria-hidden="true">*</span>' : '' ) . '</label>';
	echo '<input id="' . esc_attr( $id ) . '" name="' . esc_attr( $name ) . '" aria-describedby="err-' . esc_attr( $name ) . '"' . $html . '>'; // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped -- attributes escaped above.
	echo '<p class="field-error" id="err-' . esc_attr( $name ) . '"></p></div>';
};
?>
<form id="lead-form" class="lead-form" method="post" action="<?php echo esc_url( admin_url( 'admin-post.php' ) ); ?>" novalidate data-lead-form>
	<?php brqosh_form_hidden_fields( $brqosh_type ); ?>
	<?php echo brqosh_form_status_notice(); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped -- escaped inside. ?>
	<div class="form-grid">
		<?php
		if ( 'activation' !== $brqosh_type ) {
			$brqosh_field( 'name', 'الاسم', array( 'autocomplete' => 'name', 'maxlength' => 80 ) );
		}
		$brqosh_field( 'org', 'اسم المنشأة', array( 'autocomplete' => 'organization', 'maxlength' => 120, 'required' => 'contact' !== $brqosh_type ) );

		if ( 'activation' === $brqosh_type ) {
			$brqosh_field( 'serial', 'الرقم التسلسلي للترخيص', array( 'dir' => 'ltr', 'inputmode' => 'numeric', 'autocomplete' => 'off', 'placeholder' => '00000-00000-00000-00000-00000', 'maxlength' => 40, 'spellcheck' => 'false' ) );
			$brqosh_field( 'request_code', 'رقم الطلب (مفتاح الجهاز)', array( 'dir' => 'ltr', 'autocomplete' => 'off', 'placeholder' => 'BRQ-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXX', 'maxlength' => 48, 'spellcheck' => 'false', 'autocapitalize' => 'characters' ) );
		}

		$brqosh_field( 'phone', 'رقم الجوال', array( 'type' => 'tel', 'dir' => 'ltr', 'inputmode' => 'tel', 'autocomplete' => 'tel', 'placeholder' => '05XXXXXXXX', 'maxlength' => 20 ) );
		$brqosh_field( 'email', 'البريد الإلكتروني', array( 'type' => 'email', 'dir' => 'ltr', 'autocomplete' => 'email', 'maxlength' => 120 ) );

		if ( 'demo' === $brqosh_type ) :
			?>
			<div class="field">
				<label for="f-sector">نوع النشاط <span class="req" aria-hidden="true">*</span></label>
				<select id="f-sector" name="sector" required aria-describedby="err-sector">
					<option value="">اختر…</option>
					<?php foreach ( brqosh_sectors() as $brqosh_sector ) : ?>
						<option><?php echo esc_html( $brqosh_sector['title'] ); ?></option>
					<?php endforeach; ?>
					<option>أخرى</option>
				</select>
				<p class="field-error" id="err-sector"></p>
			</div>
			<div class="field">
				<label for="f-users">عدد المستخدمين المتوقع <span class="req" aria-hidden="true">*</span></label>
				<select id="f-users" name="users" required aria-describedby="err-users">
					<option value="">اختر…</option>
					<option value="1-2">1 – 2</option>
					<option value="3-5">3 – 5</option>
					<option value="6-10">6 – 10</option>
					<option value="10+">أكثر من 10</option>
				</select>
				<p class="field-error" id="err-users"></p>
			</div>
			<?php
		endif;

		if ( 'activation' !== $brqosh_type ) :
			$brqosh_required = 'contact' === $brqosh_type;
			?>
			<div class="field field-wide">
				<label for="f-message"><?php echo 'contact' === $brqosh_type ? 'رسالتك' : 'ملاحظات (اختياري)'; ?><?php echo $brqosh_required ? ' <span class="req" aria-hidden="true">*</span>' : ''; ?></label>
				<textarea id="f-message" name="message" rows="4" maxlength="3000" aria-describedby="err-message"<?php echo $brqosh_required ? ' required minlength="10"' : ''; ?>></textarea>
				<p class="field-error" id="err-message"></p>
			</div>
		<?php endif; ?>

		<div class="field field-wide field-check">
			<label><input type="checkbox" name="consent" value="1" required aria-describedby="err-consent"> أوافق على معالجة بياناتي للتواصل معي وفق <a href="<?php echo brqosh_page_url( 'privacy' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">سياسة الخصوصية</a>.</label>
			<p class="field-error" id="err-consent"></p>
		</div>
	</div>
	<div class="form-actions">
		<button class="btn btn-primary" type="submit" data-submit><?php echo esc_html( $brqosh_submit ); ?></button>
		<a class="btn btn-ghost" href="<?php echo brqosh_whatsapp_url(); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>" target="_blank" rel="noopener"><?php echo brqosh_icon( 'whatsapp' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?> أو راسلنا عبر واتساب</a>
	</div>
</form>
