<?php
/**
 * 🆘 HC-WP+ — بخشِ عمومیِ «امکانات + دانلودِ» نرم‌افزار برای سایت (kishwifi.com).
 * شورت‌کد: [samahesab_product]  — هیرو + گریدِ امکانات + دکمهٔ دانلود.
 * نسخه/لینکِ دانلود از تنظیمات (SamaHesab ▸ دانلود) خوانده می‌شود و با ویژگیِ شورت‌کد قابلِ override است:
 *   [samahesab_product version="2.5.0" url="https://github.com/kish210/SamaHesab/releases/latest"]
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
        add_action( 'admin_init', array( $this, 'register_settings' ) );
    }

    public function register_settings() {
        register_setting( 'samahesab_download_group', 'samahesab_download_url' );
        register_setting( 'samahesab_download_group', 'samahesab_version' );
        register_setting( 'samahesab_download_group', 'samahesab_github_repo' );
    }

    private function features() {
        return array(
            array( '📒', 'حسابداریِ کامل', 'سند، دفترِ کل/معین، تراز، سود و زیان، ترازنامه، سال مالی و بستنِ دوره.' ),
            array( '🏦', 'خزانه‌داری', 'چک (چرخهٔ وصول/برگشت)، صندوق/بانک، مغایرت‌گیری، دریافتنی/پرداختنی.' ),
            array( '🛒', 'فروش و خرید', 'فاکتور، مرجوعی، لیست‌قیمت، تخفیفِ پلکانی، سقفِ اعتبارِ مشتری.' ),
            array( '📦', 'انبار', 'کاردکس، انتقال بین‌انبار، انبارگردانی، بچ/سریال/انقضا، FIFO و میانگین موزون.' ),
            array( '🧾', 'صندوق و رستوران', 'POSِ لمسی، شیفت/Z-X، میز/گارسون/آشپزخانه، چاپِ حرارتی.' ),
            array( '📊', 'گزارش‌های مدیریتی', 'ماندهٔ سنی‌شده، مالیات ارزش‌افزوده، سود کالا، ABC، گردشِ موجودی — با اکسل/PDF.' ),
            array( '🏢', 'چندشعبه و امنیت', 'جداسازیِ دادهٔ شعبه، نقش/مجوزِ دانه‌ریز (RBAC)، حسابرسی.' ),
            array( '🆘', 'مرکزِ پشتیبانی', 'گزارشِ باگ، تیکت، دانشنامه، عیب‌یابی و پشتیبانیِ ریموت — درونِ نرم‌افزار.' ),
        );
    }

    /** مخزنِ گیت‌هاب (قابلِ تنظیم در «SamaHesab ▸ دانلود»). */
    private function github_repo() {
        $r = trim( (string) get_option( 'samahesab_github_repo', 'kish210/SamaHesab' ) );
        return '' !== $r ? $r : 'kish210/SamaHesab';
    }

    /**
     * 🔗 لینکِ هوشمند: آخرین Releaseِ گیت‌هاب را زنده می‌خوانَد (نسخه + نصابِ .exe) و ۱ ساعت کَش می‌کند.
     * با ساختِ Releaseِ تازه روی گیت‌هاب، سایت خودکار به‌روز می‌شود — بدونِ تغییر در سایت.
     * در صورتِ نبودِ شبکه/خطا → null (تا fallback به تنظیماتِ دستی برود).
     */
    private function latest_release() {
        $cached = get_transient( 'samahesab_latest_release' );
        if ( is_array( $cached ) ) {
            // رکوردِ موفق «version» دارد؛ نشانگرِ شکست (failed) → null بدونِ درخواستِ دوباره.
            return empty( $cached['version'] ) ? null : $cached;
        }

        // کَشِ منفی: اگر گیت‌هاب در دسترس نبود، ۱۵ دقیقه دوباره تلاش نکن تا رندرِ صفحه بلاک نشود.
        $fail = function () {
            set_transient( 'samahesab_latest_release', array( 'failed' => 1 ), 15 * MINUTE_IN_SECONDS );
            return null;
        };

        $repo = $this->github_repo();
        $resp = wp_remote_get( "https://api.github.com/repos/{$repo}/releases/latest", array(
            'timeout' => 7,
            'headers' => array(
                'Accept'     => 'application/vnd.github+json',
                'User-Agent' => 'SamaHesab-Support-Center',
            ),
        ) );
        if ( is_wp_error( $resp ) || 200 !== (int) wp_remote_retrieve_response_code( $resp ) ) {
            return $fail();
        }
        $data = json_decode( wp_remote_retrieve_body( $resp ), true );
        if ( empty( $data['tag_name'] ) ) {
            return $fail();
        }

        // یافتنِ نصابِ .exe از assets (اولویت با فایلی که «setup» دارد = نصابِ کامل).
        $download = '';
        if ( ! empty( $data['assets'] ) && is_array( $data['assets'] ) ) {
            foreach ( $data['assets'] as $a ) {
                $name = isset( $a['name'] ) ? strtolower( $a['name'] ) : '';
                if ( '.exe' === substr( $name, -4 ) && ! empty( $a['browser_download_url'] ) ) {
                    $download = $a['browser_download_url'];
                    if ( false !== strpos( $name, 'setup' ) ) {
                        break;
                    }
                }
            }
        }
        if ( '' === $download ) {
            $download = ! empty( $data['html_url'] ) ? $data['html_url'] : "https://github.com/{$repo}/releases/latest";
        }

        $result = array(
            'version' => ltrim( (string) $data['tag_name'], 'vV' ),
            'url'     => $download,
        );
        set_transient( 'samahesab_latest_release', $result, HOUR_IN_SECONDS );
        return $result;
    }

    /** اسکرین‌شات‌های همراهِ پلاگین (URL → عنوان) برای گالریِ اعتمادساز. */
    private function shots() {
        $b = SAMAHESAB_SC_URL . 'assets/screenshots/';
        return array(
            array( $b . 'dashboard.png',     'داشبوردِ مدیریتی' ),
            array( $b . 'sales-invoice.png', 'فاکتورِ فروش' ),
            array( $b . 'pos.png',           'صندوقِ فروشگاهی (POS)' ),
            array( $b . 'accounting.png',    'اسنادِ حسابداری' ),
            array( $b . 'reports.png',       'گزارش‌های مدیریتی' ),
            array( $b . 'support.png',       'مرکزِ پشتیبانی' ),
        );
    }

    public function product( $atts ) {
        // ویژگیِ شورت‌کد (در صورتِ تعیینِ صریح) بالاترین اولویت؛ وگرنه «آخرین Releaseِ زنده»؛ وگرنه تنظیماتِ دستی.
        $atts   = shortcode_atts( array( 'version' => '', 'url' => '' ), $atts, 'samahesab_product' );
        $latest = $this->latest_release();

        $version = '' !== $atts['version'] ? $atts['version']
            : ( $latest ? $latest['version'] : get_option( 'samahesab_version', '2.5.0' ) );
        $url = '' !== $atts['url'] ? $atts['url']
            : ( $latest ? $latest['url'] : get_option( 'samahesab_download_url', 'https://github.com/' . $this->github_repo() . '/releases/latest' ) );

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
