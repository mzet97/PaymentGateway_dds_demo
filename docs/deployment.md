# PaymentGateway - Complete Deployment Guide

**Version**: 1.0
**Date**: 2026-03-19
**Status**: Production-Ready

---

## 📋 Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [VM Deployment (192.168.1.61-63)](#vm-deployment)
4. [Production Deployment](#production-deployment)
5. [Verification & Testing](#verification--testing)
6. [Troubleshooting](#troubleshooting)
7. [Monitoring & Logs](#monitoring--logs)

---

## Prerequisites

### System Requirements

**For Local Testing** (Windows/Mac/Linux):
- Docker Desktop 24.0+
- Docker Compose 2.20+
- Git
- 8GB RAM
- 20GB disk space

**For VM Deployment** (Linux servers):
- Ubuntu 22.04 LTS or CentOS 9
- .NET 8 SDK
- Docker 24.0+
- Docker Compose 2.20+
- 16GB+ RAM
- 100GB+ SSD
- SSH access
- Network connectivity (192.168.1.0/24)

### VM Details (192.168.1.61-63)

| VM | IP | Role | GPU | User | Password |
|----|----|----|-----|------|----------|
| VM1 | 192.168.1.61 | API + Processors | RTX 3080 10GB | oldds | <your-password> |
| VM2 | 192.168.1.62 | Additional Processors | N/A | oldds | <your-password> |
| VM3 | 192.168.1.63 | Test Client | N/A | oldds | <your-password> |

---

## Local Development Setup

### Step 1: Clone & Prepare

```bash
# Clone repository
git clone <repo-url>
cd PaymentGateway

# Create environment file from template
cp .env.example .env

# Edit with your values
nano .env  # Or use your editor
```

**Key Variables** (in .env):
```bash
# Database passwords (change in production!)
POSTGRES_PASSWORD=<your-password>
MONGO_PASSWORD=<your-password>
REDIS_PASSWORD=<your-password>

# External APIs
OPENROUTER_API_KEY=sk-or-your-key-here

# DDS Configuration
DDS_TELEMETRY_SAMPLING_RATE=0.1
CIRCUIT_BREAKER_FAILURE_THRESHOLD=5
CIRCUIT_BREAKER_RECOVERY_INTERVAL_SECONDS=30
```

### Step 2: Build Docker Images

```bash
# Build all services
docker-compose build

# Or build specific service (faster for testing)
docker-compose build api
docker-compose build processor
```

**Expected Output**:
```
Building api
Successfully tagged payment-gateway-api:latest
Building processor
Successfully tagged payment-gateway-processor:latest
...
```

### Step 3: Start Services

```bash
# Start all services in background
docker-compose up -d

# Watch startup logs
docker-compose logs -f

# Wait for healthy state
docker-compose ps
```

**Expected Status**:
```
NAME                        STATUS
payment-gateway-api        Up (healthy)
payment-processor          Up
fraud-detector             Up
notification-service       Up
settlement-service         Up
postgres-db                Up (healthy)
mongodb                    Up (healthy)
redis                      Up (healthy)
```

### Step 4: Verify Services

```bash
# Check API health
curl http://localhost:5000/health

# Expected response:
# {"status":"Healthy","checks":{"dds":{"status":"Healthy",...}}}

# Check database connections
docker-compose logs api | grep "Successfully"

# Test DDS communication
docker-compose logs processor | grep "subscribed"
```

### Step 5: Run Load Test

```bash
# Install k6 (if not already installed)
# macOS: brew install k6
# Windows: choco install k6
# Linux: apt install k6

# Run 1-client baseline
k6 run --vus 1 --duration 30s scripts/load-test.js

# Run 10-client light load
k6 run --vus 10 --duration 60s scripts/load-test.js

# Run 100-client medium load
k6 run --vus 100 --duration 120s scripts/load-test.js
```

### Stop & Cleanup

```bash
# Stop services
docker-compose down

# Remove volumes (careful! loses data)
docker-compose down -v

# View logs after stopping
docker-compose logs api
```

---

## VM Deployment

### Step 1: Prepare VM (SSH)

```bash
# SSH into VM1
ssh oldds@192.168.1.61

# Update system packages
sudo apt update && sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
rm get-docker.sh

# Install Docker Compose
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" \
  -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose

# Add user to docker group (avoid sudo)
sudo usermod -aG docker oldds
newgrp docker

# Verify installation
docker --version
docker-compose --version
```

### Step 2: Clone Repository

```bash
# Create directory
mkdir -p ~/applications
cd ~/applications

# Clone repo (or SCP files)
git clone <repo-url>
cd PaymentGateway

# Create .env file
cp .env.example .env

# Edit with VM-specific values
nano .env
```

**Important for VMs**:
```bash
# Database hosts (use docker service names)
POSTGRES_PASSWORD=YourSecurePassword123!
MONGO_PASSWORD=YourSecurePassword456!
REDIS_PASSWORD=YourSecurePassword789!

# DDS optimization for high performance
DDS_TELEMETRY_SAMPLING_RATE=0.05  # Lower sampling for production
CIRCUIT_BREAKER_FAILURE_THRESHOLD=5
CIRCUIT_BREAKER_RECOVERY_INTERVAL_SECONDS=30

# Performance tuning
DOTNET_GCHeapCount=4
DOTNET_GCHeapAffinitizeMask=0xFF
```

### Step 3: Build Images on VM

```bash
# Build images (takes 10-15 minutes)
time docker-compose build

# Check space (needs ~10GB)
df -h

# After build, verify images
docker images | grep payment-gateway
```

### Step 4: Deploy Services

```bash
# Start all services
docker-compose up -d

# Monitor startup (5-10 seconds)
watch docker-compose ps

# Check logs for errors
docker-compose logs --tail=20 api

# Wait for healthy status
docker-compose ps

# Check DDS communication
docker-compose logs processor | head -20
```

### Step 5: Verify Production Deployment

```bash
# Check API is accessible
curl http://localhost:5000/health

# Check all services running
docker-compose ps | grep "Up"

# View resource usage
docker stats --no-stream

# Check database connectivity
docker-compose logs api | grep -i "database\|connected"
```

### Step 6: Configure Additional VMs

**VM2 (Additional Processors)**:
```bash
# SSH into VM2
ssh oldds@192.168.1.62

# Similar setup, but modify docker-compose to only run processors
# Option: Use docker-compose profiles or create separate compose file
```

**VM3 (Test Client)**:
```bash
# Can run load tests from here instead of local machine
# Or run integration test suite
```

---

## Production Deployment

### Pre-Deployment Checklist

- [ ] All VMs have Docker & Docker Compose installed
- [ ] .env file created with production passwords
- [ ] .env file NOT committed to version control
- [ ] Firewall rules configured (ports 5000, 27017, 5432, 6379)
- [ ] SSH key-based auth configured
- [ ] Network connectivity verified
- [ ] Disk space verified (100GB+ available)
- [ ] Backup of old data completed (if upgrading)

### Security Hardening

```bash
# 1. Change default passwords in .env
POSTGRES_PASSWORD=$(openssl rand -base64 32)
MONGO_PASSWORD=$(openssl rand -base64 32)
REDIS_PASSWORD=$(openssl rand -base64 32)

# 2. Restrict network access
# Modify docker-compose.yml - don't expose ports externally
services:
  postgres:
    expose:  # Internal only, not ports:
      - "5432"

# 3. Use Docker secrets instead of .env in production
docker secret create postgres_password <(echo "your-password")

# 4. Regular backups
docker-compose exec postgres pg_dump -U app demo-gateway > backup.sql
docker-compose exec mongodb mongodump --archive > backup.archive
```

### Monitoring Setup

```bash
# Start monitoring stack (optional)
docker-compose --profile monitoring up -d

# Access dashboards
# Prometheus: http://localhost:9090
# Grafana: http://localhost:3000 (admin/admin)
```

---

## Verification & Testing

### Health Checks

```bash
# API endpoint
curl http://localhost:5000/health
# Expected: {"status":"Healthy",...}

# Detailed health
curl http://localhost:5000/health/details
# Shows individual component health

# Database health
docker-compose exec postgres psql -U app -d demo-gateway -c "SELECT 1"
# Expected: 1 row

# MongoDB health
docker-compose exec mongodb mongosh --eval "db.adminCommand('ping')"
# Expected: {ok: 1}

# Redis health
docker-compose exec redis redis-cli ping
# Expected: PONG
```

### Functional Testing

```bash
# 1. Create payment
curl -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -d '{"merchantId":"MERCH-001","amount":100.00,"currency":"USD"}'

# 2. List payments
curl http://localhost:5000/payments?limit=10

# 3. Check processor logs (verify DDS communication)
docker-compose logs processor | grep "Received"

# 4. Verify MongoDB has data
docker-compose exec mongodb mongosh --eval "db.payments.count()"

# 5. Check PostgreSQL has read data
docker-compose exec postgres psql -U app -d demo-gateway -c "SELECT COUNT(*) FROM payments"
```

### Performance Testing

```bash
# Baseline (1 client)
k6 run --vus 1 --duration 30s scripts/load-test.js
# Expected: ~100-200 RPS, p50 latency ~30-50ms

# Light load (10 clients)
k6 run --vus 10 --duration 60s scripts/load-test.js
# Expected: ~1000-2000 RPS, p95 latency ~100-200ms

# Medium load (100 clients)
k6 run --vus 100 --duration 120s scripts/load-test.js
# Expected: ~5000-10000 RPS, p95 latency ~200-500ms
```

---

## Troubleshooting

### Services Won't Start

```bash
# Check logs
docker-compose logs api

# Common issues:
# 1. Port already in use
sudo lsof -i :5000
# Kill: sudo kill -9 <PID>

# 2. Docker daemon not running
sudo systemctl start docker

# 3. Compose file issues
docker-compose config  # Validate syntax

# 4. Image build failed
docker-compose build --no-cache api
```

### Database Connection Errors

```bash
# Check if services are running
docker-compose ps postgres mongodb redis

# Verify network
docker network inspect payment-network

# Test connection directly
docker-compose exec api curl http://postgres:5432
# Should get connection refused (port is open but SQL not HTTP)

# Check connection string in .env
echo $ConnectionStrings__DefaultConnection
```

### DDS Communication Failing

```bash
# Check processor logs
docker-compose logs processor | grep -i "dds\|error"

# Verify circuit breaker state
docker-compose logs api | grep -i "circuit\|fallback"

# Check CycloneDDS configuration
docker-compose exec api env | grep DDS

# If DDS fails, check fallback is working
docker-compose logs api | grep -i "in-memory"
```

### High CPU/Memory Usage

```bash
# Monitor resources
docker stats

# Check GC settings
docker-compose exec api env | grep DOTNET_GC

# Reduce concurrent clients in k6
k6 run --vus 5 --duration 30s scripts/load-test.js

# Scale up instances
docker-compose up -d --scale processor=5
```

### Logs Won't Stop

```bash
# Tail only recent logs
docker-compose logs --tail=50 api

# Follow specific service
docker-compose logs -f processor

# Stream from specific time
docker-compose logs --since 5m api

# Exit: Ctrl+C
```

---

## Monitoring & Logs

### View Logs

```bash
# All services
docker-compose logs

# Specific service
docker-compose logs api
docker-compose logs processor

# Follow in real-time
docker-compose logs -f api

# Last 100 lines
docker-compose logs --tail=100 api

# Since specific time
docker-compose logs --since 2h api
```

### Export Logs

```bash
# Save to file
docker-compose logs api > api-logs.txt

# Compress
docker-compose logs > all-logs.txt && gzip all-logs.txt

# Upload to S3 (if configured)
aws s3 cp all-logs.txt.gz s3://my-bucket/logs/
```

### Log Levels

**In .env** (adjust logging verbosity):
```bash
LOG_LEVEL_DEFAULT=Information      # Default
LOG_LEVEL_MICROSOFT=Warning         # Microsoft libs
LOG_LEVEL_SYSTEM=Warning            # System libs

# Production: Use "Warning" or "Error" only
# Development: Use "Debug" for detailed logs
```

### Performance Metrics

```bash
# Via Prometheus (if monitoring enabled)
curl http://localhost:9090/api/v1/query?query=http_requests_total

# Via Grafana dashboard
# Navigate to: http://localhost:3000
# Default credentials: admin / admin
# Import dashboard: ID 1860 (Node Exporter)
```

### Alert Setup (Optional)

```bash
# In prometheus.yml configuration:
global:
  evaluation_interval: 15s

alerting:
  alertmanagers:
    - static_configs:
        - targets:
            - alertmanager:9093

rule_files:
  - alert_rules.yml
```

---

## Scaling

### Horizontal Scaling (More VMs)

```bash
# Add VM4 for additional processor capacity
ssh oldds@192.168.1.64

# Same setup as VM1-3
# Scale processor replicas:
docker-compose up -d --scale processor=5

# Verify all running
docker-compose ps processor | grep "Up"
```

### Vertical Scaling (More Resources)

```bash
# Increase resource limits in docker-compose.yml
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '4'
          memory: 2G
        reservations:
          cpus: '2'
          memory: 1G
```

### Load Balancing (Production)

```yaml
# Add nginx load balancer (optional)
services:
  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./ssl:/etc/nginx/ssl:ro
    depends_on:
      - api
```

---

## Backup & Recovery

### Database Backups

```bash
# PostgreSQL backup
docker-compose exec postgres pg_dump -U app demo-gateway > backup-pg-$(date +%Y%m%d).sql

# MongoDB backup
docker-compose exec mongodb mongodump --archive > backup-mongo-$(date +%Y%m%d).archive

# Restore PostgreSQL
docker-compose exec -T postgres psql -U app demo-gateway < backup-pg-20260317.sql

# Restore MongoDB
docker-compose exec -T mongodb mongorestore --archive < backup-mongo-20260317.archive
```

### Container Recovery

```bash
# If container crashes, restart it
docker-compose restart api

# If service gets stuck, force recreate
docker-compose down && docker-compose up -d

# Check for zombie processes
ps aux | grep docker
```

---

## Next Steps

1. ✅ Deploy to local Docker Compose (test)
2. ✅ Deploy to VM1 (primary API + processor)
3. ✅ Deploy to VM2 (additional processors)
4. ✅ Run k6 load tests from VM3
5. ✅ Monitor via Prometheus/Grafana
6. ✅ Configure alerts (optional)
7. ✅ Setup automated backups
8. ✅ Document any custom configurations

---

## Support

For issues:
1. Check logs: `docker-compose logs <service>`
2. Verify health: `curl http://localhost:5000/health`
3. Review troubleshooting section above
4. Check project documentation

**Phase 6** (VM Testing & Validation) will run comprehensive integration tests and performance benchmarks to validate all optimizations.

---

**Status**: 🟢 Ready for Deployment
**Version**: 1.0
**Last Updated**: 2026-03-19

