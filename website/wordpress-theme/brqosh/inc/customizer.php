<?php
/**
 * Customizer: contact data, pricing, release metadata and lead handling.
 *
 * Everything an operator changes without touching code lives here
 * (Appearance → Customize → Brqosh).
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

add_action(
	'customize_register',
	static function ( WP_Customize_Manager $wp_customize ): void {
		$wp_customize->add_panel( 'brqosh', array( 'title' => 'إعدادات برقوش', 'priority' => 30 ) );

		$sections = array(
			'brqosh_contact' => array(
				'title'  => 'بيانات التواصل والشركة',
				'fields' => array(
					'email'        => array( 'البريد الإلكتروني', 'email' ),
					'phone'        => array( 'رقم الجوال (بصيغة دولية)', 'phone' ),
					'whatsapp'     => array( 'رقم واتساب (أرقام فقط، مثل 9665…)', 'digits' ),
					'address'      => array( 'العنوان', 'text' ),
					'company_name' => array( 'الاسم النظامي للشركة', 'text' ),
					'cr_number'    => array( 'رقم السجل التجاري (اختياري)', 'digits' ),
					'vat_number'   => array( 'الرقم الضريبي (اختياري)', 'digits' ),
				),
			),
			'brqosh_pricing' => array(
				'title'  => 'الأسعار والتجربة',
				'fields' => array(
					'price_from' => array( 'السعر الابتدائي (ر.س)', 'text' ),
					'trial_days' => array( 'أيام التجربة', 'digits' ),
				),
			),
			'brqosh_release' => array(
				'title'  => 'الإصدار والتحميل',
				'fields' => array(
					'release_version' => array( 'رقم الإصدار', 'version' ),
					'release_date'    => array( 'تاريخ الإصدار (YYYY-MM-DD)', 'date' ),
					'release_size'    => array( 'حجم المُنصِّب', 'text' ),
					'release_sha256'  => array( 'بصمة SHA-256', 'sha256' ),
					'download_url'    => array( 'رابط تحميل مباشر (اتركه فارغاً لطلب الرابط بالنموذج)', 'url' ),
				),
			),
			'brqosh_leads'   => array(
				'title'  => 'الطلبات الواردة',
				'fields' => array(
					'lead_email'     => array( 'بريد استقبال الطلبات (فارغ = بريد مدير الموقع)', 'email' ),
					'lead_retention' => array( 'مدة الاحتفاظ بالطلبات بالأيام (PDPL)', 'digits' ),
				),
			),
		);

		$defaults = brqosh_setting_defaults();
		$priority = 0;
		foreach ( $sections as $section_id => $section ) {
			$wp_customize->add_section( $section_id, array( 'title' => $section['title'], 'panel' => 'brqosh', 'priority' => ++$priority ) );
			foreach ( $section['fields'] as $key => list( $label, $type ) ) {
				$wp_customize->add_setting(
					'brqosh_' . $key,
					array(
						'default'           => $defaults[ $key ] ?? '',
						'sanitize_callback' => brqosh_customizer_sanitizer( $type ),
						'transport'         => 'refresh',
					)
				);
				$wp_customize->add_control(
					'brqosh_' . $key,
					array(
						'label'   => $label,
						'section' => $section_id,
						'type'    => 'url' === $type ? 'url' : ( 'email' === $type ? 'email' : 'text' ),
					)
				);
			}
		}
	}
);

/**
 * Returns a sanitize callback for a Customizer field type.
 *
 * Invalid input returns '' (WordPress then keeps the field empty and the
 * template falls back to the default), never a partially cleaned value.
 *
 * @param string $type Field type.
 * @return callable
 */
function brqosh_customizer_sanitizer( string $type ): callable {
	return static function ( $value ) use ( $type ): string {
		$value = trim( (string) $value );
		switch ( $type ) {
			case 'email':
				return (string) sanitize_email( $value );
			case 'url':
				return '' === $value ? '' : (string) esc_url_raw( $value, array( 'https' ) );
			case 'digits':
				return (string) preg_replace( '/\D+/', '', $value );
			case 'phone':
				return preg_match( '/^\+?\d{8,15}$/', preg_replace( '/[\s-]+/', '', $value ) ) ? preg_replace( '/[\s-]+/', '', $value ) : '';
			case 'version':
				return preg_match( '/^\d+\.\d+\.\d+$/', $value ) ? $value : '';
			case 'date':
				return preg_match( '/^\d{4}-\d{2}-\d{2}$/', $value ) ? $value : '';
			case 'sha256':
				return preg_match( '/^[a-f0-9]{64}$/i', $value ) ? strtolower( $value ) : '';
			default:
				return sanitize_text_field( $value );
		}
	};
}
