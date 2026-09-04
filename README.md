# AspNetIdentity2026

ASP.NET Core (.NET 10) Web API, laid out as a Visual Studio solution with Docker Compose
container orchestration.

## Layout

```
AspNetIdentity2026.sln          Visual Studio solution
docker-compose.dcproj           VS "Container Orchestrator Support" project
docker-compose.yml              Service definition (deployable as-is)
docker-compose.override.yml     Development-only overrides, applied automatically
.env                            Compose variable substitution (API_PORT)
src/AspNetIdentity2026.Api/
    AspNetIdentity2026.Api.csproj
    Program.cs                  Minimal API endpoints
    Dockerfile                  Multi-stage build; context is the solution root
```

## Run

**Visual Studio** — open `AspNetIdentity2026.sln`, set **docker-compose** as the startup
project, press F5. The API is debugged inside the container.

**CLI, in Docker:**

```bash
docker compose up --build
```

**CLI, on the host:**

```bash
dotnet run --project src/AspNetIdentity2026.Api
```

## Endpoints

| Endpoint           | Notes                                    |
| ------------------ | ---------------------------------------- |
| `/health`          | Liveness probe                           |
| `/weatherforecast` | Sample endpoint from the template        |
| `/openapi/v1.json` | OpenAPI document (Development only)      |

In Docker the app listens on container port `8080`, published to host `${API_PORT}`
(`8080` by default — change it in `.env`).

## Notes

- The container serves plain HTTP; TLS is expected to terminate at a reverse proxy or
  ingress in front of it. The `https` launch profile still uses the dev certificate when
  running outside Docker.
- `docker-compose.dcproj` is a Visual Studio project type. `dotnet build` on the
  **solution** cannot load it — build `src/AspNetIdentity2026.Api/AspNetIdentity2026.Api.csproj`
  directly, or use MSBuild from a VS Developer Command Prompt.
