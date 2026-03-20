#!/bin/bash
# ============================================
# PaymentGateway VM1 Deployment Script
# ============================================
# Run on: VM1 (192.168.1.61)
# User: oldds
# Purpose: Automate Phase 5 deployment
# Time: ~15 minutes (mostly Docker build waiting)

set -e  # Exit on any error

echo "================================"
echo "PaymentGateway VM1 Deployment"
echo "================================"
echo "Target: 192.168.1.61"
echo "User: oldds"
echo ""

# Step 1: Prepare directories
echo "[1/6] Preparing directories..."
mkdir -p ~/applications
cd ~/applications
echo "✓ Directory ready: $(pwd)"
echo ""

# Step 2: Clone or verify repository
echo "[2/6] Cloning repository..."
if [ -d "PaymentGateway" ]; then
    echo "! Repository already exists, updating..."
    cd PaymentGateway
    git pull origin main || true
    cd ..
else
    echo "Cloning repository..."
    git clone https://github.com/your-repo/PaymentGateway.git || {
        echo "! Git clone failed. Manual steps:"
        echo "  1. cd ~/applications"
        echo "  2. git clone <repo-url>"
        echo "  3. cd PaymentGateway"
        echo "  4. Run this script again"
        exit 1
    }
fi
cd PaymentGateway
echo "✓ Repository ready at: $(pwd)"
echo ""

# Step 3: Configure environment
echo "[3/6] Configuring environment..."
if [ -f ".env" ]; then
    echo "! .env already exists. Backing up to .env.backup"
    cp .env .env.backup
fi
cp .env.example .env
echo "! Created .env from template"
echo ""
echo "⚠️  IMPORTANT: Edit .env with production passwords:"
echo "   nano .env"
echo ""
echo "   Required changes:"
echo "   - POSTGRES_PASSWORD=<change-to-secure-password>"
echo "   - MONGO_PASSWORD=<change-to-secure-password>"
echo "   - REDIS_PASSWORD=<change-to-secure-password>"
echo ""
read -p "Press ENTER after editing .env, or Ctrl+C to cancel: "
echo ""

# Step 4: Build Docker images
echo "[4/6] Building Docker images..."
echo "This will take 10-15 minutes. Please wait..."
echo ""

if ! docker-compose build; then
    echo "✗ Docker build failed!"
    echo "Troubleshooting:"
    echo "  1. Check Docker is running: systemctl status docker"
    echo "  2. Check disk space: df -h"
    echo "  3. View build logs: docker-compose build --verbose"
    exit 1
fi

echo "✓ Docker images built successfully"
echo ""

# Step 5: Verify images
echo "[5/6] Verifying Docker images..."
echo ""
docker images | grep payment-gateway || {
    echo "✗ No payment-gateway images found!"
    exit 1
}
echo "✓ All images present"
echo ""

# Step 6: Start services
echo "[6/6] Starting services..."
echo "Starting 9 services (api, processor, fraud-detector, notification,"
echo "settlement, mongo-sync, postgres, mongodb, redis)..."
echo ""

docker-compose up -d

echo "Waiting for services to become healthy (30 seconds)..."
sleep 30

echo ""
echo "================================"
echo "Deployment Status"
echo "================================"
docker-compose ps
echo ""

# Verify health
echo "Checking API health..."
HEALTH=$(curl -s http://localhost:5000/health || echo '{"status":"Failed"}')
if echo "$HEALTH" | grep -q "Healthy"; then
    echo "✓ API is healthy"
    echo ""
    echo "================================"
    echo "✅ DEPLOYMENT SUCCESSFUL!"
    echo "================================"
    echo ""
    echo "Next steps:"
    echo "1. Verify all services are 'Up (healthy)'"
    echo "2. Check logs: docker-compose logs --tail=50"
    echo "3. For benchmarks (Day 2):"
    echo "   cd tests/PaymentGateway.Benchmarks"
    echo "   dotnet run -c Release"
    echo ""
    echo "URL: http://192.168.1.61:5000"
    echo ""
    exit 0
else
    echo "⚠️  API health check failed: $HEALTH"
    echo ""
    echo "Troubleshooting:"
    echo "  1. Check Docker: docker-compose ps"
    echo "  2. View logs: docker-compose logs api"
    echo "  3. Wait longer: services take 1-2 minutes to initialize"
    echo "  4. Restart: docker-compose restart api"
    echo ""
    exit 1
fi
