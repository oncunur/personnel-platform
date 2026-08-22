# Engineering Agent Guide

## Architecture
- Keep the backend as a modular monolith until an explicit ADR changes that decision.
- Domain must not reference Application, Infrastructure or Api.
- Application may reference Domain only.
- Infrastructure implements application-facing technical concerns and may reference Application + Domain.
- Api composes the application; business rules do not belong in endpoints/middleware.

## Data
- PostgreSQL migrations are versioned and reviewed.
- Use UUID primary keys, `timestamptz` for instants and integer minutes for duration.
- Historical salary/price/payroll data is append/version oriented; do not silently overwrite history.
- Critical uniqueness and overlap rules should be protected in the database as well as the application layer.

## Security
- Authorization is Role + Permission + Scope + Field Access.
- Never rely on hidden frontend controls for authorization.
- Never log passwords, tokens, full identity numbers, IBANs or salary details.
- Sensitive file access must flow through authorized backend endpoints.

## API
- Base business API path is `/api/v1`.
- Use explicit action endpoints for state transitions (approve, close, check-in, check-out).
- Errors must expose stable machine-readable codes and a correlation/trace id.

## Quality
- New business rules require unit tests.
- DB constraints, scope and workflows require integration tests.
- A story is not Done without permission/scope/audit/error-path validation.
