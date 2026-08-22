# Personnel & Administrative Affairs Platform

Personnel & Administrative Affairs platform implemented as a modular monolith.

## Baseline stack

- Backend: ASP.NET Core / .NET 10 LTS
- Frontend: Next.js 16.3 + React 19.2 + TypeScript
- Database: PostgreSQL 18
- Cache: Redis 8.2
- Runtime: Docker Compose
- Architecture: Modular Monolith

## Current implementation

Sprint 0 platform foundation is complete. Sprint 1 identity work currently includes:

- User and refresh-token persistence
- PBKDF2-SHA512 password hashing
- Login with failed-attempt lockout
- HMAC-SHA256 JWT access tokens
- Rotating refresh tokens stored as SHA-256 hashes
- HttpOnly refresh-token cookie for the web client
- `/api/v1/auth/login`, `/refresh`, `/logout`, `/me`
- Development-only bootstrap admin
- Login and authenticated dashboard screens

Role, permission and scope authorization will be added next in Sprint 1.

## Repository layout

```text
personnel-platform/
├── backend/
│   ├── src/
│   │   ├── PersonnelPlatform.Api
│   │   ├── PersonnelPlatform.Application
│   │   ├── PersonnelPlatform.Domain
│   │   ├── PersonnelPlatform.Infrastructure
│   │   └── PersonnelPlatform.Worker
│   └── tests/
├── frontend/
├── infrastructure/
├── scripts/
├── docs/
└── .github/workflows/
```

## Quick start with Docker

```bash
cp .env.example .env
# Change the development JWT and bootstrap-admin values in .env before starting.
docker compose up --build
```

Then open:

- Web: http://localhost:3000
- Login: http://localhost:3000/login
- API ping: http://localhost:8080/api/v1/system/ping
- API readiness: http://localhost:8080/health/ready
- OpenAPI document (Development): http://localhost:8080/openapi/v1.json

The development bootstrap username comes from `BOOTSTRAP_ADMIN_USERNAME` (default example: `admin`).
The password comes from `BOOTSTRAP_ADMIN_PASSWORD` and is hashed before it is stored in PostgreSQL.

> Values in `.env.example` are local-development examples only. Do not reuse them in production.

## Local backend development

Requires .NET 10 SDK and PostgreSQL/Redis (or the Docker services).
The API also requires a JWT signing key with at least 32 UTF-8 bytes.

```bash
export Jwt__SigningKey='replace-this-with-a-long-development-key-at-least-32-bytes'
export BootstrapAdmin__Username='admin'
export BootstrapAdmin__Password='Admin123!ChangeMe'
export BootstrapAdmin__Email='admin@local.test'

cd backend
dotnet restore PersonnelPlatform.sln
dotnet build PersonnelPlatform.sln
dotnet test --solution PersonnelPlatform.sln
```

## Local frontend development

Requires Node.js 24 LTS.

```bash
cd frontend
npm install
npm run dev
```

## Sprint status

- [`docs/sprint-0-status.md`](docs/sprint-0-status.md)
- [`docs/sprint-1-status.md`](docs/sprint-1-status.md)
