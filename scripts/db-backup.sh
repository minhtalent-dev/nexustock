#!/bin/bash
set -e

# Load variables
PGHOST="${PGHOST:-postgres}"
PGPORT="${PGPORT:-5432}"
PGDATABASE="${PGDATABASE:-nexustock_main}"
PGUSER="${PGUSER:-kingsman}"
PGPASSWORD="${PGPASSWORD}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/nexustock}"
RETENTION_COUNT="${RETENTION_COUNT:-30}"

echo "Starting Postgres backup process..."

# Verify free disk space >= 10%
if [ -d "$BACKUP_DIR" ]; then
    FREE_SPACE_PCT=$(df -Ph "$BACKUP_DIR" | awk 'NR==2 {print $5}' | sed 's/%//')
    if [ "$FREE_SPACE_PCT" -ge 90 ]; then
        echo "ERROR: Disk space usage is above 90% ($FREE_SPACE_PCT%). Halting backup." >&2
        exit 1
    fi
else
    mkdir -p "$BACKUP_DIR"
fi

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/db_backup_$TIMESTAMP.sql.gz"
MD5_FILE="$BACKUP_FILE.md5"

export PGPASSWORD

# Perform backup dump
echo "Dumping database $PGDATABASE to $BACKUP_FILE..."
pg_dump -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" | gzip > "$BACKUP_FILE"

# Verify archive integrity
echo "Verifying archive integrity..."
gzip -t "$BACKUP_FILE"

# Create MD5 checksum
echo "Creating MD5 checksum..."
md5sum "$BACKUP_FILE" > "$MD5_FILE"

echo "Backup created successfully: $BACKUP_FILE"

# Retention policy: Keep last retention_count sql.gz files
echo "Applying retention policy (keeping last $RETENTION_COUNT backups)..."
cd "$BACKUP_DIR"
# List backups ordered by date, keep last $RETENTION_COUNT, delete older
ls -1tr db_backup_*.sql.gz 2>/dev/null | head -n -"$RETENTION_COUNT" | while read -r old_file; do
    echo "Deleting expired backup file: $old_file"
    rm -f "$old_file" "$old_file.md5"
done

echo "Backup process completed."
