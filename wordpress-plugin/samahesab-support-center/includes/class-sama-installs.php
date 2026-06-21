<?php
/**
 * 🖥 ثبت و مدیریتِ نصب‌های ERP.
 * هر کامپیوتری که سما حساب را نصب می‌کند، خود را با یک machine_idِ یکتا به سایت «اعلام» می‌کند
 * (REST: POST /register). مدیر در صفحهٔ «نصب‌ها» می‌بیند چه کسانی نصب کرده‌اند و با «تأیید/تمدید»
 * لایسنسِ همان کامپیوتر را فعال/تمدید می‌کند (تاریخِ انقضا + سقفِ سند). برنامه با هر بار اعلام،
 * وضعیتِ تأیید/لایسنس را می‌گیرد و خودکار اعمال می‌کند.
 * داده در آپشنِ `samahesab_installs` نگه‌داری می‌شود (کلید = machine_id).
 */
if ( ! defined( 'ABSPATH' ) ) { exit; }

class SamaHesab_Installs {

    const OPT = 'samahesab_installs';

    private static $instance = null;
    public static function instance() {
        if ( null === self::$instance ) { self::$instance = new self(); }
        return self::$instance;
    }

    public function hooks() {
        add_action( 'admin_menu', array( $this, 'menu' ), 20 );
    }

    // ── ذخیره‌سازی ───────────────────────────────────────────────────────────
    public static function all() {
        $v = get_option( self::OPT, array() );
        return is_array( $v ) ? $v : array();
    }
    public static function save_all( $all ) { update_option( self::OPT, $all ); }

    /** ثبت/به‌روزرسانیِ یک نصب (از REST). نصبِ تازه = «در انتظارِ تأیید». برمی‌گرداند: رکورد. */
    public static function upsert( $machine, $data ) {
        $machine = sanitize_text_field( $machine );
        if ( '' === $machine ) { return null; }
        $all = self::all();
        $now = current_time( 'mysql' );
        if ( isset( $all[ $machine ] ) ) {
            $row = $all[ $machine ];
            $row['last_seen'] = $now;
            if ( ! empty( $data['company'] ) )       { $row['company'] = sanitize_text_field( $data['company'] ); }
            if ( ! empty( $data['business_type'] ) ) { $row['business_type'] = sanitize_text_field( $data['business_type'] ); }
            if ( ! empty( $data['version'] ) )       { $row['version'] = sanitize_text_field( $data['version'] ); }
        } else {
            $row = array(
                'machine_id'    => $machine,
                'company'       => sanitize_text_field( $data['company'] ?? '' ),
                'business_type' => sanitize_text_field( $data['business_type'] ?? '' ),
                'version'       => sanitize_text_field( $data['version'] ?? '' ),
                'first_seen'    => $now,
                'last_seen'     => $now,
                'status'        => 'pending',
                'api_key'       => '',
                'license_id'    => '',
                'expiry'        => '',
                'doc_limit'     => 0,
            );
        }
        $all[ $machine ] = $row;
        self::save_all( $all );
        return $row;
    }

    /** payloadِ وضعیت برای بازگشت به ERP (کلید/لایسنس فقط پس از تأیید فاش می‌شود). */
    public static function status_payload( $row ) {
        $expiry   = (string) ( $row['expiry'] ?? '' );
        $expired  = ( '' !== $expiry && strtotime( $expiry ) < current_time( 'timestamp' ) );
        $days     = ( '' !== $expiry ) ? (int) ceil( ( strtotime( $expiry ) - current_time( 'timestamp' ) ) / DAY_IN_SECONDS ) : null;
        $approved = ( ( $row['status'] ?? 'pending' ) === 'approved' );
        return array(
            'status'         => $row['status'] ?? 'pending',
            'approved'       => $approved,
            'valid'          => $approved && ! $expired,
            'expired'        => $expired,
            'expiry'         => $expiry,
            'days_remaining' => $days,
            'doc_limit'      => intval( $row['doc_limit'] ?? 0 ),
            'api_key'        => $approved ? (string) ( $row['api_key'] ?? '' ) : '',
            'license_id'     => $approved ? (string) ( $row['license_id'] ?? '' ) : '',
            'customer_id'    => (string) ( $row['machine_id'] ?? '' ),
        );
    }

    // ── صفحهٔ ادمین ──────────────────────────────────────────────────────────
    public function menu() {
        add_submenu_page( 'samahesab', 'نصب‌های نرم‌افزار', 'نصب‌ها', 'manage_options',
            'samahesab-installs', array( $this, 'page' ) );
    }

    public function page() {
        $all = self::all();

        // تأیید/تمدیدِ یک نصب (تنظیمِ انقضا + سقفِ سند + ساختِ کلید در صورتِ نبود).
        if ( isset( $_POST['sama_inst_approve'] ) && check_admin_referer( 'sama_installs' ) ) {
            $m = sanitize_text_field( wp_unslash( $_POST['machine'] ?? '' ) );
            if ( isset( $all[ $m ] ) ) {
                $all[ $m ]['status']     = 'approved';
                $all[ $m ]['expiry']     = sanitize_text_field( wp_unslash( $_POST['expiry'] ?? '' ) );
                $all[ $m ]['doc_limit']  = max( 0, intval( $_POST['doc_limit'] ?? 0 ) );
                if ( empty( $all[ $m ]['api_key'] ) )   { $all[ $m ]['api_key'] = wp_generate_password( 32, false ); }
                if ( empty( $all[ $m ]['license_id'] ) ){ $all[ $m ]['license_id'] = sanitize_text_field( wp_unslash( $_POST['license_id'] ?? 'STD' ) ); }
                self::save_all( $all );
                echo '<div class="notice notice-success"><p>نصب تأیید/تمدید شد؛ برنامه با اعلامِ بعدی فعال می‌شود.</p></div>';
            }
        }
        // حذفِ یک نصب.
        if ( isset( $_POST['sama_inst_delete'] ) && check_admin_referer( 'sama_installs' ) ) {
            $m = sanitize_text_field( wp_unslash( $_POST['machine'] ?? '' ) );
            if ( isset( $all[ $m ] ) ) { unset( $all[ $m ] ); self::save_all( $all ); echo '<div class="notice notice-success"><p>نصب حذف شد.</p></div>'; }
        }

        $today_plus_year = date( 'Y-m-d', strtotime( '+1 year', current_time( 'timestamp' ) ) );
        ?>
        <div class="wrap" dir="rtl" style="font-family:Tahoma,sans-serif">
            <h1>🖥 نصب‌های نرم‌افزار</h1>
            <p>هر کامپیوتری که سما حساب را نصب کند این‌جا ظاهر می‌شود. با «تأیید/تمدید» تاریخِ انقضا و سقفِ سند را تنظیم کنید؛ برنامه آن را خودکار از سایت می‌گیرد و اعمال می‌کند.</p>
            <table class="widefat striped">
                <thead><tr>
                    <th>شرکت</th><th>صنف</th><th>نسخه</th><th>شناسهٔ کامپیوتر</th>
                    <th>اولین نصب</th><th>آخرین اتصال</th><th>وضعیت</th><th>انقضا / سقفِ سند</th><th>عملیات</th>
                </tr></thead>
                <tbody>
                <?php if ( empty( $all ) ) : ?>
                    <tr><td colspan="9" style="text-align:center;color:#888;padding:20px">هنوز نصبی ثبت نشده است.</td></tr>
                <?php endif; ?>
                <?php foreach ( $all as $m => $row ) :
                    $expiry   = $row['expiry'] ?? '';
                    $limit    = intval( $row['doc_limit'] ?? 0 );
                    $approved = ( ( $row['status'] ?? 'pending' ) === 'approved' );
                    $expired  = ( '' !== $expiry && strtotime( $expiry ) < current_time( 'timestamp' ) );
                    ?>
                    <tr>
                        <td><strong><?php echo esc_html( $row['company'] ?: '—' ); ?></strong></td>
                        <td><?php echo esc_html( $row['business_type'] ?: '—' ); ?></td>
                        <td><?php echo esc_html( $row['version'] ?: '—' ); ?></td>
                        <td><code style="font-size:11px"><?php echo esc_html( substr( $m, 0, 18 ) ); ?></code></td>
                        <td><?php echo esc_html( $row['first_seen'] ?? '' ); ?></td>
                        <td><?php echo esc_html( $row['last_seen'] ?? '' ); ?></td>
                        <td><?php
                            if ( ! $approved )      { echo '<span style="color:#e67e22;font-weight:bold">در انتظارِ تأیید</span>'; }
                            elseif ( $expired )     { echo '<span style="color:#c0392b;font-weight:bold">منقضی</span>'; }
                            else                    { echo '<span style="color:#27ae60;font-weight:bold">فعال</span>'; }
                        ?></td>
                        <td><?php echo $expiry ? esc_html( $expiry ) : '<span style="color:#888">—</span>'; ?>
                            <?php echo $limit > 0 ? ' / ' . esc_html( number_format_i18n( $limit ) ) . ' سند' : ' / نامحدود'; ?></td>
                        <td>
                            <form method="post" style="display:flex;gap:4px;align-items:center;flex-wrap:wrap">
                                <?php wp_nonce_field( 'sama_installs' ); ?>
                                <input type="hidden" name="machine" value="<?php echo esc_attr( $m ); ?>">
                                <input name="expiry" type="date" value="<?php echo esc_attr( $expiry ?: $today_plus_year ); ?>" style="width:135px">
                                <input name="doc_limit" type="number" min="0" value="<?php echo intval( $limit ); ?>" style="width:80px" title="سقفِ سند (۰=نامحدود)">
                                <button class="button button-primary button-small" name="sama_inst_approve" value="1"><?php echo $approved ? 'تمدید' : 'تأیید'; ?></button>
                                <button class="button button-small" name="sama_inst_delete" value="1" onclick="return confirm('این نصب حذف شود؟')">حذف</button>
                            </form>
                        </td>
                    </tr>
                <?php endforeach; ?>
                </tbody>
            </table>
        </div>
        <?php
    }
}
