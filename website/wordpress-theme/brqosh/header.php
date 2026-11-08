<?php
/**
 * Site header: skip link, brand, primary navigation and CTA.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;
?><!DOCTYPE html>
<html <?php language_attributes(); ?>>
<head>
<meta charset="<?php bloginfo( 'charset' ); ?>">
<meta name="viewport" content="width=device-width, initial-scale=1">
<?php wp_head(); ?>
</head>
<body <?php body_class(); ?>>
<?php wp_body_open(); ?>
<a class="skip-link" href="#main">تخطَّ إلى المحتوى</a>
<header class="site-header" data-header>
	<div class="container header-inner">
		<a class="brand" href="<?php echo esc_url( home_url( '/' ) ); ?>" aria-label="برقوش ERP — الرئيسية">
			<?php
			if ( has_custom_logo() ) {
				echo wp_kses_post( wp_get_attachment_image( (int) get_theme_mod( 'custom_logo' ), 'full', false, array( 'class' => 'brand-img' ) ) );
			} else {
				echo '<span class="brand-logo">' . brqosh_svg_file( 'logo' ) . '</span><span class="brand-tag">ERP</span>'; // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped -- theme-owned SVG.
			}
			?>
		</a>
		<button class="nav-toggle" type="button" aria-expanded="false" aria-controls="site-nav" data-nav-toggle>
			<?php echo brqosh_icon( 'menu', 'icon-open' ) . brqosh_icon( 'close', 'icon-close' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>
			<span class="sr-only">القائمة</span>
		</button>
		<nav id="site-nav" class="site-nav" aria-label="القائمة الرئيسية" data-nav>
			<?php brqosh_nav( 'primary', 'nav-list' ); ?>
			<div class="nav-cta">
				<a class="btn btn-ghost btn-sm" href="<?php echo brqosh_page_url( 'activation' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">التفعيل</a>
				<a class="btn btn-primary btn-sm" href="<?php echo brqosh_page_url( 'demo' ); // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>">جرّبه مجاناً</a>
			</div>
		</nav>
	</div>
</header>
<main id="main" tabindex="-1">
