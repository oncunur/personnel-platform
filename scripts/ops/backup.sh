#!/usr/bin/env bash
set -euo pipefail

BACKUP_ROOT="${1:-${BACKUP_ROOT:-backups}}"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
TARGET_DIR="$BACKUP_ROOT/$TIMESTAMP"

mkdir -p "$TARGET_DIR"

echo "Creating PostgreSQL backup..."
docker compose exec -T postgres sh -c 'pg_dump --format=custom --no-owner --no-privileges -U "$POSTGRES_USER" -d "$POSTGRES_DB"' >"$TARGET_DIR/database.dump"

echo "Creating application file-storage backup..."
docker compose exec -T api sh -c 'mkdir -p /app/storage && tar -C /app/storage -czf - .' >"$TARGET_DIR/files.tar.gz"

revision="unknown"
if command -v git >/dev/null 2>&1; then
  revision="$(git rev-parse HEAD 2>/dev/null || printf 'unknown')"
fi

cat >"$TARGET_DIR/manifest.txt" <<MANIFEST
created_at_utc=$TIMESTAMP
git_revision=$revision
contents=postgresql-custom-dump,application-file-storage
secrets_included=false
MANIFEST

(
  cd "$TARGET_DIR"
  sha256sum database.dump files.tar.gz manifest.txt >checksums.sha256
)

cat <<REPORT
Backup completed.
Directory: $TARGET_DIR
Database: $TARGET_DIR/database.dump
Files: $TARGET_DIR/files.tar.gz
Manifest: $TARGET_DIR/manifest.txt
Checksums: $TARGET_DIR/checksums.sha256

Secrets and encryption keys are intentionally NOT included. Restore them from the approved secret-management source.
REPORT
