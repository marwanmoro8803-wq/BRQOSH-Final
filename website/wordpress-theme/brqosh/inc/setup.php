<?php
/**
 * Theme supports, assets, menus and first-activation page provisioning.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

add_action(
	'after_setup_theme',
	static function (): void {
		load_theme_textdomain( 'brqosh', BRQOSH_THEME_DIR . '/languages' );
		add_theme_support( 'title-tag' );
		add_theme_support( 'post-thumbnails' );
		add_theme_support( 'html5', array( 'search-form', 'gallery', 'caption', 'style', 'script' ) );
		add_theme_support( 'responsive-embeds' );
		add_theme_support( 'custom-logo', array( 'height' => 64, 'width' => 220, 'flex-width' => true, 'flex-height' => true ) );
		register_nav_menus(
			array(
				'primary' => 'القائمة الرئيسية',
				'footer'  => 'قائمة التذييل',
			)
		);
	}
);

add_action(
	'wp_enqueue_scripts',
	static function (): void {
		$css = BRQOSH_THEME_DIR . '/assets/css/main.css';
		$js  = BRQOSH_THEME_DIR . '/assets/js/main.js';
		wp_enqueue_style( 'brqosh-main', BRQOSH_THEME_URI . '/assets/css/main.css', array(), (string) filemtime( $css ) );
		wp_enqueue_script( 'brqosh-main', BRQOSH_THEME_URI . '/assets/js/main.js', array(), (string) filemtime( $js ), array( 'strategy' => 'defer', 'in_footer' => true ) );

		// Block library CSS is only needed on pages rendered from the editor (legal pages).
		if ( is_front_page() || brqosh_is_theme_page() ) {
			wp_dequeue_style( 'wp-block-library' );
			wp_dequeue_style( 'global-styles' );
			wp_dequeue_style( 'classic-theme-styles' );
		}
	},
	20
);

/**
 * Preloads the Arabic body font so text renders without a visible swap.
 */
add_action(
	'wp_head',
	static function (): void {
		$href = BRQOSH_THEME_URI . '/assets/fonts/ibm-plex-sans-arabic-arabic-400-normal.woff2';
		echo '<link rel="preload" href="' . esc_url( $href ) . '" as="font" type="font/woff2" crossorigin>' . "\n";
		echo '<meta name="theme-color" content="#6B4420">' . "\n";
	},
	1
);

/**
 * Site icon fallback when none is set in Customizer.
 */
add_action(
	'wp_head',
	static function (): void {
		if ( ! has_site_icon() ) {
			echo '<link rel="icon" href="' . esc_url( BRQOSH_THEME_URI . '/assets/img/app-icon.png' ) . '" type="image/png">' . "\n";
		}
	}
);

/**
 * True when the current page uses one of the theme's designed templates.
 *
 * @return bool
 */
function brqosh_is_theme_page(): bool {
	if ( ! is_page() ) {
		return false;
	}
	$slug = (string) get_post_field( 'post_name', get_queried_object_id() );
	return is_readable( BRQOSH_THEME_DIR . '/page-' . sanitize_file_name( $slug ) . '.php' );
}

/**
 * Creates the pages the theme renders when they are missing, sets the static
 * front page and assigns the primary menu. Idempotent: existing pages and
 * existing settings are never overwritten.
 */
function brqosh_provision_site(): void {
	foreach ( brqosh_required_pages() as $slug => $title ) {
		if ( get_page_by_path( $slug ) ) {
			continue;
		}
		wp_insert_post(
			array(
				'post_type'    => 'page',
				'post_status'  => 'publish',
				'post_name'    => $slug,
				'post_title'   => $title,
				'post_content' => in_array( $slug, array( 'privacy', 'terms', 'eula' ), true )
					? '<!-- wp:paragraph --><p>أضف نص الصفحة من المحرر بعد مراجعته القانونية.</p><!-- /wp:paragraph -->'
					: '',
			)
		);
	}

	if ( 'page' !== get_option( 'show_on_front' ) ) {
		$home = get_page_by_path( 'home' );
		if ( ! $home ) {
			$home_id = wp_insert_post(
				array(
					'post_type'   => 'page',
					'post_status' => 'publish',
					'post_name'   => 'home',
					'post_title'  => 'الرئيسية',
				)
			);
		} else {
			$home_id = $home->ID;
		}
		if ( $home_id && ! is_wp_error( $home_id ) ) {
			update_option( 'show_on_front', 'page' );
			update_option( 'page_on_front', (int) $home_id );
		}
	}
}
add_action( 'after_switch_theme', 'brqosh_provision_site' );

/**
 * Prints the primary navigation: the assigned WP menu, or the built-in list.
 *
 * @param string $location Menu location.
 * @param string $class    UL class.
 */
function brqosh_nav( string $location, string $class ): void {
	if ( has_nav_menu( $location ) ) {
		wp_nav_menu(
			array(
				'theme_location' => $location,
				'container'      => false,
				'menu_class'     => $class,
				'depth'          => 1,
				'fallback_cb'    => false,
			)
		);
		return;
	}
	echo '<ul class="' . esc_attr( $class ) . '">';
	foreach ( brqosh_nav_items() as $item ) {
		$current = brqosh_is_page( $item['slug'] ) ? ' aria-current="page"' : '';
		echo '<li><a href="' . brqosh_page_url( $item['slug'] ) . '"' . $current . '>' . esc_html( $item['label'] ) . '</a></li>'; // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped -- URL escaped by helper.
	}
	echo '</ul>';
}

/**
 * Adds aria-current to the active item of assigned WP menus.
 */
add_filter(
	'nav_menu_link_attributes',
	static function ( array $atts, $item ): array {
		if ( ! empty( $item->current ) ) {
			$atts['aria-current'] = 'page';
		}
		return $atts;
	},
	10,
	2
);
