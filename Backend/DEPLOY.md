# Backend — деплой на backend.relkvinstudio.top

Единственный и актуальный стек: **PHP 7.4+ + PDO/MySQL** поверх web-хостинга (Apache + ISPmanager).

Php-бэкенд для Rhythm Parkour: пинг-мониторинг в меню, регистрация/логин, анонимная статистика.
Никаких фреймворков — чистый PHP, работает на shared-хостинге.

## Требования к хостингу

| Что | Почему |
|---|---|
| PHP 7.4+ | синтаксис;(PDO входит в стандартную поставку) |
| ext-mbstring | длина/обрезка UTF-8 строк — стандартен на PHP-тарифах |
| mod_rewrite | `.htaccess` переписывает все запросы на `index.php` |
| MySQL 5.7+ / MariaDB 10.2+ | схема в `schema.sql` |

## Шаги

1. **База данных.** ISPmanager → Базы данных → создать базу `rhythmparkour` и пользователя,
   привязать пользователя к базе (все права, кроме как у вас). Хост обычно `localhost`.
2. **Схема.** Открываем phpMyAdmin (ISPmanager → Web-настройки → phpMyAdmin), выбираем базу →
   вкладка SQL → вставить всё содержимое `api/schema.sql` → Выполнить. Должно появиться 5 таблиц:
   `users`, `auth_tokens`, `rate_limit`, `stats_sessions`, `stats_level_results`.
3. **Файлы.** Залейте **всё содержимое** папки `api/` в папку сайта на хостинге.
   Рекомендую в подпапку `api` в корне сайта (тогда URL вида `https://backend.relkvinstudio.top/api/ping`).
   Внутри должно лежать: `index.php`, `config.php`, `lib/*.php`, `schema.sql`, `.htaccess`.
4. **Секреты.** Отредактируйте `api/config.php`:
   ```php
   define('RP_DB_NAME', 'имя_базы');
   define('RP_DB_USER', 'пользователь_базы');
   define('RP_DB_PASS', 'пароль_базы');
   define('RP_CLIENT_SECRET', 'секрет_какаой_впишил_в_игру');
   ```
   `RP_CLIENT_SECRET` должен быть строкой из латиницы и цифр (без спецсимволов),
   и **абсолютно таким же**, как значение в инспекторе `BackendService` (поле `Client Secret`).
5. **Клиентская часть.** В Unity настройте префаб `BackendService` (ProjectContext):
   Base URL = `https://backend.relkvinstudio.top/api/`, Client Secret = тот же секрет.
6. Загрузите сайт по домену `backend.relkvinstudio.top` (в ISPmanager привязан к папке сайта).

## Smoke-тесты (Windows: `curl.exe`)

### 1. Пинг — должен отдать `ok` и `serverTime`
```bat
curl.exe -H "RhythmParkour-Client: СЕКРЕТ" https://backend.relkvinstudio.top/api/ping
```
Ожидаемо: `{"ok":true,"error":null,"serverTime":"...","minVersion":""}`

### 2. Без секрета — должно отказать
```bat
curl.exe https://backend.relkvinstudio.top/api/ping
```
Ожидаемо: `{"ok":false,"error":"server"}` (HTTP 401)

### 3. Без HTTPS — редирект
```bat
curl.exe -H "RhythmParkour-Client: СЕКРЕТ" http://backend.relkvinstudio.top/api/ping
```
Ожидаемо: HTTP 301 на https (curl покажет телом редиректа, не JSON — это нормально).

### 4. Регистрация
```bat
curl.exe -H "RhythmParkour-Client: СЕКРЕТ" -H "Content-Type: application/json" ^
  -d "{\"username\":\"TestUser\",\"password\":\"supersecret1\"}" ^
  https://backend.relkvinstudio.top/api/register
```
Ожидаемо: `{"ok":true,"error":null,"token":"<64 hex>","accountName":"TestUser"}`

### 5. Логин (сразу после регистрации)
```bat
curl.exe -H "RhythmParkour-Client: СЕКРЕТ" -H "Content-Type: application/json" ^
  -d "{\"username\":\"TestUser\",\"password\":\"supersecret1\"}" ^
  https://backend.relkvinstudio.top/api/login
```

### 6. /me (подставьте тот же токен из шагов 4–5)
```bat
curl.exe -H "RhythmParkour-Client: СЕКРЕТ" -H "X-Auth-Token: <TOKEN>" ^
  https://backend.relkvinstudio.top/api/me
```

### 7. Внутренние файлы закрыты
```bat
curl.exe https://backend.relkvinstudio.top/api/config.php
curl.exe https://backend.relkvinstudio.top/api/lib/db.php
curl.exe https://backend.relkvinstudio.top/api/schema.sql
```
Ожидаемо: HTTP 403 (не тело файлов).

## Логи и статистика

- анонимные сессии: `SELECT * FROM stats_sessions ORDER BY id DESC LIMIT 20;`
- результаты уровней: `SELECT * FROM stats_level_results ORDER BY id DESC LIMIT 20;`
- пользователи: `SELECT id, username, created_at FROM users;`
- статистика не привязывается к пользователю, если он не вошёл в игру (`user_id` = NULL).

## Что делать при ошибках

| Симптом | Причина |
|---|---|
| HTTP 500 на любом роуте | нет соединения с БД — сверяйте DB_NAME/USER/PASS; включите `RP_DEBUG` чтобы увидеть текст ошибки |
| HTTP 500 + текст `Class 'PDO' not found` | PDO отключён у хостера — обычно включён, проверьте через ISPmanager → Параметры PHP |
| Апач не отдает статические файлы, всё даёт JSON | неверный порядок правил в `.htaccess` — сюда только содержимое не копируйте повторно |
| `404` на `/api/ping` | mod_rewrite отключён или `.htaccess` не разрешён (AllowOverride) |
| `{"ok":false,"error":"https_required"}` | клиент пришёл по http — ручной редирект в `.htaccess` уже есть, проверьте что вы зашли по https |
| `rate_limited` слишком часто | лимит по IP-хешу; у игроков за NAT общий лимит — параметры в `index.php` (`rp_rate_limit(...)`) |

## Безопасность: что уже сделано и что висит на вас

- Пароли: `password_hash` / `password_verify` (bcrypt) — никогда не хранятся в открытом виде.
- SQL: только подготовленные запросы через PDO.
- Токены: 256 бит энтропии, в БД хранится его sha256 (утечка дампа не даёт живых сессий).
- Rate-limit: таблица `rate_limit`, по IP-хешу + маршрут.
- Секрет клиента: исключает «драйверных ботов», но любой может разделить секрет из билда
  (про INTERNET-секрет это не разгадка — это фильтр, не замок). Серьёзные границы доверия:
    - нарушения хостинга → компромисят и статистику, и скрипты; держите стойкий пароль, 2FA.
    - Для обновлений игры есть отдельный сайт `wh35665.web4.maze-tech.ru` — там своя
      `.htaccess`-защита с тем же секретом заголовка.
- Менее не утечите `RP_CLIENT_SECRET` в публичный репозиторий — далее не менять без правки билда.

## Файлы (что за что отвечает)

| Файл | Роль |
|---|---|
| `index.php` | единая точка входа: HTTPS-проверка → секрет → роутинг |
| `config.php` | доступ к БД, секрет клиента, лимиты |
| `lib/db.php` | PDO-синглтон с utf8mb4 |
| `lib/security.php` | ответы JSON, парсинг тела, rate-limit, заголовки, https |
| `lib/auth.php` | register / login / logout / me, выдача токенов |
| `lib/stats.php` | приём анонимной сессии и результатов уровней |
| `schema.sql` | таблицы (импортировать в phpMyAdmin) |
| `.htaccess` | HTTPS, запрет `.sql/.md/.inc` и `lib/`, всё на `index.php` |
