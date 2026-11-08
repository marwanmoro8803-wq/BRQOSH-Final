<?php
/**
 * Structured data and a meta-description fallback.
 *
 * Rank Math (installed on the live site) owns titles, Open Graph and the
 * Organization/WebSite graph; the theme adds only what Rank Math does not
 * know: the SoftwareApplication facts and the FAQ, and a description when no
 * SEO plugin is active.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

/**
 * Page descriptions for theme pages (used only without an SEO plugin).
 *
 * @return array<string,string>
 */
function brqosh_page_descriptions(): array {
	return array(
		''            => 'برقوش ERP برنامج محاسبة عربي لويندوز للمنشآت في السعودية والخليج: قيود بالاعتماد والترحيل، فواتير إلكترونية وفق متطلبات المرحلة الثانية، مخزون وفروع ونقاط بيع ورواتب. جرّبه مجاناً.',
		'features'    => 'كل أقسام برقوش ERP: الحسابات العامة والمبيعات والمشتريات والمخزون والخزينة ونقاط البيع والرواتب والأصول والتقارير والتصنيع والمقاولات وCRM.',
		'sectors'     => 'برقوش ERP للتجارة والتصنيع والتجزئة والمطاعم والفنادق والمقاولات والعقارات والعيادات والنقل — وحدات مخصصة لكل نشاط فوق نواة محاسبية واحدة.',
		'e-invoicing' => 'الفوترة الإلكترونية في برقوش ERP: فاتورة UBL 2.1 موقّعة برمز QR وتسلسل ICV وPIH، وتسجيل المنشأة من الإعدادات وفق متطلبات المرحلة الثانية.',
		'pricing'     => 'أسعار برقوش ERP تبدأ من 1,500 ريال حسب المنشأة وعدد المستخدمين والفروع، بترخيص سنوي أو دائم، مع تجربة مجانية كاملة.',
		'downloads'   => 'تحميل آخر إصدار من برقوش ERP مع بصمة SHA-256 وسجل التحديثات ومتطلبات التشغيل.',
		'activation'  => 'فعّل ترخيص برقوش ERP: أرسل رقم الطلب والرقم التسلسلي واستلم رمز التفعيل الموقّع.',
		'demo'        => 'جرّب برقوش ERP مجاناً بكل الأقسام على جهازك، ونساعدك في التنصيب وتجهيز دليل الحسابات.',
		'contact'     => 'تواصل مع فريق برقوش ERP في الرياض عبر النموذج أو واتساب أو البريد.',
		'company'     => 'شركة برقوش في الرياض تطوّر برقوش ERP: برنامج محاسبة عربي يتبع الأنظمة المحلية ويحفظ بيانات المنشأة على أجهزتها.',
	);
}

add_action(
	'wp_head',
	static function (): void {
		if ( defined( 'RANK_MATH_VERSION' ) || defined( 'WPSEO_VERSION' ) || defined( 'AIOSEO_VERSION' ) ) {
			return;
		}
		$slug = is_front_page() ? '' : ( is_page() ? (string) get_post_field( 'post_name', get_queried_object_id() ) : null );
		$map  = brqosh_page_descriptions();
		if ( null !== $slug && isset( $map[ $slug ] ) ) {
			echo '<meta name="description" content="' . esc_attr( $map[ $slug ] ) . '">' . "\n";
		}
	},
	2
);

add_action(
	'wp_head',
	static function (): void {
		$graph = array();
		if ( is_front_page() || is_page( 'features' ) || is_page( 'pricing' ) || is_page( 'downloads' ) ) {
			$graph[] = array(
				'@type'               => 'SoftwareApplication',
				'name'                => 'برقوش ERP',
				'alternateName'       => 'BRQOSH ERP',
				'applicationCategory' => 'BusinessApplication',
				'applicationSubCategory' => 'Accounting',
				'operatingSystem'     => 'Windows 10, Windows 11, Windows Server 2016+',
				'softwareVersion'     => brqosh_setting( 'release_version' ),
				'datePublished'       => brqosh_setting( 'release_date' ),
				'inLanguage'          => array( 'ar', 'en' ),
				'url'                 => home_url( '/' ),
				'image'               => BRQOSH_THEME_URI . '/assets/img/app-icon.png',
				'offers'              => array(
					'@type'         => 'Offer',
					'price'         => (string) preg_replace( '/\D+/', '', brqosh_setting( 'price_from' ) ),
					'priceCurrency' => 'SAR',
					'url'           => home_url( '/pricing/' ),
				),
				'publisher'           => array( '@type' => 'Organization', 'name' => brqosh_setting( 'company_name' ), 'url' => home_url( '/' ) ),
			);
		}
		if ( is_front_page() || is_page( 'pricing' ) ) {
			$graph[] = array(
				'@type'      => 'FAQPage',
				'mainEntity' => array_map(
					static fn( array $f ): array => array(
						'@type'          => 'Question',
						'name'           => $f['q'],
						'acceptedAnswer' => array( '@type' => 'Answer', 'text' => $f['a'] ),
					),
					brqosh_faqs()
				),
			);
		}
		if ( ! $graph ) {
			return;
		}
		$json = wp_json_encode( array( '@context' => 'https://schema.org', '@graph' => $graph ), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_HEX_TAG );
		echo '<script type="application/ld+json">' . $json . '</script>' . "\n"; // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped -- JSON_HEX_TAG prevents </script> breakout.
	},
	20
);
