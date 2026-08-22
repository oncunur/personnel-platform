# Sprint 0 — Implementation Status

## Code baseline completed

The repository contains implementation baselines for all Sprint 0 technical cards:

- TECH-000 Monorepo and folder baseline
- TECH-001 ASP.NET Core layered project skeleton
- TECH-002 Next.js + TypeScript frontend skeleton
- TECH-003 PostgreSQL Docker baseline and EF Core context/migration
- TECH-004 Redis Docker baseline and readiness probe
- TECH-005 Docker Compose development stack
- TECH-006 Standard API exception envelope
- TECH-007 Correlation ID / structured request scope baseline
- TECH-008 `/health/live` and `/health/ready`
- TECH-009 Unit/integration test project skeletons
- TECH-010 CI workflow baseline
- TECH-011 Secrets/environment baseline (`.env.example`, no production secrets)

## Required verification gate

Runtime verification is still pending because the generation environment does not contain the .NET SDK or Docker engine. Run the commands in `docs/validation.md` on CI or a developer machine before Sprint 0 is marked Done.

## Next implementation slice

After that gate passes, continue directly with Sprint 1:

1. AUTH-001 Login + secure password hashing.
2. AUTH-002 Refresh token rotation/revocation.
3. SEC-001 Users.
4. SEC-002/003 Roles and permissions.
5. SEC-004 Company scope authorization.
6. AUD-001 audit persistence/viewer.
