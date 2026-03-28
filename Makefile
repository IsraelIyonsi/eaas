# =============================================================================
# EaaS Makefile — Developer Convenience Commands
# =============================================================================

.DEFAULT_GOAL := help
.PHONY: help dev build test migrate logs clean deploy backup health \
        lint format restore stop restart status

# --- Configuration ---
COMPOSE := docker compose
DOTNET  := dotnet
APP_DIR := /opt/eaas

# =============================================================================
# Development
# =============================================================================

## Start all services locally with Docker Compose
dev:
	@echo "Starting all services..."
	@cp -n .env.example .env 2>/dev/null || true
	$(COMPOSE) up -d
	@echo ""
	@echo "Services started. Endpoints:"
	@echo "  API:        http://localhost:5000"
	@echo "  Dashboard:  http://localhost:5001"
	@echo "  Webhook:    http://localhost:5002"
	@echo "  RabbitMQ:   http://localhost:15672"
	@echo ""
	@$(MAKE) --no-print-directory health

## Build the .NET solution
build:
	@echo "Building solution..."
	$(DOTNET) build --configuration Release /p:TreatWarningsAsErrors=true

## Run all tests with coverage
test:
	@echo "Running tests..."
	$(DOTNET) test \
		--configuration Release \
		--logger "console;verbosity=normal" \
		--collect:"XPlat Code Coverage" \
		--results-directory ./test-results

## Run database migrations
migrate:
	@echo "Running database migrations..."
	$(DOTNET) ef database update --project src/EaaS.Api

## Tail all container logs
logs:
	$(COMPOSE) logs -f --tail=100

## Stop and remove all containers, volumes, and networks
clean:
	@echo "Stopping and removing all containers, volumes, and networks..."
	$(COMPOSE) down -v --remove-orphans
	@echo "Cleaned up."

## Stop all containers (without removing volumes)
stop:
	$(COMPOSE) stop

## Restart all containers
restart:
	$(COMPOSE) restart

## Show container status
status:
	$(COMPOSE) ps

# =============================================================================
# Code Quality
# =============================================================================

## Check code formatting (CI-style, no changes)
lint:
	@echo "Checking code formatting..."
	$(DOTNET) format --verify-no-changes --verbosity diagnostic

## Auto-fix code formatting
format:
	@echo "Formatting code..."
	$(DOTNET) format

# =============================================================================
# Deployment & Operations
# =============================================================================

## Trigger deployment (push release branch)
deploy:
	@BRANCH=$$(git rev-parse --abbrev-ref HEAD); \
	if echo "$$BRANCH" | grep -q "^release/"; then \
		echo "Pushing $$BRANCH to trigger deployment..."; \
		git push origin "$$BRANCH"; \
	else \
		echo "ERROR: Not on a release branch (current: $$BRANCH)"; \
		echo "Create a release branch first: git checkout -b release/v1.x.x"; \
		exit 1; \
	fi

## Run database backup (daily)
backup:
	@echo "Running database backup..."
	@if [ -f scripts/backup-db.sh ]; then \
		bash scripts/backup-db.sh daily; \
	else \
		$(COMPOSE) exec postgres pg_dump -U eaas eaas | gzip > "backup_$$(date +%Y%m%d_%H%M%S).sql.gz"; \
		echo "Backup saved locally."; \
	fi

## Restore database from backup file
restore:
	@if [ -z "$(FILE)" ]; then \
		echo "Usage: make restore FILE=path/to/backup.sql.gz"; \
		exit 1; \
	fi
	@echo "Restoring database from $(FILE)..."
	$(COMPOSE) stop api worker webhook-processor
	gunzip -c "$(FILE)" | $(COMPOSE) exec -T postgres psql -U eaas -d eaas
	$(COMPOSE) start api worker webhook-processor
	@echo "Database restored."

## Check all service health endpoints
health:
	@echo "Checking service health..."
	@echo ""
	@printf "  API:       "; \
	HTTP_CODE=$$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health 2>/dev/null); \
	if [ "$$HTTP_CODE" = "200" ]; then echo "HEALTHY ($$HTTP_CODE)"; else echo "UNHEALTHY ($$HTTP_CODE)"; fi
	@printf "  Dashboard: "; \
	HTTP_CODE=$$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5001/health 2>/dev/null); \
	if [ "$$HTTP_CODE" = "200" ]; then echo "HEALTHY ($$HTTP_CODE)"; else echo "UNHEALTHY ($$HTTP_CODE)"; fi
	@printf "  Webhook:   "; \
	HTTP_CODE=$$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5002/health 2>/dev/null); \
	if [ "$$HTTP_CODE" = "200" ]; then echo "HEALTHY ($$HTTP_CODE)"; else echo "UNHEALTHY ($$HTTP_CODE)"; fi
	@printf "  Postgres:  "; \
	if $(COMPOSE) exec -T postgres pg_isready -U eaas > /dev/null 2>&1; then echo "HEALTHY"; else echo "UNHEALTHY"; fi
	@printf "  Redis:     "; \
	if $(COMPOSE) exec -T redis redis-cli ping > /dev/null 2>&1; then echo "HEALTHY"; else echo "UNHEALTHY"; fi
	@printf "  RabbitMQ:  "; \
	if $(COMPOSE) exec -T rabbitmq rabbitmq-diagnostics -q ping > /dev/null 2>&1; then echo "HEALTHY"; else echo "UNHEALTHY"; fi
	@echo ""

# =============================================================================
# Help
# =============================================================================

## Show this help message
help:
	@echo ""
	@echo "EaaS — Available Commands"
	@echo "========================="
	@echo ""
	@echo "Development:"
	@echo "  make dev        Start all services locally with Docker Compose"
	@echo "  make build      Build .NET solution (warnings = errors)"
	@echo "  make test       Run all tests with coverage"
	@echo "  make migrate    Run database migrations"
	@echo "  make logs       Tail all container logs"
	@echo "  make clean      Stop and remove all containers/volumes"
	@echo "  make stop       Stop all containers"
	@echo "  make restart    Restart all containers"
	@echo "  make status     Show container status"
	@echo ""
	@echo "Code Quality:"
	@echo "  make lint       Check code formatting (no changes)"
	@echo "  make format     Auto-fix code formatting"
	@echo ""
	@echo "Operations:"
	@echo "  make deploy     Push release branch to trigger deployment"
	@echo "  make backup     Run database backup"
	@echo "  make restore    Restore from backup (FILE=path/to/backup.sql.gz)"
	@echo "  make health     Check all service health endpoints"
	@echo ""
