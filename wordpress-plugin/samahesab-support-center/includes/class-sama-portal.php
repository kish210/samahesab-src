<?php
/**
 * 🆘 HC-WP — پرتالِ مشتری (شورت‌کد). صفحاتِ frontend:
 *   [samahesab_portal]   → تیکت‌ها/گزارش‌های مشتریِ واردشده + وضعیت.
 *   [samahesab_kb]       → دانشنامه (جست‌وجو).
 *   [samahesab_releases] → یادداشت‌های نسخه.
 * نگاشتِ مشتری: متاکاربریِ `samahesab_customer_id` روی کاربرِ وردپرس.
 */
if ( ! defined( 'ABSPATH' ) ) { exit; }

class SamaHesab_Portal {

    private static $instance = null;
    public static function instance() {
        if ( null === self::$instance ) { self::$instance = new self(); }
        return self::$instance;
    }

    public function hooks() {
        add_shortcode( 'samahesab_portal', array( $this, 'portal' ) );
        add_shortcode( 'samahesab_kb', array( $this, 'kb' ) );
        add_shortcode( 'samahesab_releases', array( $this, 'releases' ) );
    }

    private function wrap( $html ) {
        return '<div class="samahesab-portal" dir="rtl" style="font-family:Tahoma,sans-serif;line-height:1.9">' . $html . '</div>';
    }

    public function portal() {
        if ( ! is_user_logged_in() ) {
            return $this->wrap( '<p>برای مشاهدهٔ درخواست‌ها لطفاً وارد شوید.</p>' );
        }
        $customer_id = get_user_meta( get_current_user_id(), 'samahesab_customer_id', true );

        $args = array(
            'post_type'   => array( 'samahesab_ticket', 'samahesab_bug', 'samahesab_feature' ),
            'numberposts' => 50,
            'orderby'     => 'date',
            'order'       => 'DESC',
        );
        if ( $customer_id ) {
            $args['meta_query'] = array(
                array( 'key' => 'sh_customer_id', 'value' => $customer_id ),
            );
        }
        $posts  = get_posts( $args );
        $labels = SamaHesab_CPT::status_labels();

        $rows = '';
        foreach ( $posts as $p ) {
            $status = intval( get_post_meta( $p->ID, 'sh_status', true ) );
            $rows  .= sprintf(
                '<tr><td>%s</td><td>%s</td><td><span style="background:#eef3fb;border-radius:10px;padding:2px 10px">%s</span></td><td>%s</td></tr>',
                esc_html( get_post_type_object( $p->post_type )->labels->singular_name ),
                esc_html( get_the_title( $p ) ),
                esc_html( isset( $labels[ $status ] ) ? $labels[ $status ] : '' ),
                esc_html( get_the_date( '', $p ) )
            );
        }
        if ( '' === $rows ) {
            $rows = '<tr><td colspan="4">درخواستی ثبت نشده است.</td></tr>';
        }

        $html  = '<h2>درخواست‌های من</h2>';
        $html .= '<table style="width:100%;border-collapse:collapse" border="1" cellpadding="8">';
        $html .= '<thead><tr><th>نوع</th><th>عنوان</th><th>وضعیت</th><th>تاریخ</th></tr></thead><tbody>' . $rows . '</tbody></table>';
        return $this->wrap( $html );
    }

    public function kb() {
        $search = isset( $_GET['kbs'] ) ? sanitize_text_field( wp_unslash( $_GET['kbs'] ) ) : '';
        $posts  = get_posts( array(
            'post_type'   => 'samahesab_article',
            'numberposts' => 50,
            's'           => $search,
        ) );
        $items = '';
        foreach ( $posts as $p ) {
            $items .= sprintf(
                '<li><a href="%s">%s</a> <small style="color:#888">(%s)</small></li>',
                esc_url( get_permalink( $p ) ),
                esc_html( get_the_title( $p ) ),
                esc_html( get_post_meta( $p->ID, 'sh_kind', true ) ?: 'article' )
            );
        }
        $html  = '<h2>دانشنامه</h2>';
        $html .= '<form method="get"><input type="search" name="kbs" value="' . esc_attr( $search ) . '" placeholder="جست‌وجو..."><button>جست‌وجو</button></form>';
        $html .= '<ul>' . ( $items ?: '<li>مقاله‌ای یافت نشد.</li>' ) . '</ul>';
        return $this->wrap( $html );
    }

    public function releases() {
        $posts = get_posts( array( 'post_type' => 'samahesab_release', 'numberposts' => 20 ) );
        $html  = '<h2>یادداشت‌های نسخه</h2>';
        foreach ( $posts as $p ) {
            $current = '1' === (string) get_post_meta( $p->ID, 'sh_iscurrent', true ) ? ' — نسخهٔ فعلی' : '';
            $html   .= '<h3>' . esc_html( get_the_title( $p ) . $current ) . '</h3>';
            $html   .= '<p><strong>قابلیت‌های نو:</strong> ' . esc_html( get_post_meta( $p->ID, 'sh_highlights', true ) ) . '</p>';
            $html   .= '<p><strong>رفعِ باگ:</strong> ' . esc_html( get_post_meta( $p->ID, 'sh_bugfixes', true ) ) . '</p>';
        }
        return $this->wrap( $html );
    }
}
