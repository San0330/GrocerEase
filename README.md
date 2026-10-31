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

`GrocerEase.Api/auth.http` has sample requests.

### With Docker

```bash
cp .env.example .env     # then fill in JWT_SECRET and POSTGRES_PASSWORD
docker compose up --build
```

The API is at `http://localhost:5000` and Postgres at `localhost:5432`. `.env` is gitignored, and compose refuses to start if either value is missing.

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
