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
| `POST /auth/register` | public | Create a user and return a JWT. Duplicate email returns `409`, validation failures return `422` |
| `POST /auth/login` | public | Verify credentials and return a JWT. Wrong credentials return `401` |
| `GET /profile` | bearer token | Return the caller's id and email |

## Error responses

Errors are returned as `application/problem+json`. Clients should branch on `code`, not `title`.

| Status | `code` | When | Body |
|---|---|---|---|
| `400` | `Request.Malformed` | The body cannot be read or bound: broken JSON, `null`, an empty body, a wrong type, or a missing property. Also Kestrel rejecting the request while reading the body (`400`, `408`, `413`) | `errors` keyed by the JSON field name (`password`) or, for a body that cannot be parsed, a JSON path (`$.email`), except Kestrel's, which has `detail` |
| `400` | `Database.ConstraintViolation` | A database constraint other than the unique email failed (register) | `detail` |
| `401` | `Auth.Unauthorized` | A protected endpoint such as `/profile` was called without a bearer token, or with one that is expired, tampered with, or issued for another audience. `WWW-Authenticate` is `Bearer`, or `Bearer error="invalid_token"` when a token was sent | `detail` |
| `401` | `User.InvalidCredentials` | Wrong email or password | `detail` |
| `409` | `User.DuplicateEmail` | The email is already registered | `detail` |
| `422` | `Validation.Failed` | The body was read but a value broke a rule, such as an empty string or a weak password | `errors` keyed by the JSON field name (`email`, `firstName`), as the client sent it |
| `429` | `RateLimit.Exceeded` | `/auth/register` or `/auth/login` was called more than the limit allows from one client address. The `Retry-After` header says how many seconds to wait | `detail` |
| `500` | `Server.Unexpected` | Anything else | `detail` |

A missing property is `400` while a present but empty one is `422`. This split comes from how ASP.NET Core model binding treats non-nullable properties as required, not from a rule in the HTTP standards. Many APIs use a single status for both.

**Known exception: `415`.** A request whose `Content-Type` is not JSON gets `415 Unsupported Media Type` in ASP.NET Core's default shape (`type`, `title`, `status`, `traceId`) with `Content-Type: application/json`. It has no `code` field. This is deliberate for now: changing it means replacing MVC's problem-details factory, which also changes every other framework-generated `4xx`. It is tracked in [`TODO.md`](TODO.md).

**Rate limit.** `/auth/register` and `/auth/login` share one budget per client address, 10 requests per 60 seconds by default, because each attempt costs a BCrypt hash. Change it with `RateLimiting:Auth:PermitLimit` and `RateLimiting:Auth:WindowSeconds`. The address is the socket's, so behind a reverse proxy every client looks like the proxy until forwarded headers are configured.

The OpenAPI document is served at `/openapi/v1.json` when the app runs in the Development environment.

What is done and what is still open is tracked in [`TODO.md`](TODO.md).

## Project layout

The solution is `GrocerEase.slnx`, on .NET 10, and follows Clean Architecture.

| Project | Role |
|---|---|
| `GrocerEase.Domain` | Entities |
| `GrocerEase.Application` | Services, commands, validators, and the interfaces the other layers implement |
| `GrocerEase.Infrastructure` | EF Core / Npgsql, repositories, BCrypt hashing, JWT generation |
| `GrocerEase.Contracts` | HTTP request and response shapes |
| `GrocerEase.Api` | Controllers, error-handling middleware, startup, OpenAPI |
| `GrocerEase.UnitTests` | Services, validators, tokens, controllers, and middleware. No database and no running host |
| `GrocerEase.IntegrationTests` | Register, login, and profile against the real host and a Postgres 16 container |

## Running it

### Locally

You need the .NET 10 SDK and a PostgreSQL instance. Set the connection string in `GrocerEase.Api/appsettings.Development.json`, or override it with the `ConnectionStrings__DefaultConnection` environment variable.

The JWT signing secret is not in the repository. The host will not start without it. Store it as a user secret:

```bash
dotnet user-secrets set "JwtSettings:Secret" "<at least 32 characters>" --project GrocerEase.Api
dotnet run --project GrocerEase.Api --launch-profile https
```

The launch profiles set `Database__MigrateOnStartup=true`, so the tables are created or updated when the host starts. Outside those profiles the setting defaults to off; apply migrations yourself with `dotnet ef database update --project GrocerEase.Infrastructure --startup-project GrocerEase.Api`.

`GrocerEase.Api/auth.http` has sample requests.

### With Docker

```bash
cp .env.example .env     # then fill in JWT_SECRET and POSTGRES_PASSWORD
docker compose up --build
```

The API is at `http://localhost:5000` and Postgres at `localhost:5432`. Compose sets `Database__MigrateOnStartup=true`, so the API creates the tables on first start, and runs it in the `Production` environment as a non-root user, so the OpenAPI document is not served; run the API locally for that. `.env` is gitignored, and compose refuses to start if either value is missing.

## Tests

```bash
dotnet test                                                         # both projects
dotnet test --filter "FullyQualifiedName!~IntegrationTests"         # unit tests only
```

The integration tests need Docker running, because they start a Postgres container.

## Notes

The learning notes for this project are not in the repository. They live in an Obsidian vault on the author's machine:

```
~/Documents/Guides to Great SW/Better NCC DEV/Applications/Grocery Project
```

They cover each step in order, from project setup and Docker through auth, error handling, tests, Unit of Work, and user secrets. The folder also holds:

- `Software Design Description.md` (also as `.pdf` and `.tex`), the full design document
- `Notes Review.md`, a review of the notes against the code, with open items
