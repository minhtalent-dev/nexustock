#!/bin/bash
set -e

# Load inputs
PREVIOUS_API_IMAGE="${PREVIOUS_API_IMAGE}"
PREVIOUS_WEB_IMAGE="${PREVIOUS_WEB_IMAGE}"
COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.prod.yml}"
HEALTH_URL="${HEALTH_URL:-http://localhost:5024/health/ready}"
DB_RESTORE_PATH="$1"

echo "Initiating deployment rollback runbook..."

if [ -z "$PREVIOUS_API_IMAGE" ] || [ -z "$PREVIOUS_WEB_IMAGE" ]; then
    echo "ERROR: PREVIOUS_API_IMAGE or PREVIOUS_WEB_IMAGE variable is not specified." >&2
    exit 1
fi

# 1. Restore Database if pre-deploy backup path provided
if [ -n "$DB_RESTORE_PATH" ]; then
    echo "Restoring database state to pre-deploy backup..."
    export ALLOW_RESTORE_TO_TARGET="true"
    bash "$(dirname "$0")/db-restore.sh" "$DB_RESTORE_PATH"
fi

# 2. Update service image tags in current compose environment via export overrides
echo "Reverting container tags..."
export NEXUSTOCK_API_IMAGE="$PREVIOUS_API_IMAGE"
export NEXUSTOCK_WEB_IMAGE="$PREVIOUS_WEB_IMAGE"

# 3. Restart Compose stack with reverted images
echo "Restarting service stack with previous image tags..."
docker compose -f "$COMPOSE_FILE" down
docker compose -f "$COMPOSE_FILE" up -d api web

# 4. Verify readiness
echo "Verifying health check target: $HEALTH_URL"
RETRIES=12
count=0
while [ $count -lt $RETRIES ]; do
    if curl -s -f -o /dev/null "$HEALTH_URL"; then
        echo "SUCCESS: Rollback complete. Target health ready."
        exit 0
    fi
    echo "Waiting for health check ready... ($((count+1))/$RETRIES)"
    sleep 5
    count=$((count+1))
done

echo "ERROR: Health check verification failed after rollback." >&2
exit 1
