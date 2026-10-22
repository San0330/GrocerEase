# GrocerEase — TODO

---

## Code Quality Assessment

### Current Rating: 6.5 / 10

The architecture foundation is genuinely good. Most projects at this stage are a single `Program.cs` with inline SQL. The patterns here — clean layering, typed exceptions, command objects, global error handling, value converters — are things most teams only add after getting burned by not having them.

### Rating Progression

| State | Rating | Description |
|---|---|---|
| Right now | **6.5** | Solid architecture, real gaps |
| TODO list done | **8.5** | Production-ready auth service |
| + Observability + more features | **9.0** | Proven at scale |
| + DDD domain model + CI/CD | **9.5** | Senior-level codebase |

> 10 / 10 doesn't exist — every codebase has tradeoffs. 9.5 is the practical ceiling for a real project.

### What Fixing the TODO Gets You

| Fix | Why it matters |
|---|---|
| Async throughout | Non-negotiable for a production web API — moves it from "prototype" to "production-ready" |
| Unit of Work + tests | Tests are the difference between code you trust and code you hope works |
| JWT auth middleware | Done — `GET /profile` is `[Authorize]`-only; other routes are still public |
| Options validation at startup | Done — missing or short `JwtSettings` stop the host before it listens |
| HTTPS, CORS | Deployment hygiene — registration passwords already require uppercase, a digit, and a special character |

### The Ceiling Beyond the TODO

These won't appear on a TODO list but separate very good from excellent:

- **Observability gap** — structured logging exists on exceptions but not on the happy path. No request/response logging, no timing metrics. In production you'd be blind.
- **Only one feature** — Architecture quality is only proven under stress. One entity, one service, two endpoints is too small to know if the patterns hold. The moment you add `Products`, `Orders`, `Categories`, the patterns either prove themselves or show cracks.
- **No domain model** — `User` is an anemic data bag. The real complexity of a grocery app is in `Order`, `Cart`, `Product` — that's where DDD either pays off or becomes over-engineering.
- **No CI/CD** — Code quality without automation degrades. No pipeline means this TODO can drift back to red.

---

## 🔴 Critical

- [ ] **Make everything `async`** — `IAuthenticationService`, `IUserRepository`, `AuthenticationService`, `UserRepository` all use synchronous DB calls (`SaveChanges`, `SingleOrDefault`). Blocking thread pool threads kills scalability. Convert to `async Task<T>` throughout.

- [ ] **Move `SaveChanges` out of the repository** — `UserRepository.Add()` calls `SaveChangesAsync()` internally, coupling transaction boundaries to individual repository operations. Introduce `IUnitOfWork` and let the service own the commit.

- [ ] **Add tests** — No unit or integration tests exist. The validators, service logic, `AuthenticationResult.From()`, and middleware are all untested.

---

## 🟡 Important

_None right now._

---

## 🟢 Minor

- [ ] **`app.UseHttpsRedirection()`** — Missing from `Program.cs`. Fine for local dev, required for production.

- [ ] **CORS policy** — No policy configured. Required as soon as a frontend is added.

- [ ] **Token refresh** — No refresh token mechanism. Expired JWTs require re-login with no silent renewal path.

- [ ] **Pre-check for duplicate email** — Currently relies purely on the DB unique constraint + exception catching. An application-level check before insert gives a faster, friendlier error.

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
- [x] `GET /profile` — returns the caller's id and email, `[Authorize]` only
- [x] `422` validation errors documented in the OpenAPI spec
