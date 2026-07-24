# AeroFly.Web

ASP.NET Core MVC airline reservation system targeting .NET 8 and SQL Server.

Before first run, read [SECURITY_SETUP.md](SECURITY_SETUP.md). Database, email,
Stripe API, and webhook secrets must come from environment variables or .NET User
Secrets; committed configuration files intentionally contain no credentials.

## Run locally

```bash
dotnet --info
dotnet restore AeroFly.Web.sln
dotnet tool restore --tool-manifest AeroFly.Web/.config/dotnet-tools.json
dotnet ef database update --project AeroFly.Web/AeroFly.Web.csproj
dotnet run --project AeroFly.Web/AeroFly.Web.csproj
```

The configured SQL Server must be reachable before `database update` or `run`, because the application applies pending migrations and initializes required data at startup.

For a local run without the external SQL Server, use the Development environment. It uses SQLite at `/tmp/aerofly-local.db` while SQL Server remains the default for non-development environments:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project AeroFly.Web/AeroFly.Web.csproj
```

## Verify

```bash
dotnet build AeroFly.Web.sln --configuration Release
dotnet test AeroFly.Web.sln --configuration Release
```
