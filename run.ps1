<#
.SYNOPSIS
    Cross-platform Windows/PowerShell automation task runner for Marks API.
    Can be used interchangeably with 'make <target>'.

.EXAMPLE
    .\run.ps1 build
    .\run.ps1 run
    .\run.ps1 test
    .\run.ps1 smoke-test
    .\run.ps1 migrate
#>

param(
    [Parameter(Position=0)]
    [ValidateSet("help", "restore", "build", "run", "test", "smoke-test", "secrets", "migrate", "migration-add", "schema-export", "docker-up", "docker-db", "docker-down", "clean")]
    [string]$Target = "help",

    [Parameter(Position=1)]
    [string]$Name
)

$ErrorActionPreference = "Stop"

$ProjectDir = "src/MarksApi"
$TestDir = "tests/MarksApi.Tests"
$DbName = "marks_db"
$DbUser = "marks_user"
$DbPass = "marks_pass_123"
$DbHost = "localhost"
$DbPort = "5432"
$JwtSecret = "ThisIsASecretKeyForMarksApiJwtAuth2026SecureKey!"

# Ensure dotnet & postgres in PATH
$env:PATH = "C:\Program Files\dotnet;C:\Program Files\PostgreSQL\16\bin;C:\Users\samee\.dotnet\tools;C:\Users\samee\AppData\Local\Programs\Python\Python313;$env:PATH"

switch ($Target) {
    "help" {
        Write-Host "======================================================================" -ForegroundColor Cyan
        Write-Host " Marks API - Local Automation Runner (PowerShell / Make Equivalent)" -ForegroundColor Cyan
        Write-Host "======================================================================" -ForegroundColor Cyan
        Write-Host "Usage: .\run.ps1 <command>" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Available commands:"
        Write-Host "  .\run.ps1 restore       - Restore NuGet packages"
        Write-Host "  .\run.ps1 build         - Build the API and test projects"
        Write-Host "  .\run.ps1 run           - Run the Web API on local machine"
        Write-Host "  .\run.ps1 test          - Run xUnit automated tests"
        Write-Host "  .\run.ps1 smoke-test    - Run end-to-end startup smoke tests"
        Write-Host "  .\run.ps1 secrets       - Configure local development User Secrets"
        Write-Host "  .\run.ps1 migrate       - Apply EF Core migrations to PostgreSQL"
        Write-Host "  .\run.ps1 migration-add - Add an EF migration (e.g. .\run.ps1 migration-add -Name AddField)"
        Write-Host "  .\run.ps1 schema-export - Export db/schema.sql via pg_dump"
        Write-Host "  .\run.ps1 docker-up     - Start API and PostgreSQL in Docker"
        Write-Host "  .\run.ps1 docker-db     - Start only PostgreSQL container in Docker"
        Write-Host "  .\run.ps1 docker-down   - Stop all Docker containers"
        Write-Host "  .\run.ps1 clean         - Remove bin and obj build artifacts"
        Write-Host "======================================================================" -ForegroundColor Cyan
    }

    "restore" {
        Write-Host "Restoring NuGet packages..." -ForegroundColor Green
        dotnet restore "$ProjectDir/MarksApi.csproj"
        dotnet restore "$TestDir/MarksApi.Tests.csproj"
    }

    "build" {
        Write-Host "Building API and test projects..." -ForegroundColor Green
        dotnet build "$ProjectDir/MarksApi.csproj"
        dotnet build "$TestDir/MarksApi.Tests.csproj"
    }

    "run" {
        Write-Host "Starting Web API on local machine..." -ForegroundColor Green
        dotnet run --project $ProjectDir
    }

    "test" {
        Write-Host "Running xUnit automated tests..." -ForegroundColor Green
        dotnet test $TestDir
    }

    "smoke-test" {
        Write-Host "Executing live startup smoke tests against real PostgreSQL..." -ForegroundColor Green
        dotnet run --project $ProjectDir -- --smoke-test
    }

    "secrets" {
        Write-Host "Configuring local development User Secrets..." -ForegroundColor Green
        Push-Location $ProjectDir
        try {
            dotnet user-secrets set "ConnectionStrings:Database" "Host=$DbHost;Port=$DbPort;Database=$DbName;Username=$DbUser;Password=$DbPass"
            dotnet user-secrets set "Jwt:Key" $JwtSecret
            dotnet user-secrets set "SmokeTest:TeacherUsername" "teacher1"
            dotnet user-secrets set "SmokeTest:TeacherPassword" "TeacherPassword123!"
            dotnet user-secrets set "SmokeTest:StudentUsername" "student1"
            dotnet user-secrets set "SmokeTest:StudentPassword" "StudentPassword123!"
            dotnet user-secrets list
        }
        finally {
            Pop-Location
        }
    }

    "migrate" {
        Write-Host "Applying EF Core migrations to PostgreSQL ($DbName)..." -ForegroundColor Green
        Push-Location $ProjectDir
        try {
            dotnet ef database update
        }
        finally {
            Pop-Location
        }
    }

    "migration-add" {
        if (-not $Name) {
            Write-Error "Please specify a migration name: .\run.ps1 migration-add -Name <MigrationName>"
            return
        }
        Write-Host "Adding migration: $Name..." -ForegroundColor Green
        Push-Location $ProjectDir
        try {
            dotnet ef migrations add $Name
        }
        finally {
            Pop-Location
        }
    }

    "schema-export" {
        Write-Host "Exporting PostgreSQL schema to db/schema.sql..." -ForegroundColor Green
        $env:PGPASSWORD = $DbPass
        pg_dump -U $DbUser -h $DbHost -p $DbPort -d $DbName --schema-only -f db/schema.sql
        Write-Host "Schema successfully exported to db/schema.sql." -ForegroundColor Green
    }

    "docker-up" {
        Write-Host "Starting containers via Docker Compose..." -ForegroundColor Green
        docker compose up -d --build
    }

    "docker-db" {
        Write-Host "Starting PostgreSQL container only..." -ForegroundColor Green
        docker compose up -d postgres
    }

    "docker-down" {
        Write-Host "Stopping Docker containers..." -ForegroundColor Green
        docker compose down
    }

    "clean" {
        Write-Host "Cleaning build output directories..." -ForegroundColor Green
        dotnet clean "$ProjectDir/MarksApi.csproj"
        dotnet clean "$TestDir/MarksApi.Tests.csproj"
    }
}
