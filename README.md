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

The platform now covers the Sprint 0–16 product baseline, including:

- Configurable MFA (disabled by default), session security, role/permission and company-scope authorization
- Organization, personnel, sensitive profile and digital personnel documents
- Leave, attendance, shifts, daily calculation and overtime approvals
- Camp, meal, payroll, cost reporting and ERP reconciliation
- Assets, stock, vehicles and administrative-affairs operations
- Workflow approvals, notifications, integrations and import operations
- Audit, monitoring, backup/restore, migration staging and UAT contracts
- Responsive Turkish web interface for daily operational use

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

## Low-resource local start without Docker

For macOS development, PostgreSQL and Redis can run as lightweight native services while the API and web application run directly on the computer:

```bash
bash scripts/native-dev-up.sh
```

The default mode does not start the background Worker, reducing idle CPU use. See the one-time setup and troubleshooting guide:

- [`docs/development/native-local-development.md`](docs/development/native-local-development.md)

## Docker Compose start

Docker remains available for isolated full-stack and CI-equivalent testing:

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
- [`docs/sprint-16-status.md`](docs/sprint-16-status.md)
