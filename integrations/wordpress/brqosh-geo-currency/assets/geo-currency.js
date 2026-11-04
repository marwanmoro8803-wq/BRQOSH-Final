/**
 * Brqosh Geo Currency — front-end glue.
 *
 * 1. Persists the visitor's country (theme chip choice or IP geolocation) in the
 *    `bq_cc` cookie so the server can pre-select the delivery country and price
 *    the cart / checkout in that country's currency.
 * 2. On checkout, when the customer changes the delivery country, keeps the
 *    theme chip (localStorage `bq_cc`) and the cookie in sync.
 * 3. When the chip is used on the checkout page, moves the checkout country
 *    select to the same country so WooCommerce recalculates in that currency.
 */
(function () {
	'use strict';

	var cfg = window.bqGeoCurrency || {};
	var COOKIE = cfg.cookie || 'bq_cc';
	var allowed = cfg.countries || [];

	function isAllowed(cc) {
		return typeof cc === 'string' && allowed.indexOf(cc) !== -1;
	}

	function ls(key, value) {
		try {
			if (value === undefined) {
				return window.localStorage.getItem(key);
			}
			window.localStorage.setItem(key, value);
		} catch (e) {
			return null;
		}
		return null;
	}

	function getCookie() {
		var m = document.cookie.match(new RegExp('(?:^|; )' + COOKIE + '=([A-Z]{2})'));
		return m ? m[1] : '';
	}

	function setCookie(cc) {
		if (!isAllowed(cc)) {
			return;
		}
		var secure = window.location.protocol === 'https:' ? '; Secure' : '';
		document.cookie = COOKIE + '=' + cc + '; Max-Age=31536000; Path=/; SameSite=Lax' + secure;
	}

	function remember(cc) {
		if (!isAllowed(cc)) {
			return;
		}
		ls('bq_cc', cc);
		setCookie(cc);
	}

	// --- 1. Initial country: chip choice > geolocation --------------------------
	function initCountry() {
		if (getCookie()) {
			return;
		}
		var stored = ls('bq_cc');
		if (isAllowed(stored)) {
			setCookie(stored);
			return;
		}
		if (!window.fetch) {
			return;
		}
		window.fetch('https://api.country.is/')
			.then(function (r) { return r.json(); })
			.then(function (j) {
				if (j && isAllowed(j.country)) {
					setCookie(j.country);
					if (cfg.isCheckout) {
						applyToCheckout(j.country);
					}
				}
			})
			.catch(function () {});
	}

	// --- 2. Checkout country -> chip + cookie (classic checkout) ----------------
	function deliveryCountry() {
		var diff = document.getElementById('ship-to-different-address-checkbox');
		var ship = document.getElementById('shipping_country');
		var bill = document.getElementById('billing_country');
		if (diff && diff.checked && ship && ship.value) {
			return ship.value;
		}
		return bill ? bill.value : '';
	}

	function onCheckoutCountryChange() {
		var cc = deliveryCountry();
		if (isAllowed(cc)) {
			remember(cc);
		}
	}

	function bindClassicCheckout() {
		if (window.jQuery) {
			// selectWoo triggers jQuery events, not native ones.
			window.jQuery(document.body).on(
				'change',
				'#billing_country, #shipping_country, #ship-to-different-address-checkbox',
				onCheckoutCountryChange
			);
		} else {
			document.addEventListener('change', function (e) {
				var id = e.target && e.target.id;
				if (id === 'billing_country' || id === 'shipping_country' || id === 'ship-to-different-address-checkbox') {
					onCheckoutCountryChange();
				}
			});
		}
	}

	// --- 2b. Same for the block-based checkout ---------------------------------
	function bindBlockCheckout() {
		var wp = window.wp;
		if (!wp || !wp.data || !wp.data.subscribe || !wp.data.select) {
			return;
		}
		var last = '';
		wp.data.subscribe(function () {
			var store = wp.data.select('wc/store/cart');
			if (!store || !store.getCustomerData) {
				return;
			}
			var data = store.getCustomerData() || {};
			var cc = (data.shippingAddress && data.shippingAddress.country) ||
				(data.billingAddress && data.billingAddress.country) || '';
			if (cc && cc !== last) {
				last = cc;
				if (isAllowed(cc)) {
					remember(cc);
				}
			}
		});
	}

	// --- 3. Chip used on checkout -> move the checkout country ------------------
	function applyToCheckout(cc) {
		if (!isAllowed(cc)) {
			return;
		}
		var $ = window.jQuery;
		var bill = document.getElementById('billing_country');
		if (!bill) {
			// Cart page or block checkout: reload so the server re-prices.
			if (cfg.isCheckout && cfg.currency && document.querySelector('.woocommerce-cart, .wp-block-woocommerce-cart')) {
				window.location.reload();
			}
			return;
		}
		var targets = [bill];
		var diff = document.getElementById('ship-to-different-address-checkbox');
		var ship = document.getElementById('shipping_country');
		if (diff && diff.checked && ship) {
			targets = [ship];
		}
		targets.forEach(function (sel) {
			if (sel.value === cc || !sel.querySelector('option[value="' + cc + '"]')) {
				return;
			}
			if ($) {
				$(sel).val(cc).trigger('change');
			} else {
				sel.value = cc;
				sel.dispatchEvent(new Event('change', { bubbles: true }));
			}
		});
	}

	function bindChip() {
		document.addEventListener('click', function (e) {
			if (!e.target || !e.target.closest || !e.target.closest('.bqx-ccmenu button')) {
				return;
			}
			// The theme stores the choice in localStorage inside its own handler.
			window.setTimeout(function () {
				var cc = ls('bq_cc');
				if (!isAllowed(cc) || cc === getCookie()) {
					return;
				}
				setCookie(cc);
				if (cfg.isCheckout) {
					applyToCheckout(cc);
				}
			}, 0);
		});
	}

	function start() {
		initCountry();
		bindChip();
		if (cfg.isCheckout) {
			bindClassicCheckout();
			bindBlockCheckout();
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', start);
	} else {
		start();
	}
})();
