# GrocerEase — TODO

---

## Code Quality Assessment

### Current Rating: 8.0 / 10

The architecture foundation is genuinely good. Most projects at this stage are a single `Program.cs` with inline SQL. The patterns here — clean layering, typed exceptions, command objects, global error handling, value converters — are things most teams only add after getting burned by not having them.

### Rating Progression

| State | Rating | Description |
|---|---|---|
| Right now | **8.0** | Auth is tested, and the service commits through `IUnitOfWork`. Tokens refresh with rotation |
| TODO list done | **8.5** | Production-ready auth service |
| + Observability + more features | **9.0** | Proven at scale |
| + DDD domain model + CI/CD | **9.5** | Senior-level codebase |

> 10 / 10 doesn't exist — every codebase has tradeoffs. 9.5 is the practical ceiling for a real project.

### What Fixing the TODO Gets You

| Fix | Why it matters |
|---|---|
| Async throughout | Done — register and login await the database instead of blocking a thread |
| Tests | Done — `GrocerEase.UnitTests` checks services, validators, tokens, and error mapping with no database; `GrocerEase.IntegrationTests` runs register, login, and profile against the real host and Postgres |
| Unit of Work | Done — `AddAsync` only tracks the user. `RegisterCommandHandler` calls `IUnitOfWork.SaveChangesAsync`, so one use case can commit several repositories together |
| JWT auth middleware | Done — `GET /profile` is `[Authorize]`-only; other routes are still public |
| Options validation at startup | Done — missing or short `JwtSettings`, and a BCrypt cost outside 4–31, stop the host before it listens |
| CORS | Done — `Cors:AllowedOrigins` lists the origins that may call the API from a browser; `http://localhost:3000` is the default. HTTP is also redirected to HTTPS when the host knows an HTTPS port |

### The Ceiling Beyond the TODO

These won't appear on a TODO list but separate very good from excellent:

- **Nothing watches the telemetry** — logs, audit events, metrics and traces now exist, and the Aspire Dashboard shows them in development. There are still no alerts, SLOs or runbooks, and no production backend: the dashboard keeps everything in memory.
- **Only one feature** — Architecture quality is only proven under stress. One entity, one service, two endpoints is too small to know if the patterns hold. The moment you add `Products`, `Orders`, `Categories`, the patterns either prove themselves or show cracks.
- **No domain model** — `User` is an anemic data bag. The real complexity of a grocery app is in `Order`, `Cart`, `Product` — that's where DDD either pays off or becomes over-engineering.
- **There is CI but no CD** — `.github/workflows` runs the tests and publishes a scanned image (both green on GitHub since commit `84f783b`), but no environment deploys the image.

---

## 🔴 Critical

_None right now._

---

## 🟡 Important

_None right now._

---

## 🟢 Minor

- [ ] **Delete expired and revoked refresh tokens** — rows stay in `RefreshTokens` forever. A periodic job could delete rows past `ExpiresAt`, keeping revoked ones for a while so reuse is still detected.

- [ ] **Absolute session lifetime** — each refresh extends a session by another 30 days, so a session that is used often never ends. A `SessionExpiresAt` on the family would cap it.

## 🚀 Production readiness

Assumes the planned features are complete. Found by checking the repo on 2026-11-03; **nothing here is implemented**. The order is a suggested one: **A** before any real traffic, **B** before a public launch or scaling out, **C** later. Items already tracked elsewhere in this file are listed at the end of the section, not repeated.

### A. Before any real traffic

**CI/CD**

- [ ] **Make the pipeline checks required** — `CI / build-and-test` and `Image / build-scan-push` passed on GitHub for `84f783b`, but without branch protection on `main` a red run does not block a merge. This is a repository setting, not a file.

- [ ] **The image has no deploy step** — `image.yml` publishes `ghcr.io/<owner>/grocerease-api:<sha>` and `:main`, and nothing runs it yet. Also: no signing or provenance (cosign, SBOM), and no `.trivyignore.yaml` review reminder beyond its 2026-12-06 expiry.

- [ ] **A deploy target and environments** — compose is the only runtime. Choose a platform, then add staging and production with approvals, infrastructure as code, per-environment configuration, a secrets manager (today: `.env` and user secrets), TLS termination, and a rollback plan.

- [ ] **Static analysis and build hygiene** — there is no `.editorconfig`, `global.json`, `Directory.Build.props`, analyzer set or warnings-as-errors. Pin the SDK (`ci.yml` installs `10.0.x`), enable analyzers, add CodeQL, and publish coverage (`coverlet.collector` is referenced but never run).

- [ ] **Dependency and secret scanning** — a NuGet scan on 2026-11-03 found no vulnerable or deprecated packages, but that was a one-off. Dependabot now watches the Dockerfile and the workflow actions, but not NuGet. Add the `nuget` ecosystem, `dotnet list package --vulnerable` in CI, and secret scanning.

**Observability**

- [ ] **Alerts, SLOs and runbooks** — alert on the 5xx rate, p95 latency, the failed-login ratio (`grocerease.auth.attempts`) and `/health/ready`, and write a runbook for each alert. The metrics exist; nothing reads them.

- [ ] **A production telemetry backend** — the dashboard in `docker-compose.yml` is unsecured, in memory, and bound to loopback. A real deployment needs a collector or hosted backend with retention, an OTLP API key and TLS (`OTEL_EXPORTER_OTLP_HEADERS`), and a sampling decision for traces.

- [ ] **The response `traceId` and the log `TraceId` differ in form** — error responses carry the full W3C string (`00-<32 hex>-<16 hex>-01`), logs carry the 32-hex part. Returning only the trace id would make them identical but changes the documented error contract.

- [ ] **One reuse incident can log twice** — after a family is revoked, the owner's next attempt with their own (now dead) token logs event 1007 again, with `0 token(s) revoked`. A separate event, or a lower level when nothing was live, would stop it paging twice.

- [ ] **Startup logs an Error on an empty database** — EF logs the failed read of `__EFMigrationsHistory` at Error before `MigrateOnStartup` creates it. It is expected, but it looks like a fault in the first log lines of a new volume.

**Transport and configuration**

- [ ] **HSTS, and stop serving plain HTTP** — there is no `UseHsts`, and `UseHttpsRedirection` alone does not protect the first request (note 24). Pair this with forwarded headers (tracked below).

- [ ] **`AllowedHosts`** — it is `"*"` in `appsettings.json`. Restrict it to the real hostnames.

- [ ] **Request body size limit** — none is set, so Kestrel's default (about 30 MB) applies to JSON endpoints that need a few hundred bytes.

**Data**

- [ ] **Backups and a tested restore** — the only store is a Docker volume. Use managed Postgres with point-in-time recovery, and rehearse a restore before relying on it.

- [ ] **Database TLS and connection resilience** — the connection string has no `SSL Mode`, and `UseNpgsql` is configured with no retry (`EnableRetryOnFailure`), pool size or timeouts. Use `SSL Mode=VerifyFull` in production, and add retries with care for writes that are not idempotent.

### B. Before a public launch or scaling out

- [ ] **Account lifecycle** — password reset, email verification, per-account lockout (the rate limit is per address, not per email), and a decision on MFA.

- [ ] **Token lifecycle** — refresh tokens are done and can be revoked. Access tokens cannot: one stays valid until it expires, even after logout. `ClockSkew` is not set, so a token lives 65 minutes, not 60. Tokens are HS256 with one shared secret, so there is no `kid` or key rotation. Consider asymmetric signing if another service validates them.

- [ ] **Shared rate limiter** — it is in memory per instance, so two replicas allow twice the limit. Use a shared store, or enforce the limit at the gateway.

- [ ] **API versioning** — routes are `/auth/register`, not `/v1/auth/register`. This is cheapest to add before any client exists.

- [ ] **Security headers, and `Cache-Control: no-store` on auth responses** — none are set.

- [ ] **Container limits** — compose sets no memory or CPU limits and no read-only root filesystem.

- [ ] **Load and soak testing** — BCrypt at cost 11 is CPU-heavy per login and has never been measured under load. Size the replicas and the rate limit from the numbers.

- [ ] **Graceful shutdown and probes** — check the host's shutdown timeout against the orchestrator's grace period, and add a startup probe if migrations ever run at boot.

- [ ] **Publish the API documentation** — the OpenAPI document is served only in Development.

- [ ] **Version stamping** — nothing exposes which build or commit is running.

### C. Later

- [ ] **Caching and pagination** — once list endpoints exist.

- [ ] **A WAF or CDN** in front of the API.

- [ ] **Privacy tooling** — names and emails are personal data: retention, deletion, export, and a rule for what may appear in logs.

- [ ] **Resilience for outbound calls** — when email or payments arrive: timeouts, retries, and idempotency keys.

- [ ] **Security and contract testing** — a DAST scan of a deployed instance, and contract tests against the OpenAPI document.

### Already tracked elsewhere in this file

These production items are open at the end of the file, under Done, and are not repeated here: forwarded headers for the rate limiter, registration revealing which emails exist, migrations as a pipeline step, separating the migration role from the runtime role, authorization (roles and policies), and the JSON naming policy in `ErrorKeys`.

---

## Done ✅

- [x] Clean Architecture layering (Domain → Application → Infrastructure, Api)
- [x] Separate Contracts layer for HTTP shapes
- [x] DI extension methods per layer
- [x] Repository pattern with interface in Application
- [x] Unique index on `Users.Email` with migration
- [x] Case-insensitive email via EF Core value converter + `ToLowerInvariant()`
- [x] `IAppException` interface — middleware handles any typed domain exception automatically
- [x] `InvalidCredentialsException` → 401 Unauthorized
- [x] Global `ErrorHandlingMiddleware` with `ProblemDetails` + `traceId`
- [x] `application/problem+json` content type on all error responses
- [x] FluentValidation on `RegisterCommand` and `LoginCommand` → 422 with field-level errors
- [x] Command parameter objects (`RegisterCommand`, `LoginCommand`)
- [x] `AuthenticationResult.From()` static factory to avoid long positional constructors
- [x] OpenAPI wired up (`AddOpenApi`, `MapOpenApi`, `[ProducesResponseType]` on controller)
- [x] Password hashing with BCrypt
- [x] JWT token generation
- [x] JWT bearer middleware (`AddJwtBearer`, `UseAuthentication`, `UseAuthorization`)
- [x] `JwtSettings` validated at startup (`ValidateDataAnnotations` + `ValidateOnStart`)
- [x] Password strength on `RegisterCommand` — uppercase, digit, and special character
- [x] `IDateTimeProvider` — injected into `JwtTokenGenerator` and used for token expiry; the only `DateTime.UtcNow` call is inside `DateTimeProvider`
- [x] Async register and login — `SingleOrDefaultAsync` / `SaveChangesAsync`, with `CancellationToken` from the request
- [x] `GET /profile` — returns the caller's id and email, `[Authorize]` only
- [x] `422` validation errors documented in the OpenAPI spec
- [x] Unit tests in `GrocerEase.UnitTests` — validators, the command handlers, `AuthenticationResult.From()`, password hasher, JWT generator, auth and profile controllers, error middleware, and OpenAPI examples. No database and no running host
- [x] Integration tests in `GrocerEase.IntegrationTests` — `POST /auth/register`, `POST /auth/login`, and `GET /profile` through `WebApplicationFactory` and a Postgres 16 container, including duplicate email (`409`), weak passwords (`422`), invalid credentials, and bearer tokens. `Program` is public so the test host can start the app
- [x] Duplicate-email pre-check — `RegisterAsync` looks up the lowercased email and throws `DuplicateEmailException` before hashing or inserting. The response is still `409` / `User.DuplicateEmail` / "Duplicate Email". The unique index remains for two requests that pass the check at the same time
- [x] `IUnitOfWork` — `UserRepository.AddAsync` no longer calls `SaveChangesAsync`. `RegisterCommandHandler` commits after the add. `UnitOfWork` and the repository share the scoped `AppDbContext`
- [x] BCrypt cost — `PasswordHashing:Cost` defaults to 11 and is validated at startup (4–31). New hashes use that cost. `Verify` reads the cost stored in the hash, so existing passwords still match after the setting is raised
- [x] `app.UseHttpsRedirection()` — HTTP requests redirect to the HTTPS port with `307` when the host is listening on HTTPS (`dotnet run --launch-profile https`). The `http` launch profile and the integration-test host have no HTTPS port, so those requests stay on HTTP
- [x] JWT signing secret removed from `appsettings.json` — a deployment that forgets `JwtSettings__Secret` now fails at startup. locally the value comes from `dotnet user-secrets` (`UserSecretsId` on `GrocerEase.Api`; set it with `dotnet user-secrets set "JwtSettings:Secret" "<value>" --project GrocerEase.Api`), and `docker-compose.yml` supplies a development value
- [x] Password capped at 72 bytes — BCrypt ignores everything after byte 72, so `RegisterCommandValidator` rejects longer passwords with `Password must be at most 72 bytes in UTF-8; accented letters and emoji use more than one byte each.` (login rejects them too, through the shared `PasswordRules`)
- [x] Expected failures (validation, typed domain errors below 500) log at `Information` without a stack trace; unexpected exceptions still log at `Error`
- [x] Client disconnects (`RequestAborted`) return `499` quietly instead of a `500` and an error log
- [x] `IAppException.Title` — an optional title override, so `DuplicateEmailException` no longer needs its own middleware arm
- [x] `docker-compose.yml` secrets moved to a gitignored `.env` — `JWT_SECRET` and `POSTGRES_PASSWORD` are required (`${VAR:?message}`), `.env.example` is committed as the template, and `.dockerignore` keeps `.env` out of the image build context. Copy `.env.example` to `.env` before `docker compose up`
- [x] Postgres published on loopback only — `docker-compose.yml` maps `127.0.0.1:5432:5432`, so the database that holds a superuser login is no longer reachable from other machines. The API still reaches it over the compose network, and tools on the host still connect to `localhost:5432`
- [x] The API connects as a non-superuser role — `grocerease.db-init` (a one-shot compose service running `docker/postgres/init-app-role.sql`) creates `grocerease_app` on every `docker compose up`, sets its password from `APP_DB_PASSWORD`, and makes it the owner of `GrocerEaseDb` and its tables. The role can create the trusted `citext` extension as the database owner, so startup migrations still work. The `postgres` superuser stays for setup and administration; add `APP_DB_PASSWORD` to an existing `.env`
- [x] The plaintext password no longer appears in `ToString()` — `RegisterCommand`, `LoginCommand`, `RegisterRequest` and `LoginRequest` are records, and a record prints every property, so logging or interpolating one would have written the caller's password out. Each overrides `PrintMembers` and prints `Password = [redacted]`; the other fields still print. `PasswordRedactionTests` checks the four types and also finds, by reflection, any type in Application or Contracts with a password-like property, so a future `ChangePasswordCommand` is covered. Nothing logged one before; this closes the latent path
- [x] CORS policy for the browser frontend — `Cors:AllowedOrigins` (default `http://localhost:3000` in `appsettings.json`) feeds a `frontend` policy that allows any method and header and exposes `Retry-After`; credentials stay off because auth is a bearer token. `UseCors` runs before the rate limiter and auth, so a preflight needs no token and 401/429 responses stay readable by the frontend. A malformed origin (trailing slash, no scheme, a path) stops the host at startup. `CorsTests` covers it; other environments set `Cors__AllowedOrigins__0`
- [x] The bearer token no longer appears in `ToString()` — `AuthenticationResult` and `AuthenticationResponse` override `PrintMembers` and print `Token = [redacted]`. `PasswordRedactionTests` now finds, by reflection, any type in Application or Contracts with a password-like or token-like string property, so the refresh token is covered as soon as it exists
- [x] Refresh tokens with rotation and reuse detection — register and login return a `refreshToken` beside the JWT. `POST /auth/refresh` revokes it and returns a new pair; presenting a revoked token again revokes the whole family and returns `401 Auth.InvalidRefreshToken`. `POST /auth/logout` revokes the family and always returns `204`. Tokens are 256 random bits and only their SHA-256 hash is stored (`RefreshTokens`, unique on the hash, `xmin` as the row version so two simultaneous refreshes cannot both win: the loser gets `409 Database.ConcurrentUpdate`). Lifetime is `JwtSettings:RefreshTokenExpiryDays` (30). The token is redacted in `ToString()` everywhere it appears. Delivery is the JSON body, so CORS is unchanged
- [x] One structured log line per request — `RequestLoggingMiddleware` is first in the pipeline and logs method, route pattern, status, elapsed time and the signed-in user id after the response. It never reads headers, query strings or bodies; an unmatched route is logged as `unmatched`; passing `/health/*` is `Debug`. The console is JSON outside Development, with the trace and span id on each line, and EF's SQL command log stays at `Warning`
- [x] Authentication audit events — fixed ids 1001–1009 for register, login (success and failure), refresh, reuse, logout and rate-limit rejection, written through source-generated `[LoggerMessage]` methods. They carry ids, outcomes and reasons, never a password, token or email (an email is a 16-character SHA-256 fingerprint). An integration test runs the whole flow and fails if any secret appears in anything logged
- [x] OpenTelemetry traces, metrics and logs — ASP.NET Core, runtime and Npgsql instrumentation under the service name `grocerease-api`, exported over OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. `/health/*` is not traced, an unreachable collector never affects a request, and Infrastructure registers the Npgsql part so the Api still never names a database type
- [x] Application metrics — the `GrocerEase` meter counts register, login, refresh and logout by operation and outcome (never by reason), refresh-token reuse and rate-limit rejections, and times BCrypt hashing and checking in seconds
- [x] A telemetry dashboard in compose — `grocerease.telemetry` runs the .NET Aspire Dashboard (pinned `13.6.2`), published on `127.0.0.1:18888` only because it runs unsecured; its OTLP port is not published and the API reaches it over the compose network without waiting for it
- [x] Test projects added to `GrocerEase.slnx` — `GrocerEase.UnitTests` and `GrocerEase.IntegrationTests` now belong to the solution, so `dotnet test` from the root and IDE test discovery run both. The integration tests need Docker for the Postgres container; skip them with `dotnet test --filter "FullyQualifiedName!~IntegrationTests"`

- [x] CI pipeline — `.github/workflows/ci.yml` restores, builds in Release, and runs the unit tests and the Docker-backed integration tests on every pull request and push to `main`. Read-only token, actions pinned by commit SHA, newer runs cancel older ones, `.trx` results uploaded
- [x] Container image pipeline — `.github/workflows/image.yml` builds the image on pull requests that touch code or the Dockerfile, scans it with Trivy (fails on fixable HIGH/CRITICAL), and on `main` pushes it to GHCR tagged by commit and `main`. The base images are pinned by digest, Dependabot proposes new digests and action versions, and the image carries its own `HEALTHCHECK`. One apt finding in Microsoft's base is waived in `.trivyignore.yaml` until 2026-12-06

- [ ] **Forwarded headers for the rate limiter** — `/auth/*` is limited per socket address. Behind a proxy every client shares one budget until `UseForwardedHeaders` is configured with the proxy's address.

- [ ] **Registration still reveals which emails exist** — `409 User.DuplicateEmail`. Login no longer leaks it through timing. Fixing register needs an email-confirmation flow.

- [ ] **Apply migrations as a pipeline step** — `Database:MigrateOnStartup` covers local runs and compose. A production database needs a deliberate step.

- [ ] **Read the JSON naming policy in `ErrorKeys`** — error keys are camel-cased by a fixed rule. If the JSON options ever change, the keys must follow them.

- [ ] **Authorization** — there are no roles or policies, so nothing can be marked admin-only, and a `403` has no producer yet. Needed before the inventory dashboard.

- [ ] **Separate the migration role from the runtime role** — the API now connects as `grocerease_app`, which is not a superuser, but that role owns the database and both runs and migrates, so a leaked credential can still drop tables. A runtime role limited to `SELECT`/`INSERT`/`UPDATE`/`DELETE`, plus a separate role that migrates, needs a second connection string for `ApplyMigrationsAsync`.
