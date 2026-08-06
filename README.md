# Unidemix Backend

Standalone ASP.NET Core 8 Web API for the Unidemix language-learning frontend.

## Run with Docker

```bash
docker compose up --build
```

Create a local `.env` first (it is ignored by Git):

```env
POSTGRES_PASSWORD=your-strong-password
```

- API: http://localhost:5001
- Swagger: http://localhost:5001/swagger
- Health: http://localhost:5001/health
- Allowed frontend origin: http://localhost:3002

Database migrations and demo seed data are applied automatically at startup.

## Demo account

- Email: `demo@unidemix.local`
- Password: `Demo123!`

Development seeding also creates 15 additional test accounts (for example
`ali@unidemix.local`, `mina@unidemix.local`, and `admin@unidemix.local`) with
the same test password. The idempotent catalog contains 6 courses, 48 lessons,
181 exercises, and varied progress history for realistic UI development.

## Main endpoints

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET|PUT /api/profile`
- `GET /api/courses`
- `GET /api/courses/{courseId}/lessons`
- `GET /api/courses/lessons/{lessonId}`
- `PUT /api/progress/lessons/{lessonId}`
- `GET /api/progress/summary`

Use the access token returned by login as `Authorization: Bearer <token>`.

## Local development and tests

```bash
dotnet restore
dotnet test
dotnet run --project src/Unidemix.Api --urls http://localhost:5001
```

For local execution outside Docker, start PostgreSQL with `docker compose up postgres -d` first. Production deployments must override `Jwt__Key`, database credentials, and `FrontendUrl`.
