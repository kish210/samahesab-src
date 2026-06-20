<?php
/**
 * 🆘 HC-WP — REST API در فضای‌نامِ samahesab/v1 (هم‌راستا با SupportApiClientِ ERP).
 *   POST /bug · /feature · /ticket        → ثبت، بازگشتِ {Id}
 *   GET  /releases · /articles?search=     → فهرستِ DTO
 *   GET  /status/{id}                      → وضعیتِ یک رکورد
 * کلیدهای JSON با PascalCase تا با رکوردهای C# (case-insensitive) سازگار باشد.
 */
if ( ! defined( 'ABSPATH' ) ) { exit; }

class SamaHesab_REST {

    private static $instance = null;
    public static function instance() {
        if ( null === self::$instance ) { self::$instance = new self(); }
        return self::$instance;
    }

    public function hooks() {
        add_action( 'rest_api_init', array( $this, 'routes' ) );
    }

    public function routes() {
        $perm = array( 'SamaHesab_Auth', 'permission' );

        register_rest_route( SAMAHESAB_SC_NS, '/bug', array(
            'methods'             => 'POST',
            'callback'            => array( $this, 'create_bug' ),
            'permission_callback' => $perm,
        ) );
        register_rest_route( SAMAHESAB_SC_NS, '/feature', array(
            'methods'             => 'POST',
            'callback'            => array( $this, 'create_feature' ),
            'permission_callback' => $perm,
        ) );
        register_rest_route( SAMAHESAB_SC_NS, '/ticket', array(
            'methods'             => 'POST',
            'callback'            => array( $this, 'create_ticket' ),
            'permission_callback' => $perm,
        ) );
        register_rest_route( SAMAHESAB_SC_NS, '/remote', array(
            'methods'             => 'POST',
            'callback'            => array( $this, 'create_remote' ),
            'permission_callback' => $perm,
        ) );
        register_rest_route( SAMAHESAB_SC_NS, '/releases', array(
            'methods'             => 'GET',
            'callback'            => array( $this, 'list_releases' ),
            'permission_callback' => $perm,
        ) );
        register_rest_route( SAMAHESAB_SC_NS, '/articles', array(
            'methods'             => 'GET',
            'callback'            => array( $this, 'list_articles' ),
            'permission_callback' => $perm,
        ) );
        register_rest_route( SAMAHESAB_SC_NS, '/status/(?P<id>[\w\-]+)', array(
            'methods'             => 'GET',
            'callback'            => array( $this, 'get_status' ),
            'permission_callback' => $perm,
        ) );
    }

    private function customer_id( $request ) {
        $row = SamaHesab_Auth::authenticate( $request );
        return $row && ! empty( $row['customer_id'] ) ? $row['customer_id'] : 'UNKNOWN';
    }

    /** متنِ ثبت + متاها → پستِ جدید؛ بازگشتِ شناسه به‌صورتِ رشته. */
    private function insert( $type, $title, $content, $meta, $request ) {
        $post_id = wp_insert_post( array(
            'post_type'    => $type,
            'post_status'  => 'publish',
            'post_title'   => wp_strip_all_tags( $title ),
            'post_content' => wp_kses_post( $content ),
        ), true );

        if ( is_wp_error( $post_id ) ) {
            return new WP_Error( 'samahesab_insert_failed', $post_id->get_error_message(), array( 'status' => 500 ) );
        }

        $meta['customer_id'] = $this->customer_id( $request );
        $meta['status']      = isset( $meta['status'] ) ? $meta['status'] : 0;
        foreach ( $meta as $k => $v ) {
            update_post_meta( $post_id, 'sh_' . $k, is_scalar( $v ) ? sanitize_text_field( (string) $v ) : wp_json_encode( $v ) );
        }

        do_action( 'samahesab_item_created', $type, $post_id, $meta );

        return new WP_REST_Response( array( 'Id' => (string) $post_id ), 201 );
    }

    public function create_bug( $request ) {
        $b = $request->get_json_params();
        return $this->insert( 'samahesab_bug',
            isset( $b['Title'] ) ? $b['Title'] : 'گزارشِ باگ',
            isset( $b['Description'] ) ? $b['Description'] : '',
            array(
                'severity'  => isset( $b['Severity'] ) ? intval( $b['Severity'] ) : 1,
                'category'  => isset( $b['Category'] ) ? intval( $b['Category'] ) : 10,
                'expected'  => isset( $b['ExpectedResult'] ) ? $b['ExpectedResult'] : '',
                'actual'    => isset( $b['ActualResult'] ) ? $b['ActualResult'] : '',
                'steps'     => isset( $b['StepsToReproduce'] ) ? $b['StepsToReproduce'] : '',
                'screen'    => isset( $b['ScreenName'] ) ? $b['ScreenName'] : '',
                'diag'      => isset( $b['DiagnosticsJson'] ) ? $b['DiagnosticsJson'] : '',
            ),
            $request
        );
    }

    public function create_feature( $request ) {
        $b = $request->get_json_params();
        return $this->insert( 'samahesab_feature',
            isset( $b['Title'] ) ? $b['Title'] : 'درخواستِ قابلیت',
            isset( $b['Description'] ) ? $b['Description'] : '',
            array(
                'benefit'  => isset( $b['BusinessBenefit'] ) ? $b['BusinessBenefit'] : '',
                'priority' => isset( $b['Priority'] ) ? intval( $b['Priority'] ) : 1,
            ),
            $request
        );
    }

    public function create_ticket( $request ) {
        $b = $request->get_json_params();
        return $this->insert( 'samahesab_ticket',
            isset( $b['Subject'] ) ? $b['Subject'] : 'تیکتِ پشتیبانی',
            isset( $b['Body'] ) ? $b['Body'] : '',
            array(
                'category' => isset( $b['Category'] ) ? intval( $b['Category'] ) : 10,
                'status'   => 1, // باز
            ),
            $request
        );
    }

    public function create_remote( $request ) {
        $b = $request->get_json_params();
        return $this->insert( 'samahesab_remote',
            isset( $b['Code'] ) ? $b['Code'] : 'نشستِ ریموت',
            isset( $b['Note'] ) ? $b['Note'] : '',
            array(
                'tool'        => isset( $b['Tool'] ) ? $b['Tool'] : 'rustdesk',
                'connect_id'  => isset( $b['ConnectId'] ) ? $b['ConnectId'] : '',
                'requested_by'=> isset( $b['RequestedBy'] ) ? $b['RequestedBy'] : '',
                'diag'        => isset( $b['DiagnosticsJson'] ) ? $b['DiagnosticsJson'] : '',
                'status'      => 0, // در انتظار
            ),
            $request
        );
    }

    public function list_releases( $request ) {
        $posts = get_posts( array(
            'post_type'   => 'samahesab_release',
            'numberposts' => 50,
            'orderby'     => 'date',
            'order'       => 'DESC',
        ) );
        $out = array();
        foreach ( $posts as $p ) {
            $out[] = array(
                'RemoteId'    => (string) $p->ID,
                'Version'     => get_the_title( $p ),
                'Highlights'  => get_post_meta( $p->ID, 'sh_highlights', true ),
                'BugFixes'    => get_post_meta( $p->ID, 'sh_bugfixes', true ),
                'KnownIssues' => get_post_meta( $p->ID, 'sh_knownissues', true ),
                'PublishedAt' => get_post_time( 'c', true, $p ),
                'IsCurrent'   => '1' === (string) get_post_meta( $p->ID, 'sh_iscurrent', true ),
            );
        }
        return new WP_REST_Response( $out, 200 );
    }

    public function list_articles( $request ) {
        $search = sanitize_text_field( (string) $request->get_param( 'search' ) );
        $posts  = get_posts( array(
            'post_type'   => 'samahesab_article',
            'numberposts' => 100,
            's'           => $search,
            'orderby'     => 'date',
            'order'       => 'DESC',
        ) );
        $out = array();
        foreach ( $posts as $p ) {
            $out[] = array(
                'RemoteId'    => (string) $p->ID,
                'Title'       => get_the_title( $p ),
                'Category'    => intval( get_post_meta( $p->ID, 'sh_category', true ) ),
                'Summary'     => get_the_excerpt( $p ),
                'Body'        => wp_strip_all_tags( $p->post_content ),
                'Url'         => get_permalink( $p ),
                'Kind'        => get_post_meta( $p->ID, 'sh_kind', true ) ?: 'article',
                'PublishedAt' => get_post_time( 'c', true, $p ),
            );
        }
        return new WP_REST_Response( $out, 200 );
    }

    public function get_status( $request ) {
        $id = intval( $request['id'] );
        $p  = get_post( $id );
        if ( ! $p ) {
            return new WP_Error( 'samahesab_not_found', 'رکورد یافت نشد.', array( 'status' => 404 ) );
        }
        $status = intval( get_post_meta( $id, 'sh_status', true ) );
        $labels = SamaHesab_CPT::status_labels();
        return new WP_REST_Response( array(
            'RemoteId'   => (string) $id,
            'Status'     => $status,
            'StatusText' => isset( $labels[ $status ] ) ? $labels[ $status ] : '',
            'UpdatedAt'  => get_post_modified_time( 'c', true, $p ),
        ), 200 );
    }
}
