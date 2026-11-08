<?php
/**
 * Lead forms: rendering helpers and the admin-post handler.
 *
 * Abuse controls, in order: method + same-origin check, honeypot, signed
 * time token (rejects instant bots and tokens older than 30 days — works with
 * full-page caching, unlike nonces), per-IP rate limit, then strict field
 * validation. Valid leads are stored (inc/leads.php) and emailed.
 *
 * Responds with JSON to fetch() submissions and with a redirect otherwise,
 * so the forms work without JavaScript.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

const BRQOSH_FORM_ACTION      = 'brqosh_lead';
const BRQOSH_FORM_MIN_SECONDS = 3;
const BRQOSH_FORM_MAX_AGE     = 30 * DAY_IN_SECONDS; // Longer than any page-cache TTL.
const BRQOSH_RATE_LIMIT       = 5;
const BRQOSH_RATE_WINDOW      = HOUR_IN_SECONDS;

/**
 * Issues a signed timestamp token for a form type.
 *
 * @param string $type Form type.
 * @return string
 */
function brqosh_form_token( string $type ): string {
	$ts = (string) time();
	return $ts . '.' . hash_hmac( 'sha256', $type . '|' . $ts, wp_salt( 'nonce' ) );
}

/**
 * Verifies a token: authentic, at least BRQOSH_FORM_MIN_SECONDS old, not expired.
 *
 * @param string $type  Form type.
 * @param string $token Submitted token.
 * @return bool
 */
function brqosh_verify_form_token( string $type, string $token ): bool {
	if ( ! preg_match( '/^(\d{10})\.([a-f0-9]{64})$/', $token, $m ) ) {
		return false;
	}
	$expected = hash_hmac( 'sha256', $type . '|' . $m[1], wp_salt( 'nonce' ) );
	$age      = time() - (int) $m[1];
	return hash_equals( $expected, $m[2] ) && $age >= BRQOSH_FORM_MIN_SECONDS && $age <= BRQOSH_FORM_MAX_AGE;
}

/**
 * Prints the hidden fields every lead form needs.
 *
 * @param string $type Form type.
 */
function brqosh_form_hidden_fields( string $type ): void {
	echo '<input type="hidden" name="action" value="' . esc_attr( BRQOSH_FORM_ACTION ) . '">';
	echo '<input type="hidden" name="form_type" value="' . esc_attr( $type ) . '">';
	echo '<input type="hidden" name="form_token" value="' . esc_attr( brqosh_form_token( $type ) ) . '">';
	echo '<input type="hidden" name="return_to" value="' . esc_attr( (string) get_permalink() ) . '">';
	// Honeypot: hidden from people and assistive tech; bots fill it.
	echo '<div class="hp" aria-hidden="true"><label>الموقع الإلكتروني<input type="text" name="website" tabindex="-1" autocomplete="off"></label></div>';
}

/**
 * Status message after a no-JS redirect (?lead=sent|error|limit).
 *
 * @return string Safe HTML.
 */
function brqosh_form_status_notice(): string {
	$state = isset( $_GET['lead'] ) ? sanitize_key( wp_unslash( $_GET['lead'] ) ) : ''; // phpcs:ignore WordPress.Security.NonceVerification.Recommended
	$map   = array(
		'sent'  => array( 'ok', 'وصل طلبك، وسنتواصل معك خلال يوم عمل.' ),
		'error' => array( 'err', 'تعذّر إرسال الطلب. راجع الحقول وحاول مرة أخرى، أو تواصل معنا عبر واتساب.' ),
		'limit' => array( 'err', 'وصلنا عدد كبير من الطلبات من جهازك. حاول بعد ساعة أو تواصل معنا عبر واتساب.' ),
	);
	if ( ! isset( $map[ $state ] ) ) {
		return '<div class="form-status" role="status" aria-live="polite" hidden></div>';
	}
	return '<div class="form-status is-' . esc_attr( $map[ $state ][0] ) . '" role="status" aria-live="polite">' . esc_html( $map[ $state ][1] ) . '</div>';
}

/**
 * Validates the submitted fields for a form type.
 *
 * @param string               $type Form type.
 * @param array<string,mixed>  $in   Raw (unslashed) input.
 * @return array{0:array<string,string>,1:array<string,string>} [clean fields, errors]
 */
function brqosh_validate_lead( string $type, array $in ): array {
	$get    = static fn( string $k ): string => isset( $in[ $k ] ) && is_string( $in[ $k ] ) ? $in[ $k ] : '';
	$clean  = array();
	$errors = array();

	$require_line = static function ( string $key, int $min, int $max, string $msg ) use ( $get, &$clean, &$errors ): void {
		$v = brqosh_clean_line( $get( $key ), $max );
		if ( mb_strlen( $v ) < $min ) {
			$errors[ $key ] = $msg;
		}
		$clean[ $key ] = $v;
	};

	if ( 'activation' !== $type ) {
		$require_line( 'name', 2, 80, 'اكتب اسمك.' );
	}
	if ( 'contact' === $type ) {
		$clean['org'] = brqosh_clean_line( $get( 'org' ), 120 );
	} else {
		$require_line( 'org', 2, 120, 'اكتب اسم المنشأة.' );
	}

	$phone = brqosh_normalize_phone( $get( 'phone' ) );
	if ( '' === $phone ) {
		$errors['phone'] = 'رقم الجوال غير صحيح. مثال: 05XXXXXXXX';
	}
	$clean['phone'] = $phone;

	$email = sanitize_email( $get( 'email' ) );
	if ( '' === $email || ! is_email( $email ) ) {
		$errors['email'] = 'البريد الإلكتروني غير صحيح.';
	}
	$clean['email'] = $email;

	switch ( $type ) {
		case 'demo':
			$sectors = array_column( brqosh_sectors(), 'title' );
			$sector  = brqosh_clean_line( $get( 'sector' ), 80 );
			if ( ! in_array( $sector, $sectors, true ) && 'أخرى' !== $sector ) {
				$errors['sector'] = 'اختر نوع النشاط.';
			}
			$clean['sector'] = $sector;
			$users           = $get( 'users' );
			if ( ! in_array( $users, array( '1-2', '3-5', '6-10', '10+' ), true ) ) {
				$errors['users'] = 'اختر عدد المستخدمين.';
			}
			$clean['users']   = $users;
			$clean['message'] = brqosh_clean_text( $get( 'message' ), 1000 );
			break;
		case 'contact':
			$message = brqosh_clean_text( $get( 'message' ), 3000 );
			if ( mb_strlen( $message ) < 10 ) {
				$errors['message'] = 'اكتب رسالتك (10 أحرف على الأقل).';
			}
			$clean['message'] = $message;
			break;
		case 'activation':
			$serial = brqosh_normalize_serial( $get( 'serial' ) );
			if ( '' === $serial ) {
				$errors['serial'] = 'الرقم التسلسلي 25 رقماً في خمس مجموعات.';
			}
			$clean['serial'] = $serial;
			$code            = brqosh_normalize_request_code( $get( 'request_code' ) );
			if ( '' === $code ) {
				$errors['request_code'] = 'رقم الطلب غير صحيح. انسخه بزر النسخ من شاشة التفعيل؛ يبدأ بـ BRQ.';
			}
			$clean['request_code'] = $code;
			break;
	}

	if ( empty( $in['consent'] ) ) {
		$errors['consent'] = 'الموافقة على سياسة الخصوصية مطلوبة.';
	}

	return array( $clean, $errors );
}

/**
 * Per-IP rate limiter backed by a transient. The IP is stored only as a
 * salted hash.
 *
 * @return bool True when the request is within the limit (and counts it).
 */
function brqosh_rate_limit_ok(): bool {
	$ip  = isset( $_SERVER['REMOTE_ADDR'] ) ? sanitize_text_field( wp_unslash( $_SERVER['REMOTE_ADDR'] ) ) : 'unknown';
	$key = 'brqosh_rl_' . substr( hash_hmac( 'sha256', $ip, wp_salt( 'auth' ) ), 0, 32 );
	$hit = (int) get_transient( $key );
	if ( $hit >= BRQOSH_RATE_LIMIT ) {
		return false;
	}
	set_transient( $key, $hit + 1, BRQOSH_RATE_WINDOW );
	return true;
}

/**
 * True when Origin/Referer (if sent) point to this site.
 *
 * @return bool
 */
function brqosh_same_origin(): bool {
	$home   = wp_parse_url( home_url(), PHP_URL_HOST );
	$source = '';
	if ( ! empty( $_SERVER['HTTP_ORIGIN'] ) ) {
		$source = sanitize_text_field( wp_unslash( $_SERVER['HTTP_ORIGIN'] ) );
	} elseif ( ! empty( $_SERVER['HTTP_REFERER'] ) ) {
		$source = sanitize_text_field( wp_unslash( $_SERVER['HTTP_REFERER'] ) );
	}
	if ( '' === $source ) {
		return true; // Privacy tools strip both; the token still protects the form.
	}
	return wp_parse_url( $source, PHP_URL_HOST ) === $home;
}

/**
 * Emails a stored lead to the configured inbox.
 *
 * @param string               $type   Form type.
 * @param array<string,string> $fields Clean fields.
 * @param int                  $id     Lead post ID.
 * @return bool
 */
function brqosh_mail_lead( string $type, array $fields, int $id ): bool {
	$schema = brqosh_lead_schema()[ $type ];
	$to     = brqosh_setting( 'lead_email' );
	$to     = is_email( $to ) ? $to : (string) get_option( 'admin_email' );
	$who    = brqosh_lead_who( $fields );
	$lines  = array();
	foreach ( $schema['fields'] as $key => $label ) {
		$value = $fields[ $key ] ?? '';
		if ( 'phone' === $key && '' !== $value ) {
			$value = '+' . $value;
		}
		$lines[] = $label . ': ' . $value;
	}
	$lines[] = '';
	$lines[] = 'في لوحة التحكم: ' . admin_url( 'post.php?post=' . $id . '&action=edit' );

	$headers = array( 'Content-Type: text/plain; charset=UTF-8' );
	if ( ! empty( $fields['email'] ) ) {
		$headers[] = 'Reply-To: ' . $fields['email'];
	}
	// Subject is single-line by construction (brqosh_clean_line), no header injection.
	return wp_mail( $to, '[برقوش] ' . $schema['label'] . ' — ' . $who, implode( "\n", $lines ), $headers );
}

/**
 * Sends the response in the format the client asked for and exits.
 *
 * @param bool                 $ok     Success flag.
 * @param string               $state  sent|error|limit.
 * @param array<string,string> $errors Field errors.
 * @param string               $return Return URL.
 */
function brqosh_form_respond( bool $ok, string $state, array $errors, string $return ): void {
	$wants_json = isset( $_SERVER['HTTP_ACCEPT'] ) && str_contains( sanitize_text_field( wp_unslash( $_SERVER['HTTP_ACCEPT'] ) ), 'application/json' );
	if ( $wants_json ) {
		$messages = array(
			'sent'  => 'وصل طلبك، وسنتواصل معك خلال يوم عمل.',
			'error' => 'راجع الحقول المحددة.',
			'limit' => 'وصلنا عدد كبير من الطلبات من جهازك. حاول بعد ساعة أو تواصل معنا عبر واتساب.',
			'stale' => 'انتهت صلاحية الصفحة. حدّثها ثم أعد الإرسال.',
		);
		wp_send_json(
			array(
				'ok'      => $ok,
				'message' => $messages[ $state ] ?? $messages['error'],
				'errors'  => (object) $errors,
			),
			$ok ? 200 : ( 'limit' === $state ? 429 : 422 )
		);
	}
	$state = 'stale' === $state ? 'error' : $state;
	wp_safe_redirect( add_query_arg( 'lead', $state, $return ) . '#lead-form', 303 );
	exit;
}

/**
 * admin-post handler for every lead form (logged-in and anonymous).
 */
function brqosh_handle_lead(): void {
	$in     = wp_unslash( $_POST ); // phpcs:ignore WordPress.Security.NonceVerification.Missing -- signed token below.
	$type   = isset( $in['form_type'] ) && is_string( $in['form_type'] ) ? $in['form_type'] : '';
	$return = isset( $in['return_to'] ) && is_string( $in['return_to'] ) ? wp_validate_redirect( esc_url_raw( $in['return_to'] ), home_url( '/' ) ) : home_url( '/' );

	if ( 'POST' !== ( $_SERVER['REQUEST_METHOD'] ?? '' ) || ! isset( brqosh_lead_schema()[ $type ] ) || ! brqosh_same_origin() ) {
		brqosh_form_respond( false, 'error', array(), $return );
	}

	// Bots that fill the honeypot get a success response and nothing is stored.
	if ( ! empty( $in['website'] ) ) {
		brqosh_form_respond( true, 'sent', array(), $return );
	}

	if ( ! brqosh_verify_form_token( $type, isset( $in['form_token'] ) && is_string( $in['form_token'] ) ? $in['form_token'] : '' ) ) {
		brqosh_form_respond( false, 'stale', array(), $return );
	}

	list( $fields, $errors ) = brqosh_validate_lead( $type, $in );
	if ( $errors ) {
		brqosh_form_respond( false, 'error', $errors, $return );
	}

	if ( ! brqosh_rate_limit_ok() ) {
		brqosh_form_respond( false, 'limit', array(), $return );
	}

	$id = brqosh_store_lead( $type, $fields );
	if ( 0 === $id ) {
		brqosh_form_respond( false, 'error', array(), $return );
	}
	brqosh_mail_lead( $type, $fields, $id ); // Stored even if mail fails; visible in wp-admin.
	brqosh_form_respond( true, 'sent', array(), $return );
}
add_action( 'admin_post_nopriv_' . BRQOSH_FORM_ACTION, 'brqosh_handle_lead' );
add_action( 'admin_post_' . BRQOSH_FORM_ACTION, 'brqosh_handle_lead' );
