<?php
/**
 * Brqosh ERP theme bootstrap.
 *
 * Loads the theme modules in dependency order. Each module is a plain file of
 * functions and hooks; nothing here runs output.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

define( 'BRQOSH_THEME_VERSION', '1.0.0' );
define( 'BRQOSH_THEME_DIR', get_template_directory() );
define( 'BRQOSH_THEME_URI', get_template_directory_uri() );

require BRQOSH_THEME_DIR . '/inc/helpers.php';
require BRQOSH_THEME_DIR . '/inc/content.php';
require BRQOSH_THEME_DIR . '/inc/icons.php';
require BRQOSH_THEME_DIR . '/inc/setup.php';
require BRQOSH_THEME_DIR . '/inc/customizer.php';
require BRQOSH_THEME_DIR . '/inc/security.php';
require BRQOSH_THEME_DIR . '/inc/validation.php';
require BRQOSH_THEME_DIR . '/inc/leads.php';
require BRQOSH_THEME_DIR . '/inc/forms.php';
require BRQOSH_THEME_DIR . '/inc/seo.php';
