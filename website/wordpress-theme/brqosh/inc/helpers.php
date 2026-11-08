<?php
/**
 * Small, pure helpers shared by templates and modules.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

/**
 * Reads a theme setting (Customizer theme_mod) with the theme's default.
 *
 * Defaults live in one place, brqosh_setting_defaults(), so templates never
 * hard-code contact data or release numbers.
 *
 * @param string $key Setting key without prefix.
 * @return string
 */
function brqosh_setting( string $key ): string {
	$defaults = brqosh_setting_defaults();
	$default  = $defaults[ $key ] ?? '';
	$value    = get_theme_mod( 'brqosh_' . $key, $default );
	$value    = is_scalar( $value ) ? trim( (string) $value ) : '';
	// An empty or rejected Customizer value falls back to the default.
	return '' === $value ? $default : $value;
}

/**
 * Default values for every Customizer setting.
 *
 * Release defaults come from the newest entry in brqosh_releases(), so the
 * download page and the changelog cannot disagree out of the box.
 *
 * @return array<string,string>
 */
function brqosh_setting_defaults(): array {
	$latest = brqosh_releases()[0];
	return array(
		'email'            => 'admin@brqosh-erp.com',
		'phone'            => '+966582778405',
		'whatsapp'         => '966582778405',
		'address'          => 'الرياض، حي اليرموك، شارع برج العرب، عمارة 7373',
		'company_name'     => 'شركة برقوش',
		'cr_number'        => '',
		'vat_number'       => '',
		'price_from'       => '1,500',
		'trial_days'       => '7',
		'release_version'  => $latest['version'],
		'release_date'     => $latest['date'],
		'release_size'     => '95.4 MB',
		'release_sha256'   => '759712828e6740f3728dfaf39e2d900e7c9b6fd12827292de103901c916b7a40',
		'download_url'     => '',
		'lead_email'       => '',
		'lead_retention'   => '365',
	);
}

/**
 * Formats a phone number in E.164 for display with spaces: +966 58 277 8405.
 *
 * @param string $phone Digits with optional leading plus.
 * @return string
 */
function brqosh_phone_display( string $phone ): string {
	$digits = preg_replace( '/\D+/', '', $phone );
	if ( 12 === strlen( $digits ) && str_starts_with( $digits, '966' ) ) {
		return '+966 ' . substr( $digits, 3, 2 ) . ' ' . substr( $digits, 5, 3 ) . ' ' . substr( $digits, 8 );
	}
	return '+' . $digits;
}

/**
 * Builds a wa.me link with an optional prefilled message.
 *
 * @param string $text Message text (plain).
 * @return string Escaped URL.
 */
function brqosh_whatsapp_url( string $text = '' ): string {
	$number = preg_replace( '/\D+/', '', brqosh_setting( 'whatsapp' ) );
	$url    = 'https://wa.me/' . $number;
	if ( '' !== $text ) {
		$url .= '?text=' . rawurlencode( $text );
	}
	return esc_url( $url );
}

/**
 * URL of a page by slug, falling back to /slug/ when the page does not exist.
 *
 * @param string $slug Page slug ('' for the front page).
 * @return string Escaped URL.
 */
function brqosh_page_url( string $slug ): string {
	if ( '' === $slug ) {
		return esc_url( home_url( '/' ) );
	}
	$page = get_page_by_path( $slug );
	return esc_url( $page ? get_permalink( $page ) : home_url( '/' . $slug . '/' ) );
}

/**
 * Returns the content of a theme SVG file, cached per request.
 *
 * Files are theme-owned (never user input), so they are safe to inline.
 *
 * @param string $name File name inside assets/img without extension.
 * @return string
 */
function brqosh_svg_file( string $name ): string {
	static $cache = array();
	if ( ! isset( $cache[ $name ] ) ) {
		$path            = BRQOSH_THEME_DIR . '/assets/img/' . basename( $name ) . '.svg';
		$cache[ $name ]  = is_readable( $path ) ? (string) file_get_contents( $path ) : ''; // phpcs:ignore WordPress.WP.AlternativeFunctions.file_get_contents_file_get_contents
	}
	return $cache[ $name ];
}

/**
 * True when the current request is for the page with this slug.
 *
 * @param string $slug Page slug.
 * @return bool
 */
function brqosh_is_page( string $slug ): bool {
	return '' === $slug ? is_front_page() : is_page( $slug );
}
