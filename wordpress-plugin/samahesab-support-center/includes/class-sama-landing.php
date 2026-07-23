<?php
/**
 * 🆘 HC-WP+ — بخشِ عمومیِ «امکانات + دانلودِ» نرم‌افزار برای سایت (kishwifi.com).
 * شورت‌کد: [samahesab_product]  — هیرو + گریدِ امکانات + دکمهٔ دانلود.
 * نسخه/لینکِ دانلود از تنظیمات (SamaHesab ▸ دانلود) خوانده می‌شود و با ویژگیِ شورت‌کد قابلِ override است:
 *   [samahesab_product version="2.5.0" url="https://kishwifi.com/download/SamaHesab_Setup_v2.9.3.exe"]
 * منبعِ زندهٔ نسخه/لینک: از @2026-07-22 به‌جایِ GitHub Releases API، از
 * https://kishwifi.com/download/version.json خوانده می‌شود (همان قالبِ manifestی که
 * UpdateService.csِ دسکتاپ هم استفاده می‌کند: {version, notes, files:[{name,url}]}) —
 * چون کاربر خواست نصاب‌ها رویِ دامنهٔ خودش هم میزبانی و از همان‌جا چک شوند، نه فقط GitHub.
 */
if ( ! defined( 'ABSPATH' ) ) { exit; }

class SamaHesab_Landing {

    private static $instance = null;
    public static function instance() {
        if ( null === self::$instance ) { self::$instance = new self(); }
        return self::$instance;
    }

    public function hooks() {
        add_shortcode( 'samahesab_product', array( $this, 'product' ) );
        add_shortcode( 'samahesab_versions', array( $this, 'versions' ) );
        add_action( 'admin_init', array( $this, 'register_settings' ) );
    }

    public function register_settings() {
        register_setting( 'samahesab_download_group', 'samahesab_download_url' );
        register_setting( 'samahesab_download_group', 'samahesab_version' );
        register_setting( 'samahesab_download_group', 'samahesab_manifest_url' );
    }

    private function features() {
        return array(
            array( '📒', 'حسابداریِ کامل', 'سند، دفترِ کل/معین/روزنامه، تراز، سود و زیان، ترازنامه، سال مالی و بستنِ دوره.' ),
            array( '🏦', 'خزانه‌داری', 'چک (چرخهٔ وصول/برگشت)، صندوق/بانک، مغایرت‌گیری، دریافتنی/پرداختنی.' ),
            array( '🛒', 'فروش و خرید', 'فاکتور، مرجوعی، پیش‌فاکتور، فاکتورِ دوره‌ای، لیست‌قیمت، تخفیفِ پلکانی، سقفِ اعتبارِ مشتری.' ),
            array( '📦', 'انبار', 'کاردکس، انتقال بین‌انبار، انبارگردانی، بچ/سریال/انقضا، FIFO و میانگین موزون، نمای انبار.' ),
            array( '🧾', 'صندوق و رستوران', 'POSِ لمسی، شیفت/Z-X، میز/گارسون/آشپزخانه، چاپِ حرارتی.' ),
            array( '📊', 'گزارش‌های مدیریتی', 'ماندهٔ سنی‌شده، مالیات ارزش‌افزوده، سود کالا، ABC، گردشِ موجودی — با اکسل/PDFِ بومیِ فارسی.' ),
            array( '🏢', 'چندشعبه و امنیت', 'جداسازیِ دادهٔ شعبه، نقش/مجوزِ دانه‌ریز (RBAC)، حسابرسی.' ),
            array( '🆘', 'مرکزِ پشتیبانی', 'گزارشِ باگ، تیکت، دانشنامه، عیب‌یابی و پشتیبانیِ ریموت — درونِ نرم‌افزار.' ),
            // ✨ امکاناتِ تازهٔ تجربهٔ کاربری (نسخهٔ ۲.۵.۱۳)
            array( '🔎', 'جست‌وجوی سراسری (Ctrl+K)', 'یافتنِ هر مشتری/کالا/فاکتور/سند/حساب و بازکردنِ آن با چند کلید — بدونِ گشتن در منوها.' ),
            array( '⌨️', 'پنجرهٔ دستورات و میان‌برها', 'اجرای هر کار با صفحه‌کلید + راهنمای میان‌بر (F1)؛ ثبتِ سندِ کامل بدونِ ماوس.' ),
            array( '🧭', 'سایدبارِ آکاردئونی + دسترسیِ سریع', 'منوی جمع‌شونده با بخش‌های بازشونده و صفحاتِ اخیر برای ناوبریِ سریع.' ),
            array( '🚀', 'راه‌اندازیِ هوشمند', 'ویزاردِ گام‌به‌گام: اطلاعاتِ شرکت، صنف/شغل، انتخابِ ماژول‌ها و دموی متناسب با کسب‌وکارِ شما.' ),
            // ✨ ماژول‌هایِ تازه (نسخهٔ ۲.۹.۳ — کاملاً روی وب هم در دسترس)
            array( '🏨', 'هتل و اقامتگاه (PMS)', 'مدیریتِ اتاق/نرخ، رزرو، ورود/خروجِ مهمان، فولیو و صورتحسابِ اقامت.' ),
            array( '🕗', 'حضور و غیابِ حرفه‌ای', 'تردد، شیفت، مرخصی، تجمیعِ ماهانه — با اتصالِ مستقیم به دستگاه‌هایِ کارت‌خوان/اثرانگشتِ زدکتکو.' ),
            array( '🏗', 'پیمانکاری', 'صورت‌وضعیتِ پیمان با موتورِ آبشارِ کسورات (حسن‌انجام/بیمه/مالیات)، پیش‌پرداخت، ضمانت‌نامه و داشبوردِ مالیِ پروژه.' ),
            array( '🧾', 'صورتحسابِ الکترونیکیِ مودیان', 'اتصال به سامانهٔ مودیانِ سازمانِ امورِ مالیاتی برایِ صدورِ صورتحسابِ الکترونیکیِ فروش.' ),
        );
    }

    /** آدرسِ manifestِ نسخه (قابلِ تنظیم در «SamaHesab ▸ دانلود»). */
    private function manifest_url() {
        $r = trim( (string) get_option( 'samahesab_manifest_url', 'https://kishwifi.com/download/version.json' ) );
        return '' !== $r ? $r : 'https://kishwifi.com/download/version.json';
    }

    /**
     * 🔗 لینکِ هوشمند: manifestِ version.jsonِ رویِ kishwifi.com/download را زنده می‌خوانَد
     * (نسخه + نصابِ .exe) و ۱ ساعت کَش می‌کند. با آپلودِ نصابِ تازه + به‌روزرسانیِ version.json
     * (طبقِ راهنمایِ cPanel)، سایت خودکار به‌روز می‌شود — بدونِ تغییر در کدِ سایت.
     * در صورتِ نبودِ شبکه/خطا → null (تا fallback به تنظیماتِ دستی برود).
     */
    private function latest_release() {
        $cached = get_transient( 'samahesab_latest_release' );
        if ( is_array( $cached ) ) {
            // رکوردِ موفق «version» دارد؛ نشانگرِ شکست (failed) → null بدونِ درخواستِ دوباره.
            return empty( $cached['version'] ) ? null : $cached;
        }

        // کَشِ منفی: اگر سرور در دسترس نبود، ۱۵ دقیقه دوباره تلاش نکن تا رندرِ صفحه بلاک نشود.
        $fail = function () {
            set_transient( 'samahesab_latest_release', array( 'failed' => 1 ), 15 * MINUTE_IN_SECONDS );
            return null;
        };

        $resp = wp_remote_get( $this->manifest_url(), array(
            'timeout' => 7,
            'headers' => array(
                'Accept'     => 'application/json',
                'User-Agent' => 'SamaHesab-Support-Center',
            ),
        ) );
        if ( is_wp_error( $resp ) || 200 !== (int) wp_remote_retrieve_response_code( $resp ) ) {
            return $fail();
        }
        $data = json_decode( wp_remote_retrieve_body( $resp ), true );
        if ( empty( $data['version'] ) ) {
            return $fail();
        }

        // یافتنِ نصابِ .exe از files — هدف: «نصابِ تک‌سیستمی» (کاملِ برنامه + پایگاه‌دادهٔ محلی).
        // ترتیبِ اولویت: ۱) نصابِ تک‌سیستمی (شاملِ «setup» ولی نه «client»/«server») ۲) هر «setup» ۳) هر .exe.
        $single = '';   // SamaHesab_Setup_vX.exe — تک‌سیستمی
        $anySetup = '';
        $anyExe = '';
        if ( ! empty( $data['files'] ) && is_array( $data['files'] ) ) {
            foreach ( $data['files'] as $f ) {
                $name = isset( $f['name'] ) ? strtolower( $f['name'] ) : '';
                $u    = ! empty( $f['url'] ) ? $f['url'] : '';
                if ( '' === $u || '.exe' !== substr( $name, -4 ) ) {
                    continue;
                }
                if ( '' === $anyExe ) { $anyExe = $u; }
                $is_setup = ( false !== strpos( $name, 'setup' ) );
                if ( $is_setup && '' === $anySetup ) { $anySetup = $u; }
                // تک‌سیستمی = نصابِ setup که کلاینت/سرور نیست.
                if ( $is_setup && false === strpos( $name, 'client' ) && false === strpos( $name, 'server' ) && '' === $single ) {
                    $single = $u;
                }
            }
        }
        $download = $single ?: ( $anySetup ?: $anyExe );
        if ( '' === $download ) {
            return $fail();
        }

        $result = array(
            'version' => ltrim( (string) $data['version'], 'vV' ),
            'url'     => $download,
        );
        set_transient( 'samahesab_latest_release', $result, HOUR_IN_SECONDS );
        return $result;
    }

    /**
     * 📚 U-WP-VERSION-ARCHIVE — «همهٔ نسخه‌ها»: برخلافِ `latest_release()` (فقط آخرین نسخه)،
     * این متد کلِ آرشیوِ نسخه‌ها را از https://kishwifi.com/download/versions.json می‌خوانَد
     * (آرایه‌ای از {version, publishedAt, notes, files:[{name,url}]}) — عمداً از هیچ API‌ای
     * بیرون از kishwifi.com (نه GitHub) نمی‌خواند، طبقِ تصمیمِ کاربر که دانلود فقط از دامنهٔ
     * خودش سرو شود. نسخه‌هایی که هنوز فایل‌شان روی سرور آپلود نشده (`files` خالی) با نشانِ
     * «به‌زودی» نمایش داده می‌شوند، نه لینکِ شکسته.
     */
    private function version_archive() {
        $cached = get_transient( 'samahesab_version_archive' );
        if ( is_array( $cached ) ) {
            return $cached;
        }
        $url = trailingslashit( dirname( $this->manifest_url() ) ) . 'versions.json';
        $resp = wp_remote_get( $url, array(
            'timeout' => 7,
            'headers' => array( 'Accept' => 'application/json', 'User-Agent' => 'SamaHesab-Support-Center' ),
        ) );
        if ( is_wp_error( $resp ) || 200 !== (int) wp_remote_retrieve_response_code( $resp ) ) {
            set_transient( 'samahesab_version_archive', array(), 15 * MINUTE_IN_SECONDS );
            return array();
        }
        $data = json_decode( wp_remote_retrieve_body( $resp ), true );
        $list = is_array( $data ) ? $data : array();
        set_transient( 'samahesab_version_archive', $list, HOUR_IN_SECONDS );
        return $list;
    }

    /** تبدیلِ تاریخِ میلادی (ISO 8601) به رشتهٔ شمسیِ «۱۴۰۵/۰۴/۳۱» — بدونِ وابستگی به افزونهٔ جانبی. */
    private function to_jalali( $iso_date ) {
        $ts = strtotime( (string) $iso_date );
        if ( ! $ts ) { return ''; }
        $gy = (int) gmdate( 'Y', $ts ); $gm = (int) gmdate( 'n', $ts ); $gd = (int) gmdate( 'j', $ts );
        $g_days_in_month = array( 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 );
        $gy2 = ( $gm > 2 ) ? ( $gy + 1 ) : $gy;
        $days = 355666 + ( 365 * $gy ) + (int) ( ( $gy2 + 3 ) / 4 ) - (int) ( ( $gy2 + 99 ) / 100 )
            + (int) ( ( $gy2 + 399 ) / 400 ) + $gd + $g_days_in_month[ $gm - 1 ];
        if ( $gm > 2 && ( ( $gy % 4 === 0 && $gy % 100 !== 0 ) || $gy % 400 === 0 ) ) { $days++; }
        $jy = -1595 + ( 33 * (int) ( $days / 12053 ) );
        $days %= 12053;
        $jy += 4 * (int) ( $days / 1461 );
        $days %= 1461;
        if ( $days > 365 ) { $jy += (int) ( ( $days - 1 ) / 365 ); $days = ( $days - 1 ) % 365; }
        if ( $days < 186 ) { $jm = 1 + (int) ( $days / 31 ); $jd = 1 + ( $days % 31 ); }
        else { $jm = 7 + (int) ( ( $days - 186 ) / 30 ); $jd = 1 + ( ( $days - 186 ) % 30 ); }
        $fa = array( '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' );
        $s = sprintf( '%04d/%02d/%02d', $jy, $jm, $jd );
        return strtr( $s, array( '0' => $fa[0], '1' => $fa[1], '2' => $fa[2], '3' => $fa[3], '4' => $fa[4],
            '5' => $fa[5], '6' => $fa[6], '7' => $fa[7], '8' => $fa[8], '9' => $fa[9] ) );
    }

    /** [samahesab_versions] — جدولِ همهٔ نسخه‌های منتشرشده + لینکِ دانلودِ هرکدام (اگر آپلود شده باشد). */
    public function versions( $atts ) {
        $list = $this->version_archive();
        ob_start();
        ?>
        <style>
        .samahesab-versions{font-family:'Vazirmatn','IRANSansX','Tahoma',sans-serif !important;direction:rtl;max-width:900px;margin:24px auto}
        .samahesab-versions *{box-sizing:border-box}
        .sh-vrow{display:flex;align-items:center;gap:16px;padding:16px 18px;border:1px solid #e7ecf1;border-radius:12px;margin-bottom:10px;background:#fff}
        .sh-vver{font-weight:800;font-size:16px;color:#243140;min-width:90px}
        .sh-vdate{color:#5b6675;font-size:13px;min-width:110px}
        .sh-vnotes{flex:1;color:#5b6675;font-size:13.5px}
        .sh-vdl{display:flex;gap:8px;flex-wrap:wrap}
        .sh-vdl a{display:inline-flex;align-items:center;gap:6px;font-size:13px;font-weight:700;color:#2c7fb8;
            border:1px solid #2c7fb8;border-radius:8px;padding:6px 12px;text-decoration:none;white-space:nowrap}
        .sh-vdl a:hover{background:#eaf4fb}
        .sh-vsoon{font-size:12.5px;color:#9aa4b0;background:#f4f6f9;border-radius:8px;padding:6px 12px;white-space:nowrap}
        .sh-vempty{text-align:center;color:#5b6675;padding:30px}
        </style>
        <div class="samahesab-versions" dir="rtl">
            <?php if ( empty( $list ) ) : ?>
                <div class="sh-vempty">فهرستِ نسخه‌ها فعلاً در دسترس نیست.</div>
            <?php else : foreach ( $list as $v ) :
                $ver   = isset( $v['version'] ) ? esc_html( $v['version'] ) : '';
                $date  = isset( $v['publishedAt'] ) ? $this->to_jalali( $v['publishedAt'] ) : '';
                $notes = isset( $v['notes'] ) ? esc_html( $v['notes'] ) : '';
                $files = ( isset( $v['files'] ) && is_array( $v['files'] ) ) ? $v['files'] : array();
                ?>
                <div class="sh-vrow">
                    <span class="sh-vver">نسخهٔ <?php echo $ver; ?></span>
                    <span class="sh-vdate"><?php echo esc_html( $date ); ?></span>
                    <span class="sh-vnotes"><?php echo $notes; ?></span>
                    <span class="sh-vdl">
                        <?php if ( empty( $files ) ) : ?>
                            <span class="sh-vsoon">به‌زودی</span>
                        <?php else : foreach ( $files as $f ) :
                            if ( empty( $f['url'] ) ) { continue; } ?>
                            <a href="<?php echo esc_url( $f['url'] ); ?>">⬇️ <?php echo esc_html( isset( $f['name'] ) ? $f['name'] : 'دانلود' ); ?></a>
                        <?php endforeach; endif; ?>
                    </span>
                </div>
            <?php endforeach; endif; ?>
        </div>
        <?php
        return ob_get_clean();
    }

    /** اسکرین‌شات‌های همراهِ پلاگین (URL → عنوان) برای گالریِ اعتمادساز. */
    private function shots() {
        $b = SAMAHESAB_SC_URL . 'assets/screenshots/';
        return array(
            array( $b . 'dashboard.png',      'داشبوردِ مدیریتی' ),
            array( $b . 'sales-invoice.png',  'فاکتورِ فروش' ),
            array( $b . 'pos.png',            'صندوقِ فروشگاهی (POS)' ),
            array( $b . 'accounting.png',     'اسنادِ حسابداری' ),
            array( $b . 'reports.png',        'گزارش‌های مدیریتی' ),
            array( $b . 'command-palette.png','جست‌وجوی سراسری و دستورات (Ctrl+K)' ),
            array( $b . 'shortcuts.png',      'راهنمای میان‌برهای صفحه‌کلید (F1)' ),
            array( $b . 'support.png',        'مرکزِ پشتیبانی' ),
        );
    }

    public function product( $atts ) {
        // «آخرین manifestِ زندهٔ kishwifi.com/download» بالاترین اولویت دارد (نسخه همیشه به‌روز می‌مانَد).
        // فقط اگر سرور در دسترس نبود → ویژگیِ صریحِ شورت‌کد، سپس تنظیماتِ دستی (fallback).
        $atts   = shortcode_atts( array( 'version' => '', 'url' => '' ), $atts, 'samahesab_product' );
        $latest = $this->latest_release();

        $version = $latest ? $latest['version']
            : ( '' !== $atts['version'] ? $atts['version'] : get_option( 'samahesab_version', '2.5.0' ) );
        $url = $latest ? $latest['url']
            : ( '' !== $atts['url'] ? $atts['url'] : get_option( 'samahesab_download_url', 'https://kishwifi.com/download/' ) );

        $version = esc_html( $version );
        $url     = esc_url( $url );

        $shots    = $this->shots();
        $hero_img = SAMAHESAB_SC_URL . 'assets/screenshots/dashboard.png';

        ob_start();
        ?>
        <style>
        .samahesab-product{
            --sh-primary:#2c7fb8; --sh-accent:#2f9e58;
            --sh-dark:#243140; --sh-text:#5b6675; --sh-line:#e7ecf1; --sh-tint:#eaf4fb;
            font-family:'Vazirmatn','IRANSansX','Tahoma','B Nazanin',sans-serif !important;
            color:var(--sh-dark); background:#ffffff; direction:rtl; line-height:1.85;
            max-width:1200px; margin:24px auto; padding:12px 30px 34px; border-radius:18px;
        }
        .samahesab-product *{box-sizing:border-box}
        .samahesab-product h1,.samahesab-product h2,.samahesab-product h3,
        .samahesab-product p,.samahesab-product a,.samahesab-product span,
        .samahesab-product figcaption,.samahesab-product div{
            font-family:inherit !important;
        }
        .sh-block{padding:50px 0}
        .sh-eyebrow{display:inline-flex;align-items:center;gap:8px;font-weight:700;font-size:13px;
            padding:6px 14px;border-radius:30px;color:var(--sh-primary);background:var(--sh-tint)}
        .sh-hero{display:grid;grid-template-columns:1.05fr 1fr;gap:44px;align-items:center;padding:54px 0}
        .sh-hero h1{font-size:40px;line-height:1.25;margin:16px 0 12px;color:var(--sh-dark);font-weight:800}
        .sh-hero p.lead{font-size:18px;color:var(--sh-text);margin:0 0 26px}
        .sh-cta{display:flex;flex-wrap:wrap;gap:12px;align-items:center}
        .sh-btn{display:inline-flex;align-items:center;gap:8px;font-weight:700;font-size:16px;
            padding:14px 32px;border-radius:12px;text-decoration:none;transition:transform .15s,box-shadow .15s,border-color .15s}
        .sh-btn-primary{background:var(--sh-dark);color:#fff;box-shadow:0 10px 24px rgba(20,40,80,.16)}
        .sh-btn-primary:hover{transform:translateY(-2px);box-shadow:0 14px 30px rgba(20,40,80,.22);color:#fff}
        .sh-btn-ghost{background:transparent;color:var(--sh-dark);border:2px solid var(--sh-line)}
        .sh-btn-ghost:hover{border-color:var(--sh-primary);color:var(--sh-dark)}
        .sh-chips{display:flex;flex-wrap:wrap;gap:10px;margin-top:18px;font-size:13px;color:var(--sh-text)}
        .sh-chips span{display:inline-flex;align-items:center;gap:6px}
        .sh-frame{border:1px solid var(--sh-line);border-radius:14px;overflow:hidden;background:#fff;
            box-shadow:0 20px 55px rgba(20,40,80,.16)}
        .sh-frame .bar{display:flex;gap:7px;padding:11px 13px;background:#f4f6f9;border-bottom:1px solid var(--sh-line)}
        .sh-frame .bar i{width:11px;height:11px;border-radius:50%;background:#d6dbe2}
        .sh-frame img{display:block;width:100%;height:auto}
        .sh-h2{text-align:center;font-size:28px;color:var(--sh-dark);margin:0 0 8px;font-weight:800}
        .sh-sub{text-align:center;color:var(--sh-text);margin:0 auto 30px;max-width:640px}
        .sh-features{display:grid;grid-template-columns:repeat(auto-fit,minmax(255px,1fr));gap:18px}
        .sh-card{background:#fff;border:1px solid var(--sh-line);border-radius:16px;padding:22px;transition:transform .18s,box-shadow .18s,border-color .18s}
        .sh-card:hover{transform:translateY(-3px);border-color:var(--sh-primary);box-shadow:0 16px 36px rgba(20,40,80,.1)}
        .sh-ic{width:54px;height:54px;border-radius:15px;display:flex;align-items:center;justify-content:center;font-size:26px;
            background:var(--sh-tint)}
        .sh-card h3{color:var(--sh-dark);margin:12px 0 6px;font-size:18px}
        .sh-card p{font-size:13.5px;color:var(--sh-text);margin:0}
        .sh-gallery{display:grid;grid-template-columns:repeat(auto-fit,minmax(330px,1fr));gap:24px}
        .sh-g{margin:0}
        .sh-g a{display:block;text-decoration:none}
        .sh-g .sh-frame{transition:transform .18s,box-shadow .18s}
        .sh-g a:hover .sh-frame{transform:translateY(-5px);box-shadow:0 26px 60px rgba(20,40,80,.22)}
        .sh-g figcaption{text-align:center;margin-top:12px;font-weight:700;color:var(--sh-dark)}
        .sh-dl{text-align:center;border-radius:22px;padding:52px 24px;
            background:linear-gradient(160deg,#e3eff7,#ffffff 75%);
            border:1px solid var(--sh-line)}
        .sh-dl h2{font-size:28px;color:var(--sh-dark);margin:0 0 10px;font-weight:800}
        .sh-dl p{color:var(--sh-text);margin:0 0 22px}
        .sh-note{font-size:12.5px;color:var(--sh-text);margin-top:14px}
        @media (max-width:880px){
            .sh-hero{grid-template-columns:1fr;text-align:center;padding:30px 0}
            .sh-hero .sh-cta{justify-content:center}
            .sh-hero h1{font-size:30px}
        }
        </style>

        <div class="samahesab-product" dir="rtl">
            <!-- هیرو -->
            <section class="sh-hero">
                <div>
                    <span class="sh-eyebrow">✔ نرم‌افزارِ حسابداریِ ایرانی</span>
                    <h1>سما حساب — مدیریتِ کاملِ کسب‌وکارِ شما</h1>
                    <p class="lead">حسابداری، خزانه‌داری، فروش و خرید، انبار، صندوقِ فروشگاهی و گزارش‌های مدیریتی — یکجا، فارسی و راست‌به‌چپ با تقویمِ شمسی.</p>
                    <div class="sh-cta">
                        <a class="sh-btn sh-btn-primary" href="<?php echo $url; ?>">⬇️ دانلودِ نسخهٔ <?php echo $version; ?></a>
                        <a class="sh-btn sh-btn-ghost" href="#sh-features">مشاهدهٔ امکانات</a>
                    </div>
                    <div class="sh-chips">
                        <span>🪟 ویندوز ۱۰/۱۱</span><span>📦 نصبِ خودکفا</span><span>🎁 نسخهٔ آزمایشیِ رایگان</span><span>🇮🇷 پشتیبانیِ فارسی</span>
                    </div>
                </div>
                <div class="sh-frame">
                    <div class="bar"><i></i><i></i><i></i></div>
                    <img src="<?php echo esc_url( $hero_img ); ?>" alt="داشبوردِ سما حساب" loading="eager" fetchpriority="high" decoding="async">
                </div>
            </section>

            <!-- امکانات -->
            <section class="sh-block" id="sh-features">
                <h2 class="sh-h2">یک نرم‌افزار، همهٔ نیازهای کسب‌وکار</h2>
                <p class="sh-sub">از ثبتِ سند تا گزارش‌های مدیریتی و صندوقِ فروشگاهی — همه در یک بستر یکپارچه.</p>
                <div class="sh-features">
                    <?php foreach ( $this->features() as $f ) : ?>
                        <div class="sh-card">
                            <div class="sh-ic"><?php echo esc_html( $f[0] ); ?></div>
                            <h3><?php echo esc_html( $f[1] ); ?></h3>
                            <p><?php echo esc_html( $f[2] ); ?></p>
                        </div>
                    <?php endforeach; ?>
                </div>
            </section>

            <!-- گالریِ اسکرین‌شات -->
            <section class="sh-block">
                <h2 class="sh-h2">نگاهی به محیطِ نرم‌افزار</h2>
                <p class="sh-sub">تصاویرِ واقعی از بخش‌های مختلفِ سما حساب — برای بزرگ‌نمایی روی هر تصویر کلیک کنید.</p>
                <div class="sh-gallery">
                    <?php foreach ( $shots as $s ) : ?>
                        <figure class="sh-g">
                            <a href="<?php echo esc_url( $s[0] ); ?>" target="_blank" rel="noopener">
                                <div class="sh-frame">
                                    <div class="bar"><i></i><i></i><i></i></div>
                                    <img src="<?php echo esc_url( $s[0] ); ?>" alt="<?php echo esc_attr( $s[1] ); ?>" loading="lazy">
                                </div>
                            </a>
                            <figcaption><?php echo esc_html( $s[1] ); ?></figcaption>
                        </figure>
                    <?php endforeach; ?>
                </div>
            </section>

            <!-- دانلود -->
            <section class="sh-block">
                <div class="sh-dl">
                    <h2>همین حالا رایگان شروع کنید</h2>
                    <p>نصابِ خودکفا (شاملِ همهٔ پیش‌نیازها). نصب در چند دقیقه، روی ویندوز ۱۰ و ۱۱.</p>
                    <a class="sh-btn sh-btn-primary" href="<?php echo $url; ?>">⬇️ دانلودِ سما حساب <?php echo $version; ?></a>
                    <div class="sh-note">پشتیبانی و راهنما از طریقِ مرکزِ پشتیبانیِ درونِ نرم‌افزار یا تماس با سماع رایانه کیش.</div>
                </div>
            </section>
        </div>
        <?php
        return ob_get_clean();
    }
}
