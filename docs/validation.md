# Scaffold Validation

Validated on 2026-08-22 in the generation environment.

## Completed

- JSON files parsed successfully.
- `.csproj` and `.props` XML parsed successfully.
- YAML files parsed successfully.
- Repository file layout and cross-project references were reviewed statically.
- Archive SHA-256 generated.

## Environment limitation

The generation runtime does not include the .NET SDK or Docker engine. Therefore the backend solution and Docker Compose stack could not be compiled/launched here.

The first runtime gate on a development machine or CI runner is:

```bash
cd backend
dotnet restore PersonnelPlatform.sln
dotnet build PersonnelPlatform.sln
dotnet test PersonnelPlatform.sln

cd ..
cp .env.example .env
docker compose up --build
```

Expected endpoints after a successful start:

- `GET http://localhost:8080/health/live`
- `GET http://localhost:8080/health/ready`
- `GET http://localhost:8080/api/v1/system/ping`
- `GET http://localhost:8080/openapi/v1.json`
- `GET http://localhost:3000`
