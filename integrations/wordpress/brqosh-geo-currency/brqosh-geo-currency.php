<?php
/**
 * Plugin Name:       Brqosh Geo Currency
 * Description:       يجعل سلة الشراء وصفحة إتمام الطلب تعمل فعلياً بعملة بلد التوصيل (البحرين ← دينار بحريني، الكويت ← دينار كويتي ...) بدل الريال الثابت، مع تحديد البلد تلقائياً من الموقع الجغرافي للزائر. الطلب يُنشأ ويُسجَّل بعملة العميل مع حفظ سعر الصرف والمعادل بالريال.
 * Version:           1.0.0
 * Author:            Brqosh
 * Requires at least: 6.2
 * Requires PHP:      7.4
 * Requires Plugins:  woocommerce
 * WC requires at least: 8.0
 * Text Domain:       brqosh-geo-currency
 *
 * How it works (server side, no payment-provider account needed):
 *  - The "active currency" is derived from the customer's delivery country
 *    (shipping country, falling back to billing country) using COUNTRY_MAP.
 *  - Conversion is applied only on the cart / checkout / order-pay pages and on
 *    AJAX / Store-API calls made FROM those pages. Every other page keeps
 *    rendering in the store base currency (SAR) so the LiteSpeed page cache
 *    and the theme's display-only converter keep working unchanged.
 *  - Product prices, shipping rates, free-shipping thresholds and fixed coupons
 *    are converted with a live rate (open.er-api.com, no API key) cached for
 *    6 hours; GCC currencies pegged to USD fall back to their official pegs.
 *  - Amounts are rounded to the store's decimals (0.01) in every currency so
 *    totals never change between checkout, admin, e-mails and REST.
 *  - The created order carries the customer's currency, plus meta with the
 *    rate used and the equivalent total in the base currency.
 *
 * @package BrqoshGeoCurrency
 */

defined( 'ABSPATH' ) || exit;

if ( ! class_exists( 'Brqosh_Geo_Currency' ) ) :

	/**
	 * Server-side currency switching for WooCommerce driven by delivery country.
	 */
	final class Brqosh_Geo_Currency {

		const VERSION = '1.0.0';

		/** Cookie shared with the front-end script (country code chosen / geolocated). */
		const COOKIE = 'bq_cc';

		/** Transient holding the latest live rates. */
		const RATES_TRANSIENT = 'bq_geo_fx_rates';

		/** Transient set after a failed fetch so we do not retry on every request. */
		const RATES_BACKOFF = 'bq_geo_fx_backoff';

		/** Option keeping the last successful rates as a long-lived fallback. */
		const RATES_LAST_GOOD = 'bq_geo_fx_last_good';

		/**
		 * Country => [currency code, decimals, display symbol].
		 * Mirrors the list used by the theme currency chip so both stay in sync.
		 */
		const COUNTRY_MAP = array(
			'SA' => array( 'SAR', 2, 'ر.س' ),
			'AE' => array( 'AED', 2, 'د.إ' ),
			'KW' => array( 'KWD', 3, 'د.ك' ),
			'BH' => array( 'BHD', 3, 'د.ب' ),
			'QA' => array( 'QAR', 2, 'ر.ق' ),
			'OM' => array( 'OMR', 3, 'ر.ع' ),
			'JO' => array( 'JOD', 3, 'د.أ' ),
			'EG' => array( 'EGP', 2, 'ج.م' ),
			'PS' => array( 'ILS', 2, '₪' ),
		);

		/**
		 * Official USD pegs (units per 1 USD). Used to derive a safe fallback
		 * rate when the live feed is unreachable. Floating currencies (KWD basket,
		 * EGP, ILS) have no fallback: without a live rate they stay in base currency.
		 */
		const USD_PEGS = array(
			'SAR' => 3.75,
			'AED' => 3.6725,
			'BHD' => 0.376,
			'QAR' => 3.64,
			'OMR' => 0.3845,
			'JOD' => 0.709,
		);

		/** @var Brqosh_Geo_Currency|null */
		private static $instance = null;

		/** @var bool|null Per-request cache of the scope decision (only once the query is resolved). */
		private $scope_cache = null;

		/** @var bool|null Per-request cache of the referer decision. */
		private $referer_cache = null;

		/** @var array|null Per-request cache of rates. */
		private $rates_cache = null;

		/** @var bool Re-entrancy guard for the currency computation. */
		private $resolving = false;

		/**
		 * Singleton accessor.
		 *
		 * @return Brqosh_Geo_Currency
		 */
		public static function instance() {
			if ( null === self::$instance ) {
				self::$instance = new self();
			}
			return self::$instance;
		}

		/**
		 * Register hooks.
		 */
		private function __construct() {
			// Currency + formatting.
			add_filter( 'woocommerce_currency', array( $this, 'filter_currency' ), 999 );
			add_filter( 'wc_price_args', array( $this, 'filter_price_args' ), 999 );
			add_filter( 'woocommerce_currency_symbol', array( $this, 'filter_symbol' ), 999, 2 );

			// Product prices (simple, variable, variations).
			foreach ( array( 'woocommerce_product_get_', 'woocommerce_product_variation_get_', 'woocommerce_variation_prices_' ) as $prefix ) {
				foreach ( array( 'price', 'regular_price', 'sale_price' ) as $field ) {
					add_filter( $prefix . $field, array( $this, 'convert_amount' ), 999 );
				}
			}
			add_filter( 'woocommerce_get_variation_prices_hash', array( $this, 'filter_variation_hash' ), 999 );

			// Shipping.
			add_filter( 'woocommerce_cart_shipping_packages', array( $this, 'tag_shipping_packages' ), 999 );
			add_filter( 'woocommerce_package_rates', array( $this, 'convert_package_rates' ), 999, 2 );
			add_filter( 'woocommerce_shipping_free_shipping_instance_option', array( $this, 'convert_free_shipping_option' ), 999, 2 );

			// Coupons.
			add_filter( 'woocommerce_coupon_get_amount', array( $this, 'convert_coupon_amount' ), 999, 2 );
			add_filter( 'woocommerce_coupon_get_minimum_amount', array( $this, 'convert_amount' ), 999 );
			add_filter( 'woocommerce_coupon_get_maximum_amount', array( $this, 'convert_amount' ), 999 );

			// Keep cart totals consistent with the currency of the current request.
			add_action( 'wp_loaded', array( $this, 'sync_customer_country_from_cookie' ), 30 );
			add_action( 'wp', array( $this, 'maybe_recalculate_cart' ), 1 );
			add_filter( 'rest_pre_dispatch', array( $this, 'maybe_recalculate_cart_for_rest' ), 10, 1 );
			add_action( 'woocommerce_after_calculate_totals', array( $this, 'remember_cart_currency' ) );

			// Payment gateways limited to the base currency.
			add_filter( 'woocommerce_available_payment_gateways', array( $this, 'filter_gateways' ), 999 );

			// Orders.
			add_action( 'woocommerce_checkout_create_order', array( $this, 'stamp_order' ) );
			add_action( 'woocommerce_store_api_checkout_update_order_meta', array( $this, 'stamp_order' ) );
			add_action( 'woocommerce_admin_order_data_after_order_details', array( $this, 'render_admin_order_info' ) );

			// Front-end.
			add_action( 'woocommerce_review_order_before_payment', array( $this, 'render_checkout_notice' ) );
			add_action( 'woocommerce_before_cart_totals', array( $this, 'render_checkout_notice' ) );
			add_action( 'wp_enqueue_scripts', array( $this, 'enqueue_script' ) );
		}

		/*
		|--------------------------------------------------------------------------
		| Currency resolution
		|--------------------------------------------------------------------------
		*/

		/**
		 * Store base currency (unfiltered option).
		 *
		 * @return string
		 */
		public function base_currency() {
			return (string) get_option( 'woocommerce_currency', 'SAR' );
		}

		/**
		 * Whether the current request should be priced in the customer's currency.
		 *
		 * @return bool
		 */
		public function in_scope() {
			if ( null !== $this->scope_cache ) {
				return $this->scope_cache;
			}

			if ( wp_doing_cron() || ( defined( 'WP_CLI' ) && WP_CLI ) ) {
				return $this->scope_cache = false;
			}

			$is_ajax = wp_doing_ajax() || isset( $_GET['wc-ajax'] ); // phpcs:ignore WordPress.Security.NonceVerification.Recommended
			$is_rest = ( defined( 'REST_REQUEST' ) && REST_REQUEST ) || $this->is_store_api_request();

			if ( is_admin() && ! $is_ajax ) {
				return $this->scope_cache = false;
			}

			if ( $is_ajax || $is_rest ) {
				// Page URLs (wc_get_cart_url, url_to_postid) need the rewrite layer, which
				// does not exist yet while plugins load. Other plugins can ask for the
				// currency that early; answer with the base currency and decide later.
				if ( ! did_action( 'init' ) || empty( $GLOBALS['wp_rewrite'] ) ) {
					return false;
				}
				// Background calls inherit the page they were fired from.
				return $this->scope_cache = $this->referer_is_checkout_page();
			}

			if ( ! did_action( 'wp' ) ) {
				// Main query not resolved yet: stay in base currency, do not cache.
				return false;
			}

			// is_checkout() also covers order-pay / order-received endpoints and CartFlows steps.
			return $this->scope_cache = ( is_cart() || is_checkout() );
		}

		/**
		 * Detect a Store API request before REST_REQUEST is defined.
		 *
		 * @return bool
		 */
		private function is_store_api_request() {
			if ( empty( $_SERVER['REQUEST_URI'] ) ) {
				return false;
			}
			$uri = esc_url_raw( wp_unslash( $_SERVER['REQUEST_URI'] ) );
			return false !== strpos( $uri, '/wc/store/' ) || false !== strpos( $uri, 'rest_route=%2Fwc%2Fstore' ) || false !== strpos( $uri, 'rest_route=/wc/store' );
		}

		/**
		 * Whether the HTTP referer is this site's cart / checkout (or a CartFlows checkout step).
		 *
		 * @return bool
		 */
		private function referer_is_checkout_page() {
			if ( null !== $this->referer_cache ) {
				return $this->referer_cache;
			}
			$this->referer_cache = false;

			// The browser header is preferred; the classic checkout form also posts a
			// relative `_wp_http_referer` (e.g. "/checkout/") which counts as same-site.
			$candidates = array();
			if ( ! empty( $_SERVER['HTTP_REFERER'] ) ) {
				$candidates[] = wp_unslash( $_SERVER['HTTP_REFERER'] ); // phpcs:ignore WordPress.Security.ValidatedSanitizedInput.InputNotSanitized -- only parsed and compared.
			}
			if ( ! empty( $_REQUEST['_wp_http_referer'] ) ) { // phpcs:ignore WordPress.Security.NonceVerification.Recommended
				$candidates[] = wp_unslash( $_REQUEST['_wp_http_referer'] ); // phpcs:ignore WordPress.Security.NonceVerification.Recommended,WordPress.Security.ValidatedSanitizedInput.InputNotSanitized
			}

			foreach ( $candidates as $referer ) {
				if ( is_string( $referer ) && $this->url_is_checkout_page( $referer ) ) {
					return $this->referer_cache = true;
				}
			}
			return false;
		}

		/**
		 * Whether a same-site URL (absolute or root-relative) is the cart / checkout page.
		 *
		 * @param string $referer URL.
		 * @return bool
		 */
		private function url_is_checkout_page( $referer ) {
			$ref_host  = wp_parse_url( $referer, PHP_URL_HOST );
			$site_host = wp_parse_url( home_url(), PHP_URL_HOST );
			if ( $ref_host ) {
				if ( strtolower( $ref_host ) !== strtolower( (string) $site_host ) ) {
					return false;
				}
			} elseif ( 0 !== strpos( $referer, '/' ) || 0 === strpos( $referer, '//' ) ) {
				return false;
			} else {
				$referer = home_url( $referer );
			}

			$ref_path = $this->normalize_path( wp_parse_url( $referer, PHP_URL_PATH ) );
			$cart     = $this->normalize_path( wp_parse_url( wc_get_cart_url(), PHP_URL_PATH ) );
			$checkout = $this->normalize_path( wp_parse_url( wc_get_checkout_url(), PHP_URL_PATH ) );

			if ( '' !== $cart && '/' !== $cart && $ref_path === $cart ) {
				return true;
			}
			// Checkout path and its endpoints (order-pay, order-received).
			if ( '' !== $checkout && '/' !== $checkout && ( $ref_path === $checkout || 0 === strpos( $ref_path, $checkout . '/' ) ) ) {
				return true;
			}

			// Plain permalinks (?page_id=...) or CartFlows checkout steps.
			$post_id = url_to_postid( $referer );
			if ( $post_id ) {
				if ( in_array( $post_id, array( wc_get_page_id( 'cart' ), wc_get_page_id( 'checkout' ) ), true ) ) {
					return true;
				}
				if ( 'cartflows_step' === get_post_type( $post_id ) ) {
					return true;
				}
			}

			return false;
		}

		/**
		 * Normalize a URL path for comparison.
		 *
		 * @param string|null $path Path.
		 * @return string
		 */
		private function normalize_path( $path ) {
			return untrailingslashit( strtolower( rawurldecode( (string) $path ) ) );
		}

		/**
		 * Delivery country of the current customer (shipping first, then billing).
		 *
		 * @return string
		 */
		private function customer_country() {
			if ( ! function_exists( 'WC' ) || ! WC()->customer instanceof WC_Customer ) {
				return '';
			}
			$country = WC()->customer->get_shipping_country();
			if ( ! $country ) {
				$country = WC()->customer->get_billing_country();
			}
			return strtoupper( (string) $country );
		}

		/**
		 * Currency the current request should be priced in.
		 *
		 * @return string
		 */
		public function active_currency() {
			$base = $this->base_currency();
			if ( $this->resolving ) {
				return $base;
			}
			$this->resolving = true;
			try {
				if ( ! $this->in_scope() ) {
					return $base;
				}
				$country = $this->customer_country();
				if ( ! isset( self::COUNTRY_MAP[ $country ] ) ) {
					return $base;
				}
				$currency = self::COUNTRY_MAP[ $country ][0];
				if ( $currency === $base || null === $this->rate( $currency ) ) {
					return $base;
				}
				return $currency;
			} finally {
				$this->resolving = false;
			}
		}

		/**
		 * Rate: units of $currency per 1 unit of base currency, or null if unknown.
		 *
		 * @param string $currency ISO code.
		 * @return float|null
		 */
		public function rate( $currency ) {
			$base = $this->base_currency();
			if ( $currency === $base ) {
				return 1.0;
			}
			$rates = $this->rates();
			if ( isset( $rates[ $currency ] ) && is_numeric( $rates[ $currency ] ) && (float) $rates[ $currency ] > 0 ) {
				return (float) $rates[ $currency ];
			}
			if ( isset( self::USD_PEGS[ $currency ], self::USD_PEGS[ $base ] ) ) {
				return self::USD_PEGS[ $currency ] / self::USD_PEGS[ $base ];
			}
			return null;
		}

		/**
		 * Live rates relative to the base currency, cached.
		 *
		 * @return array<string,float>
		 */
		private function rates() {
			if ( null !== $this->rates_cache ) {
				return $this->rates_cache;
			}

			/**
			 * Allows hard-coding rates (e.g. array( 'BHD' => 0.1003 )) and skipping the live feed.
			 */
			$override = apply_filters( 'brqosh_geo_currency_rates', null, $this->base_currency() );
			if ( is_array( $override ) ) {
				return $this->rates_cache = $override;
			}

			$cached = get_transient( self::RATES_TRANSIENT );
			if ( is_array( $cached ) && ( $cached['base'] ?? '' ) === $this->base_currency() ) {
				return $this->rates_cache = $cached['rates'];
			}

			$fresh = get_transient( self::RATES_BACKOFF ) ? null : $this->fetch_rates();
			if ( is_array( $fresh ) ) {
				$payload = array(
					'base'  => $this->base_currency(),
					'rates' => $fresh,
					'time'  => time(),
				);
				set_transient( self::RATES_TRANSIENT, $payload, 6 * HOUR_IN_SECONDS );
				update_option( self::RATES_LAST_GOOD, $payload, false );
				return $this->rates_cache = $fresh;
			}

			set_transient( self::RATES_BACKOFF, 1, 15 * MINUTE_IN_SECONDS );
			$last = get_option( self::RATES_LAST_GOOD );
			// A stale live rate is only trusted for 7 days; after that pegs take over.
			if ( is_array( $last ) && ( $last['base'] ?? '' ) === $this->base_currency() && ( time() - (int) ( $last['time'] ?? 0 ) ) < 7 * DAY_IN_SECONDS ) {
				return $this->rates_cache = $last['rates'];
			}
			return $this->rates_cache = array();
		}

		/**
		 * Fetch rates from the free, key-less open.er-api.com feed.
		 *
		 * @return array<string,float>|null
		 */
		private function fetch_rates() {
			$base     = rawurlencode( $this->base_currency() );
			$response = wp_safe_remote_get(
				'https://open.er-api.com/v6/latest/' . $base,
				array(
					'timeout'    => 4,
					'user-agent' => 'BrqoshGeoCurrency/' . self::VERSION,
				)
			);
			if ( is_wp_error( $response ) || 200 !== (int) wp_remote_retrieve_response_code( $response ) ) {
				return null;
			}
			$body = json_decode( wp_remote_retrieve_body( $response ), true );
			if ( ! is_array( $body ) || 'success' !== ( $body['result'] ?? '' ) || empty( $body['rates'] ) || ! is_array( $body['rates'] ) ) {
				return null;
			}
			$rates = array();
			foreach ( self::COUNTRY_MAP as $row ) {
				$code = $row[0];
				if ( isset( $body['rates'][ $code ] ) && is_numeric( $body['rates'][ $code ] ) && (float) $body['rates'][ $code ] > 0 ) {
					$rates[ $code ] = (float) $body['rates'][ $code ];
				}
			}
			return $rates ? $rates : null;
		}

		/**
		 * Convert an amount from base currency to the active currency.
		 *
		 * @param mixed  $amount   Amount in base currency.
		 * @param string $currency Target currency (defaults to active).
		 * @return mixed
		 */
		private function to_currency( $amount, $currency = null ) {
			if ( '' === $amount || null === $amount || ! is_numeric( $amount ) ) {
				return $amount;
			}
			$currency = $currency ? $currency : $this->active_currency();
			if ( $currency === $this->base_currency() ) {
				return $amount;
			}
			$rate = $this->rate( $currency );
			// Round each converted amount to the store precision so line items,
			// subtotals and totals always add up exactly as displayed.
			return null === $rate ? $amount : round( (float) $amount * $rate, wc_get_price_decimals() );
		}

		/**
		 * Public helper for other code (e.g. shipping snippets with SAR thresholds):
		 * base-currency amount -> amount in the currency of the current request.
		 *
		 * @param float $amount Amount in base currency.
		 * @return float
		 */
		public function from_base( $amount ) {
			return (float) $this->to_currency( (float) $amount );
		}

		/**
		 * Public helper: amount in the currency of the current request -> base currency.
		 *
		 * @param float $amount Amount in the active currency.
		 * @return float
		 */
		public function to_base( $amount ) {
			$currency = $this->active_currency();
			if ( $currency === $this->base_currency() ) {
				return (float) $amount;
			}
			$rate = $this->rate( $currency );
			return $rate ? (float) $amount / $rate : (float) $amount;
		}

		/**
		 * Gateways that only accept the base currency are hidden when the cart is
		 * priced in another currency (Moyasar accounts accept SAR unless the merchant
		 * enabled more). Filter `brqosh_geo_currency_base_only_gateways` to change.
		 *
		 * @param array $gateways Available gateways.
		 * @return array
		 */
		public function filter_gateways( $gateways ) {
			if ( ! is_array( $gateways ) || $this->active_currency() === $this->base_currency() ) {
				return $gateways;
			}
			$prefixes = (array) apply_filters( 'brqosh_geo_currency_base_only_gateways', array( 'moyasar' ) );
			foreach ( array_keys( $gateways ) as $id ) {
				foreach ( $prefixes as $prefix ) {
					if ( '' !== $prefix && 0 === strpos( (string) $id, $prefix ) ) {
						unset( $gateways[ $id ] );
						break;
					}
				}
			}
			return $gateways;
		}

		/**
		 * Decimals for a currency in our map, or null.
		 *
		 * @param string $currency ISO code.
		 * @return int|null
		 */
		private function decimals_for( $currency ) {
			foreach ( self::COUNTRY_MAP as $row ) {
				if ( $row[0] === $currency ) {
					return (int) $row[1];
				}
			}
			return null;
		}

		/*
		|--------------------------------------------------------------------------
		| Filters
		|--------------------------------------------------------------------------
		*/

		/**
		 * `woocommerce_currency`.
		 *
		 * @param string $currency Currency.
		 * @return string
		 */
		public function filter_currency( $currency ) {
			if ( $this->resolving ) {
				return $currency;
			}
			$active = $this->active_currency();
			return $active !== $this->base_currency() ? $active : $currency;
		}

		/**
		 * `wc_price_args` — BHD/KWD/OMR/JOD are shown with their customary 3 decimals.
		 *
		 * Amounts are deliberately still rounded with the store setting (2 decimals):
		 * WooCommerce re-rounds order totals with wc_get_price_decimals() every time an
		 * order is loaded (admin, e-mails, REST), so a 3-decimal amount would silently
		 * become a different number outside checkout. Rounding to 0.01 keeps the amount
		 * identical everywhere; display then reads e.g. "12.030 د.ب".
		 *
		 * @param array $args Args.
		 * @return array
		 */
		public function filter_price_args( $args ) {
			$currency = ! empty( $args['currency'] ) ? $args['currency'] : get_woocommerce_currency();
			if ( $currency !== $this->base_currency() ) {
				$d = $this->decimals_for( $currency );
				if ( null !== $d ) {
					$args['decimals'] = $d;
				}
			}
			return $args;
		}

		/**
		 * `woocommerce_currency_symbol` — same short Arabic symbols as the theme chip.
		 *
		 * @param string $symbol   Symbol.
		 * @param string $currency Currency.
		 * @return string
		 */
		public function filter_symbol( $symbol, $currency ) {
			if ( $currency === $this->base_currency() ) {
				return $symbol;
			}
			foreach ( self::COUNTRY_MAP as $row ) {
				if ( $row[0] === $currency ) {
					return $row[2];
				}
			}
			return $symbol;
		}

		/**
		 * Generic amount filter.
		 *
		 * @param mixed $amount Amount.
		 * @return mixed
		 */
		public function convert_amount( $amount ) {
			return $this->to_currency( $amount );
		}

		/**
		 * Variation price cache must vary by currency.
		 *
		 * @param array $hash Hash parts.
		 * @return array
		 */
		public function filter_variation_hash( $hash ) {
			$hash[] = 'bq_cur_' . $this->active_currency();
			return $hash;
		}

		/**
		 * Tag packages with the currency so cached rates are never reused across currencies.
		 *
		 * @param array $packages Packages.
		 * @return array
		 */
		public function tag_shipping_packages( $packages ) {
			$currency = $this->active_currency();
			foreach ( $packages as $i => $package ) {
				$packages[ $i ]['bq_currency'] = $currency;
			}
			return $packages;
		}

		/**
		 * Convert computed shipping rates.
		 *
		 * @param WC_Shipping_Rate[] $rates   Rates.
		 * @param array              $package Package.
		 * @return WC_Shipping_Rate[]
		 */
		public function convert_package_rates( $rates, $package ) {
			$currency = isset( $package['bq_currency'] ) ? $package['bq_currency'] : $this->active_currency();
			if ( $currency === $this->base_currency() ) {
				return $rates;
			}
			foreach ( $rates as $rate ) {
				if ( ! $rate instanceof WC_Shipping_Rate ) {
					continue;
				}
				$rate->set_cost( $this->to_currency( (float) $rate->get_cost(), $currency ) );
				$taxes = array();
				foreach ( (array) $rate->get_taxes() as $key => $tax ) {
					$taxes[ $key ] = $this->to_currency( (float) $tax, $currency );
				}
				$rate->set_taxes( $taxes );
			}
			return $rates;
		}

		/**
		 * Convert the free-shipping minimum order amount.
		 *
		 * @param mixed  $value Option value.
		 * @param string $key   Option key.
		 * @return mixed
		 */
		public function convert_free_shipping_option( $value, $key ) {
			return 'min_amount' === $key ? $this->to_currency( $value ) : $value;
		}

		/**
		 * Convert fixed coupon amounts (percentage coupons are untouched).
		 *
		 * @param mixed     $amount Amount.
		 * @param WC_Coupon $coupon Coupon.
		 * @return mixed
		 */
		public function convert_coupon_amount( $amount, $coupon ) {
			if ( $coupon instanceof WC_Coupon && $coupon->is_type( array( 'fixed_cart', 'fixed_product' ) ) ) {
				return $this->to_currency( $amount );
			}
			return $amount;
		}

		/*
		|--------------------------------------------------------------------------
		| Cart / customer state
		|--------------------------------------------------------------------------
		*/

		/**
		 * Pre-fill the delivery country from the geolocated / chosen country cookie,
		 * only while the customer has not typed an address yet.
		 */
		public function sync_customer_country_from_cookie() {
			if ( is_admin() && ! wp_doing_ajax() ) {
				return;
			}
			if ( ! function_exists( 'WC' ) || ! WC()->session || ! WC()->customer instanceof WC_Customer ) {
				return;
			}
			$cookie = isset( $_COOKIE[ self::COOKIE ] ) ? strtoupper( sanitize_text_field( wp_unslash( $_COOKIE[ self::COOKIE ] ) ) ) : '';
			if ( ! isset( self::COUNTRY_MAP[ $cookie ] ) ) {
				return;
			}
			if ( WC()->session->get( 'bq_cc_synced' ) === $cookie ) {
				return;
			}
			WC()->session->set( 'bq_cc_synced', $cookie );

			$customer = WC()->customer;
			if ( $customer->get_billing_address_1() || $customer->get_shipping_address_1() ) {
				return; // Real address present: the checkout country is authoritative.
			}
			$allowed = WC()->countries ? array_keys( WC()->countries->get_shipping_countries() ) : array();
			if ( $allowed && ! in_array( $cookie, $allowed, true ) ) {
				return;
			}
			if ( $customer->get_shipping_country() === $cookie && $customer->get_billing_country() === $cookie ) {
				return;
			}
			$customer->set_billing_country( $cookie );
			$customer->set_shipping_country( $cookie );
			$customer->set_billing_state( '' );
			$customer->set_shipping_state( '' );
			$customer->save();
		}

		/**
		 * Totals restored from the session may have been computed in another currency.
		 */
		public function maybe_recalculate_cart() {
			if ( ! function_exists( 'WC' ) || ! WC()->cart || ! WC()->session || WC()->cart->is_empty() ) {
				return;
			}
			if ( WC()->session->get( 'bq_cart_currency' ) !== get_woocommerce_currency() ) {
				WC()->cart->calculate_totals();
			}
		}

		/**
		 * Same as maybe_recalculate_cart() for REST (Store API) requests.
		 *
		 * @param mixed $result Pre-dispatch result.
		 * @return mixed
		 */
		public function maybe_recalculate_cart_for_rest( $result ) {
			$this->maybe_recalculate_cart();
			return $result;
		}

		/**
		 * Remember which currency the session totals are in.
		 */
		public function remember_cart_currency() {
			if ( function_exists( 'WC' ) && WC()->session ) {
				WC()->session->set( 'bq_cart_currency', get_woocommerce_currency() );
			}
		}

		/*
		|--------------------------------------------------------------------------
		| Orders
		|--------------------------------------------------------------------------
		*/

		/**
		 * Save the rate used so the order can always be reconciled in base currency.
		 *
		 * @param WC_Order $order Order.
		 */
		public function stamp_order( $order ) {
			if ( ! $order instanceof WC_Order ) {
				return;
			}
			$base     = $this->base_currency();
			$currency = $order->get_currency();
			if ( $currency === $base ) {
				return;
			}
			$rate = $this->rate( $currency );
			if ( ! $rate ) {
				return;
			}
			$order->update_meta_data( '_bq_base_currency', $base );
			$order->update_meta_data( '_bq_fx_rate', wc_format_decimal( $rate, 8 ) );
		}

		/**
		 * Show the rate and base-currency equivalent on the admin order screen.
		 *
		 * @param WC_Order $order Order.
		 */
		public function render_admin_order_info( $order ) {
			if ( ! $order instanceof WC_Order ) {
				return;
			}
			$rate = (float) $order->get_meta( '_bq_fx_rate' );
			$base = (string) $order->get_meta( '_bq_base_currency' );
			if ( $rate <= 0 || '' === $base ) {
				return;
			}
			$equivalent = (float) $order->get_total() / $rate;
			echo '<p class="form-field form-field-wide"><strong>' . esc_html__( 'عملة العميل', 'brqosh-geo-currency' ) . ':</strong> '
				. esc_html( $order->get_currency() )
				. ' — ' . esc_html( sprintf( '1 %s = %s %s', $base, wc_format_decimal( $rate, 6 ), $order->get_currency() ) )
				. '<br><strong>' . esc_html__( 'المعادل بعملة المتجر', 'brqosh-geo-currency' ) . ':</strong> '
				. wp_kses_post( wc_price( $equivalent, array( 'currency' => $base ) ) )
				. '</p>';
		}

		/*
		|--------------------------------------------------------------------------
		| Front-end
		|--------------------------------------------------------------------------
		*/

		/**
		 * Small note on cart / checkout when prices are converted.
		 */
		public function render_checkout_notice() {
			$active = $this->active_currency();
			$base   = $this->base_currency();
			if ( $active === $base ) {
				return;
			}
			$rate = $this->rate( $active );
			printf(
				'<p class="bq-geo-currency-note" style="font-size:.85em;opacity:.8;margin:.5em 0">%s</p>',
				esc_html(
					sprintf(
						/* translators: 1: currency code, 2: rate line */
						__( 'الأسعار معروضة ومحسوبة بعملة بلد التوصيل (%1$s) وفق سعر صرف اليوم: %2$s', 'brqosh-geo-currency' ),
						$active,
						sprintf( '1 %s = %s %s', $base, wc_format_decimal( $rate, 4 ), $active )
					)
				)
			);
		}

		/**
		 * Enqueue the cookie / country-sync script.
		 */
		public function enqueue_script() {
			$handle = 'brqosh-geo-currency';
			wp_enqueue_script(
				$handle,
				plugins_url( 'assets/geo-currency.js', __FILE__ ),
				array(),
				self::VERSION,
				array(
					'in_footer' => true,
					'strategy'  => 'defer',
				)
			);
			wp_add_inline_script(
				$handle,
				'window.bqGeoCurrency=' . wp_json_encode(
					array(
						'countries'  => array_keys( self::COUNTRY_MAP ),
						'cookie'     => self::COOKIE,
						'isCheckout' => function_exists( 'is_checkout' ) && ( is_checkout() || is_cart() ),
						'currency'   => get_woocommerce_currency(),
					)
				) . ';',
				'before'
			);
		}
	}

	add_action(
		'plugins_loaded',
		static function () {
			if ( class_exists( 'WooCommerce' ) ) {
				Brqosh_Geo_Currency::instance();
			}
		},
		20
	);

	// Declare HPOS + Cart/Checkout blocks compatibility.
	add_action(
		'before_woocommerce_init',
		static function () {
			if ( class_exists( '\Automattic\WooCommerce\Utilities\FeaturesUtil' ) ) {
				\Automattic\WooCommerce\Utilities\FeaturesUtil::declare_compatibility( 'custom_order_tables', __FILE__, true );
				\Automattic\WooCommerce\Utilities\FeaturesUtil::declare_compatibility( 'cart_checkout_blocks', __FILE__, true );
			}
		}
	);

	register_deactivation_hook(
		__FILE__,
		static function () {
			delete_transient( Brqosh_Geo_Currency::RATES_TRANSIENT );
			delete_transient( Brqosh_Geo_Currency::RATES_BACKOFF );
		}
	);

endif;
