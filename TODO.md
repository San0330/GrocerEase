# GrocerEase — TODO

---

## Code Quality Assessment

### Current Rating: 8.0 / 10

The architecture foundation is genuinely good. Most projects at this stage are a single `Program.cs` with inline SQL. The patterns here — clean layering, typed exceptions, command objects, global error handling, value converters — are things most teams only add after getting burned by not having them.

### Rating Progression

| State | Rating | Description |
|---|---|---|
| Right now | **8.0** | Auth is tested, and the service commits through `IUnitOfWork`. CORS and token refresh remain |
| TODO list done | **8.5** | Production-ready auth service |
| + Observability + more features | **9.0** | Proven at scale |
| + DDD domain model + CI/CD | **9.5** | Senior-level codebase |

> 10 / 10 doesn't exist — every codebase has tradeoffs. 9.5 is the practical ceiling for a real project.

### What Fixing the TODO Gets You

| Fix | Why it matters |
|---|---|
| Async throughout | Done — register and login await the database instead of blocking a thread |
| Tests | Done — `GrocerEase.UnitTests` checks services, validators, tokens, and error mapping with no database; `GrocerEase.IntegrationTests` runs register, login, and profile against the real host and Postgres |
| Unit of Work | Done — `AddAsync` only tracks the user. `AuthenticationService` calls `IUnitOfWork.SaveChangesAsync`, so one use case can commit several repositories together |
| JWT auth middleware | Done — `GET /profile` is `[Authorize]`-only; other routes are still public |
| Options validation at startup | Done — missing or short `JwtSettings`, and a BCrypt cost outside 4–31, stop the host before it listens |
| CORS | Deployment hygiene — HTTP is redirected to HTTPS when the host knows an HTTPS port. A browser frontend still needs a CORS policy |

### The Ceiling Beyond the TODO

These won't appear on a TODO list but separate very good from excellent:

- **Observability gap** — structured logging exists on exceptions but not on the happy path. No request/response logging, no timing metrics. In production you'd be blind.
- **Only one feature** — Architecture quality is only proven under stress. One entity, one service, two endpoints is too small to know if the patterns hold. The moment you add `Products`, `Orders`, `Categories`, the patterns either prove themselves or show cracks.
- **No domain model** — `User` is an anemic data bag. The real complexity of a grocery app is in `Order`, `Cart`, `Product` — that's where DDD either pays off or becomes over-engineering.
- **No CI/CD** — The test projects only run when someone executes `dotnet test` locally. Nothing runs them on push, so this TODO can still drift back to red.

---

## 🔴 Critical

_None right now._

---

## 🟡 Important

_None right now._

---

## 🟢 Minor

- [ ] **CORS policy** — No policy configured. Required as soon as a frontend is added.

- [ ] **Token refresh** — No refresh token mechanism. Expired JWTs require re-login with no silent renewal path.

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
- [x] Unit tests in `GrocerEase.UnitTests` — validators, `AuthenticationService`, `AuthenticationResult.From()`, password hasher, JWT generator, auth and profile controllers, error middleware, and OpenAPI examples. No database and no running host
- [x] Integration tests in `GrocerEase.IntegrationTests` — `POST /auth/register`, `POST /auth/login`, and `GET /profile` through `WebApplicationFactory` and a Postgres 16 container, including duplicate email (`409`), weak passwords (`422`), invalid credentials, and bearer tokens. `Program` is public so the test host can start the app
- [x] Duplicate-email pre-check — `RegisterAsync` looks up the lowercased email and throws `DuplicateEmailException` before hashing or inserting. The response is still `409` / `User.DuplicateEmail` / "Duplicate Email". The unique index remains for two requests that pass the check at the same time
- [x] `IUnitOfWork` — `UserRepository.AddAsync` no longer calls `SaveChangesAsync`. `AuthenticationService` commits after the add. `UnitOfWork` and the repository share the scoped `AppDbContext`
- [x] BCrypt cost — `PasswordHashing:Cost` defaults to 11 and is validated at startup (4–31). New hashes use that cost. `Verify` reads the cost stored in the hash, so existing passwords still match after the setting is raised
- [x] `app.UseHttpsRedirection()` — HTTP requests redirect to the HTTPS port with `307` when the host is listening on HTTPS (`dotnet run --launch-profile https`). The `http` launch profile and the integration-test host have no HTTPS port, so those requests stay on HTTP
- [x] JWT signing secret removed from `appsettings.json` — a deployment that forgets `JwtSettings__Secret` now fails at startup. locally the value comes from `dotnet user-secrets` (`UserSecretsId` on `GrocerEase.Api`; set it with `dotnet user-secrets set "JwtSettings:Secret" "<value>" --project GrocerEase.Api`), and `docker-compose.yml` supplies a development value
- [x] Password capped at 72 bytes — BCrypt ignores everything after byte 72, so `RegisterCommandValidator` rejects longer passwords with `Password must be at most 72 characters.`
- [x] Expected failures (validation, typed domain errors below 500) log at `Information` without a stack trace; unexpected exceptions still log at `Error`
- [x] Client disconnects (`RequestAborted`) return `499` quietly instead of a `500` and an error log
- [x] `IAppException.Title` — an optional title override, so `DuplicateEmailException` no longer needs its own middleware arm
- [x] `docker-compose.yml` secrets moved to a gitignored `.env` — `JWT_SECRET` and `POSTGRES_PASSWORD` are required (`${VAR:?message}`), `.env.example` is committed as the template, and `.dockerignore` keeps `.env` out of the image build context. Copy `.env.example` to `.env` before `docker compose up`
- [x] Test projects added to `GrocerEase.slnx` — `GrocerEase.UnitTests` and `GrocerEase.IntegrationTests` now belong to the solution, so `dotnet test` from the root and IDE test discovery run both. The integration tests need Docker for the Postgres container; skip them with `dotnet test --filter "FullyQualifiedName!~IntegrationTests"`
