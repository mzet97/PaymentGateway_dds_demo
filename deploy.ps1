# ============================================
# PaymentGateway - Quick Deploy Script
# ============================================
# Run from: Windows PowerShell
# Target: VM1 (192.168.1.61)

param(
    [string]$RepoUrl = "https://github.com/your-repo/PaymentGateway.git",
    [string]$VMUser = "oldds",
    [string]$VMIP = "192.168.1.61"
)

Write-Host "================================" -ForegroundColor Green
Write-Host "PaymentGateway - Quick Deploy"
Write-Host "================================" -ForegroundColor Green
Write-Host ""

# Verify SSH connectivity
Write-Host "[1/4] Testing SSH connectivity to VM1..." -ForegroundColor Yellow
try {
    $null = ssh $VMUser@$VMIP "echo 'SSH OK'"
    Write-Host "✓ SSH connection successful" -ForegroundColor Green
} catch {
    Write-Host "✗ Cannot connect to VM1!" -ForegroundColor Red
    Write-Host "  Check: ping $VMIP"
    exit 1
}

Write-Host ""
Write-Host "[2/4] Preparing deployment..." -ForegroundColor Yellow

$deployScript = @"
#!/bin/bash
set -e

mkdir -p ~/applications
cd ~/applications

# Clone or update repository
if [ -d "PaymentGateway" ]; then
    echo "Repository exists, pulling latest..."
    cd PaymentGateway
    git pull origin main 2>/dev/null || true
else
    echo "Cloning repository..."
    git clone "$RepoUrl" PaymentGateway
    cd PaymentGateway
fi

# Run deployment
echo "Starting deployment..."
chmod +x scripts/deploy-vm1.sh
./scripts/deploy-vm1.sh
"@

Write-Host "✓ Deployment script prepared" -ForegroundColor Green
Write-Host ""

Write-Host "[3/4] Executing deployment on VM1..." -ForegroundColor Yellow
Write-Host "  (This will take 15-20 minutes)" -ForegroundColor Cyan
Write-Host ""

# Execute deployment via SSH
$deployScript | ssh $VMUser@$VMIP bash

Write-Host ""
Write-Host "[4/4] Verifying deployment..." -ForegroundColor Yellow

# Check if services are running
$status = ssh $VMUser@$VMIP "cd ~/applications/PaymentGateway && docker-compose ps | grep 'Up' | wc -l"

if ($status -ge 9) {
    Write-Host "================================" -ForegroundColor Green
    Write-Host "✅ DEPLOYMENT SUCCESSFUL!" -ForegroundColor Green
    Write-Host "================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "All 9 services are running" -ForegroundColor Green
    Write-Host ""
    Write-Host "Test API: curl http://192.168.1.61:5000/health" -ForegroundColor Cyan
    Write-Host ""
} else {
    Write-Host "⚠️  Deployment may have issues" -ForegroundColor Yellow
    Write-Host "Check logs: ssh $VMUser@$VMIP 'cd ~/applications/PaymentGateway && docker-compose logs'" -ForegroundColor Yellow
}
