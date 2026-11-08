<?php
/**
 * Inline SVG line icons (24px grid, stroke = currentColor).
 *
 * Drawn for this theme; no external icon font is loaded.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

/**
 * Returns an inline SVG icon.
 *
 * @param string $name  Icon key.
 * @param string $class Extra CSS class.
 * @return string Safe SVG markup.
 */
function brqosh_icon( string $name, string $class = '' ): string {
	$paths = array(
		'ledger'      => '<path d="M5 3h11a3 3 0 0 1 3 3v15H8a3 3 0 0 1-3-3z"/><path d="M5 18a3 3 0 0 1 3-3h11"/><path d="M9 7h6M9 10.5h6"/>',
		'invoice'     => '<path d="M6 2h9l4 4v16l-2.5-1.5L14 22l-2-1.5L10 22l-2.5-1.5L5 22V3a1 1 0 0 1 1-1z"/><path d="M9 8h6M9 12h6M9 16h3"/>',
		'cart'        => '<circle cx="9" cy="20" r="1.5"/><circle cx="17" cy="20" r="1.5"/><path d="M2 3h3l2.6 12.2a1.5 1.5 0 0 0 1.5 1.2h8.7a1.5 1.5 0 0 0 1.5-1.2L21 7H6"/>',
		'boxes'       => '<path d="M3 8l9-5 9 5v8l-9 5-9-5z"/><path d="M3 8l9 5 9-5M12 13v8"/>',
		'bank'        => '<path d="M3 10l9-6 9 6"/><path d="M5 10v8M9.5 10v8M14.5 10v8M19 10v8M3 21h18"/>',
		'pos'         => '<rect x="4" y="3" width="16" height="11" rx="1.5"/><path d="M8 7h8M2 21h20l-2-7H4z"/>',
		'users'       => '<circle cx="9" cy="8" r="3.5"/><path d="M2.5 20a6.5 6.5 0 0 1 13 0"/><path d="M16 4.6a3.5 3.5 0 0 1 0 6.8M18 14a6.5 6.5 0 0 1 3.5 6"/>',
		'building'    => '<rect x="4" y="2.5" width="16" height="19" rx="1"/><path d="M8 7h2M14 7h2M8 11h2M14 11h2M8 15h2M14 15h2M10 21.5v-3h4v3"/>',
		'chart'       => '<path d="M3 3v18h18"/><path d="M7 15l4-5 3 3 5-7"/>',
		'factory'     => '<path d="M2 21V10l6 4V10l6 4V6l3-3 3 3v15z"/><path d="M6 18h2M11 18h2M16 18h2"/>',
		'crane'       => '<path d="M6 21V4l12 3M6 4L2 8h4M18 7v6"/><path d="M15 13h6v3h-6zM3 21h8"/>',
		'handshake'   => '<path d="M2 11l4-4 4 2 3-2 4 1 5 3"/><path d="M6 7v7l4 4 1.5-1.5L13 18l1.5-1.5L16 18l2-2-3-4M22 10l-4 4"/>',
		'store'       => '<path d="M3 9l1.5-5h15L21 9"/><path d="M3 9a3 3 0 0 0 6 0 3 3 0 0 0 6 0 3 3 0 0 0 6 0"/><path d="M5 11v10h14V11M10 21v-5h4v5"/>',
		'utensils'    => '<path d="M6 2v8a2 2 0 0 0 4 0V2M8 10v12"/><path d="M17 2c-2 1.5-3 4-3 7h3v13"/>',
		'home'        => '<path d="M3 11l9-7 9 7"/><path d="M5 9.5V21h14V9.5M10 21v-6h4v6"/>',
		'stethoscope' => '<path d="M5 3v6a5 5 0 0 0 10 0V3"/><path d="M10 14v2a5 5 0 0 0 10 0v-2"/><circle cx="20" cy="12" r="2"/>',
		'truck'       => '<path d="M2 5h12v11H2zM14 9h4l4 4v3h-8z"/><circle cx="6" cy="18" r="2"/><circle cx="18" cy="18" r="2"/>',
		'lock'        => '<rect x="4" y="10" width="16" height="11" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3M12 14v3"/>',
		'shield'      => '<path d="M12 2l8 3v6c0 5-3.5 9-8 11-4.5-2-8-6-8-11V5z"/><path d="M8.5 12l2.5 2.5 4.5-5"/>',
		'key'         => '<circle cx="8" cy="15" r="4.5"/><path d="M11.2 11.8L20 3M16 7l3 3M14 9l2 2"/>',
		'backup'      => '<path d="M3 12a9 9 0 1 0 3-6.7L3 8"/><path d="M3 3v5h5M12 7v5l3 2"/>',
		'eye'         => '<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/>',
		'globe'       => '<circle cx="12" cy="12" r="9.5"/><path d="M2.5 12h19M12 2.5c2.8 3 2.8 16 0 19M12 2.5c-2.8 3-2.8 16 0 19"/>',
		'code'        => '<path d="M8 7l-5 5 5 5M16 7l5 5-5 5M14 4l-4 16"/>',
		'stamp'       => '<path d="M9 13V9.5a3 3 0 1 1 6 0V13"/><path d="M4 13h16v4H4zM6 21h12"/>',
		'qr'          => '<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><path d="M14 14h3v3h-3zM20 14v.01M17 20h4v-3M14 20v.01"/>',
		'chain'       => '<path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1"/><path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/>',
		'server'      => '<rect x="3" y="3" width="18" height="7" rx="1.5"/><rect x="3" y="14" width="18" height="7" rx="1.5"/><path d="M7 6.5h.01M7 17.5h.01M11 6.5h6M11 17.5h6"/>',
		'wizard'      => '<path d="M4 20L16 8M14 4v2M20 10h-2M18.5 5.5l-1.5 1.5M10 3l.5 1.5M21 14l-1.5-.5"/>',
		'check'       => '<path d="M4 12.5l5 5L20 6.5"/>',
		'arrow'       => '<path d="M19 12H5M11 6l-6 6 6 6"/>',
		'menu'        => '<path d="M3 6h18M3 12h18M3 18h18"/>',
		'close'       => '<path d="M5 5l14 14M19 5L5 19"/>',
		'mail'        => '<rect x="2.5" y="4.5" width="19" height="15" rx="2"/><path d="M3 6l9 7 9-7"/>',
		'phone'       => '<path d="M5 3h3.5l2 5-2.5 1.5a11 11 0 0 0 6.5 6.5L16 13.5l5 2V19a2 2 0 0 1-2 2A17 17 0 0 1 3 5a2 2 0 0 1 2-2z"/>',
		'pin'         => '<path d="M12 22s7-6.5 7-12a7 7 0 0 0-14 0c0 5.5 7 12 7 12z"/><circle cx="12" cy="10" r="2.5"/>',
		'download'    => '<path d="M12 3v12M7 10l5 5 5-5M4 21h16"/>',
		'plus'        => '<path d="M12 5v14M5 12h14"/>',
		'clock'       => '<circle cx="12" cy="12" r="9.5"/><path d="M12 7v5l3.5 2"/>',
		'windows'     => '<path d="M3 5.5l7.5-1v7H3zM12.5 4.2L21 3v8.5h-8.5zM3 12.5h7.5v7L3 18.5zM12.5 12.5H21V21l-8.5-1.2z"/>',
		'whatsapp'    => '<path d="M3.5 20.5l1.3-4.2A8.5 8.5 0 1 1 8 19.5z"/><path d="M9 8.5c0 3.5 3 6.5 6.5 6.5l1-1.5-2-1-1 .8a4.5 4.5 0 0 1-2.3-2.3l.8-1-1-2z"/>',
	);
	if ( ! isset( $paths[ $name ] ) ) {
		return '';
	}
	$cls = trim( 'icon ' . $class );
	return '<svg class="' . esc_attr( $cls ) . '" viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">' . $paths[ $name ] . '</svg>';
}
