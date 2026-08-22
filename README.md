# Personnel & Administrative Affairs Platform

Sprint 0 implementation scaffold for the Personnel & Administrative Affairs platform.

## Baseline stack

- Backend: ASP.NET Core / .NET 10 LTS
- Frontend: Next.js 16.3 + React 19.2 + TypeScript
- Database: PostgreSQL 18
- Cache: Redis 8.2
- Runtime: Docker Compose
- Architecture: Modular Monolith

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
docker compose up --build
```

Then open:

- Web: http://localhost:3000
- API ping: http://localhost:8080/api/v1/system/ping
- API readiness: http://localhost:8080/health/ready
- OpenAPI document (Development): http://localhost:8080/openapi/v1.json

> The credentials in `.env.example` are local-development defaults only. Never reuse them in production.

## Local backend development

Requires .NET 10 SDK and PostgreSQL/Redis (or the Docker services).

```bash
cd backend
dotnet restore PersonnelPlatform.sln
dotnet build PersonnelPlatform.sln
dotnet test PersonnelPlatform.sln
```

## Local frontend development

Requires Node.js 24 LTS.

```bash
cd frontend
npm install
npm run dev
```

## Sprint 0 status

See [`docs/sprint-0-status.md`](docs/sprint-0-status.md).
