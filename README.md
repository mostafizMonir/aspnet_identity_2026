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
src/CookieAuthentication/
    Program.cs                  Cookie auth wiring + demo endpoints
    Authentication/             ITicketStore (server-side sessions) and /auth endpoints
    Users/                      IUserStore seam + in-memory demo users
    wwwroot/loginform.html      Browser demo page
    Dockerfile
```

## Services

| Service                  | Host port | What it is                                    |
| ------------------------ | --------- | --------------------------------------------- |
| `aspnetidentity2026.api` | `8080`    | The main API                                  |
| `cookieauthentication`   | `8081`    | Session-cookie authentication sample          |

Both listen on container port `8080`; only the published host port differs. Override with
`API_PORT` / `COOKIE_AUTH_PORT` in `.env`.

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

## Testing the cookie authentication sample

Start the stack, then open **<http://localhost:8081/loginform>**.

The page has a login form (`alice` / `Password123!`, or `bob` for a non-admin) and buttons
for `/auth/me`, `/protected`, `/admin` and logout. Watch the response log as you go:

1. Call `/protected` before logging in — `401`.
2. Log in, then call it again — `200`, with no token handled by any JavaScript. The browser
   attached the cookie by itself.
3. Click **Read document.cookie** — it comes back empty. The cookie is `HttpOnly`, so script
   cannot read the session id even while the session is active.
4. Log out, then call `/auth/me` — `401`. The cookie is still in the browser and still
   unexpired; it is worthless because the server deleted the session. A stateless token
   could not be revoked this way.

### From curl

curl will not *store* a `Secure` cookie received over plain HTTP (browsers make an exception
for `localhost`; curl does not), so capture the session id and send it back by hand:

```bash
B=http://localhost:8081

# Log in, and pull the session id out of the Set-Cookie header.
SID=$(curl -s -i -X POST $B/auth/login \
        -H 'Content-Type: application/json' \
        -d '{"username":"alice","password":"Password123!"}' \
      | grep -i '^set-cookie' \
      | sed 's/.*__Host-session=\([^;]*\).*/\1/' \
      | tr -d '\r')

curl -s -b "__Host-session=$SID" $B/auth/me
curl -s -b "__Host-session=$SID" $B/protected
curl -s -b "__Host-session=$SID" -X POST $B/auth/logout
curl -s -b "__Host-session=$SID" $B/auth/me      # 401 - session revoked server-side
```

## Notes

- The cookie sample stores sessions in `AddDistributedMemoryCache`, so they are per-container
  and vanish on restart. Swap in `AddStackExchangeRedisCache` for shared, durable sessions;
  the `ITicketStore` implementation does not change.
- The container serves plain HTTP; TLS is expected to terminate at a reverse proxy or
  ingress in front of it. The `https` launch profile still uses the dev certificate when
  running outside Docker.
- `docker-compose.dcproj` is a Visual Studio project type. `dotnet build` on the
  **solution** cannot load it — build `src/AspNetIdentity2026.Api/AspNetIdentity2026.Api.csproj`
  directly, or use MSBuild from a VS Developer Command Prompt.
