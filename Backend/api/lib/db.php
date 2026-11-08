<?php
defined('RP') or exit;

function rp_db()
{
    static $db = null;
    if ($db === null) {
        $dsn = 'mysql:host=' . RP_DB_HOST . ';dbname=' . RP_DB_NAME . ';charset=' . RP_DB_CHARSET;
        try {
            $db = new PDO($dsn, RP_DB_USER, RP_DB_PASS, [
                PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
                PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
                PDO::ATTR_EMULATE_PREPARES => false,
            ]);
        } catch (PDOException $e) {
            rp_fail(RP_DEBUG ? ('server: ' . $e->getMessage()) : 'server', 500);
        }
    }
    return $db;
}
