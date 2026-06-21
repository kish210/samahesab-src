<?php
/**
 * Plugin Name:       SamaHesab Support Center
 * Plugin URI:        https://kishwifi.com
 * Description:        مرکزِ پشتیبانی و بازخوردِ سما حساب — دریافتِ گزارشِ باگ/درخواستِ قابلیت/تیکت از نصب‌های ERP، دانشنامه، یادداشت‌های نسخه و پرتالِ مشتری. (🆘 HC-WP)
 * Version:           1.4.0
 * Author:            سماع رایانه کیش
 * Author URI:        https://kishwifi.com
 * Text Domain:       samahesab-support
 * Domain Path:       /languages
 * License:           GPL-2.0-or-later
 *
 * این پلاگین با کلاینتِ ERP (SupportApiClient) از طریقِ REST مسیرِ /wp-json/samahesab/v1/* کار می‌کند.
 * احراز با «کلید-API» (هدرِ X-SamaHesab-ApiKey). فقط دادهٔ پشتیبانی/تشخیصی نگه می‌دارد — هیچ دادهٔ مالی.
 */

if ( ! defined( 'ABSPATH' ) ) {
    exit; // دسترسیِ مستقیم ممنوع.
}

define( 'SAMAHESAB_SC_VERSION', '1.4.0' );
define( 'SAMAHESAB_SC_FILE', __FILE__ );
define( 'SAMAHESAB_SC_DIR', plugin_dir_path( __FILE__ ) );
define( 'SAMAHESAB_SC_URL', plugin_dir_url( __FILE__ ) );
define( 'SAMAHESAB_SC_NS', 'samahesab/v1' );

require_once SAMAHESAB_SC_DIR . 'includes/class-sama-cpt.php';
require_once SAMAHESAB_SC_DIR . 'includes/class-sama-auth.php';
require_once SAMAHESAB_SC_DIR . 'includes/class-sama-rest.php';
require_once SAMAHESAB_SC_DIR . 'includes/class-sama-admin.php';
require_once SAMAHESAB_SC_DIR . 'includes/class-sama-email.php';
require_once SAMAHESAB_SC_DIR . 'includes/class-sama-portal.php';
require_once SAMAHESAB_SC_DIR . 'includes/class-sama-landing.php';

/**
 * بوت‌استرپِ پلاگین — همهٔ بخش‌ها را راه می‌اندازد.
 */
function samahesab_sc_boot() {
    SamaHesab_CPT::instance()->hooks();
    SamaHesab_REST::instance()->hooks();
    SamaHesab_Admin::instance()->hooks();
    SamaHesab_Email::instance()->hooks();
    SamaHesab_Portal::instance()->hooks();
    SamaHesab_Landing::instance()->hooks();
}
add_action( 'plugins_loaded', 'samahesab_sc_boot' );

/**
 * فعال‌سازی: ثبتِ CPTها سپس flushِ rewrite تا مسیرها فعال شوند.
 */
function samahesab_sc_activate() {
    SamaHesab_CPT::instance()->register();
    flush_rewrite_rules();

    // اگر کلیدی تنظیم نشده، یک کلیدِ نمونه برای شروع بساز (مدیر بعداً تغییر می‌دهد).
    if ( ! get_option( 'samahesab_api_keys' ) ) {
        update_option( 'samahesab_api_keys', array(
            array(
                'customer_id' => 'DEMO',
                'api_key'     => wp_generate_password( 32, false ),
                'license_id'  => '',
                'label'       => 'مشتریِ نمونه',
            ),
        ) );
    }
}
register_activation_hook( __FILE__, 'samahesab_sc_activate' );

/**
 * غیرفعال‌سازی: پاک‌سازیِ rewrite (دادهٔ CPT حفظ می‌شود).
 */
function samahesab_sc_deactivate() {
    flush_rewrite_rules();
}
register_deactivation_hook( __FILE__, 'samahesab_sc_deactivate' );
