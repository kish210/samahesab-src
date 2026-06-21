<?php
/**
 * 🆘 HC-WP — احرازِ «کلید-API». هر نصبِ ERP یک رکورد دارد: customer_id / api_key / license_id.
 * کلیدها در آپشنِ `samahesab_api_keys` نگه‌داری می‌شوند (مدیریت از پنلِ ادمین).
 */
if ( ! defined( 'ABSPATH' ) ) { exit; }

class SamaHesab_Auth {

    /** همهٔ کلیدهای معتبر: کلیدهای دستیِ ادمین + کلیدِ نصب‌های تأییدشده. */
    public static function keys() {
        $keys = get_option( 'samahesab_api_keys', array() );
        $keys = is_array( $keys ) ? $keys : array();

        // 🖥 کلیدِ نصب‌های «تأییدشده» را هم لحاظ کن تا /bug,/ticket,/feature,/license با
        //   کلیدی که حینِ «تأیید/تمدید» به همان کامپیوتر داده شده کار کنند (باگِ قبلی: فقط
        //   samahesab_api_keys چک می‌شد و کلیدِ نصب‌ها در samahesab_installs نادیده می‌ماند → 401).
        if ( class_exists( 'SamaHesab_Installs' ) ) {
            foreach ( SamaHesab_Installs::all() as $machine => $row ) {
                if ( ( $row['status'] ?? '' ) === 'approved' && ! empty( $row['api_key'] ) ) {
                    $keys[] = array(
                        'customer_id' => (string) $machine,
                        'api_key'     => (string) $row['api_key'],
                        'license_id'  => (string) ( $row['license_id'] ?? '' ),
                        'label'       => (string) ( $row['company'] ?? '' ),
                        'expiry'      => (string) ( $row['expiry'] ?? '' ),
                        'doc_limit'   => intval( $row['doc_limit'] ?? 0 ),
                    );
                }
            }
        }
        return $keys;
    }

    /**
     * اعتبارسنجیِ درخواست بر اساسِ هدرها. در صورتِ معتبر، رکوردِ مشتری را برمی‌گرداند؛ وگرنه null.
     *
     * @param WP_REST_Request $request
     * @return array|null
     */
    public static function authenticate( $request ) {
        $api_key     = trim( (string) $request->get_header( 'X-SamaHesab-ApiKey' ) );
        $customer_id = trim( (string) $request->get_header( 'X-SamaHesab-Customer' ) );

        if ( '' === $api_key ) {
            return null;
        }

        foreach ( self::keys() as $row ) {
            if ( ! empty( $row['api_key'] ) && hash_equals( (string) $row['api_key'], $api_key ) ) {
                // اگر customer_id فرستاده شده، باید مطابق باشد.
                if ( '' !== $customer_id && ! empty( $row['customer_id'] )
                     && (string) $row['customer_id'] !== $customer_id ) {
                    continue;
                }
                return $row;
            }
        }
        return null;
    }

    /** Permission callback برای REST: فقط با کلیدِ معتبر. */
    public static function permission( $request ) {
        if ( null !== self::authenticate( $request ) ) {
            return true;
        }
        return new WP_Error(
            'samahesab_unauthorized',
            'کلید-APIِ نامعتبر یا غایب.',
            array( 'status' => 401 )
        );
    }
}
