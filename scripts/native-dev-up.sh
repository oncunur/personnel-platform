#!/usr/bin/env sh
set -eu

NATIVE_SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
NATIVE_PROJECT_ROOT=$(CDPATH= cd -- "$NATIVE_SCRIPT_DIR/.." && pwd)
NATIVE_RUNTIME_DIR="$NATIVE_PROJECT_ROOT/.local-dev"
NATIVE_API_PORT=${NATIVE_API_PORT:-8080}
NATIVE_WEB_PORT=${NATIVE_WEB_PORT:-3000}
NATIVE_DB_HOST=${NATIVE_DB_HOST:-127.0.0.1}
NATIVE_DB_PORT=${NATIVE_DB_PORT:-5432}
NATIVE_DB_NAME=${NATIVE_DB_NAME:-personnel_platform}
NATIVE_DB_USER=${NATIVE_DB_USER:-$(id -un)}
NATIVE_DB_PASSWORD=${NATIVE_DB_PASSWORD:-}
NATIVE_REDIS_HOST=${NATIVE_REDIS_HOST:-127.0.0.1}
NATIVE_REDIS_PORT=${NATIVE_REDIS_PORT:-6379}
NATIVE_START_WORKER=${NATIVE_START_WORKER:-0}

if command -v brew >/dev/null 2>&1; then
  NATIVE_HOMEBREW_PG=$(brew --prefix postgresql@18 2>/dev/null || true)
  NATIVE_HOMEBREW_NODE=$(brew --prefix node@24 2>/dev/null || true)
  NATIVE_PATH_PREFIX=""
  [ -n "$NATIVE_HOMEBREW_PG" ] && NATIVE_PATH_PREFIX="$NATIVE_HOMEBREW_PG/bin"
  [ -n "$NATIVE_HOMEBREW_NODE" ] && NATIVE_PATH_PREFIX="${NATIVE_PATH_PREFIX:+$NATIVE_PATH_PREFIX:}$NATIVE_HOMEBREW_NODE/bin"
  [ -n "$NATIVE_PATH_PREFIX" ] && export PATH="$NATIVE_PATH_PREFIX:$PATH"
fi

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    printf 'Eksik gereksinim: %s\n' "$1" >&2
    printf 'Kurulum rehberi: docs/development/native-local-development.md\n' >&2
    exit 1
  fi
}

process_is_running() {
  NATIVE_PID_FILE=$1
  [ -f "$NATIVE_PID_FILE" ] || return 1
  NATIVE_PID=$(sed -n '1p' "$NATIVE_PID_FILE")
  case "$NATIVE_PID" in
    ''|*[!0-9]*) return 1 ;;
  esac
  kill -0 "$NATIVE_PID" 2>/dev/null
}

case "$NATIVE_DB_NAME" in
  ''|*[!abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_]*)
    printf 'NATIVE_DB_NAME yalnızca harf, sayı ve alt çizgi içerebilir.\n' >&2
    exit 1
    ;;
esac
case "$NATIVE_DB_USER" in
  ''|*[!abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.-]*)
    printf 'NATIVE_DB_USER geçersiz karakter içeriyor.\n' >&2
    exit 1
    ;;
esac
case "$NATIVE_DB_PORT:$NATIVE_REDIS_PORT:$NATIVE_API_PORT:$NATIVE_WEB_PORT" in
  *[!0123456789:]*|:*|*::*|*:)
    printf 'Yerel servis portları sayısal ve dolu olmalıdır.\n' >&2
    exit 1
    ;;
esac
case "$NATIVE_DB_HOST:$NATIVE_DB_PASSWORD" in
  *';'*)
    printf 'Yerel veritabanı bağlantı değerleri noktalı virgül içeremez.\n' >&2
    exit 1
    ;;
esac
case "$NATIVE_START_WORKER" in
  0|1) ;;
  *)
    printf 'NATIVE_START_WORKER yalnızca 0 veya 1 olabilir.\n' >&2
    exit 1
    ;;
esac

require_command dotnet
require_command node
require_command curl
require_command pg_isready
require_command psql
require_command createdb
require_command redis-cli

if ! pg_isready -h "$NATIVE_DB_HOST" -p "$NATIVE_DB_PORT" >/dev/null 2>&1; then
  printf 'PostgreSQL hazır değil (%s:%s). Önce yerel servisi başlatın.\n' "$NATIVE_DB_HOST" "$NATIVE_DB_PORT" >&2
  exit 1
fi

if ! redis-cli -h "$NATIVE_REDIS_HOST" -p "$NATIVE_REDIS_PORT" ping 2>/dev/null | grep -q '^PONG$'; then
  printf 'Redis hazır değil (%s:%s). Önce yerel servisi başlatın.\n' "$NATIVE_REDIS_HOST" "$NATIVE_REDIS_PORT" >&2
  exit 1
fi

if [ ! -d "$NATIVE_PROJECT_ROOT/frontend/node_modules/next" ]; then
  printf 'Ön yüz paketleri eksik. Önce frontend klasöründe npm install çalıştırın.\n' >&2
  exit 1
fi

mkdir -p "$NATIVE_RUNTIME_DIR/storage"

if [ -n "$NATIVE_DB_PASSWORD" ]; then
  export PGPASSWORD="$NATIVE_DB_PASSWORD"
fi

if ! psql -h "$NATIVE_DB_HOST" -p "$NATIVE_DB_PORT" -U "$NATIVE_DB_USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$NATIVE_DB_NAME'" | grep -q '^1$'; then
  printf 'Yerel veritabanı oluşturuluyor: %s\n' "$NATIVE_DB_NAME"
  createdb -h "$NATIVE_DB_HOST" -p "$NATIVE_DB_PORT" -U "$NATIVE_DB_USER" -O "$NATIVE_DB_USER" "$NATIVE_DB_NAME"
fi

NATIVE_CONNECTION_STRING="Host=$NATIVE_DB_HOST;Port=$NATIVE_DB_PORT;Database=$NATIVE_DB_NAME;Username=$NATIVE_DB_USER;Include Error Detail=true"
if [ -n "$NATIVE_DB_PASSWORD" ]; then
  NATIVE_CONNECTION_STRING="$NATIVE_CONNECTION_STRING;Password=$NATIVE_DB_PASSWORD"
fi

if ! process_is_running "$NATIVE_RUNTIME_DIR/api.pid"; then
  (
    cd "$NATIVE_PROJECT_ROOT"
    nohup env \
      ASPNETCORE_ENVIRONMENT=Development \
      ASPNETCORE_URLS="http://127.0.0.1:$NATIVE_API_PORT" \
      Cors__WebOrigin="http://localhost:$NATIVE_WEB_PORT" \
      Cors__LoopbackOrigin="http://127.0.0.1:$NATIVE_WEB_PORT" \
      ConnectionStrings__Postgres="$NATIVE_CONNECTION_STRING" \
      Redis__Host="$NATIVE_REDIS_HOST" \
      Redis__Port="$NATIVE_REDIS_PORT" \
      Jwt__SigningKey="${NATIVE_JWT_SIGNING_KEY:-dev-only-signing-key-change-me-at-least-32-bytes-long}" \
      Security__DataProtectionKey="${NATIVE_DATA_PROTECTION_KEY:-MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=}" \
      FileStorage__RootPath="$NATIVE_RUNTIME_DIR/storage" \
      BootstrapAdmin__Username="${NATIVE_ADMIN_USERNAME:-admin}" \
      BootstrapAdmin__Password="${NATIVE_ADMIN_PASSWORD:-Admin123!ChangeMe}" \
      BootstrapAdmin__Email="${NATIVE_ADMIN_EMAIL:-admin@local.test}" \
      dotnet run --project backend/src/PersonnelPlatform.Api/PersonnelPlatform.Api.csproj --no-launch-profile \
      >"$NATIVE_RUNTIME_DIR/api.log" 2>&1 &
    printf '%s\n' "$!" >"$NATIVE_RUNTIME_DIR/api.pid"
  )
  printf 'API başlatıldı.\n'
else
  printf 'API zaten çalışıyor.\n'
fi

if ! process_is_running "$NATIVE_RUNTIME_DIR/web.pid"; then
  (
    cd "$NATIVE_PROJECT_ROOT/frontend"
    nohup env \
      NEXT_PUBLIC_API_BASE_URL="http://localhost:$NATIVE_API_PORT" \
      API_INTERNAL_URL="http://127.0.0.1:$NATIVE_API_PORT" \
      node node_modules/next/dist/bin/next dev --hostname 127.0.0.1 --port "$NATIVE_WEB_PORT" \
      >"$NATIVE_RUNTIME_DIR/web.log" 2>&1 &
    printf '%s\n' "$!" >"$NATIVE_RUNTIME_DIR/web.pid"
  )
  printf 'Web arayüzü başlatıldı.\n'
else
  printf 'Web arayüzü zaten çalışıyor.\n'
fi

NATIVE_WAIT_COUNT=0
while ! curl -fsS "http://127.0.0.1:$NATIVE_API_PORT/health/live" >/dev/null 2>&1; do
  NATIVE_WAIT_COUNT=$((NATIVE_WAIT_COUNT + 1))
  if [ "$NATIVE_WAIT_COUNT" -ge 45 ]; then
    printf 'API zamanında hazır olmadı. Ayrıntı: %s\n' "$NATIVE_RUNTIME_DIR/api.log" >&2
    exit 1
  fi
  sleep 1
done

if [ "$NATIVE_START_WORKER" = "1" ]; then
  if ! process_is_running "$NATIVE_RUNTIME_DIR/worker.pid"; then
    (
      cd "$NATIVE_PROJECT_ROOT"
      nohup env \
        DOTNET_ENVIRONMENT=Development \
        ConnectionStrings__Postgres="$NATIVE_CONNECTION_STRING" \
        Redis__Host="$NATIVE_REDIS_HOST" \
        Redis__Port="$NATIVE_REDIS_PORT" \
        Security__DataProtectionKey="${NATIVE_DATA_PROTECTION_KEY:-MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=}" \
        FileStorage__RootPath="$NATIVE_RUNTIME_DIR/storage" \
        dotnet run --project backend/src/PersonnelPlatform.Worker/PersonnelPlatform.Worker.csproj --no-launch-profile \
        >"$NATIVE_RUNTIME_DIR/worker.log" 2>&1 &
      printf '%s\n' "$!" >"$NATIVE_RUNTIME_DIR/worker.pid"
    )
    printf 'Arka plan işleyicisi başlatıldı.\n'
  else
    printf 'Arka plan işleyicisi zaten çalışıyor.\n'
  fi
else
  printf 'Düşük kaynak modu: arka plan işleyicisi kapalı.\n'
fi

printf '\nPlatform Docker kullanılmadan çalışıyor:\n'
printf '  Uygulama: http://localhost:%s\n' "$NATIVE_WEB_PORT"
printf '  API:      http://localhost:%s\n' "$NATIVE_API_PORT"
printf '  Durdur:   bash scripts/native-dev-down.sh\n'
printf '  Kayıtlar: %s\n' "$NATIVE_RUNTIME_DIR"
