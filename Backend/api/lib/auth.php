<?php
defined('RP') or exit;

const RP_USERNAME_RE = '/^[A-Za-z0-9_.\-]{3,24}$/';
const RP_TOKEN_LENGTH = 64;

function rp_token_hash($token)
{
    return hash('sha256', $token);
}

function rp_user_by_key(PDO $db, $usernameKey)
{
    $q = $db->prepare('SELECT id, username, password_hash, created_at FROM users WHERE username_key = ?');
    $q->execute([$usernameKey]);
    $row = $q->fetch();
    return is_array($row) ? $row : null;
}

function rp_issue_token(PDO $db, $userId)
{
    $token = bin2hex(random_bytes(32));
    $db->prepare('INSERT INTO auth_tokens (user_id, token_hash, expires_at) VALUES (?, ?, DATE_ADD(UTC_TIMESTAMP(), INTERVAL ' . (int)RP_TOKEN_TTL_DAYS . ' DAY))')
        ->execute([$userId, rp_token_hash($token)]);
    return $token;
}

function rp_optional_user($db)
{
    $token = rp_header('X-Auth-Token');
    if ($token === '' || strlen($token) !== RP_TOKEN_LENGTH || !$db) {
        return null;
    }
    $q = $db->prepare('SELECT u.id, u.username, u.created_at FROM auth_tokens t JOIN users u ON u.id = t.user_id WHERE t.token_hash = ? AND t.expires_at > UTC_TIMESTAMP()');
    $q->execute([rp_token_hash($token)]);
    $row = $q->fetch();
    return is_array($row) ? $row : null;
}

function rp_auth_user($db)
{
    $user = rp_optional_user($db);
    if ($user === null) {
        rp_fail('bad_credentials', 401);
    }
    return $user;
}

function rp_register($db, array $body)
{
    rp_rate_limit('register', 20, 86400);

    $username = rp_body_str($body, 'username', 24);
    $password = isset($body['password']) && is_string($body['password']) ? $body['password'] : '';

    if (!preg_match(RP_USERNAME_RE, $username)) {
        rp_fail('invalid_username');
    }
    if (strlen($password) < 8 || strlen($password) > 128) {
        rp_fail('weak_password');
    }

    $usernameKey = mb_strtolower($username, 'UTF-8');
    if (rp_user_by_key($db, $usernameKey) !== null) {
        rp_fail('username_taken', 409);
    }

    try {
        $db->prepare('INSERT INTO users (username, username_key, password_hash) VALUES (?, ?, ?)')
            ->execute([$username, $usernameKey, password_hash($password, PASSWORD_DEFAULT)]);
    } catch (PDOException $e) {
        if (isset($e->errorInfo[1]) && (int)$e->errorInfo[1] === 1062) {
            rp_fail('username_taken', 409);
        }
        rp_fail('server', 500);
    }

    $token = rp_issue_token($db, (int)$db->lastInsertId());
    rp_ok(['token' => $token, 'accountName' => $username]);
    return;
}

function rp_login($db, array $body)
{
    rp_rate_limit('login', 60, 900);

    $username = rp_body_str($body, 'username', 24);
    $password = isset($body['password']) && is_string($body['password']) ? $body['password'] : '';

    if (!preg_match(RP_USERNAME_RE, $username) || $password === '' || strlen($password) > 128) {
        rp_fail('bad_credentials', 401);
    }

    $user = rp_user_by_key($db, mb_strtolower($username, 'UTF-8'));
    if ($user === null || !password_verify($password, $user['password_hash'])) {
        rp_fail('bad_credentials', 401);
    }

    $token = rp_issue_token($db, (int)$user['id']);
    rp_ok(['token' => $token, 'accountName' => $user['username']]);
    return;
}

function rp_logout($db, $user)
{
    $token = rp_header('X-Auth-Token');
    if ($token !== '') {
        $db->prepare('DELETE FROM auth_tokens WHERE token_hash = ?')->execute([rp_token_hash($token)]);
    }
    rp_ok();
    return;
}

function rp_me($db, $user)
{
    rp_ok(['accountName' => (string)$user['username'], 'created' => (string)$user['created_at']]);
    return;
}
