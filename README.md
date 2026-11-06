# GrocerEase

GrocerEase is a web-based grocery shopping platform. It is meant to let customers browse, search, and order basic grocery items remotely, and to give store administrators a way to manage stock.

The planned scope, from the Software Design Description:

- Customer registration, login, and session management
- Product browsing and keyword / category search
- Shopping cart and order placement
- Remote payment (Stripe)
- Order delivery tracking
- Receipt generation and email (SendGrid / SMTP)
- Admin dashboard for inventory: add, update, and remove products, and view stock levels

The planned stack is a ReactJS frontend, an ASP.NET Core REST API, and PostgreSQL.

## Current status

Only the backend's authentication slice exists so far. There are no products, cart, orders, or payments yet, and no frontend.

| Endpoint | Auth | Purpose |
|---|---|---|
| `POST /auth/register` | public | Create a user and return a JWT and a refresh token with `201`. Duplicate email returns `409`, validation failures return `422` |
| `POST /auth/login` | public | Verify credentials and return a JWT and a refresh token. Wrong credentials return `401` |
| `POST /auth/refresh` | public | Trade a refresh token for a new JWT and a new refresh token. The old refresh token stops working. An unknown, expired or already used token returns `401` |
| `POST /auth/logout` | public | Revoke the session the refresh token belongs to and return `204`. Any token, known or not, gets `204` |
| `GET /profile` | bearer token | Return the caller's id and email |
| `GET /health/live` | public | `200 Healthy` while the process answers. Runs no checks |
| `GET /health/ready` | public | `200 Healthy`, or `503 Unhealthy` when the database cannot be reached. Compose uses it as the API's healthcheck |

## Error responses

Errors are returned as `application/problem+json`. Clients should branch on `code`, not `title`.

| Status | `code` | When | Body |
|---|---|---|---|
| `400` | `Request.Malformed` | The body cannot be read or bound: broken JSON, `null`, an empty body, a wrong type, or a missing property. Also Kestrel rejecting the request while reading the body (`400`, `408`, `413`) | `errors` keyed by the JSON field name (`password`) or, for a body that cannot be parsed, a JSON path (`$.email`), except Kestrel's, which has `detail` |
| `400` | `Database.ConstraintViolation` | A database constraint other than the unique email failed (register) | `detail` |
| `401` | `Auth.Unauthorized` | A protected endpoint such as `/profile` was called without a bearer token, or with one that is expired, tampered with, or issued for another audience. `WWW-Authenticate` is `Bearer`, or `Bearer error="invalid_token"` when a token was sent | `detail` |
| `401` | `User.InvalidCredentials` | Wrong email or password | `detail` |
| `401` | `Auth.InvalidRefreshToken` | `/auth/refresh` was given a refresh token that is unknown, expired, or already used. One message covers all three. Presenting a used token also revokes every token from the same login | `detail` |
| `404` | `Request.NotFound` | No such route | `detail` |
| `405` | `Request.MethodNotAllowed` | The route exists but not for this method. `Allow` lists the methods that work | `detail` |
| `409` | `User.DuplicateEmail` | The email is already registered | `detail` |
| `409` | `Database.ConcurrentUpdate` | Two requests changed the same row at once, such as two `/auth/refresh` calls with the same token. One succeeds; retry the other with the newest token | `detail` |
| `415` | `Request.UnsupportedMediaType` | The body is not sent as `application/json` | `detail` |
| `422` | `Validation.Failed` | The body was read but a value broke a rule, such as an empty string or a weak password | `errors` keyed by the JSON field name (`email`, `firstName`), as the client sent it |
| `429` | `RateLimit.Exceeded` | An `/auth/*` endpoint was called more than the limit allows from one client address. The `Retry-After` header says how many seconds to wait | `detail` |
| `500` | `Server.Unexpected` | Anything else | `detail` |

A missing property is `400` while a present but empty one is `422`. This split comes from how ASP.NET Core model binding treats non-nullable properties as required, not from a rule in the HTTP standards. Many APIs use a single status for both.

**Framework responses.** A route that does not exist, a wrong method, a body that is not JSON, and a bare `401` or `403` all answer in the same shape, because a status-code handler gives them a body. The `code` is `Request.NotFound`, `Request.MethodNotAllowed`, `Request.UnsupportedMediaType`, `Auth.Forbidden`, or `Request.Rejected` for any other `4xx`. Every error is `application/problem+json` (RFC 9457) with `code` and `traceId` as extension members.

**Refresh tokens.** The JWT lasts 60 minutes (`JwtSettings:ExpiryMinutes`). Register and login also return a `refreshToken`, valid for 30 days (`JwtSettings:RefreshTokenExpiryDays`). Send it to `POST /auth/refresh` as `{ "refreshToken": "..." }` for a new pair; every refresh replaces the token, so always keep the newest one. If a token that was already replaced is presented again, the API assumes it was copied and revokes every token from that login, so the user must log in again. Each login is its own session, and `POST /auth/logout` ends one without touching the others. The database stores only a hash of each token. Keep the token where page script cannot leak it, and make one refresh call at a time: a second call with the same token at the same moment gets `409`.

**Rate limit.** `/auth/register`, `/auth/login`, `/auth/refresh` and `/auth/logout` share one budget per client address, 10 requests per 60 seconds by default, because each attempt costs a BCrypt hash. Change it with `RateLimiting:Auth:PermitLimit` and `RateLimiting:Auth:WindowSeconds`. The address is the socket's, so behind a reverse proxy every client looks like the proxy until forwarded headers are configured.

**CORS.** A browser frontend may call the API from the origins in `Cors:AllowedOrigins`, which defaults to `http://localhost:3000`. Any method and header is allowed, and `Retry-After` is exposed so the frontend can back off after a `429`. Cookies are not allowed, since authentication is a bearer token. Another environment sets its own list with `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1` and so on. Each entry is scheme, host and optional port with no trailing slash (`https://app.example.com`); anything else stops the host at startup.

The OpenAPI document is served at `/openapi/v1.json` when the app runs in the Development environment.

What is done and what is still open is tracked in [`TODO.md`](TODO.md).

## Observability

**Logs.** The console writes one JSON object per line (plain text in the Development environment). Every request produces one line after it is answered: `POST /auth/login responded 401 in 83.412 ms`, with `Method`, `Route`, `StatusCode` and `ElapsedMs` as fields, and `UserId` when the caller is signed in. `Route` is the route pattern (`/products/{id}`), never the path, and a request that matched no route is `unmatched`, so an id, a token or a scanner's probe does not reach the log. Headers, query strings and bodies are never logged. Passing `/health/*` checks are logged at `Debug`. Each line's `Scopes` carry the `TraceId` and `SpanId`.

**Authentication events.** These have fixed ids, so a query or an alert can name one. They carry ids, an outcome and a reason, never a password, a token or an email address; an email appears only as a 16-character fingerprint, so repeated attempts against one account can be seen.

| Id | Level | Event |
|---|---|---|
| 1001 | Information | A user registered |
| 1002 | Information | A registration was refused because the email is taken |
| 1003 | Information | A login succeeded |
| 1004 | Warning | A login failed (`unknown_email` or `wrong_password`; the response never says which) |
| 1005 | Information | A refresh token was exchanged |
| 1006 | Information | A refresh was refused (`unknown_token`, `expired_token` or `user_missing`) |
| 1007 | Warning | A refresh token that had already been replaced was presented again, and its family was revoked |
| 1008 | Information | A logout |
| 1009 | Warning | The rate limiter rejected a request, with the path and the client address |

**Correlating an error with its logs.** An error response carries `traceId` as a W3C trace context, `00-<32 hex>-<16 hex>-01`. The `TraceId` in the logs and in the dashboard is the 32-character middle part. Search for that.

**Metrics.** Besides the framework's request and runtime metrics, the `GrocerEase` meter has:

| Metric | Tags | Meaning |
|---|---|---|
| `grocerease.auth.attempts` | `operation` (register, login, refresh, logout), `outcome` (success, failure) | Calls to the four endpoints. Failure is not split by reason, so it cannot be used to tell which emails exist |
| `grocerease.auth.refresh_token_reuse` | none | Refresh tokens presented again after being replaced |
| `grocerease.auth.ratelimit_rejections` | none | Requests turned away by the rate limiter |
| `grocerease.password.hash.duration` | `operation` (hash, verify) | Seconds BCrypt took |

**Export.** Traces, metrics and logs are sent over OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (for example `http://localhost:4317`); otherwise nothing leaves the process. The service is named `grocerease-api`. `/health/*` is not traced. A collector that is down or unreachable does not affect requests. Running it is described under [Running it](#running-it).

## Project layout

The solution is `GrocerEase.slnx`, on .NET 10, and follows Clean Architecture. Package versions live in `Directory.Packages.props`; the ones that ship with ASP.NET Core share one `AspNetCoreVersion`, so bump that one number to update them together.

| Project | Role |
|---|---|
| `GrocerEase.Domain` | `User`, the `Email` value object, and the domain exceptions. Knows nothing about HTTP or the database |
| `GrocerEase.Application` | One handler per use case (`RegisterCommandHandler`, `LoginCommandHandler`), commands, validators, and the interfaces the other layers implement. A validating decorator runs a command's validator before its handler |
| `GrocerEase.Infrastructure` | EF Core / Npgsql, repositories, BCrypt hashing, JWT generation |
| `GrocerEase.Contracts` | HTTP request and response shapes |
| `GrocerEase.Api` | Controllers, the JWT bearer scheme, error handling (one `ApiProblem` writes every error), rate limiting, health endpoints, request logging and OpenTelemetry setup, startup, OpenAPI |
| `GrocerEase.UnitTests` | Services, validators, tokens, controllers, and middleware. No database and no running host |
| `GrocerEase.IntegrationTests` | Register, login, and profile against the real host and a Postgres 16 container |

## Running it

### Locally

You need the .NET 10 SDK and a PostgreSQL instance. Set the connection string in `GrocerEase.Api/appsettings.Development.json`, or override it with the `ConnectionStrings__DefaultConnection` environment variable.

The migrations create the `citext` extension (a case-insensitive text type for the email column). A database owner can do that on PostgreSQL 13 and later; on an older server, or with a more restricted role, run `CREATE EXTENSION citext;` once first.

The JWT signing secret is not in the repository. The host will not start without it. Store it as a user secret:

```bash
dotnet user-secrets set "JwtSettings:Secret" "<at least 32 characters>" --project GrocerEase.Api
dotnet run --project GrocerEase.Api --launch-profile https
```

The launch profiles set `Database__MigrateOnStartup=true`, so the tables are created or updated when the host starts. Outside those profiles the setting defaults to off; apply migrations yourself with `dotnet ef database update --project GrocerEase.Infrastructure --startup-project GrocerEase.Api`.

`GrocerEase.Api/auth.http` has sample requests.

To see traces, metrics and logs while running locally, start the dashboard and point the API at it:

```bash
docker run --rm -p 127.0.0.1:18888:18888 -p 127.0.0.1:4317:18889 \
  -e DASHBOARD__FRONTEND__AUTHMODE=Unsecured -e DASHBOARD__OTLP__AUTHMODE=Unsecured \
  mcr.microsoft.com/dotnet/aspire-dashboard:13.6.2
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run --project GrocerEase.Api --launch-profile https
```

Open `http://localhost:18888`. Without `OTEL_EXPORTER_OTLP_ENDPOINT` the API exports nothing.

### With Docker

```bash
cp .env.example .env     # then fill in JWT_SECRET, POSTGRES_PASSWORD and APP_DB_PASSWORD
docker compose up --build
```

The API is at `http://localhost:5000` and Postgres at `localhost:5432`. Postgres is published on the loopback interface only, so other machines on your network cannot reach it. Compose sets `Database__MigrateOnStartup=true`, so the API creates the tables on first start, and runs it in the `Production` environment as a non-root user, so the OpenAPI document is not served; run the API locally for that. `.env` is gitignored, and compose refuses to start if a value is missing.

The `grocerease.telemetry` service is the .NET Aspire Dashboard. Compose points the API at it, and the dashboard is at `http://localhost:18888`, published on the loopback interface only because it runs without a login. It keeps what it receives in memory, so restarting it empties it, and the API does not wait for it: with the dashboard stopped, requests are unaffected.

The API does not connect as the `postgres` superuser. On every `up`, the one-shot `grocerease.db-init` service runs `docker/postgres/init-app-role.sql`, which creates the `grocerease_app` role (no superuser rights), gives it `APP_DB_PASSWORD`, and makes it the owner of `GrocerEaseDb`, including tables an older volume already holds. The API starts only after that succeeds. Use `postgres` with `POSTGRES_PASSWORD` for administration from a database tool.

## Tests

```bash
dotnet test                                                         # both projects
dotnet test --filter "FullyQualifiedName!~IntegrationTests"         # unit tests only
```

The integration tests need Docker running, because they start a Postgres container.

`GrocerEase.UnitTests/Architecture` holds the architecture tests: the layer dependency rule (Domain depends on nothing, Application only on Domain, Api never touches EF Core) and a few conventions (sealed handlers, no public setters on entities). If one fails, a layer reached somewhere it should not; read its message before changing the test.

## CI/CD

`.github/workflows/ci.yml` runs on every pull request and every push to `main`: restore, build in Release, the unit tests, then the integration tests (the runner's Docker daemon hosts the Postgres container). Test results are attached to the run as `.trx` files.

`.github/workflows/image.yml` builds the `Dockerfile`, scans the result with Trivy, and fails on a fixable `HIGH` or `CRITICAL` finding. Findings that are waived, with a reason and an end date, are in `.trivyignore.yaml`. A push to `main` also publishes `ghcr.io/<owner>/grocerease-api` tagged with the commit SHA and `main`. Nothing deploys the image yet.

Base images and workflow actions are pinned by digest or commit SHA; Dependabot opens the pull requests that move them.

## Notes

The learning notes for this project are not in the repository. They live in an Obsidian vault on the author's machine:

```
~/Documents/Guides to Great SW/Better NCC DEV/Applications/Grocery Project
```

They cover each step in order, from project setup and Docker through auth, error handling, tests, Unit of Work, and user secrets. The folder also holds:

- `Software Design Description.md` (also as `.pdf` and `.tex`), the full design document
- `Notes Review.md`, a review of the notes against the code, with open items
