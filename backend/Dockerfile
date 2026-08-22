FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props PersonnelPlatform.sln ./
COPY src/PersonnelPlatform.Api/PersonnelPlatform.Api.csproj src/PersonnelPlatform.Api/
COPY src/PersonnelPlatform.Application/PersonnelPlatform.Application.csproj src/PersonnelPlatform.Application/
COPY src/PersonnelPlatform.Domain/PersonnelPlatform.Domain.csproj src/PersonnelPlatform.Domain/
COPY src/PersonnelPlatform.Infrastructure/PersonnelPlatform.Infrastructure.csproj src/PersonnelPlatform.Infrastructure/
RUN dotnet restore src/PersonnelPlatform.Api/PersonnelPlatform.Api.csproj
COPY . .
RUN dotnet publish src/PersonnelPlatform.Api/PersonnelPlatform.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PersonnelPlatform.Api.dll"]
