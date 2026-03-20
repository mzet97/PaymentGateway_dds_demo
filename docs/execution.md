# EXECUTE - Master Deployment Orchestrator

**Status**: READY TO EXECUTE
**Date**: 2026-03-19
**Next Step**: Follow instructions below

---

## IMMEDIATE ACTION (RIGHT NOW)

### Pre-Flight Checklist (2 minutes)

```bash
# From your Windows machine, verify VMs are accessible:

# Test VM1 connectivity
ping 192.168.1.61
# Expected: Reply from 192.168.1.61: bytes=32 time=X ms

# Test VM3 connectivity
ping 192.168.1.63
# Expected: Reply from 192.168.1.63: bytes=32 time=X ms

# Test SSH access
ssh oldds@192.168.1.61 "echo 'VM1 SSH OK'"
# Expected: VM1 SSH OK

ssh oldds@192.168.1.63 "echo 'VM3 SSH OK'"
# Expected: VM3 SSH OK
```

**If all above work**: Proceed to PHASE 1 below
**If any fail**: Check network connectivity before continuing

---

## PHASE 1: DEPLOYMENT (Day 1)

### Duration: 15 minutes active time
### Target: VM1 (192.168.1.61)
### Goal: Deploy 9-service Docker architecture

---

### STEP 1: SSH to VM1
```bash
ssh oldds@192.168.1.61
```

**You are now on VM1. Everything below runs here.**

---

### STEP 2: Prepare Directory
```bash
mkdir -p ~/applications && cd ~/applications
```

---

### STEP 3: Clone Repository

**Option A: Git Clone** (Recommended)
```bash
git clone https://github.com/your-repo/PaymentGateway.git
cd PaymentGateway
```

**Option B: SCP from Windows** (If git not available)
```bash
# From Windows (PowerShell):
scp -r "E:\TI\git\tese\PaymentGateway_dds_demo\*" oldds@192.168.1.61:~/applications/PaymentGateway/

# Back on VM1:
cd ~/applications/PaymentGateway
```

---

### STEP 4: Run Deployment Script
```bash
# Make script executable
chmod +x scripts/deploy-vm1.sh

# Run deployment
./scripts/deploy-vm1.sh
```

**What the script does:**
1. Verifies prerequisites
2. Creates .env from template
3. **Waits for you to edit .env** -- IMPORTANT STEP
4. Builds Docker images (10-15 min)
5. Starts services
6. Verifies health

---

### STEP 5: Edit .env (When Script Prompts)

**When you see this:**
```
IMPORTANT: Edit .env with production passwords:
   nano .env

Press ENTER after editing .env, or Ctrl+C to cancel:
```

**Do this:**
```bash
# Edit the file
nano .env

# Find these lines (use Ctrl+W to search):
POSTGRES_PASSWORD=Admin@123
MONGO_PASSWORD=Admin@123
REDIS_PASSWORD=Admin@123

# Change them to secure passwords:
POSTGRES_PASSWORD=MySecurePassword123!
MONGO_PASSWORD=MySecurePassword456!
REDIS_PASSWORD=MySecurePassword789!

# Save: Ctrl+O → Enter → Ctrl+X

# Press ENTER to continue script
```

---

### STEP 6: Wait for Docker Build

**Script will show:**
```
[4/6] Building Docker images...
This will take 10-15 minutes. Please wait...
```

**What's happening:**
- Building all Docker images
- Downloading base images (Alpine Linux)
- Compiling .NET 8 services
- Creating final optimized images

**Grab a coffee! This takes 10-15 minutes.**

---

### STEP 7: Verify Deployment Complete

**Script will show:**
```
================================
DEPLOYMENT SUCCESSFUL!
================================

All services running and healthy!

docker-compose ps:
NAME                      STATUS
payment-gateway-api       Up (healthy)
payment-processor         Up
fraud-detector            Up
...
```

**If you see DEPLOYMENT SUCCESSFUL**, you're done with Phase 1!

---

### VERIFY PHASE 1 SUCCESS

Run these commands to confirm:

```bash
# Check all services
docker-compose ps
# All should show "Up"

# Test API
curl http://localhost:5000/health
# Should return: {"status":"Healthy",...}

# Check DDS
docker-compose logs processor | grep subscribed
# Should show subscription success
```

---

## PHASE 2: BENCHMARKS (Day 2)

### Duration: 45 min - 2 hours
### Target: VM1 (192.168.1.61)
### Goal: Validate Phase 1-3 code optimizations

---

### WAIT FOR DAY 2, THEN:

```bash
# SSH to VM1
ssh oldds@192.168.1.61

# Navigate to project
cd ~/applications/PaymentGateway

# Run benchmarks
chmod +x scripts/benchmark-phase4.sh
./scripts/benchmark-phase4.sh

# This will:
# 1. Build benchmarks
# 2. Run all 38 tests (45 min - 2 hours)
# 3. Save results to BenchmarkDotNet.Artifacts/results/

# When complete, review results:
cat tests/PaymentGateway.Benchmarks/BenchmarkDotNet.Artifacts/results/results.txt
```

---

## PHASE 3: LOAD TESTS (Days 3-4)

### Duration: 2-3 hours per day
### Target: VM3 (192.168.1.63)
### Goal: Validate system performance under load

---

### WAIT FOR DAY 3, THEN:

```bash
# SSH to VM3
ssh oldds@192.168.1.63

# Navigate to project
cd ~/applications/PaymentGateway

# Run load tests
chmod +x scripts/test-phase6.sh
./scripts/test-phase6.sh http://192.168.1.61:5000

# Script will run 3 load profiles:
# 1. Baseline (1 VU, 30 sec) - expect p99 <200ms
# 2. Light load (10 VU, 60 sec) - expect RPS >1000
# 3. Medium load (100 VU, 120 sec) - expect RPS >5000

# Results saved to: results-baseline.json, results-light.json, results-medium.json
```

---

## PHASE 4: FINAL REPORT (Day 5)

### Duration: 1-2 hours
### Target: Any machine
### Goal: Compile results and sign off

---

### WAIT FOR DAY 5, THEN:

```bash
# Collect results from VM1
ssh oldds@192.168.1.61 "mkdir -p ~/final-report && \
  cp -r ~/benchmarks-phase4 ~/final-report/ && \
  cp -r ~/loadtest-results ~/final-report/"

# Download results to your machine
scp -r oldds@192.168.1.61:~/final-report ./PaymentGateway-Results/

# Generate final report (see RESULTS.md template)
# Verify all success criteria met
# Obtain approval signatures
```

---

## SUCCESS CRITERIA

### Phase 1 Deployment
- All 9 services "Up (healthy)"
- API responds to health check
- DDS communication active
- No critical errors in logs

### Phase 2 Benchmarks
- 20+/38 benchmarks passed
- Lock-free: 5-10x faster at 10+ threads
- Circuit breaker: <0.5ms overhead
- Health check: <1% cost

### Phase 3 Load Tests
- 1 VU: p99 <200ms, RPS >100
- 10 VU: RPS >1000, p95 <200ms
- 100 VU: RPS >5000, error <2%

### Phase 4 Final
- All results documented
- Report generated
- Approval obtained
- Status: PRODUCTION READY

---

## TROUBLESHOOTING

### SSH Connection Fails
```bash
# Check VM is on network
ping 192.168.1.61

# Check username (should be "oldds", not "root")
ssh oldds@192.168.1.61

# If still fails, check:
# 1. VM IP address
# 2. Network connectivity
# 3. Firewall rules
# 4. SSH service on VM
```

### Docker Build Fails
```bash
# Install dependencies
sudo apt install -y build-essential cmake libddsc-dev openssl-dev

# Try again
docker-compose build --verbose
```

### Services Won't Start
```bash
# Check logs
docker-compose logs

# See specific service
docker-compose logs api

# Restart all
docker-compose down
docker-compose up -d
```

### API Health Check Fails
```bash
# Wait longer (services take 1-2 minutes)
sleep 60
curl http://localhost:5000/health

# Or restart API
docker-compose restart api
```

### Benchmark Build Fails
```bash
# Ensure .NET 8 SDK
dotnet --version

# Restore and rebuild
cd tests/PaymentGateway.Benchmarks
dotnet restore
dotnet build -c Release
```

---

## PROGRESS TRACKING

**Track your progress here:**

### Day 1 - Deployment
- [ ] SSH to VM1 successful
- [ ] Repository cloned
- [ ] .env edited
- [ ] Docker images built
- [ ] Services started
- [ ] All 9 services healthy
- [ ] API health check passes

### Day 2 - Benchmarks
- [ ] Benchmarks build
- [ ] 38 benchmarks run
- [ ] Results generated
- [ ] Lock-free improvement confirmed
- [ ] Results saved

### Day 3 - Load Tests 1-2
- [ ] VM3 connectivity verified
- [ ] 1 VU test passes
- [ ] 10 VU test passes
- [ ] Results saved

### Day 4 - Load Tests 3 + Resilience
- [ ] 100 VU test passes
- [ ] DDS failure test passes
- [ ] Recovery verified
- [ ] Results saved

### Day 5 - Final Report
- [ ] Results collected
- [ ] Report generated
- [ ] All criteria met
- [ ] Approval signed

---

## SUPPORT

### Quick Commands Reference

**VM1 Deployment**:
```bash
ssh oldds@192.168.1.61
cd ~/applications/PaymentGateway
./scripts/deploy-vm1.sh
```

**VM1 Check Status**:
```bash
docker-compose ps
docker-compose logs --tail=50 api
curl http://localhost:5000/health
```

**VM1 Benchmarks**:
```bash
cd tests/PaymentGateway.Benchmarks
./scripts/benchmark-phase4.sh
```

**VM3 Load Tests**:
```bash
ssh oldds@192.168.1.63
cd ~/applications/PaymentGateway
./scripts/test-phase6.sh http://192.168.1.61:5000
```

---

## What You'll Achieve

After 5 days, you will have:

**Validated Code Optimizations**
- 5-10x lock-free concurrency improvement
- <0.5ms circuit breaker overhead
- <1% health check monitoring cost

**Measured System Performance**
- 1000+ RPS at 10 concurrent clients
- <200ms p99 latency under baseline load
- 5000+ RPS at 100 concurrent clients

**Confirmed Resilience**
- Automatic failure detection
- Graceful degradation
- Self-recovery from failures

**Production Sign-Off**
- All metrics documented
- Success criteria verified
- Team approval obtained
- System declared production-ready

---

## START NOW

### Your Next Step (Right Now):

```bash
# 1. Open terminal/PowerShell
# 2. SSH to VM1
ssh oldds@192.168.1.61

# 3. Prepare directory
mkdir -p ~/applications && cd ~/applications

# 4. Clone repository (or use SCP)
git clone https://github.com/your-repo/PaymentGateway.git
cd PaymentGateway

# 5. Run deployment script
chmod +x scripts/deploy-vm1.sh
./scripts/deploy-vm1.sh

# Follow prompts:
# - Edit .env when asked
# - Press ENTER after editing
# - Wait for build (10-15 min)
# - Verify deployment successful
```

---

## Timeline

| When | What | Duration |
|------|------|----------|
| **NOW** | Deploy to VM1 | 15 min |
| **Tomorrow (Day 2)** | Run benchmarks | 45 min - 2h |
| **Day 3** | Run load tests 1-2 | 1-2 hours |
| **Day 4** | Run load tests 3 + resilience | 2-3 hours |
| **Day 5** | Final report | 1-2 hours |

**Total**: 5 days | **Active**: 8-10 hours | **Passive**: Rest is waiting

---

## YOU ARE HERE

```
Phase 1-3: Code optimizations     COMPLETE
Phase 4-6: Infrastructure        READY
Automation scripts               READY
Documentation                   COMPLETE

NEXT STEP: Execute Phase 1 deployment (NOW)

ssh oldds@192.168.1.61
./scripts/deploy-vm1.sh
```

---

**Status**: Ready | **Blocker**: None | **Action**: Deploy now

**Go!**
