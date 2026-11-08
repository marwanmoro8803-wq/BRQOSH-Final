<?php
/**
 * Unit tests for inc/validation.php (plain PHP, no WordPress, no framework).
 *
 * Request-code vectors were produced by running the desktop app's own
 * RequestCode class (src/Brqosh.Infrastructure/Licensing/OfflineActivation.cs)
 * on .NET 10, so the website accepts exactly what the program displays.
 *
 * Run: php website/tests/validation-test.php
 */

define( 'ABSPATH', __DIR__ );
require __DIR__ . '/../wordpress-theme/brqosh/inc/validation.php';

$failures = 0;
$count    = 0;
function check( string $name, $actual, $expected ): void {
	global $failures, $count;
	$count++;
	if ( $actual !== $expected ) {
		$failures++;
		fwrite( STDERR, "FAIL {$name}: expected " . var_export( $expected, true ) . ', got ' . var_export( $actual, true ) . "\n" );
	}
}

// Vectors from the C# RequestCode.FromDeviceHash (see header).
$vectors = array(
	'00000000000000000000000000000000' => 'BRQ-AAAAA-AAAAA-AAAAA-AAAAA-AAAAA-AG6',
	'ffffffffffffffffffffffffffffffff' => 'BRQ-99999-99999-99999-99999-99999-6MJ',
	'0123456789abcdef0123456789abcdef' => 'BRQ-AETWL-36KXR-G88AK-DJXV2-VL8P7-6EJ',
	'9f86d081884c7d659a2feaa0c55ad015' => 'BRQ-V8DPB-ANJKT-8YMGT-R7LSN-LYYSC-WCN',
);
foreach ( $vectors as $hash => $code ) {
	check( "encode {$hash}", brqosh_request_code_from_hash( $hash ), $code );
	check( "accept {$code}", brqosh_normalize_request_code( $code ), $code );
	check( "accept lowercase/no dashes {$code}", brqosh_normalize_request_code( strtolower( str_replace( '-', '', $code ) ) ), $code );
}
check( 'reject wrong check chars', brqosh_normalize_request_code( 'BRQ-AAAAA-AAAAA-AAAAA-AAAAA-AAAAA-AAZ' ), '' );
check( 'reject one typo in body', brqosh_normalize_request_code( 'BRQ-AETWL-36KXR-G88AK-DJXV2-VL8P8-6EJ' ), '' );
check( 'reject ambiguous letter O', brqosh_normalize_request_code( 'BRQ-OAAAA-AAAAA-AAAAA-AAAAA-AAAAA-AG6' ), '' );
check( 'reject short', brqosh_normalize_request_code( 'BRQ-AAAAA' ), '' );
check( 'reject wrong prefix', brqosh_normalize_request_code( 'BRX-AAAAA-AAAAA-AAAAA-AAAAA-AAAAA-AG6' ), '' );
check( 'reject activation code pasted by mistake', brqosh_normalize_request_code( 'BRQA1.eyJ2IjoxfQ.sig' ), '' );

// Serial: 25 digits, five groups.
check( 'serial plain', brqosh_normalize_serial( '1234512345123451234512345' ), '12345-12345-12345-12345-12345' );
check( 'serial grouped', brqosh_normalize_serial( '12345-12345-12345-12345-12345' ), '12345-12345-12345-12345-12345' );
check( 'serial 24 digits', brqosh_normalize_serial( '123451234512345123451234' ), '' );
check( 'serial letters', brqosh_normalize_serial( '12345-12345-12345-12345-1234A' ), '' );

// Phones.
check( 'sa local 05', brqosh_normalize_phone( '0551234567' ), '966551234567' );
check( 'sa local spaced', brqosh_normalize_phone( '055 123 4567' ), '966551234567' );
check( 'sa no zero', brqosh_normalize_phone( '551234567' ), '966551234567' );
check( 'sa +966', brqosh_normalize_phone( '+966 55 123 4567' ), '966551234567' );
check( 'sa 00966', brqosh_normalize_phone( '00966551234567' ), '966551234567' );
check( 'arabic-indic digits', brqosh_normalize_phone( '٠٥٥١٢٣٤٥٦٧' ), '966551234567' );
check( 'sa landline rejected', brqosh_normalize_phone( '+966112345678' ), '' );
check( 'uae accepted', brqosh_normalize_phone( '+971501234567' ), '971501234567' );
check( 'too short', brqosh_normalize_phone( '12' ), '' );
check( 'letters', brqosh_normalize_phone( '05abc' ), '' );

// Text cleaning.
check( 'line strips newlines (header injection)', brqosh_clean_line( "Acme\r\nBcc: x@y.z", 120 ), 'Acme Bcc: x@y.z' );
check( 'line caps length', mb_strlen( brqosh_clean_line( str_repeat( 'ب', 200 ), 80 ) ), 80 );
check( 'text strips tags keeps newline', brqosh_clean_text( "<b>مرحبا</b>\r\nسطر", 100 ), "مرحبا\nسطر" );

echo $failures ? "{$failures}/{$count} FAILED\n" : "{$count} passed\n";
exit( $failures ? 1 : 0 );
