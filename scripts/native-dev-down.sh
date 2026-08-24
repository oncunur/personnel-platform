#!/usr/bin/env sh
set -eu

NATIVE_SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
NATIVE_PROJECT_ROOT=$(CDPATH= cd -- "$NATIVE_SCRIPT_DIR/.." && pwd)
NATIVE_RUNTIME_DIR="$NATIVE_PROJECT_ROOT/.local-dev"

stop_process() {
  NATIVE_NAME=$1
  NATIVE_PID_FILE="$NATIVE_RUNTIME_DIR/$2.pid"
  if [ ! -f "$NATIVE_PID_FILE" ]; then
    printf '%s zaten kapalı.\n' "$NATIVE_NAME"
    return
  fi

  NATIVE_PID=$(sed -n '1p' "$NATIVE_PID_FILE")
  case "$NATIVE_PID" in
    ''|*[!0-9]*)
      printf '%s için geçersiz işlem kaydı temizlendi.\n' "$NATIVE_NAME"
      rm -f "$NATIVE_PID_FILE"
      return
      ;;
  esac

  if kill -0 "$NATIVE_PID" 2>/dev/null; then
    kill "$NATIVE_PID"
    printf '%s durduruldu.\n' "$NATIVE_NAME"
  else
    printf '%s zaten kapalı.\n' "$NATIVE_NAME"
  fi
  rm -f "$NATIVE_PID_FILE"
}

stop_process "Web arayüzü" web
stop_process "Arka plan işleyicisi" worker
stop_process "API" api

printf 'PostgreSQL ve Redis ortak yerel servis oldukları için açık bırakıldı.\n'
