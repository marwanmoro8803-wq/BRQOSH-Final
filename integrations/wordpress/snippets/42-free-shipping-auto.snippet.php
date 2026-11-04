/* BRQOSH: shipping rates, after discounts and before tax. */
add_filter('woocommerce_package_rates', function ($rates, $package) {
    if (!function_exists('WC') || !WC()->cart) return $rates;
    $country = isset($package['destination']['country']) ? strtoupper($package['destination']['country']) : '';
    $thresholds = array('SA'=>299, 'JO'=>399, 'AE'=>449, 'KW'=>449, 'BH'=>449, 'QA'=>599, 'OM'=>599, 'EG'=>150, 'PS'=>150);
    $international = array('SA'=>0, 'JO'=>0, 'AE'=>45, 'KW'=>45, 'BH'=>45, 'QA'=>89, 'OM'=>89, 'EG'=>149, 'PS'=>89);
    if (!isset($thresholds[$country])) return $rates;
    $selected = null;
    foreach ($rates as $rate) {
        if (in_array($rate->get_method_id(), array('flat_rate', 'free_shipping'), true)) { $selected = $rate; break; }
    }
    if (!$selected) return $rates;
    $subtotal = (float) WC()->cart->get_cart_contents_total();
    /* Brqosh Geo Currency: thresholds are in SAR; compare the cart in SAR (costs are converted later at priority 999). */
    $bq_tol = 0.00001;
    if (class_exists('Brqosh_Geo_Currency')) { $bq_raw = $subtotal; $subtotal = Brqosh_Geo_Currency::instance()->to_base($subtotal); if ($bq_raw != $subtotal) $bq_tol = 0.5; }
    $local = $subtotal + $bq_tol >= $thresholds[$country] ? 0 : 20;
    $base = (float) $international[$country];
    $taxes = array();
    if ($local && in_array($country, array('SA','JO'), true) && wc_tax_enabled()) {
        $taxes = WC_Tax::calc_inclusive_tax($local, WC_Tax::get_shipping_tax_rates());
    }
    $label = $base ? ($local ? 'الشحن الدولي والمحلي' : 'الشحن الدولي (الشحن المحلي مجاني)') : ($local ? 'الشحن المحلي' : 'الشحن المحلي مجاني');
    $selected->set_label($label);
    $selected->set_cost($base + $local - array_sum($taxes));
    $selected->set_taxes($taxes);
    return array($selected->get_id() => $selected);
}, 101, 2);


/* Force WooCommerce to recalculate cached customer-session shipping once. */
add_filter('woocommerce_cart_shipping_packages', function ($packages) {
    foreach ($packages as $key => $package) { $packages[$key]['bq_shipping_policy_version'] = '2026-11-04-v1'; }
    return $packages;
}, 10);

/* The threshold applies to goods after discounts, before taxes. */
add_action('woocommerce_before_cart_table', function () {
    if (!WC()->cart || WC()->cart->is_empty() || !WC()->customer) return;
    $country = WC()->customer->get_shipping_country();
    $thresholds = array('SA'=>299, 'JO'=>399, 'AE'=>449, 'KW'=>449, 'BH'=>449, 'QA'=>599, 'OM'=>599, 'EG'=>150, 'PS'=>150);
    if (!isset($thresholds[$country])) return;
    $bq_g = class_exists('Brqosh_Geo_Currency') ? Brqosh_Geo_Currency::instance() : null;
    $bq_sub = (float) WC()->cart->get_cart_contents_total();
    $remaining = max(0, $thresholds[$country] - ($bq_g ? $bq_g->to_base($bq_sub) : $bq_sub));
    if ($bq_g && $remaining < 0.5 && $bq_g->to_base($bq_sub) != $bq_sub) $remaining = 0;
    $international = !in_array($country, array('SA', 'JO'), true);
    if ($remaining > 0.00001) {
        $msg = 'أضف ' . wc_price($bq_g ? $bq_g->from_base($remaining) : $remaining) . ' من قيمة المنتجات بعد الخصم وقبل الضريبة لإعفاء رسم الشحن المحلي (' . wp_strip_all_tags(wc_price($bq_g ? $bq_g->from_base(20) : 20)) . ').';
        if ($international) $msg .= ' رسوم الشحن الدولي تبقى مستحقة.';
        echo '<p class="bq-ship-hint" style="background:#f7f1e6;padding:.8em 1em;border-radius:6px">' . wp_kses_post($msg) . '</p>';
    } else {
        $msg = $international ? 'الشحن المحلي مجاني؛ تبقى رسوم الشحن الدولي.' : 'الشحن المحلي مجاني الآن.';
        echo '<p class="bq-ship-hint" style="background:#eaf3e6;padding:.8em 1em;border-radius:6px">' . esc_html($msg) . '</p>';
    }
}, 5);

/* تعريب رسائل السلة الإنجليزية */
add_filter('gettext', function ($t, $text, $domain) {
    if ($text === 'Your cart is empty. Shop now') return 'سلتك فارغة. تسوّق الآن';
    if ($text === 'Cart Total') return 'الإجمالي';
    if ($text === 'Your cart is empty.') return 'سلتك فارغة.';
    if ($text === 'Shop now') return 'تسوّق الآن';
    return $t;
}, 20, 3);