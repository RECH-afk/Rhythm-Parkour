SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS users (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    username VARCHAR(24) NOT NULL,
    username_key VARCHAR(24) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uq_users_username_key (username_key)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS auth_tokens (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id BIGINT UNSIGNED NOT NULL,
    token_hash CHAR(64) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_auth_tokens_hash (token_hash),
    KEY ix_auth_tokens_user (user_id),
    CONSTRAINT fk_auth_tokens_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS rate_limit (
    ip_hash CHAR(64) NOT NULL,
    route VARCHAR(32) NOT NULL,
    bucket_start DATETIME NOT NULL,
    count INT UNSIGNED NOT NULL DEFAULT 0,
    PRIMARY KEY (ip_hash, route),
    KEY ix_rate_limit_bucket (bucket_start)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS stats_sessions (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    session_id VARCHAR(36) NOT NULL DEFAULT '',
    app_version VARCHAR(32) NOT NULL DEFAULT '',
    platform VARCHAR(32) NOT NULL DEFAULT '',
    os VARCHAR(128) NOT NULL DEFAULT '',
    cpu_cores SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    memory_mb INT UNSIGNED NOT NULL DEFAULT 0,
    screen_w SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    screen_h SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    language VARCHAR(16) NOT NULL DEFAULT '',
    user_id BIGINT UNSIGNED NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY ix_stats_sessions_session (session_id),
    KEY ix_stats_sessions_user (user_id),
    CONSTRAINT fk_stats_sessions_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE SET NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS stats_level_results (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    session_id VARCHAR(36) NOT NULL DEFAULT '',
    level_title VARCHAR(128) NOT NULL DEFAULT '',
    total_notes INT UNSIGNED NOT NULL DEFAULT 0,
    count300 INT UNSIGNED NOT NULL DEFAULT 0,
    count100 INT UNSIGNED NOT NULL DEFAULT 0,
    count50 INT UNSIGNED NOT NULL DEFAULT 0,
    count_miss INT UNSIGNED NOT NULL DEFAULT 0,
    max_combo INT UNSIGNED NOT NULL DEFAULT 0,
    accuracy FLOAT NOT NULL DEFAULT 0,
    flow FLOAT NOT NULL DEFAULT 0,
    rank_name VARCHAR(8) NOT NULL DEFAULT '',
    failed TINYINT UNSIGNED NOT NULL DEFAULT 0,
    user_id BIGINT UNSIGNED NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY ix_stats_level_session (session_id),
    KEY ix_stats_level_user (user_id),
    CONSTRAINT fk_stats_level_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE SET NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;
