<?php
defined('RP') or exit;

# MySQL credentials (create the database and user in ISPmanager first)
define('RP_DB_HOST', 'localhost');
define('RP_DB_NAME', 'rhythmparkour');
define('RP_DB_USER', 'rhythmparkour');
define('RP_DB_PASS', 'CHANGE_ME_DB_PASSWORD');
define('RP_DB_CHARSET', 'utf8mb4');

# Shared secret. Must be identical to the value the game sends in the
# RhythmParkour-Client header (set it on the BackendService prefab in Unity).
define('RP_CLIENT_SECRET', 'CHANGE_ME_CLIENT_SECRET');

# Session token lifetime in days.
define('RP_TOKEN_TTL_DAYS', 30);

# Optional: refuse client updates below this version from /ping ("" = disabled).
define('RP_MIN_VERSION', '');

# Only enable while debugging: adds PDO error text into 'server' responses.
define('RP_DEBUG', false);
