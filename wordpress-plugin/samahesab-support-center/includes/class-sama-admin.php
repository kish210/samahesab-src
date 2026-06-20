<?php
/**
 * 🆘 HC-WP — منوی ادمینِ «SamaHesab»: داشبورد + تیکت/باگ/قابلیت/نسخه/دانشنامه + تنظیماتِ کلید-API.
 */
if ( ! defined( 'ABSPATH' ) ) { exit; }

class SamaHesab_Admin {

    private static $instance = null;
    public static function instance() {
        if ( null === self::$instance ) { self::$instance = new self(); }
        return self::$instance;
    }

    public function hooks() {
        add_action( 'admin_menu', array( $this, 'menu' ) );
        add_action( 'admin_init', array( $this, 'register_settings' ) );
        // 🆘 HC-6b — ستون‌های فهرستِ پشتیبانیِ ریموت (کد/شناسهٔ RustDesk/مشتری).
        add_filter( 'manage_samahesab_remote_posts_columns', array( $this, 'remote_columns' ) );
        add_action( 'manage_samahesab_remote_posts_custom_column', array( $this, 'remote_column' ), 10, 2 );
    }

    public function remote_columns( $cols ) {
        return array(
            'cb'         => isset( $cols['cb'] ) ? $cols['cb'] : '',
            'title'      => 'کدِ پشتیبانی',
            'sh_connect' => 'شناسهٔ RustDesk',
            'sh_tool'    => 'ابزار',
            'sh_cust'    => 'مشتری / درخواست‌کننده',
            'date'       => 'تاریخ',
        );
    }

    public function remote_column( $col, $post_id ) {
        if ( 'sh_connect' === $col ) {
            $id = get_post_meta( $post_id, 'sh_connect_id', true );
            echo $id ? '<code>' . esc_html( $id ) . '</code>' : '—';
        } elseif ( 'sh_tool' === $col ) {
            echo esc_html( get_post_meta( $post_id, 'sh_tool', true ) ?: 'rustdesk' );
        } elseif ( 'sh_cust' === $col ) {
            echo esc_html( get_post_meta( $post_id, 'sh_customer_id', true ) . ' · ' . get_post_meta( $post_id, 'sh_requested_by', true ) );
        }
    }

    public function menu() {
        add_menu_page( 'SamaHesab', 'SamaHesab', 'manage_options', 'samahesab',
            array( $this, 'dashboard_page' ), 'dashicons-sos', 58 );

        add_submenu_page( 'samahesab', 'داشبورد', 'داشبورد', 'manage_options', 'samahesab',
            array( $this, 'dashboard_page' ) );

        // زیرمنو برای هر CPT.
        foreach ( SamaHesab_CPT::types() as $slug => $info ) {
            add_submenu_page( 'samahesab', $info[1], $info[1], 'manage_options',
                'edit.php?post_type=' . $slug );
        }

        add_submenu_page( 'samahesab', 'مشتریان و کلید-API', 'مشتریان (کلید-API)', 'manage_options',
            'samahesab-keys', array( $this, 'keys_page' ) );

        add_submenu_page( 'samahesab', 'دانلودِ نرم‌افزار', 'دانلود (سایت)', 'manage_options',
            'samahesab-download', array( $this, 'download_page' ) );
    }

    /** تنظیمِ نسخه/لینکِ دانلود که شورت‌کدِ [samahesab_product] استفاده می‌کند. */
    public function download_page() {
        if ( isset( $_POST['samahesab_save_dl'] ) && check_admin_referer( 'samahesab_dl' ) ) {
            update_option( 'samahesab_github_repo', sanitize_text_field( wp_unslash( $_POST['github_repo'] ?? '' ) ) );
            update_option( 'samahesab_version', sanitize_text_field( wp_unslash( $_POST['version'] ?? '' ) ) );
            update_option( 'samahesab_download_url', esc_url_raw( wp_unslash( $_POST['download_url'] ?? '' ) ) );
            delete_transient( 'samahesab_latest_release' );   // کَش پاک تا تغییر فوراً اعمال شود
            echo '<div class="notice notice-success"><p>ذخیره شد (کَشِ آخرین نسخه هم تازه شد).</p></div>';
        }
        if ( isset( $_POST['samahesab_clear_cache'] ) && check_admin_referer( 'samahesab_dl' ) ) {
            delete_transient( 'samahesab_latest_release' );
            echo '<div class="notice notice-success"><p>کَشِ «آخرین نسخه» پاک شد؛ دفعهٔ بعد زنده از گیت‌هاب خوانده می‌شود.</p></div>';
        }
        $repo    = get_option( 'samahesab_github_repo', 'kish210/SamaHesab' );
        $version = get_option( 'samahesab_version', '2.5.0' );
        $url     = get_option( 'samahesab_download_url', 'https://github.com/kish210/SamaHesab/releases/latest' );
        ?>
        <div class="wrap" dir="rtl" style="font-family:Tahoma,sans-serif">
            <h1>دانلودِ نرم‌افزار (بخشِ سایت)</h1>
            <p><strong>لینکِ دانلود هوشمند است:</strong> شورت‌کدِ <code>[samahesab_product]</code> به‌صورتِ خودکار <em>آخرین Releaseِ</em> مخزنِ گیت‌هاب (نسخه + فایلِ نصابِ <code>.exe</code>) را می‌خوانَد و ۱ ساعت کَش می‌کند. کافی است روی گیت‌هاب یک Releaseِ جدید بسازی — سایت بدونِ هیچ تغییری به‌روز می‌شود.</p>
            <p>مقادیرِ «نسخه/لینکِ دستی» فقط <strong>fallback</strong> هستند (اگر API گیت‌هاب در دسترس نباشد).</p>
            <form method="post">
                <?php wp_nonce_field( 'samahesab_dl' ); ?>
                <table class="form-table">
                    <tr><th>مخزنِ گیت‌هاب (owner/repo)</th><td><input name="github_repo" type="text" class="regular-text" value="<?php echo esc_attr( $repo ); ?>" placeholder="kish210/SamaHesab"></td></tr>
                    <tr><th>نسخهٔ fallback</th><td><input name="version" type="text" class="regular-text" value="<?php echo esc_attr( $version ); ?>"></td></tr>
                    <tr><th>لینکِ دانلودِ fallback</th><td><input name="download_url" type="url" class="regular-text" value="<?php echo esc_attr( $url ); ?>"></td></tr>
                </table>
                <p>
                    <button class="button button-primary" name="samahesab_save_dl" value="1">ذخیره</button>
                    <button class="button" name="samahesab_clear_cache" value="1">تازه‌سازیِ آخرین نسخه از گیت‌هاب</button>
                </p>
            </form>
            <hr>
            <h2>چطور بخشِ «امکانات + دانلود» را روی سایت بگذارم؟</h2>
            <ol>
                <li>«برگه‌ها ▸ افزودنِ برگه» یک برگهٔ تازه (مثلاً «درباره سما حساب» یا «دانلود») بساز.</li>
                <li>این شورت‌کد را در متنِ برگه بگذار: <code>[samahesab_product]</code></li>
                <li>برگه را منتشر کن و در منوی سایت قرار بده.</li>
            </ol>
        </div>
        <?php
    }

    private function count_status( $type, $status ) {
        $q = new WP_Query( array(
            'post_type'   => $type,
            'post_status' => 'publish',
            'meta_key'    => 'sh_status',
            'meta_value'  => (string) $status,
            'fields'      => 'ids',
            'nopaging'    => true,
        ) );
        return $q->found_posts;
    }

    private function count_all( $type ) {
        $c = wp_count_posts( $type );
        return isset( $c->publish ) ? (int) $c->publish : 0;
    }

    public function dashboard_page() {
        $open_tickets = $this->count_all( 'samahesab_ticket' );
        $critical     = new WP_Query( array(
            'post_type'  => 'samahesab_bug', 'post_status' => 'publish',
            'meta_key'   => 'sh_severity', 'meta_value' => '3',
            'fields'     => 'ids', 'nopaging' => true,
        ) );
        $features = $this->count_all( 'samahesab_feature' );
        $bugs     = $this->count_all( 'samahesab_bug' );
        ?>
        <div class="wrap" dir="rtl" style="font-family:Tahoma,sans-serif">
            <h1>🆘 مرکزِ پشتیبانیِ سما حساب</h1>
            <div style="display:flex;gap:16px;flex-wrap:wrap;margin-top:16px">
                <?php
                $this->stat_card( 'تیکت‌ها', $open_tickets, '#1f4e79' );
                $this->stat_card( 'باگ‌های بحرانی', $critical->found_posts, '#c0392b' );
                $this->stat_card( 'کلِ باگ‌ها', $bugs, '#e67e22' );
                $this->stat_card( 'درخواست‌های قابلیت', $features, '#27ae60' );
                ?>
            </div>
            <h2 style="margin-top:28px">آخرین گزارش‌ها</h2>
            <?php $this->recent_table(); ?>
        </div>
        <?php
    }

    private function stat_card( $label, $value, $color ) {
        printf(
            '<div style="flex:1;min-width:180px;background:#fff;border:1px solid #e2e2e2;border-radius:8px;padding:18px;text-align:center">
                <div style="font-size:30px;font-weight:bold;color:%s">%d</div>
                <div style="color:#666;margin-top:6px">%s</div>
            </div>',
            esc_attr( $color ), intval( $value ), esc_html( $label )
        );
    }

    private function recent_table() {
        $posts = get_posts( array(
            'post_type'   => array( 'samahesab_ticket', 'samahesab_bug', 'samahesab_feature' ),
            'numberposts' => 15,
            'orderby'     => 'date',
            'order'       => 'DESC',
        ) );
        echo '<table class="widefat striped"><thead><tr><th>نوع</th><th>عنوان</th><th>مشتری</th><th>تاریخ</th></tr></thead><tbody>';
        foreach ( $posts as $p ) {
            printf(
                '<tr><td>%s</td><td><a href="%s">%s</a></td><td>%s</td><td>%s</td></tr>',
                esc_html( get_post_type_object( $p->post_type )->labels->singular_name ),
                esc_url( get_edit_post_link( $p->ID ) ),
                esc_html( get_the_title( $p ) ),
                esc_html( get_post_meta( $p->ID, 'sh_customer_id', true ) ),
                esc_html( get_the_date( '', $p ) )
            );
        }
        echo '</tbody></table>';
    }

    public function register_settings() {
        register_setting( 'samahesab_keys_group', 'samahesab_api_keys' );
    }

    public function keys_page() {
        // ذخیرهٔ افزودنِ کلیدِ جدید.
        if ( isset( $_POST['samahesab_add_key'] ) && check_admin_referer( 'samahesab_keys' ) ) {
            $keys   = SamaHesab_Auth::keys();
            $keys[] = array(
                'customer_id' => sanitize_text_field( wp_unslash( $_POST['customer_id'] ?? '' ) ),
                'api_key'     => wp_generate_password( 32, false ),
                'license_id'  => sanitize_text_field( wp_unslash( $_POST['license_id'] ?? '' ) ),
                'label'       => sanitize_text_field( wp_unslash( $_POST['label'] ?? '' ) ),
            );
            update_option( 'samahesab_api_keys', $keys );
            echo '<div class="notice notice-success"><p>کلیدِ جدید ساخته شد.</p></div>';
        }
        $keys = SamaHesab_Auth::keys();
        ?>
        <div class="wrap" dir="rtl" style="font-family:Tahoma,sans-serif">
            <h1>مشتریان و کلید-API</h1>
            <p>هر نصبِ ERP یک رکورد دارد. کلید را در «تنظیماتِ پشتیبانیِ» سما حساب وارد کنید.</p>
            <table class="widefat striped">
                <thead><tr><th>برچسب</th><th>شناسهٔ مشتری</th><th>کلید-API</th><th>شناسهٔ لایسنس</th></tr></thead>
                <tbody>
                <?php foreach ( $keys as $row ) : ?>
                    <tr>
                        <td><?php echo esc_html( $row['label'] ?? '' ); ?></td>
                        <td><code><?php echo esc_html( $row['customer_id'] ?? '' ); ?></code></td>
                        <td><code><?php echo esc_html( $row['api_key'] ?? '' ); ?></code></td>
                        <td><?php echo esc_html( $row['license_id'] ?? '' ); ?></td>
                    </tr>
                <?php endforeach; ?>
                </tbody>
            </table>

            <h2 style="margin-top:24px">افزودنِ مشتریِ جدید</h2>
            <form method="post">
                <?php wp_nonce_field( 'samahesab_keys' ); ?>
                <table class="form-table">
                    <tr><th>برچسب</th><td><input name="label" type="text" class="regular-text" required></td></tr>
                    <tr><th>شناسهٔ مشتری</th><td><input name="customer_id" type="text" class="regular-text" required></td></tr>
                    <tr><th>شناسهٔ لایسنس</th><td><input name="license_id" type="text" class="regular-text"></td></tr>
                </table>
                <p><button class="button button-primary" name="samahesab_add_key" value="1">ساختِ کلید-API</button></p>
            </form>
        </div>
        <?php
    }
}
