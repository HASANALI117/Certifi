# Training Platform

A training-centre management platform built on **.NET 9**. It manages the full
training lifecycle — course catalogue, scheduled sessions, enrolment, payment,
assessment, and certification — across three cooperating applications, with a
JWT-secured Web API, a JWT-consuming reporting client, real-time updates over
SignalR, and a public certificate-verification page.

> **Status:** Runs end-to-end in development against SQL Server LocalDB. Not yet
> deployed to Azure — see [Deployment](#deployment).

---

## Table of contents

- [Architecture](#architecture)
- [Tech stack](#tech-stack)
- [Projects](#projects)
- [Domain model](#domain-model)
- [Key features](#key-features)
- [Getting started](#getting-started)
- [Seeded test accounts](#seeded-test-accounts)
- [Web API](#web-api)
- [Real-time (SignalR)](#real-time-signalr)
- [Reporting application](#reporting-application)
- [Configuration & secrets](#configuration--secrets)
- [Deployment](#deployment)

---

## Architecture

```
                 ┌──────────────────────────┐
                 │  TrainingPlatform.MVC     │  Cookie auth (ASP.NET Identity)
   Browser ────► │  (primary web app)        │  EF Core ──► SQL Server
                 │  SignalR hub (real-time)  │
                 └─────────┬────────────────┘
                           │ HttpClient + JWT (cert verify; API login bridge)
                           ▼
                 ┌──────────────────────────┐
                 │  TrainingPlatform.API     │  JWT bearer auth
                 │  (REST + OpenAPI/Scalar)  │  EF Core ──► SQL Server
                 └─────────▲────────────────┘
                           │ HttpClient + JWT (read-only reporting)
                 ┌─────────┴────────────────┐
   Coordinator ►│ TrainingPlatform.Reports  │  Shared SSO cookie ──► JWT claim
                 │ (reporting client)        │  Chart.js dashboards
                 └──────────────────────────┘
```

- **MVC** is the main application. It uses cookie-based ASP.NET Identity and
  talks to the database directly via EF Core for its CRUD/workflow features. It
  consumes the API over HTTP for the two areas the API owns: public certificate
  verification, and exchanging the user's credentials for an API JWT at login.
- **API** owns JWT issuance, the public certificate-verification endpoint, and
  all reporting endpoints. It is stateless and bearer-token secured.
- **Reports** is a separate web client that holds **no `DbContext`** — it reads
  everything from the API over `HttpClient` using a JWT. Single sign-on is
  achieved by sharing the Identity auth cookie (same name + shared
  DataProtection key ring + application name); the MVC login stores the API JWT
  as a claim on that cookie, which Reports reads to call the API as the same
  user.

---

## Tech stack

| Area            | Technology                                             |
|-----------------|--------------------------------------------------------|
| Runtime         | .NET 9 (ASP.NET Core)                                   |
| Web UI          | ASP.NET Core MVC + Razor, Bootstrap 5, Bootstrap Icons |
| Auth            | ASP.NET Core Identity (cookie) + JWT bearer (API)      |
| Data            | Entity Framework Core, SQL Server                      |
| Real-time       | SignalR                                                |
| API docs        | OpenAPI + Scalar reference UI                           |
| Reporting charts| Chart.js                                               |
| Payments        | Stripe Checkout (test mode)                            |

---

## Projects

| Project                    | Type            | Default URLs (dev)                         |
|----------------------------|-----------------|--------------------------------------------|
| `TrainingPlatform.API`     | Web API         | `https://localhost:7181` / `http://…:5171` |
| `TrainingPlatform.MVC`     | MVC web app     | `https://localhost:7276` / `http://…:5054` |
| `TrainingPlatform.Reports` | MVC web app     | `https://localhost:7224` / `http://…:5093` |

The MVC and Reports projects reference the API project for the shared EF Core
data model (`Models` / `AppDbContext`).

---

## Domain model

16 entities, normalised to 3NF, configured in
[`AppDbContext`](TrainingPlatform.API/Data/AppDbContext.cs):

- **Identity / people:** `AppUser` (extends IdentityUser), with one-to-one
  `Trainee` and `Instructor` profiles; `InstructorAvailability`.
- **Catalogue:** `CourseCategory`, `Course` (self-referencing prerequisite),
  `Classroom`, `ClassroomEquipment`.
- **Scheduling & lifecycle:** `CourseSession`, `Enrollment`, `Assessment`,
  `Payment`, `Notification`.
- **Certification:** `CertificationTrack`, `CertificationTrackCourse`
  (junction with composite key), `TraineeCertification`.

Notable database-level rules enforced via Fluent API:

- Unique indexes prevent **instructor / classroom double-booking**
  (`{InstructorId, StartDateTime}`, `{ClassroomId, StartDateTime}`) and
  **duplicate enrolment** (`{TraineeId, CourseSessionId}`).
- `TraineePublicId` is unique (used by the public verification page).
- `decimal(10,2)` precision on monetary columns.
- Deliberate `DeleteBehavior.NoAction` on several FKs to avoid SQL Server's
  multiple-cascade-path restriction.

The database is created and seeded automatically on startup (migrations are
applied via `MigrateAsync`, then an **idempotent** seeder populates roles,
users, catalogue, and sample activity).

---

## Key features

- **Full enrolment lifecycle:** browse catalogue → enrol → pay (Stripe Checkout)
  → assessment → automatic certification tracking.
- **Automatic certification tracking** with promotion (InProgress → Eligible
  when all required courses are passed) and demotion (back to InProgress if a
  prior pass is corrected to a fail); already-issued certificates are never
  silently revoked.
- **Role-based access** for three roles — `TrainingCoordinator`, `Instructor`,
  `Trainee` — enforced at controller/action level and re-applied as query-level
  scoping (defence in depth).
- **Concurrency-safe enrolment:** the "claim the last seat" path runs inside a
  `Serializable` transaction, backed by the unique index.
- **Scheduling conflict validation** for instructor and classroom collisions.
- **Real-time updates** (live seat counts, per-user toast notifications,
  dashboard auto-refresh) via SignalR.
- **Public certificate verification** page that consumes the API over
  `HttpClient` (no login required).
- **Separate reporting application** with Chart.js dashboards, reading the API
  with a JWT.

---

## Getting started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- SQL Server **LocalDB** (ships with Visual Studio; or adjust the connection
  string for another SQL Server instance)

### Run

The database is created and seeded automatically on first run.

```bash
# from the repository root
dotnet build TrainingPlatform.slnx

# run each project (separate terminals), or use the Visual Studio
# multi-startup launch profile
dotnet run --project TrainingPlatform.API
dotnet run --project TrainingPlatform.MVC
dotnet run --project TrainingPlatform.Reports
```

For the full experience, start **all three**. The MVC app and the Reports app
share an auth cookie, so signing in once on MVC also authenticates Reports.

Then browse to the MVC app at `https://localhost:7276`.

---

## Seeded test accounts

All seeded accounts use the password **`Password1!`**.

| Role                | Email                      | Name            |
|---------------------|----------------------------|-----------------|
| Training Coordinator| `coordinator@platform.com` | Sarah Mitchell  |
| Instructor          | `instructor@platform.com`  | James Carter    |
| Instructor          | `maria@platform.com`       | Maria Rodriguez |
| Trainee             | `trainee@platform.com`     | Ali Hassan      |
| Trainee             | `layla@platform.com`       | Layla Khan      |
| Trainee             | `omar@platform.com`        | Omar Said       |

Coordinators land on the Reports overview after login; other roles land on the
dashboard. New public sign-ups are created as **Trainees**.

---

## Web API

Base URL (dev): `https://localhost:7181`. Interactive documentation
(OpenAPI + **Scalar**) is available at `/scalar/v1` when running in Development.

| Method | Route                                | Auth                  | Purpose                                  |
|--------|--------------------------------------|-----------------------|------------------------------------------|
| POST   | `/api/auth/login`                    | Public                | Exchange credentials for a JWT           |
| GET    | `/api/certifications/verify`         | Public                | Verify a certificate by trainee + ref    |
| GET    | `/api/reports/overview`              | `TrainingCoordinator` | Headline KPIs                            |
| GET    | `/api/reports/enrollments`           | `TrainingCoordinator` | Enrolment stats by course               |
| GET    | `/api/reports/instructors`           | `TrainingCoordinator` | Instructor workload                     |
| GET    | `/api/reports/sessions`              | `TrainingCoordinator` | Session fill rates                      |
| GET    | `/api/reports/certifications`        | `TrainingCoordinator` | Certification completion rates          |
| GET    | `/api/reports/revenue`               | `TrainingCoordinator` | Revenue by course + 12-month timeline   |
| GET    | `/api/reports/trainees`              | `TrainingCoordinator` | Per-trainee summary                     |
| GET    | `/api/reports/recent-enrollments`    | `TrainingCoordinator` | Latest enrolments                       |
| GET    | `/api/reports/upcoming-sessions`     | `TrainingCoordinator` | Next scheduled sessions                 |

**Error shape:** the API returns **RFC 7807 ProblemDetails**
(`application/problem+json`) for error responses, including `401` from both
failed login (invalid credentials) and missing/invalid bearer tokens.

---

## Real-time (SignalR)

The MVC app hosts an `EnrollmentHub` at `/hubs/enrollment`. The client
([`realtime.js`](TrainingPlatform.MVC/wwwroot/js/realtime.js)) maintains a
single auto-reconnecting connection and handles three event types:

- `EnrollmentUpdated` — live seat counts when an enrolment changes.
- `NotificationReceived` — per-user toast notifications (enrolment, payment,
  assessment, certification events).
- `DashboardRefreshRequested` — debounced soft-refresh of the current dashboard
  fragment.

---

## Reporting application

`TrainingPlatform.Reports` is a standalone MVC client that consumes the API's
`/api/reports/*` endpoints over `HttpClient`, authenticated with the JWT carried
on the shared SSO cookie. It renders coordinator dashboards (overview, revenue,
enrolments, instructors, sessions, certifications, trainees) with Chart.js. It
is coordinator-only via a fallback authorization policy and has no direct
database access.

---

## Configuration & secrets

Configuration is read from `appsettings.json` plus environment-specific
overrides and the standard ASP.NET Core configuration providers.

| Setting                          | Project          | Notes                              |
|----------------------------------|------------------|------------------------------------|
| `ConnectionStrings:DefaultConnection` | API, MVC    | SQL Server connection              |
| `JWT:Key` / `Issuer` / `Audience`| API              | Token signing & validation         |
| `ApiSettings:BaseUrl`            | MVC              | API base address                   |
| `Api:BaseUrl`                    | Reports          | API base address                   |
| `Stripe:SecretKey` / `WebhookSecret` / `PublishableKey` | MVC | Stripe Checkout      |
| `DataProtection:KeyRingPath`     | MVC, Reports     | Shared key ring for SSO            |

> ⚠️ **Secrets must not be committed.** Use **User Secrets** for local
> development and a secure store (e.g. Azure Key Vault / App Service
> configuration) in hosted environments. Move the JWT signing key, the Stripe
> keys, and connection strings out of `appsettings.json` before deploying, and
> rotate any value that has previously been committed.

To set a secret locally, for example:

```bash
dotnet user-secrets set "JWT:Key" "<a-strong-32+char-secret>" --project TrainingPlatform.API
```

---
