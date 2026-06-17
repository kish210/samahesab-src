<?php
/**
 * 🆘 HC-WP — اعلانِ ایمیلی: ثبتِ تیکت/باگ/قابلیت + تغییرِ وضعیتِ تیکت.
 */
if ( ! defined( 'ABSPATH' ) ) { exit; }

class SamaHesab_Email {

    private static $instance = null;
    public static function instance() {
        if ( null === self::$instance ) { self::$instance = new self(); }
        return self::$instance;
    }

    public function hooks() {
        add_action( 'samahesab_item_created', array( $this, 'on_created' ), 10, 3 );
        add_action( 'post_updated', array( $this, 'on_ticket_updated' ), 10, 3 );
    }

    /** ثبتِ آیتمِ تازه → اطلاع به مدیرِ سایت. */
    public function on_created( $type, $post_id, $meta ) {
        $obj   = get_post_type_object( $type );
        $label = $obj ? $obj->labels->singular_name : $type;
        $title = get_the_title( $post_id );
        $admin = get_option( 'admin_email' );

        $subject = sprintf( '[سما حساب] %s جدید: %s', $label, $title );
        $body    = sprintf(
            "یک %s جدید از مشتری «%s» ثبت شد.\n\nعنوان: %s\nمشاهده: %s",
            $label,
            isset( $meta['customer_id'] ) ? $meta['customer_id'] : '—',
            $title,
            admin_url( 'post.php?action=edit&post=' . $post_id )
        );
        wp_mail( $admin, $subject, $body );
    }

    /** تغییرِ وضعیتِ تیکت (حل‌شده/پاسخ) → اطلاع‌رسانی (اگر ایمیلِ مشتری ثبت شده باشد). */
    public function on_ticket_updated( $post_id, $post_after, $post_before ) {
        if ( 'samahesab_ticket' !== $post_after->post_type ) {
            return;
        }
        $status = intval( get_post_meta( $post_id, 'sh_status', true ) );
        $email  = get_post_meta( $post_id, 'sh_customer_email', true );
        if ( ! $email || ! is_email( $email ) ) {
            return;
        }
        // فقط وضعیت‌های مهم.
        $notify = array( 3 => 'منتظرِ پاسخِ شما', 5 => 'حل شد', 6 => 'بسته شد' );
        if ( ! isset( $notify[ $status ] ) ) {
            return;
        }
        wp_mail(
            $email,
            sprintf( '[سما حساب] تیکتِ شما %s', $notify[ $status ] ),
            sprintf( "وضعیتِ تیکتِ «%s» تغییر کرد: %s", get_the_title( $post_id ), $notify[ $status ] )
        );
    }
}
