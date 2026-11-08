<?php
defined('RP') or exit;

function rp_stats_session($db, array $body, $user)
{
    rp_rate_limit('stats_session', 120, 60);

    $sessionId = rp_body_str($body, 'sessionId', 36);
    if ($sessionId === '') {
        rp_fail('server', 400);
    }

    $db->prepare('INSERT INTO stats_sessions (session_id, app_version, platform, os, cpu_cores, memory_mb, screen_w, screen_h, language, user_id) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)')
        ->execute([
            $sessionId,
            rp_body_str($body, 'appVersion', 32),
            rp_body_str($body, 'platform', 32),
            rp_body_str($body, 'os', 128),
            rp_body_int($body, 'cpuCores', 0, 1024),
            rp_body_int($body, 'memoryMb', 0, 262144),
            rp_body_int($body, 'screenW', 0, 32767),
            rp_body_int($body, 'screenH', 0, 32767),
            rp_body_str($body, 'language', 16),
            $user === null ? null : (int)$user['id'],
        ]);

    rp_ok();
    return;
}

function rp_stats_level($db, array $body, $user)
{
    rp_rate_limit('stats_level', 240, 60);

    $sessionId = rp_body_str($body, 'sessionId', 36);
    if ($sessionId === '') {
        rp_fail('server', 400);
    }

    $db->prepare('INSERT INTO stats_level_results (session_id, level_title, total_notes, count300, count100, count50, count_miss, max_combo, accuracy, flow, rank_name, failed, user_id) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)')
        ->execute([
            $sessionId,
            rp_body_str($body, 'levelTitle', 128),
            rp_body_int($body, 'totalNotes', 0, 2147483647),
            rp_body_int($body, 'count300', 0, 2147483647),
            rp_body_int($body, 'count100', 0, 2147483647),
            rp_body_int($body, 'count50', 0, 2147483647),
            rp_body_int($body, 'countMiss', 0, 2147483647),
            rp_body_int($body, 'maxCombo', 0, 2147483647),
            rp_body_float($body, 'accuracy', 0.0, 100.0),
            rp_body_float($body, 'flow', 0.0, 100.0),
            rp_body_str($body, 'rank', 8),
            rp_body_int($body, 'failed', 0, 1),
            $user === null ? null : (int)$user['id'],
        ]);

    rp_ok();
    return;
}
