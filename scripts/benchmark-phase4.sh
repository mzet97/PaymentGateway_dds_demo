#!/bin/bash
# ============================================
# Phase 4: Run Benchmarks
# ============================================
# Run on: VM1 (192.168.1.61)
# Time: 45 minutes - 2 hours
# Purpose: Validate Phase 1-3 optimizations

set -e

echo "================================"
echo "Phase 4: Running Benchmarks"
echo "================================"
echo "Target: VM1 (192.168.1.61)"
echo "Time: 45 min - 2 hours"
echo ""

# Verify we're in the right directory
if [ ! -f "CycloneDds.NET.sln" ]; then
    echo "❌ Error: Not in PaymentGateway root directory"
    echo "Run from: ~/applications/PaymentGateway"
    exit 1
fi

cd tests/PaymentGateway.Benchmarks

echo "[1/3] Building benchmarks (Release mode)..."
echo ""

# Build in Release mode (optimized)
if ! dotnet build -c Release; then
    echo "❌ Build failed!"
    echo ""
    echo "Troubleshooting:"
    echo "  1. Check .NET 8 SDK: dotnet --version"
    echo "  2. Restore packages: dotnet restore"
    echo "  3. Check CycloneDDS: dpkg -l | grep cyclonedds"
    echo ""
    exit 1
fi

echo "✓ Build successful"
echo ""

echo "[2/3] Running benchmarks..."
echo "This will take 45 minutes to 2 hours depending on system load."
echo "Results will be saved to: BenchmarkDotNet.Artifacts/results/"
echo ""
echo "⏱️  Starting benchmark run..."
echo ""

# Run benchmarks (this is the long-running step)
if ! dotnet run -c Release; then
    echo "❌ Benchmarks failed!"
    echo ""
    echo "Troubleshooting:"
    echo "  1. Check logs: cat BenchmarkDotNet.Artifacts/results/*.log"
    echo "  2. Verify no system resource issues: free -h && df -h"
    echo "  3. Try smaller subset: --filter '*JsonSerialize*'"
    exit 1
fi

echo ""
echo "[3/3] Processing results..."
echo ""

# Collect results
if [ -d "BenchmarkDotNet.Artifacts/results" ]; then
    echo "Results directory:"
    ls -lh BenchmarkDotNet.Artifacts/results/ | head -20
    echo ""
else
    echo "⚠️  Results directory not found"
fi

echo "================================"
echo "✅ BENCHMARKS COMPLETE"
echo "================================"
echo ""
echo "Results saved to:"
echo "  BenchmarkDotNet.Artifacts/results/"
echo ""
echo "Key files:"
ls -1 BenchmarkDotNet.Artifacts/results/*.md 2>/dev/null || echo "  (markdown reports not found yet)"
echo ""
echo "Next steps:"
echo "1. Review results: cat BenchmarkDotNet.Artifacts/results/results.txt"
echo "2. Check specific benchmarks:"
echo "   - Lock-free: grep -A5 'ConcurrentDict' results.txt"
echo "   - Circuit breaker: grep -A5 'CircuitBreaker' results.txt"
echo "   - JSON: grep -A5 'Json' results.txt"
echo "   - Health: grep -A5 'Health' results.txt"
echo ""
echo "3. Save results for Day 5 report:"
echo "   cp -r BenchmarkDotNet.Artifacts /tmp/phase4-results-$(date +%Y%m%d)"
echo ""
echo "Expected improvements:"
echo "  ✓ Lock-free collections: 5-10x faster at 10+ threads"
echo "  ✓ Circuit breaker CLOSED: <0.5ms overhead"
echo "  ✓ Circuit breaker OPEN: <0.1ms (in-memory fallback)"
echo "  ✓ Health check: <1% CPU cost"
echo ""
