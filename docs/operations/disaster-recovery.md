# Backup, Restore and Disaster Recovery Runbook

This runbook defines the Sprint 15 HARD-006 backup/restore and disaster-recovery baseline for the Personnel Platform.

## Protected state

A recoverable environment needs all of the following:

1. PostgreSQL database.
2. Application file storage used by documents and generated files.
3. Deployment configuration.
4. Secrets and cryptographic keys, especially `Security:DataProtectionKey` and JWT signing material.

The backup scripts intentionally include only PostgreSQL data, application files, and a non-secret manifest. Secrets are **not** written into backup archives. They must be backed up and recovered through the approved secret-management mechanism.

The Docker Compose topology mounts one shared `file_storage` volume at `/app/storage` for both API and worker services. This keeps file-backed document/report state persistent across container recreation and makes it possible to back up the same store consistently.

## Create a backup

With the platform running:

```bash
bash scripts/ops/backup.sh backups
```

A timestamped directory is created under `backups/` containing:

- `database.dump` — PostgreSQL custom-format archive.
- `files.tar.gz` — application file storage.
- `manifest.txt` — creation time, source revision, and backup contents.
- `checksums.sha256` — integrity checksums.

The local `backups/` directory is gitignored. Backup archives may contain personal, payroll, identity, and operational data and therefore must be encrypted and access-controlled when copied outside the host.

## Restore

Restore is destructive and requires explicit acknowledgement:

```bash
CONFIRM_RESTORE=YES bash scripts/ops/restore.sh backups/20260823T220000Z
```

The restore script:

1. Verifies required backup files and SHA-256 checksums.
2. Stops API, worker, and web services to prevent concurrent writes.
3. Restores PostgreSQL with `--clean --if-exists`.
4. Replaces the shared application file-storage contents.
5. Restarts application services.

After restore, do not reopen user traffic until readiness and business reconciliation are complete.

## Required post-restore validation

At minimum:

```bash
bash scripts/ops/health-watch.sh http://127.0.0.1:8080
```

Then validate the restored environment at business level:

- Authentication works and security-version/session rules behave normally.
- Sensitive fields can be decrypted; failure here usually indicates the wrong `Security:DataProtectionKey`.
- Personnel/document attachments are present and downloadable by authorized users.
- Attendance and meal integration queues are internally consistent.
- Payroll, reporting, and ERP reconciliation totals match the backup point.
- Background workers resume without recurring errors or unexpected duplicate processing.
- Audit data is present for the restored period.

## Encryption-key dependency

Sensitive identity/IBAN/salary data is encrypted at rest. A database backup without the matching data-protection key is not a usable recovery point for those fields.

Store the production data-protection key separately from the data backup, with controlled access and an independently tested recovery procedure. Never place it in Git, backup manifests, workflow output, tickets, or chat logs.

## Isolated DR test

The manual GitHub Actions workflow `dr-restore-test` validates the restore mechanics in an ephemeral Docker environment. It:

- starts PostgreSQL, Redis, API, worker, and web;
- waits for readiness;
- writes a file-storage marker;
- creates a database + file backup;
- mutates the marker after the backup;
- restores the backup;
- verifies the original marker returned;
- verifies API readiness and public health probes.

The workflow uses only disposable CI state and deletes volumes at the end. It does not upload the generated backup as an artifact, avoiding accidental exposure of seeded data.

## Production DR drill

Before Go/No-Go, execute a production-like restore into an isolated environment using an approved backup and independently recovered configuration/secrets. Record:

- backup timestamp and source environment;
- restore start/end timestamps;
- measured recovery time (RTO evidence);
- recovered data timestamp / maximum data loss (RPO evidence);
- checksum verification result;
- database migration/version state;
- file count/size reconciliation;
- business reconciliation results;
- monitoring/alerting validation;
- named approvers and unresolved findings.

A technical restore that starts successfully is not sufficient by itself. HARD-006 is operationally accepted only when data, files, encryption keys, business totals, and monitoring are all verified together.

## Recovery ownership

Production Go-Live must name owners for database restore, secret/key recovery, file-storage recovery, application deployment, business reconciliation, security validation, and incident command. The runbook should be exercised whenever storage topology, encryption keys, database major versions, or deployment architecture materially change.
