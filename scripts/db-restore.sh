#!/bin/bash
set -e

# Load variables
PGHOST="${PGHOST:-postgres}"
PGPORT="${PGPORT:-5432}"
PGDATABASE="${PGDATABASE:-nexustock_main}"
PGUSER="${PGUSER:-kingsman}"
PGPASSWORD="${PGPASSWORD}"
ALLOW_RESTORE_TO_TARGET="${ALLOW_RESTORE_TO_TARGET:-false}"

BACKUP_FILE="$1"

if [ -z "$BACKUP_FILE" ]; then
    echo "ERROR: Missing backup file path argument." >&2
    echo "Usage: $0 /path/to/backup.sql.gz" >&2
    exit 1
fi

if [ ! -f "$BACKUP_FILE" ]; then
    echo "ERROR: Backup file not found: $BACKUP_FILE" >&2
    exit 1
fi

echo "Starting Postgres restore process..."

# Verify MD5 checksum if exists
if [ -f "$BACKUP_FILE.md5" ]; then
    echo "Verifying MD5 checksum..."
    md5sum -c "$BACKUP_FILE.md5"
else
    echo "WARNING: Checksum file not found ($BACKUP_FILE.md5). Skipping checksum check."
fi

# Safeguard against accidental production restore
if [ "$ALLOW_RESTORE_TO_TARGET" != "true" ]; then
    echo "ERROR: Target restoration is protected. Set ALLOW_RESTORE_TO_TARGET=true to proceed." >&2
    exit 1
fi

export PGPASSWORD

echo "Restoring $BACKUP_FILE into database $PGDATABASE..."
# Drop existing public schema tables and restore (we assume pg_restore/psql rules)
gunzip -c "$BACKUP_FILE" | psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE"

echo "Database restoration completed successfully."
