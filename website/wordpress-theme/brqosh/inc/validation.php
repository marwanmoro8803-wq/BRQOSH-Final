<?php
/**
 * Pure input validators for the lead forms (no WordPress dependency, so they
 * are unit-tested with plain PHP in website/tests/).
 *
 * The licence formats mirror the desktop product:
 * - Serial: 25 digits in five groups (src/Brqosh.Infrastructure/Licensing/NumericLicense.cs).
 * - Request code: "BRQ-" + Base32 of a 16-byte device hash + 2 check chars
 *   (src/Brqosh.Infrastructure/Licensing/OfflineActivation.cs, RequestCode).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

const BRQOSH_BASE32_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

/**
 * Normalises a Gulf mobile number to E.164 digits (no plus), or '' if invalid.
 *
 * Accepts Saudi local forms (05XXXXXXXX, 5XXXXXXXX) and international numbers
 * with 00 or + prefixes, 8–15 digits. Arabic-Indic digits are converted.
 *
 * @param string $raw User input.
 * @return string
 */
function brqosh_normalize_phone( string $raw ): string {
	$raw    = strtr( $raw, array_combine( array( '٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩', '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' ), array( '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' ) ) );
	$digits = (string) preg_replace( '/[\s\-().]+/', '', $raw );
	if ( ! preg_match( '/^\+?\d+$/', $digits ) ) {
		return '';
	}
	$digits = ltrim( $digits, '+' );
	if ( str_starts_with( $digits, '00' ) ) {
		$digits = substr( $digits, 2 );
	}
	if ( preg_match( '/^05\d{8}$/', $digits ) ) {
		return '966' . substr( $digits, 1 );
	}
	if ( preg_match( '/^5\d{8}$/', $digits ) ) {
		return '966' . $digits;
	}
	if ( str_starts_with( $digits, '966' ) ) {
		return preg_match( '/^9665\d{8}$/', $digits ) ? $digits : '';
	}
	return preg_match( '/^[1-9]\d{7,14}$/', $digits ) ? $digits : '';
}

/**
 * Normalises a 25-digit licence serial to 00000-00000-00000-00000-00000, or ''.
 *
 * @param string $raw User input.
 * @return string
 */
function brqosh_normalize_serial( string $raw ): string {
	$digits = (string) preg_replace( '/[\s-]+/', '', $raw );
	if ( ! preg_match( '/^\d{25}$/', $digits ) ) {
		return '';
	}
	return implode( '-', str_split( $digits, 5 ) );
}

/**
 * Base32 encoding exactly as RequestCode.Base32 in the desktop app.
 *
 * @param string $bytes Binary string.
 * @return string
 */
function brqosh_base32_encode( string $bytes ): string {
	$out    = '';
	$buffer = 0;
	$bits   = 0;
	$len    = strlen( $bytes );
	for ( $i = 0; $i < $len; $i++ ) {
		$buffer = ( ( $buffer << 8 ) | ord( $bytes[ $i ] ) ) & 0xFFFF;
		$bits  += 8;
		while ( $bits >= 5 ) {
			$out  .= BRQOSH_BASE32_ALPHABET[ ( $buffer >> ( $bits - 5 ) ) & 31 ];
			$bits -= 5;
		}
	}
	if ( $bits > 0 ) {
		$out .= BRQOSH_BASE32_ALPHABET[ ( $buffer << ( 5 - $bits ) ) & 31 ];
	}
	return $out;
}

/**
 * Base32 decoding exactly as RequestCode.FromBase32 in the desktop app.
 *
 * @param string $text   Base32 text.
 * @param int    $length Expected byte length.
 * @return string|null Binary string, or null on an invalid character/short input.
 */
function brqosh_base32_decode( string $text, int $length ): ?string {
	$out    = '';
	$buffer = 0;
	$bits   = 0;
	$len    = strlen( $text );
	for ( $i = 0; $i < $len; $i++ ) {
		$v = strpos( BRQOSH_BASE32_ALPHABET, $text[ $i ] );
		if ( false === $v ) {
			return null;
		}
		$buffer = ( ( $buffer << 5 ) | $v ) & 0xFFFF;
		$bits  += 5;
		if ( $bits >= 8 ) {
			$out  .= chr( ( $buffer >> ( $bits - 8 ) ) & 0xFF );
			$bits -= 8;
		}
	}
	return strlen( $out ) >= $length ? substr( $out, 0, $length ) : null;
}

/**
 * Validates an activation request code (with its two check characters) and
 * returns it canonically grouped (BRQ-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXX), or ''.
 *
 * @param string $raw User input.
 * @return string
 */
function brqosh_normalize_request_code( string $raw ): string {
	$text = strtoupper( str_replace( array( '-', ' ' ), '', trim( $raw ) ) );
	if ( ! str_starts_with( $text, 'BRQ' ) ) {
		return '';
	}
	$body = substr( $text, 3 );
	if ( 28 !== strlen( $body ) ) {
		return '';
	}
	$bytes = brqosh_base32_decode( substr( $body, 0, 26 ), 16 );
	if ( null === $bytes ) {
		return '';
	}
	$check = substr( brqosh_base32_encode( substr( hash( 'sha256', $bytes, true ), 0, 1 ) ), 0, 2 );
	if ( ! hash_equals( $check, substr( $body, 26 ) ) ) {
		return '';
	}
	return 'BRQ-' . implode( '-', str_split( $body, 5 ) );
}

/**
 * Builds a request code from a 32-hex-char device hash (used by tests and to
 * document the format; mirrors RequestCode.FromDeviceHash).
 *
 * @param string $device_hash_hex 32 hex characters.
 * @return string
 */
function brqosh_request_code_from_hash( string $device_hash_hex ): string {
	$bytes = (string) hex2bin( $device_hash_hex );
	$body  = brqosh_base32_encode( $bytes ) . substr( brqosh_base32_encode( substr( hash( 'sha256', $bytes, true ), 0, 1 ) ), 0, 2 );
	return 'BRQ-' . implode( '-', str_split( $body, 5 ) );
}

/**
 * Trims a single-line text field, removes control characters and caps length.
 *
 * @param string $raw Input.
 * @param int    $max Maximum characters.
 * @return string
 */
function brqosh_clean_line( string $raw, int $max ): string {
	$raw = (string) preg_replace( '/[\x00-\x1F\x7F]+/u', ' ', $raw );
	$raw = trim( (string) preg_replace( '/\s+/u', ' ', $raw ) );
	return mb_substr( $raw, 0, $max );
}

/**
 * Cleans a multi-line message: keeps newlines, strips other control chars and tags.
 *
 * @param string $raw Input.
 * @param int    $max Maximum characters.
 * @return string
 */
function brqosh_clean_text( string $raw, int $max ): string {
	$raw = strip_tags( $raw );
	$raw = (string) preg_replace( '/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]+/u', '', $raw );
	$raw = str_replace( "\r\n", "\n", $raw );
	return trim( mb_substr( $raw, 0, $max ) );
}
