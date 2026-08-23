#!/usr/bin/env bash
set -euo pipefail

BACKUP_DIR="${1:-}"
if [[ -z "$BACKUP_DIR" ]]; then
  echo "Usage: CONFIRM_RESTORE=YES bash scripts/ops/restore.sh <backup-directory>" >&2
  exit 2
fi

if [[ "${CONFIRM_RESTORE:-}" != "YES" ]]; then
  echo "Restore is destructive. Set CONFIRM_RESTORE=YES to continue." >&2
  exit 2
fi

for required in database.dump files.tar.gz manifest.txt checksums.sha256; do
  if [[ ! -f "$BACKUP_DIR/$required" ]]; then
    echo "Missing backup file: $BACKUP_DIR/$required" >&2
    exit 2
  fi
done

(
  cd "$BACKUP_DIR"
  sha256sum --check checksums.sha256
)

echo "Stopping application services before restore..."
docker compose stop api worker web >/dev/null

restart_services() {
  echo "Starting application services..."
  docker compose up -d api worker web >/dev/null || true
}
trap restart_services EXIT

echo "Restoring PostgreSQL database..."
docker compose exec -T postgres sh -c 'pg_restore --clean --if-exists --exit-on-error --no-owner --no-privileges -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <"$BACKUP_DIR/database.dump"

echo "Restoring application file storage..."
cat "$BACKUP_DIR/files.tar.gz" | docker compose run --rm -T --no-deps --entrypoint sh api -c 'mkdir -p /app/storage && find /app/storage -mindepth 1 -maxdepth 1 -exec rm -rf {} + && tar -C /app/storage -xzf -'

restart_services
trap - EXIT

echo "Restore completed. Validate /health/ready and business reconciliation before reopening traffic."
