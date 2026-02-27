# MessagingPoc task runner
# Usage: just <recipe>

set shell         := ["/bin/bash", "-c"]
set windows-shell := ["powershell.exe", "-c"]

COMPOSE_FILE := "localstack/docker-compose.yml"
COMPOSE_DIR  := "localstack"

# List all available recipes
default:
    @just --list

# ── AWS profile setup (runs before LocalStack, idempotent) ────────────────────

configure-aws:
    aws configure set aws_access_key_id test --profile localstack
    aws configure set aws_secret_access_key test --profile localstack
    aws configure set region us-east-1 --profile localstack

# ── LocalStack ────────────────────────────────────────────────────────────────

[unix]
localstack: configure-aws
    #!/usr/bin/env bash
    set -euo pipefail
    if ! docker ps --filter "name=localstack" --filter "status=running" -q | grep -q .; then
        echo "Starting LocalStack..."
        docker compose -f "{{COMPOSE_FILE}}" --project-directory "{{COMPOSE_DIR}}" up -d
    else
        echo "LocalStack already running."
    fi
    echo "Waiting for LocalStack to be healthy..."
    for i in $(seq 1 30); do
        if curl -sf http://localhost:4566/_localstack/health 2>/dev/null | grep -q '"sqs": *"running"'; then
            echo "LocalStack is ready."
            exit 0
        fi
        echo "  ($i/30) not ready yet, retrying in 2s..."
        sleep 2
    done
    echo "ERROR: LocalStack did not become healthy in 60s." && exit 1

[windows]
localstack: configure-aws
    #!powershell.exe
    $running = docker ps --filter "name=localstack" --filter "status=running" -q
    if (-not $running) {
        Write-Host "Starting LocalStack..."
        docker compose -f "{{COMPOSE_FILE}}" --project-directory "{{COMPOSE_DIR}}" up -d
    } else {
        Write-Host "LocalStack already running."
    }
    Write-Host "Waiting for LocalStack to be healthy..."
    for ($i = 1; $i -le 30; $i++) {
        try {
            $h = Invoke-RestMethod -Uri "http://localhost:4566/_localstack/health" -EA Stop
            if ($h.services.sqs -eq "running") { Write-Host "LocalStack is ready."; exit 0 }
        } catch {}
        Write-Host "  ($i/30) not ready yet, retrying in 2s..."
        Start-Sleep -Seconds 2
    }
    Write-Host "ERROR: LocalStack did not become healthy in 60s."; exit 1

localstack-down:
    docker compose -f "{{COMPOSE_FILE}}" --project-directory "{{COMPOSE_DIR}}" down

# ── Applications ──────────────────────────────────────────────────────────────

# Run MessagingPoc.Aws — port 5009
aws: localstack
    dotnet run --project MessagingPoc.Aws/MessagingPoc.Aws.csproj

# Run MessagingPoc.Wolverine — port 5004
wolverine: localstack
    dotnet run --project MessagingPoc.Wolverine/MessagingPoc.Wolverine.csproj

# Run MessagingPoc.Rebus — port 5010
[unix]
rebus: localstack
    AWS_PROFILE=localstack dotnet run --project MessagingPoc.Rebus/MessagingPoc.Rebus.csproj

[windows]
rebus: localstack
    $env:AWS_PROFILE="localstack"; dotnet run --project MessagingPoc.Rebus/MessagingPoc.Rebus.csproj
