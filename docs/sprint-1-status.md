# Sprint 1 — Identity Core Status

## Implemented in this slice

- `system.users` domain model and EF configuration
- `system.refresh_tokens` domain model and EF configuration
- Identity migration `202608220002_IdentityCore`
- Development bootstrap-admin seeding after migrations
- PBKDF2-SHA512 password hashing (210,000 iterations)
- Failed-login counter and temporary lockout
- HS256 JWT access tokens (15-minute default)
- 7-day rotating refresh tokens (configurable)
- Refresh tokens stored only as SHA-256 hashes
- Refresh token bound to user `security_version`
- HttpOnly / SameSite=Lax refresh-token cookie
- Login / refresh / logout / me endpoints
- Frontend login page and authenticated dashboard
- Unit/integration tests for user lockout, hashing and token issuance

## Security decisions

- The browser never receives the refresh token in JSON; it is kept in an HttpOnly cookie.
- The short-lived access token is held in browser session storage in this initial web slice.
- Production JWT signing keys must be supplied through environment/secret management and are not committed.
- The development bootstrap password is read from configuration and stored only as a password hash.
- A refresh token becomes invalid if the user's `security_version` changes.

## Remaining Sprint 1 work

- Roles
- Permissions
- User-role assignment
- Role-permission assignment
- User scopes (company first)
- Permission/scope authorization policies
- `/api/v1/auth/me` enrichment with roles/permissions/scopes
- Refresh-token revoke-all and security-version invalidation flow
- Security/audit events for login/logout/failures
- Identity administration UI
