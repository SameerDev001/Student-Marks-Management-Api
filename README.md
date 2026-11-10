# Student Marks Management API

A production-style RESTful Web API for managing students, courses, enrollments, and academic marks, built with **ASP.NET Core 8**, **Entity Framework Core 8**, and **PostgreSQL 16**.

Developed for **FAST NUCES - Department of Artificial Intelligence and Data Science**, Advanced Programming (C# & .NET) Assignment 1.

---

## 1. Project Overview

The department requires a reliable and secure service to store student identities, curriculum courses, course enrollments, and grading results. The API enforces:
- **Relational Integrity**: Direct database check constraints, unique indexes, and restricted foreign key deletions in PostgreSQL.
- **Role-Based Security**: Complete JWT authentication with role authorization (`Teacher` vs. `Student`) and strict server-side student ownership protection.
- **Academic Rules**: Credit-weighted transcript GPA calculation, dynamic course performance statistics, and configurable pass marks.
- **Production Habits**: Clean layered architecture, DTO encapsulation, RFC 7807 problem details error format, health checks, UTC timestamps, OpenAPI (Swagger UI), and zero credentials committed to Git.

---

## 2. Technologies & Key Libraries

| Technology / Package | Version | Purpose |
| :--- | :--- | :--- |
| **.NET SDK** | `8.0.427` | Core runtime & Web API hosting |
| **PostgreSQL** | `16.15+` | ACID relational database engine |
| **Npgsql.EntityFrameworkCore.PostgreSQL** | `8.0.11` | PostgreSQL database provider for EF Core |
| **EFCore.NamingConventions** | `8.0.3` | Automatic `snake_case` naming conventions for PostgreSQL |
| **Microsoft.AspNetCore.Authentication.JwtBearer** | `8.0.11` | Stateless JWT Bearer token authentication |
| **Microsoft.AspNetCore.Identity.PasswordHasher** | Built-in V3 | PBKDF2 password hashing (HMAC-SHA256, salted) |
| **HealthChecks.EntityFrameworkCore** | `8.0.11` | Public liveness probe (`/health`) checking database status |
| **Swashbuckle.AspNetCore** | `6.6.2` | Interactive Swagger UI with Bearer Auth documentation |
| **User Secrets** | `8.0.x` | Secure out-of-repo credential management in development |

---

## 3. Prerequisites

Ensure the following tools are installed on your machine:
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`dotnet --version`)
- [PostgreSQL 16+](https://www.postgresql.org/download/) (`psql --version`)
- [Postman](https://www.postman.com/downloads/) (Desktop app)
- [Git](https://git-scm.com/) (`git --version`)
- EF Core CLI tool:
  ```bash
  dotnet tool install --global dotnet-ef
  ```

---

## 4. Step-by-Step Setup Guide

Follow these steps in order to set up and run the application from a clean clone.

### Step 1: Database & User Creation in PostgreSQL

Open a terminal or command prompt and connect as the PostgreSQL administrator:
```bash
psql -U postgres
```
Execute the following SQL commands:
```sql
CREATE USER marks_user WITH PASSWORD 'marks_pass_123';
CREATE DATABASE marks_db OWNER marks_user;
\q
```

*(Alternative with Docker)*:
```bash
docker run -d --name marks-postgres -e POSTGRES_USER=marks_user -e POSTGRES_PASSWORD=marks_pass_123 -e POSTGRES_DB=marks_db -p 5432:5432 postgres:16
```

### Step 2: Configure Development Secrets (User Secrets)

Never commit database passwords or JWT signing keys to source control. Configure them locally:
```bash
cd src/MarksApi

dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=marks_db;Username=marks_user;Password=marks_pass_123"
dotnet user-secrets set "Jwt:Key" "ThisIsASecretKeyForMarksApiJwtAuth2026SecureKey!"
dotnet user-secrets set "SmokeTest:TeacherUsername" "teacher1"
dotnet user-secrets set "SmokeTest:TeacherPassword" "TeacherPassword123!"
dotnet user-secrets set "SmokeTest:StudentUsername" "student1"
dotnet user-secrets set "SmokeTest:StudentPassword" "StudentPassword123!"
```

*(For production environments)*: supply these values as standard environment variables:
- `ConnectionStrings__Database`
- `Jwt__Key`

### Step 3: Apply Database Migrations

Apply the committed EF Core migrations to create the PostgreSQL tables, indexes, and constraints:
```bash
dotnet ef database update
```

### Step 4: Run the Application

Start the Web API:
```bash
dotnet run
```
The console will display the listening address (e.g., `http://localhost:5236` or `http://localhost:5000`).

Open your browser and navigate to the root address or `/swagger`:
```
http://localhost:5236
```
The interactive Swagger UI will load with all 18 endpoints documented.

---

## 5. Development Demo Credentials

The database automatically seeds development records upon initial startup:

| Username | Password | Role | Associated Student |
| :--- | :--- | :--- | :--- |
| `teacher1` | `TeacherPassword123!` | `Teacher` | None (Staff access) |
| `student1` | `StudentPassword123!` | `Student` | Ali Raza (`CS-2022-001`), Student ID: `1` |
| `student2` | `StudentPassword123!` | `Student` | Sara Khan (`CS-2022-002`), Student ID: `2` |

> **Security Notice**: These credentials are strictly for local development and grading demonstration. All passwords are stored hashed with `PasswordHasher` in the database.

---

## 6. Complete API Endpoint Reference

All endpoints return JSON responses. Errors follow the RFC 7807 ProblemDetails specification.

| # | Method | Path | Allowed Roles | Description | Expected Status Codes |
|---|---|---|---|---|---|
| 1 | `POST` | `/api/auth/login` | Anyone | Authenticate user & issue signed JWT | `200`, `400`, `401` |
| 2 | `GET` | `/api/students` | `Teacher` | Paginated list of students (`page`, `pageSize`, `search`, `sort`) | `200`, `401`, `403` |
| 3 | `GET` | `/api/students/{id}` | `Teacher`, `Student` (Own only) | Get student profile | `200`, `401`, `403`, `404` |
| 4 | `POST` | `/api/students` | `Teacher` | Register new student record | `201` (with `Location`), `400`, `401`, `403`, `409` |
| 5 | `PUT` | `/api/students/{id}` | `Teacher` | Update existing student profile | `200`, `400`, `401`, `403`, `404`, `409` |
| 6 | `DELETE` | `/api/students/{id}` | `Teacher` | Delete student (blocked if has enrollments) | `204`, `401`, `403`, `404`, `409` |
| 7 | `GET` | `/api/courses` | `Teacher`, `Student` | Paginated list of courses | `200`, `401` |
| 8 | `GET` | `/api/courses/{id}` | `Teacher`, `Student` | Get course details | `200`, `401`, `404` |
| 9 | `POST` | `/api/courses` | `Teacher` | Create new course (credit hours: 1–4) | `201` (with `Location`), `400`, `401`, `403`, `409` |
| 10 | `PUT` | `/api/courses/{id}` | `Teacher` | Update course details | `200`, `400`, `401`, `403`, `404`, `409` |
| 11 | `DELETE` | `/api/courses/{id}` | `Teacher` | Delete course (blocked if has enrollments) | `204`, `401`, `403`, `404`, `409` |
| 12 | `POST` | `/api/courses/{courseId}/enrollments` | `Teacher` | Enroll student into course | `201` (with `Location`), `400`, `401`, `403`, `404`, `409` |
| 13 | `GET` | `/api/courses/{courseId}/enrollments` | `Teacher` | Get list of enrollments for course | `200`, `401`, `403`, `404` |
| 14 | `PUT` | `/api/enrollments/{id}/mark` | `Teacher` | Record or update mark (0–100 or null) | `200`, `400`, `401`, `403`, `404` |
| 15 | `DELETE` | `/api/enrollments/{id}` | `Teacher` | Remove enrollment | `204`, `401`, `403`, `404` |
| 16 | `GET` | `/api/courses/{courseId}/stats` | `Teacher` | Course analytics (avg, highest, lowest, passed) | `200`, `401`, `403`, `404` |
| 17 | `GET` | `/api/students/{id}/transcript` | `Teacher`, `Student` (Own only) | Credit-weighted GPA transcript | `200`, `401`, `403`, `404` |
| 18 | `GET` | `/health` | Anyone | Liveness check (PostgreSQL connectivity) | `200`, `503` |

---

## 7. Business Rules & Logic

1. **Mark Validation**:
   - Marks must be whole numbers between `0` and `100` inclusive, or `null` (ungraded).
   - Any value `< 0` or `> 100` returns `400 Bad Request`.
2. **Duplicate Protection**:
   - Registration number, course code, student email, and composite `(student_id, course_id)` enrollment must be strictly unique.
   - Any attempt to duplicate returns `409 Conflict`.
3. **Deletion Restriction**:
   - A student with existing enrollments cannot be deleted (`409 Conflict`).
   - A course with existing enrollments cannot be deleted (`409 Conflict`).
   - Enrollments must be deleted first.
4. **Configurable Pass Mark**:
   - Configured in `appsettings.json` under `Marking:PassMark` (default: `50`).
   - Never hardcoded in business logic.
5. **Course Statistics**:
   - `average`, `highest`, and `lowest` marks are computed across graded enrollments only.
   - If no students in a course are graded yet, `average` is reported as `null` (never `0`).
6. **Credit-Weighted Transcript GPA**:
   $$\text{Weighted Average} = \frac{\sum (\text{mark} \times \text{credit\_hours})}{\sum \text{credit\_hours}}$$
   - Ungraded courses are excluded.
   - Seeded example: Ali Raza (`CS401`: 3 cr, mark 80; `MA201`: 2 cr, mark 60) $\rightarrow (80 \times 3 + 60 \times 2) / 5 = 360 / 5 = 72.0$.
7. **Student Ownership Rule**:
   - A `Student` user may only access `GET /api/students/{id}` and `GET /api/students/{id}/transcript` where `id` equals their own `StudentId`.
   - Accessing another student's ID immediately returns `403 Forbidden`.

---

## 8. Postman Testing Guide

The repository includes a complete Postman collection and environment ready for automated execution:
- Collection: `postman/MarksApi.postman_collection.json` (39 requests, 7 folders)
- Environment: `postman/Local.postman_environment.json`

### Import Instructions
1. Open the Postman Desktop application.
2. Click **Import** (top left) and select both files from the `postman/` directory.
3. In the top-right environment dropdown, select **Local**.
4. Confirm `baseUrl` is set to your running API port (default: `http://localhost:5236`).

### Running the Collection Runner
1. Right-click the **Marks API** collection in the left sidebar and select **Run collection**.
2. Ensure all 7 folders are checked:
   - `1. Auth`
   - `2. Students`
   - `3. Courses`
   - `4. Enrollments and Marks`
   - `5. Reports`
   - `6. Health`
   - `7. Negative Tests`
3. Click **Run Marks API**.
4. All 39 requests will execute in sequence with **100% tests passed**.

---

## 9. Startup Smoke Test Module

The application includes an internal, zero-mock smoke test runner that connects to real PostgreSQL and executes real HTTP requests across all 18 endpoints, validating positive cases, error codes, and performing automatic cleanup.

### Execution Command
Run the application with the `--smoke-test` argument:
```bash
cd src/MarksApi
dotnet run -- --smoke-test
```
*(Or set environment variable `SMOKE_TEST=true`)*:
```bash
$env:SMOKE_TEST="true"
dotnet run
```

### Sample Smoke Test Output
```text
================================================================================
                  STARTING PRODUCTION STARTUP SMOKE TESTS                       
 Target API Base Address: http://localhost:5236
================================================================================
[PASS] GET    /health                                  | Expected: 200 OK           | Actual: 200 OK           
[PASS] POST   /api/auth/login (Teacher)                | Expected: 200 OK           | Actual: 200 OK           
[PASS] POST   /api/auth/login (Student)                | Expected: 200 OK           | Actual: 200 OK           
[PASS] POST   /api/auth/login (Invalid Creds)          | Expected: 401 Unauthorized | Actual: 401 Unauthorized 
[PASS] POST   /api/auth/login (Empty Fields)           | Expected: 400 BadRequest   | Actual: 400 BadRequest   
[PASS] GET    /api/students (No Auth)                  | Expected: 401 Unauthorized | Actual: 401 Unauthorized 
[PASS] GET    /api/students (Student Role)             | Expected: 403 Forbidden    | Actual: 403 Forbidden    
[PASS] GET    /api/students (Teacher Role)             | Expected: 200 OK           | Actual: 200 OK           
[PASS] POST   /api/students (Valid Create)             | Expected: 201 Created      | Actual: 201 Created      
[PASS] POST   /api/students (Duplicate RegNo)          | Expected: 409 Conflict     | Actual: 409 Conflict     
[PASS] GET    /api/students/1 (Student Own)            | Expected: 200 OK           | Actual: 200 OK           
[PASS] GET    /api/students/2 (Student Forbidden)      | Expected: 403 Forbidden    | Actual: 403 Forbidden    
[PASS] POST   /api/courses (Valid Create)              | Expected: 201 Created      | Actual: 201 Created      
[PASS] POST   /api/courses (Duplicate Code)            | Expected: 409 Conflict     | Actual: 409 Conflict     
[PASS] POST   /api/courses/1/enrollments (Duplicate)   | Expected: 409 Conflict     | Actual: 409 Conflict     
[PASS] PUT    /api/enrollments/1/mark (Mark 101)       | Expected: 400 BadRequest   | Actual: 400 BadRequest   
[PASS] PUT    /api/enrollments/1/mark (Valid Mark 85)  | Expected: 200 OK           | Actual: 200 OK           
[PASS] GET    /api/courses/1/stats                     | Expected: 200 OK           | Actual: 200 OK           
[PASS] GET    /api/students/1/transcript (Student Own) | Expected: 200 OK           | Actual: 200 OK           
[PASS] DELETE /api/students/1 (Has Enrollments)        | Expected: 409 Conflict     | Actual: 409 Conflict     
[PASS] DELETE /api/courses/1 (Has Enrollments)         | Expected: 409 Conflict     | Actual: 409 Conflict     
================================================================================
 SMOKE TEST SUMMARY: Total: 36 | Passed: 36 | Failed: 0
================================================================================
```
Exit code is `0` when all checks pass.

---

## 10. Repository Folder Structure

```text
RegNo_Name_Assignment1/
│
├── src/
│   └── MarksApi/
│       ├── Program.cs                         # Application entry, middleware, dependency injection & routing
│       ├── appsettings.json                   # Non-secret application configuration
│       ├── MarksApi.csproj                    # Project dependencies (.NET 8)
│       ├── Authentication/                    # JWT token generation, claims extensions, PasswordService
│       ├── Configuration/                     # Strongly-typed configuration options (JwtSettings, MarkingSettings)
│       ├── Data/                              # AppDbContext, entity configurations, DbSeeder
│       ├── Dtos/                              # Request, response, and pagination DTO contracts
│       ├── Endpoints/                         # Route group definitions (Auth, Students, Courses, Enrollments, Reports, Health)
│       ├── Entities/                          # Domain entities (Student, Course, Enrollment, User)
│       ├── Exceptions/                        # Custom ApiException domain exceptions
│       ├── Interfaces/                        # Service interfaces (IStudentService, ICourseService, etc.)
│       ├── Middleware/                        # Centralized RFC 7807 ProblemDetails exception handling
│       ├── Migrations/                        # Committed EF Core database migration files
│       ├── Services/                          # Business logic service implementations
│       └── SmokeTests/                        # Zero-mock startup smoke test runner module
│
├── postman/
│   ├── MarksApi.postman_collection.json       # Postman Collection v2.1 with 39 chained requests
│   └── Local.postman_environment.json         # Local environment configuration
│
├── db/
│   └── schema.sql                             # Schema exported via pg_dump --schema-only
│
├── docs/
│   ├── erd.png                                # Relational ER Diagram (300 DPI)
│   ├── report.pdf                             # 4-page technical report
│   └── screenshots/                           # Evidence screenshots (Runner, 201, 400, 403, 409, Smoke test)
│
├── appsettings.Example.json                   # Template configuration with fake values
├── README.md                                  # Complete developer and grader documentation
└── .gitignore                                 # Excludes bin/, obj/, user secrets, and build artifacts
```

---

## 11. Design Decisions & Rationale

- **Why 409 Conflict for Duplicates and Not 400?**:
  In RESTful API design (RFC 9110), HTTP `400 Bad Request` indicates malformed client syntax or constraint violations discernible at the edge (e.g., negative mark, malformed email). In contrast, HTTP `409 Conflict` indicates that the request was structurally valid, but cannot be processed due to a conflict with the current state of the resource (e.g., duplicate unique registration number or duplicate enrollment).
- **Why Rely on PostgreSQL Unique Indexes Rather Than Check-Then-Insert?**:
  Under concurrent loads, two simultaneous requests can both execute a `SELECT` check at the exact same millisecond and pass. Only an atomic unique index in PostgreSQL can guarantee uncompromised consistency. The application catches the resulting Npgsql `23505` unique violation and transforms it into a clean `409 Conflict` response.
- **Why Separate DTOs from Entities?**:
  Exposing EF Core entities directly leads to security vulnerabilities (mass assignment / over-posting), cyclic JSON serialization errors, and couples API consumers to internal database schema choices.

---

## 12. Troubleshooting

| Issue | Cause | Solution |
| :--- | :--- | :--- |
| `Npgsql.NpgsqlException: Connection refused` | PostgreSQL service is stopped | Start PostgreSQL service: `net start postgresql-x64-16` or check Docker container. |
| `401 Unauthorized` on protected endpoints | Missing or expired token | Call `POST /api/auth/login` as Teacher or Student to obtain a fresh token. |
| `403 Forbidden` on Student transcript | Requesting another student's ID | Ensure the requested `{id}` matches the logged-in student's ID (e.g. ID `1` for `student1`). |
| `dotnet ef database update` fails | Incorrect connection string | Verify user secrets with `dotnet user-secrets list` and confirm credentials connect in `psql`. |

---

## 13. License & Authors

Developed by **Sameer** for **FAST NUCES Chiniot-Faisalabad Campus** (Advanced Programming C# & .NET). All rights reserved.
