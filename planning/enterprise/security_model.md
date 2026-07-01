# Security model

## Tenancy

- Model: multi-warehouse cùng tenant.
- `tenantId` = company/organization.
- `warehouseId` = operating warehouse under tenant.
- API must reject cross-tenant access even when ID is guessed.
- Warehouse-scoped permissions must check both `tenantId` and allowed `warehouseId`.

## Auth and authorization

- Human users authenticate through app auth.
- Mutation APIs require permission code and audit.
- Device operations require station pairing and device permission.
- Integration APIs require partner credential and idempotency key.

## Local agent trust model

- Local Agent runs as Windows service.
- Bind only `127.0.0.1:9000`; never bind `0.0.0.0`.
- Browser origin allowlist required.
- First pairing uses short-lived pairing code generated from authenticated UI.
- Pairing token stored in OS-protected user/machine storage, not plain text config.
- Agent token rotation required on admin revoke, station reinstall, or suspected compromise.
- WebSocket messages include `messageId`, `stationId`, `deviceId`, `timestamp`, `traceId`.
- Agent rejects stale messages older than configured skew.

## Device priority

1. Local agent Windows service.
2. Scanner keyboard wedge.
3. Scale COM.
4. Zebra ZPL printer.
5. TSC TSPL printer.

## Health endpoint rule

- `/health/live` returns process liveness only.
- `/health/ready` returns dependency readiness without secrets.
- Health endpoints should be restricted by network/reverse proxy where possible, not normal business auth.
- Response must not expose connection string, token, printer path, COM details or machine secret.

## Secret handling

- Never log password, token, HMAC secret, pairing token or raw authorization header.
- Mask integration credentials in UI and logs.
- Rotation events must be audited.

## Audit

Audit required for:

- Permission changes.
- Pairing/revoking station.
- Manual weight override.
- Reprint.
- Import commit.
- Webhook replay.
- Deployment approval/checklist sign-off.
