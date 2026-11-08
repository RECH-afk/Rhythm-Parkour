<?php
defined('RP') or exit;

function rp_fail($code, $http = 400)
{
    http_response_code($http);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode(['ok' => false, 'error' => $code], JSON_UNESCAPED_SLASHES);
    exit;
}

function rp_ok($fields = [], $http = 200)
{
    http_response_code($http);
    header('Content-Type: application/json; charset=utf-8');
    $payload = array_merge(['ok' => true, 'error' => null], $fields);
    echo json_encode($payload, JSON_UNESCAPED_SLASHES);
    exit;
}

function rp_https_ok()
{
    if (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') {
        return true;
    }
    if ((int)($_SERVER['SERVER_PORT'] ?? 0) === 443) {
        return true;
    }
    $forwarded = $_SERVER['HTTP_X_FORWARDED_PROTO'] ?? '';
    if (is_string($forwarded) && stripos($forwarded, 'https') !== false) {
        return true;
    }
    return false;
}

function rp_header($name)
{
    $key = 'HTTP_' . strtoupper(str_replace('-', '_', $name));
    $value = $_SERVER[$key] ?? '';
    if ($value === '' && function_exists('apache_request_headers')) {
        $headers = apache_request_headers();
        if (is_array($headers)) {
            foreach ($headers as $k => $v) {
                if (strcasecmp((string)$k, $name) === 0) {
                    $value = (string)$v;
                    break;
                }
            }
        }
    }
    return is_string($value) ? trim($value) : '';
}

function rp_secret_ok()
{
    $given = rp_header('RhythmParkour-Client');
    if ($given === '') {
        return false;
    }
    return hash_equals(RP_CLIENT_SECRET, $given);
}

function rp_body_json()
{
    $raw = file_get_contents('php://input');
    if ($raw === false || $raw === '' || strlen($raw) > 8192) {
        return [];
    }
    $data = json_decode($raw, true);
    return is_array($data) ? $data : [];
}

function rp_body_str($body, $key, $maxLen)
{
    $value = isset($body[$key]) && is_string($body[$key]) ? trim($body[$key]) : '';
    if ($value === '') {
        return '';
    }
    if (mb_strlen($value) > $maxLen) {
        $value = mb_substr($value, 0, $maxLen);
    }
    return $value;
}

function rp_body_int($body, $key, $min, $max)
{
    $value = $body[$key] ?? null;
    if ($value === null || (!is_int($value) && !is_float($value) && !is_string($value))) {
        return $min;
    }
    $number = (int)$value;
    return max($min, min($max, $number));
}

function rp_body_float($body, $key, $min, $max)
{
    $value = $body[$key] ?? null;
    if ($value === null || (!is_int($value) && !is_float($value) && !is_string($value))) {
        return $min;
    }
    $number = (float)$value;
    return max($min, min($max, $number));
}

function rp_ip_hash()
{
    $ip = $_SERVER['REMOTE_ADDR'] ?? '0.0.0.0';
    return hash('sha256', $ip . '|' . RP_CLIENT_SECRET);
}

function rp_route_path()
{
    $uri = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
    $uri = is_string($uri) ? $uri : '/';
    $script = str_replace('\\', '/', dirname($_SERVER['SCRIPT_NAME'] ?? ''));
    $script = trim($script, '/');
    if ($script === '.') {
        $script = '';
    }
    $uri = trim($uri, '/');
    if ($script !== '' && strpos($uri, $script) === 0) {
        $uri = trim(substr($uri, strlen($script)), '/');
    }
    return strtolower(str_replace(['../', '..\\'], '', $uri));
}

function rp_rate_limit($route, $limit, $windowSec)
{
    $db = rp_db();
    $ipHash = rp_ip_hash();
    $bucket = gmdate('Y-m-d H:i:s', (int)(floor(time() / $windowSec) * $windowSec));

    $select = $db->prepare('SELECT count, bucket_start FROM rate_limit WHERE ip_hash = ? AND route = ?');
    $select->execute([$ipHash, $route]);
    $row = $select->fetch();

    if ($row === false) {
        try {
            $db->prepare('INSERT INTO rate_limit (ip_hash, route, bucket_start, count) VALUES (?, ?, ?, 1)')
                ->execute([$ipHash, $route, $bucket]);
        } catch (PDOException $e) {
            rp_fail('server', 429);
        }
    } elseif ($row['bucket_start'] !== $bucket) {
        $db->prepare('UPDATE rate_limit SET count = 1, bucket_start = ? WHERE ip_hash = ? AND route = ?')
            ->execute([$bucket, $ipHash, $route]);
    } elseif ((int)$row['count'] >= $limit) {
        rp_fail('rate_limited', 429);
    } else {
        $db->prepare('UPDATE rate_limit SET count = count + 1 WHERE ip_hash = ? AND route = ?')
            ->execute([$ipHash, $route]);
    }

    if (mt_rand(1, 500) === 1) {
        $db->prepare('DELETE FROM rate_limit WHERE bucket_start < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 2 DAY)')->execute();
    }
}
