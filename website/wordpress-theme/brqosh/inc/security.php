<?php
/**
 * Front-end hardening that a marketing site can apply safely.
 *
 * Kept conservative on purpose: nothing here touches wp-admin, login, or the
 * REST routes that plugins (Rank Math, Hostinger tools) rely on — only the
 * public user listing, which leaks admin usernames.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

// Do not advertise the WordPress version.
remove_action( 'wp_head', 'wp_generator' );
add_filter( 'the_generator', '__return_empty_string' );

// XML-RPC is a brute-force and pingback-amplification surface this site does not use.
// xmlrpc_enabled only disables authenticated methods, so the endpoint is refused outright.
// (Re-enable by removing this block if a Jetpack-style plugin ever needs it.)
add_filter( 'xmlrpc_enabled', '__return_false' );
add_action(
	'init',
	static function (): void {
		if ( defined( 'XMLRPC_REQUEST' ) && XMLRPC_REQUEST ) {
			status_header( 403 );
			header( 'Content-Type: text/plain; charset=UTF-8' );
			exit( 'XML-RPC is disabled.' );
		}
	},
	0
);
add_filter(
	'wp_headers',
	static function ( array $headers ): array {
		unset( $headers['X-Pingback'] );
		return $headers;
	}
);
remove_action( 'wp_head', 'rsd_link' );
remove_action( 'wp_head', 'wlwmanifest_link' );

/**
 * Security headers for public pages. HSTS is left to the host (it must be
 * set only once TLS is guaranteed on every subdomain).
 */
add_action(
	'send_headers',
	static function (): void {
		if ( is_admin() || headers_sent() ) {
			return;
		}
		header( 'X-Content-Type-Options: nosniff' );
		header( 'X-Frame-Options: SAMEORIGIN' );
		header( 'Referrer-Policy: strict-origin-when-cross-origin' );
		header( 'Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=(), usb=()' );
		header( 'Cross-Origin-Opener-Policy: same-origin' );
	}
);

/**
 * Blocks username enumeration through ?author=N for anonymous visitors.
 */
add_action(
	'template_redirect',
	static function (): void {
		if ( ! is_user_logged_in() && ( isset( $_GET['author'] ) || is_author() ) ) { // phpcs:ignore WordPress.Security.NonceVerification.Recommended
			wp_safe_redirect( home_url( '/' ), 301 );
			exit;
		}
	},
	0 // Before redirect_canonical (priority 10), which would reveal /author/<username>/.
);

/**
 * Hides the public users listing from anonymous REST requests.
 */
add_filter(
	'rest_endpoints',
	static function ( array $endpoints ): array {
		if ( is_user_logged_in() ) {
			return $endpoints;
		}
		foreach ( array_keys( $endpoints ) as $route ) {
			if ( str_starts_with( $route, '/wp/v2/users' ) ) {
				unset( $endpoints[ $route ] );
			}
		}
		return $endpoints;
	}
);

// Author archives and oEmbed author data are not needed for a product site.
add_filter(
	'oembed_response_data',
	static function ( array $data ): array {
		unset( $data['author_name'], $data['author_url'] );
		return $data;
	}
);
