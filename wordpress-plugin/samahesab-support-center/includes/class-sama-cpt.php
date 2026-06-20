<?php
/**
 * 🆘 HC-WP — ثبتِ Custom Post Typeهای مرکزِ پشتیبانی.
 * samahesab_ticket / samahesab_bug / samahesab_feature / samahesab_release / samahesab_article
 */
if ( ! defined( 'ABSPATH' ) ) { exit; }

class SamaHesab_CPT {

    private static $instance = null;
    public static function instance() {
        if ( null === self::$instance ) { self::$instance = new self(); }
        return self::$instance;
    }

    public function hooks() {
        add_action( 'init', array( $this, 'register' ) );
    }

    /** فهرستِ CPTها: کلید → برچسبِ مفرد/جمع. */
    public static function types() {
        return array(
            'samahesab_ticket'  => array( 'تیکت', 'تیکت‌ها', 'dashicons-tickets-alt' ),
            'samahesab_bug'     => array( 'گزارشِ باگ', 'گزارش‌های باگ', 'dashicons-warning' ),
            'samahesab_feature' => array( 'درخواستِ قابلیت', 'درخواست‌های قابلیت', 'dashicons-lightbulb' ),
            'samahesab_release' => array( 'یادداشتِ نسخه', 'یادداشت‌های نسخه', 'dashicons-update' ),
            'samahesab_article' => array( 'مقالهٔ دانشنامه', 'دانشنامه', 'dashicons-book' ),
            'samahesab_remote'  => array( 'نشستِ ریموت', 'پشتیبانیِ ریموت', 'dashicons-desktop' ),
        );
    }

    public function register() {
        foreach ( self::types() as $slug => $info ) {
            list( $singular, $plural, $icon ) = $info;

            // مقاله و یادداشتِ نسخه عمومی (برای پرتال)، بقیه خصوصی (فقط ادمین/REST).
            $public = in_array( $slug, array( 'samahesab_article', 'samahesab_release' ), true );

            register_post_type( $slug, array(
                'labels'              => array(
                    'name'          => $plural,
                    'singular_name' => $singular,
                    'add_new_item'  => $singular . ' جدید',
                    'edit_item'     => 'ویرایشِ ' . $singular,
                    'search_items'  => 'جست‌وجوی ' . $plural,
                ),
                'public'              => $public,
                'show_ui'             => true,
                'show_in_menu'        => false, // زیرِ منوی «SamaHesab» نمایش داده می‌شود.
                'show_in_rest'        => true,
                'has_archive'         => $public,
                'menu_icon'           => $icon,
                'supports'            => array( 'title', 'editor', 'custom-fields', 'comments' ),
                'capability_type'     => 'post',
            ) );
        }
    }

    /** برچسب‌های فارسیِ وضعیت (هم‌راستا با enum سمتِ ERP). */
    public static function status_labels() {
        return array(
            0 => 'جدید', 1 => 'باز', 2 => 'در حالِ بررسی', 3 => 'منتظرِ مشتری',
            4 => 'در حالِ آزمایش', 5 => 'حل‌شده', 6 => 'بسته',
        );
    }

    public static function severity_labels() {
        return array( 0 => 'کم', 1 => 'متوسط', 2 => 'زیاد', 3 => 'بحرانی' );
    }
}
