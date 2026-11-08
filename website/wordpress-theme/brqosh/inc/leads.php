<?php
/**
 * Lead storage: a private post type visible to administrators only, with a
 * read-only detail view and PDPL-minded retention.
 *
 * Leads hold personal data (name, phone, email) and licence identifiers, so
 * every capability maps to manage_options and nothing is exposed publicly or
 * through REST.
 *
 * @package Brqosh
 */

defined( 'ABSPATH' ) || exit;

const BRQOSH_LEAD_TYPE = 'brqosh_lead';

/**
 * Human labels for the form types and their fields.
 *
 * @return array<string,array{label:string,fields:array<string,string>}>
 */
function brqosh_lead_schema(): array {
	return array(
		'demo'       => array(
			'label'  => 'طلب تجربة',
			'fields' => array(
				'name'     => 'الاسم',
				'org'      => 'المنشأة',
				'sector'   => 'نوع النشاط',
				'users'    => 'عدد المستخدمين',
				'phone'    => 'الجوال',
				'email'    => 'البريد',
				'message'  => 'ملاحظات',
			),
		),
		'contact'    => array(
			'label'  => 'رسالة تواصل',
			'fields' => array(
				'name'    => 'الاسم',
				'org'     => 'المنشأة',
				'phone'   => 'الجوال',
				'email'   => 'البريد',
				'message' => 'الرسالة',
			),
		),
		'activation' => array(
			'label'  => 'طلب رمز تفعيل',
			'fields' => array(
				'org'          => 'المنشأة',
				'serial'       => 'الرقم التسلسلي',
				'request_code' => 'رقم الطلب (مفتاح الجهاز)',
				'phone'        => 'الجوال',
				'email'        => 'البريد',
			),
		),
	);
}

add_action(
	'init',
	static function (): void {
		register_post_type(
			BRQOSH_LEAD_TYPE,
			array(
				'labels'              => array(
					'name'          => 'الطلبات الواردة',
					'singular_name' => 'طلب',
					'menu_name'     => 'الطلبات الواردة',
					'all_items'     => 'كل الطلبات',
					'edit_item'     => 'تفاصيل الطلب',
					'search_items'  => 'بحث في الطلبات',
					'not_found'     => 'لا توجد طلبات',
				),
				'public'              => false,
				'publicly_queryable'  => false,
				'exclude_from_search' => true,
				'show_ui'             => true,
				'show_in_menu'        => true,
				'show_in_rest'        => false,
				'menu_icon'           => 'dashicons-email-alt',
				'menu_position'       => 26,
				'supports'            => array( 'title' ),
				'rewrite'             => false,
				'query_var'           => false,
				'capability_type'     => 'post',
				'map_meta_cap'        => false,
				'capabilities'        => array(
					'edit_post'          => 'manage_options',
					'read_post'          => 'manage_options',
					'delete_post'        => 'manage_options',
					'edit_posts'         => 'manage_options',
					'edit_others_posts'  => 'manage_options',
					'delete_posts'       => 'manage_options',
					'publish_posts'      => 'manage_options',
					'read_private_posts' => 'manage_options',
					'create_posts'       => 'do_not_allow',
				),
			)
		);
	}
);

/**
 * Short identity of a lead: organisation if given, else the person's name.
 *
 * @param array<string,string> $fields Clean fields.
 * @return string
 */
function brqosh_lead_who( array $fields ): string {
	return '' !== ( $fields['org'] ?? '' ) ? $fields['org'] : ( $fields['name'] ?? '' );
}

/**
 * Stores a validated lead. Returns the post ID or 0.
 *
 * @param string               $type   Form type key.
 * @param array<string,string> $fields Clean field values.
 * @return int
 */
function brqosh_store_lead( string $type, array $fields ): int {
	$schema = brqosh_lead_schema()[ $type ];
	$who    = brqosh_lead_who( $fields );
	$id     = wp_insert_post(
		array(
			'post_type'   => BRQOSH_LEAD_TYPE,
			'post_status' => 'private',
			'post_title'  => $schema['label'] . ' — ' . $who,
		),
		true
	);
	if ( is_wp_error( $id ) ) {
		return 0;
	}
	update_post_meta( $id, '_brqosh_type', $type );
	foreach ( $fields as $key => $value ) {
		update_post_meta( $id, '_brqosh_' . $key, $value );
	}
	update_post_meta( $id, '_brqosh_consent_at', gmdate( 'c' ) );
	update_post_meta( $id, '_brqosh_status', 'new' );
	return (int) $id;
}

// List table columns.
add_filter(
	'manage_' . BRQOSH_LEAD_TYPE . '_posts_columns',
	static function (): array {
		return array(
			'cb'            => '<input type="checkbox" />',
			'title'         => 'الطلب',
			'brqosh_type'   => 'النوع',
			'brqosh_phone'  => 'الجوال',
			'brqosh_email'  => 'البريد',
			'brqosh_status' => 'الحالة',
			'date'          => 'التاريخ',
		);
	}
);

add_action(
	'manage_' . BRQOSH_LEAD_TYPE . '_posts_custom_column',
	static function ( string $column, int $post_id ): void {
		$schema = brqosh_lead_schema();
		switch ( $column ) {
			case 'brqosh_type':
				$type = (string) get_post_meta( $post_id, '_brqosh_type', true );
				echo esc_html( $schema[ $type ]['label'] ?? $type );
				break;
			case 'brqosh_phone':
				$phone = (string) get_post_meta( $post_id, '_brqosh_phone', true );
				echo '<a dir="ltr" href="' . esc_url( 'https://wa.me/' . $phone ) . '" target="_blank" rel="noopener">+' . esc_html( $phone ) . '</a>';
				break;
			case 'brqosh_email':
				$email = (string) get_post_meta( $post_id, '_brqosh_email', true );
				echo '<a href="' . esc_url( 'mailto:' . $email ) . '">' . esc_html( $email ) . '</a>';
				break;
			case 'brqosh_status':
				echo 'done' === get_post_meta( $post_id, '_brqosh_status', true ) ? 'تمت المتابعة' : '<strong>جديد</strong>';
				break;
		}
	},
	10,
	2
);

// Read-only detail box and a "followed up" toggle.
add_action(
	'add_meta_boxes_' . BRQOSH_LEAD_TYPE,
	static function (): void {
		add_meta_box(
			'brqosh_lead_details',
			'تفاصيل الطلب',
			static function ( WP_Post $post ): void {
				$type   = (string) get_post_meta( $post->ID, '_brqosh_type', true );
				$schema = brqosh_lead_schema()[ $type ] ?? array( 'fields' => array() );
				echo '<table class="widefat striped"><tbody>';
				foreach ( $schema['fields'] as $key => $label ) {
					$value = (string) get_post_meta( $post->ID, '_brqosh_' . $key, true );
					echo '<tr><th style="width:30%">' . esc_html( $label ) . '</th><td style="white-space:pre-wrap">' . esc_html( $value ) . '</td></tr>';
				}
				echo '<tr><th>وقت الموافقة على الخصوصية</th><td dir="ltr">' . esc_html( (string) get_post_meta( $post->ID, '_brqosh_consent_at', true ) ) . '</td></tr>';
				echo '</tbody></table>';
				$done = 'done' === get_post_meta( $post->ID, '_brqosh_status', true );
				wp_nonce_field( 'brqosh_lead_status_' . $post->ID, 'brqosh_lead_status_nonce' );
				echo '<p><label><input type="checkbox" name="brqosh_lead_done" value="1" ' . checked( $done, true, false ) . '> تمت المتابعة</label></p>';
			},
			BRQOSH_LEAD_TYPE,
			'normal',
			'high'
		);
	}
);

add_action(
	'save_post_' . BRQOSH_LEAD_TYPE,
	static function ( int $post_id ): void {
		$nonce = isset( $_POST['brqosh_lead_status_nonce'] ) ? sanitize_text_field( wp_unslash( $_POST['brqosh_lead_status_nonce'] ) ) : '';
		if ( '' === $nonce || ! wp_verify_nonce( $nonce, 'brqosh_lead_status_' . $post_id ) || ! current_user_can( 'manage_options' ) ) {
			return;
		}
		update_post_meta( $post_id, '_brqosh_status', empty( $_POST['brqosh_lead_done'] ) ? 'new' : 'done' );
	}
);

/**
 * Daily retention: permanently deletes leads older than the configured days.
 */
function brqosh_purge_old_leads(): void {
	$days = max( 30, (int) brqosh_setting( 'lead_retention' ) );
	$old  = get_posts(
		array(
			'post_type'      => BRQOSH_LEAD_TYPE,
			'post_status'    => 'any',
			'fields'         => 'ids',
			'posts_per_page' => 200,
			'date_query'     => array( array( 'before' => gmdate( 'Y-m-d H:i:s', time() - $days * DAY_IN_SECONDS ), 'column' => 'post_date_gmt' ) ),
		)
	);
	foreach ( $old as $id ) {
		wp_delete_post( (int) $id, true );
	}
}
add_action( 'brqosh_purge_leads', 'brqosh_purge_old_leads' );

add_action(
	'init',
	static function (): void {
		if ( ! wp_next_scheduled( 'brqosh_purge_leads' ) ) {
			wp_schedule_event( time() + HOUR_IN_SECONDS, 'daily', 'brqosh_purge_leads' );
		}
	}
);

add_action(
	'switch_theme',
	static function (): void {
		wp_clear_scheduled_hook( 'brqosh_purge_leads' );
	}
);
