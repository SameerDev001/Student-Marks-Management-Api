.PHONY: help restore build run test smoke-test secrets migrate migration-add schema-export docker-up docker-down docker-db clean

# Default shell
SHELL := bash

# Variables
PROJECT_DIR = src/MarksApi
TEST_DIR = tests/MarksApi.Tests
DB_NAME = marks_db
DB_USER = marks_user
DB_PASS = marks_pass_123
DB_HOST = localhost
DB_PORT = 5432
JWT_SECRET = ThisIsASecretKeyForMarksApiJwtAuth2026SecureKey!

help: ## Show this help message
	@echo "======================================================================"
	@echo "Marks API - Local Automation Makefile"
	@echo "======================================================================"
	@echo "Available commands:"
	@echo "  make restore       - Restore NuGet packages"
	@echo "  make build         - Build the API and test projects"
	@echo "  make run           - Run the Web API on local machine"
	@echo "  make test          - Run xUnit automated tests"
	@echo "  make smoke-test    - Run end-to-end startup smoke tests (--smoke-test)"
	@echo "  make secrets       - Configure local development User Secrets"
	@echo "  make migrate       - Apply EF Core migrations to PostgreSQL"
	@echo "  make migration-add - Add a new EF migration (e.g. make migration-add NAME=AddColumn)"
	@echo "  make schema-export - Export db/schema.sql via pg_dump"
	@echo "  make docker-up     - Start API and PostgreSQL in Docker Compose"
	@echo "  make docker-db     - Start only PostgreSQL container in Docker"
	@echo "  make docker-down   - Stop all Docker containers"
	@echo "  make clean         - Remove bin and obj build artifacts"
	@echo "======================================================================"

restore: ## Restore NuGet packages
	dotnet restore src/MarksApi/MarksApi.csproj
	dotnet restore tests/MarksApi.Tests/MarksApi.Tests.csproj

build: ## Build API and tests
	dotnet build src/MarksApi/MarksApi.csproj
	dotnet build tests/MarksApi.Tests/MarksApi.Tests.csproj

run: ## Run the Web API locally
	dotnet run --project $(PROJECT_DIR)

test: ## Run xUnit automated service-layer tests
	dotnet test $(TEST_DIR)

smoke-test: ## Run startup smoke test module against live API and database
	dotnet run --project $(PROJECT_DIR) -- --smoke-test

secrets: ## Configure development user-secrets
	cd $(PROJECT_DIR) && dotnet user-secrets set "ConnectionStrings:Database" "Host=$(DB_HOST);Port=$(DB_PORT);Database=$(DB_NAME);Username=$(DB_USER);Password=$(DB_PASS)"
	cd $(PROJECT_DIR) && dotnet user-secrets set "Jwt:Key" "$(JWT_SECRET)"
	cd $(PROJECT_DIR) && dotnet user-secrets set "SmokeTest:TeacherUsername" "teacher1"
	cd $(PROJECT_DIR) && dotnet user-secrets set "SmokeTest:TeacherPassword" "TeacherPassword123!"
	cd $(PROJECT_DIR) && dotnet user-secrets set "SmokeTest:StudentUsername" "student1"
	cd $(PROJECT_DIR) && dotnet user-secrets set "SmokeTest:StudentPassword" "StudentPassword123!"
	cd $(PROJECT_DIR) && dotnet user-secrets list

migrate: ## Apply database migrations to PostgreSQL
	cd $(PROJECT_DIR) && dotnet ef database update

migration-add: ## Add a new migration (specify NAME=YourMigrationName)
	@if [ -z "$(NAME)" ]; then echo "Error: Please specify NAME (e.g. make migration-add NAME=MyMigration)"; exit 1; fi
	cd $(PROJECT_DIR) && dotnet ef migrations add $(NAME)

schema-export: ## Export PostgreSQL schema to db/schema.sql
	pg_dump -U $(DB_USER) -h $(DB_HOST) -p $(DB_PORT) -d $(DB_NAME) --schema-only -f db/schema.sql

docker-up: ## Run API and PostgreSQL via Docker Compose
	docker compose up -d --build

docker-db: ## Run only PostgreSQL container via Docker Compose
	docker compose up -d postgres

docker-down: ## Stop Docker containers
	docker compose down

clean: ## Clean build outputs
	dotnet clean src/MarksApi/MarksApi.csproj
	dotnet clean tests/MarksApi.Tests/MarksApi.Tests.csproj
