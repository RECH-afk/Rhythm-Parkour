<?php
define('RP', true);

ini_set('display_errors', '0');
ini_set('default_charset', 'UTF-8');
error_reporting(E_ALL);

require __DIR__ . '/config.php';
require __DIR__ . '/lib/security.php';
require __DIR__ . '/lib/db.php';
require __DIR__ . '/lib/auth.php';
require __DIR__ . '/lib/stats.php';

header('X-Content-Type-Options: nosniff');
header('Referrer-Policy: no-referrer');
header('Cache-Control: no-store');

$method = $_SERVER['REQUEST_METHOD'] ?? 'GET';
$path = rp_route_path();

if ($method === 'OPTIONS') {
    http_response_code(204);
    exit;
}

if (!rp_https_ok()) {
    rp_fail('https_required', 400);
}

if (!rp_secret_ok()) {
    rp_fail('server', 401);
}

$db = rp_db();

switch ($path) {
    case 'ping':
        if ($method !== 'GET') {
            rp_fail('server', 405);
        }
        rp_rate_limit('ping', 240, 60);
        rp_ok(['serverTime' => gmdate('c'), 'minVersion' => RP_MIN_VERSION]);
        break;

    case 'register':
        if ($method !== 'POST') {
            rp_fail('server', 405);
        }
        rp_register($db, rp_body_json());
        break;

    case 'login':
        if ($method !== 'POST') {
            rp_fail('server', 405);
        }
        rp_login($db, rp_body_json());
        break;

    case 'logout':
        if ($method !== 'POST') {
            rp_fail('server', 405);
        }
        rp_logout($db, rp_auth_user($db));
        break;

    case 'me':
        if ($method !== 'GET') {
            rp_fail('server', 405);
        }
        rp_me($db, rp_auth_user($db));
        break;

    case 'stats/session':
        if ($method !== 'POST') {
            rp_fail('server', 405);
        }
        rp_stats_session($db, rp_body_json(), rp_optional_user($db));
        break;

    case 'stats/level':
        if ($method !== 'POST') {
            rp_fail('server', 405);
        }
        rp_stats_level($db, rp_body_json(), rp_optional_user($db));
        break;

    default:
        rp_fail('server', 404);
        break;
}
