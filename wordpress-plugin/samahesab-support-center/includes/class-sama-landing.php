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

    public function product( $atts ) {
        $atts = shortcode_atts( array(
            'version' => get_option( 'samahesab_version', '2.5.0' ),
            'url'     => get_option( 'samahesab_download_url', 'https://github.com/kish210/SamaHesab/releases/latest' ),
        ), $atts, 'samahesab_product' );

        $version = esc_html( $atts['version'] );
        $url     = esc_url( $atts['url'] );

        ob_start();
        ?>
        <div class="samahesab-product" dir="rtl" style="font-family:Tahoma,'B Nazanin',sans-serif;max-width:1080px;margin:auto;line-height:1.9;color:#1a1a1a">
            <!-- هیرو -->
            <section style="text-align:center;background:linear-gradient(135deg,#1f4e79,#2d6da3);color:#fff;border-radius:14px;padding:40px 20px;margin-bottom:28px">
                <h1 style="font-size:34px;margin:0 0 8px">سما حساب</h1>
                <p style="font-size:17px;opacity:.92;margin:0 0 22px">نرم‌افزارِ جامعِ حسابداری و مدیریتِ کسب‌وکار (ERP) — فارسی، راست‌به‌چپ، با تقویمِ شمسی.</p>
                <a href="<?php echo $url; ?>" style="display:inline-block;background:#fff;color:#1f4e79;font-weight:bold;font-size:17px;padding:14px 34px;border-radius:10px;text-decoration:none">⬇️ دانلودِ نسخهٔ <?php echo $version; ?></a>
                <div style="font-size:12.5px;opacity:.8;margin-top:12px">ویندوز ۱۰/۱۱ · خودکفا (بدونِ پیش‌نیاز) · نسخهٔ آزمایشیِ رایگان</div>
            </section>

            <!-- گریدِ امکانات -->
            <h2 style="text-align:center;color:#1f4e79;margin:0 0 18px">امکاناتِ نرم‌افزار</h2>
            <div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:16px">
                <?php foreach ( $this->features() as $f ) : ?>
                    <div style="background:#fff;border:1px solid #e2e2e2;border-radius:10px;padding:18px">
                        <div style="font-size:28px"><?php echo esc_html( $f[0] ); ?></div>
                        <h3 style="color:#1f4e79;margin:8px 0 6px;font-size:17px"><?php echo esc_html( $f[1] ); ?></h3>
                        <p style="font-size:13px;color:#555;margin:0"><?php echo esc_html( $f[2] ); ?></p>
                    </div>
                <?php endforeach; ?>
            </div>

            <!-- دانلود -->
            <section style="text-align:center;background:#f5f8fc;border:1px solid #e2e2e2;border-radius:14px;padding:32px 20px;margin-top:28px">
                <h2 style="color:#1f4e79;margin:0 0 8px">همین حالا شروع کنید</h2>
                <p style="margin:0 0 18px;color:#555">نصابِ خودکفا — شاملِ همهٔ پیش‌نیازها. نصب در چند دقیقه.</p>
                <a href="<?php echo $url; ?>" style="display:inline-block;background:#1f4e79;color:#fff;font-weight:bold;font-size:17px;padding:14px 40px;border-radius:10px;text-decoration:none">⬇️ دانلودِ سما حساب <?php echo $version; ?></a>
                <div style="font-size:12.5px;color:#888;margin-top:14px">برای پشتیبانی و راهنما: مرکزِ پشتیبانیِ درونِ نرم‌افزار یا تماس با سماع رایانه کیش.</div>
            </section>
        </div>
        <?php
        return ob_get_clean();
    }
}
