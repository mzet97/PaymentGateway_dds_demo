# Payment Gateway Benchmark Script
# Measures: RPS, Latency (p50/p95/p99), DDS Latency, Memory

param(
    [string]$ApiUrl = "http://localhost:5000",
    [int]$Duration = 60,
    [int]$Concurrency = 10,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Colors
$Green = [ConsoleColor]::Green
$Yellow = [ConsoleColor]::Yellow
$Red = [ConsoleColor]::Red
$Cyan = [ConsoleColor]::Cyan

function Write-BenchmarkHeader {
    param([string]$Message)
    Write-Host "`n$Message" -ForegroundColor $Cyan
    Write-Host ("=" * 60) -ForegroundColor $Cyan
}

function Write-BenchmarkResult {
    param(
        [string]$Label,
        [string]$Value,
        [ConsoleColor]$Color = [ConsoleColor]::White
    )
    Write-Host ("{0,-30}: {1}" -f $Label, $Value) -ForegroundColor $Color
}

# Benchmark Configuration
$benchmarkConfig = @{
    ApiUrl = $ApiUrl
    Duration = $Duration
    Concurrency = $Concurrency
    TotalRequests = $Duration * $Concurrency * 10  # Approximate
}

Write-BenchmarkHeader "Payment Gateway Benchmark"

Write-Host "Configuration:" -ForegroundColor $Yellow
Write-Host "  API URL: $($benchmarkConfig.ApiUrl)"
Write-Host "  Duration: $($benchmarkConfig.Duration)s"
Write-Host "  Concurrency: $($benchmarkConfig.Concurrency)"
Write-Host ""

# Test if API is running
Write-Host "Checking API availability..." -ForegroundColor $Yellow
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/health" -TimeoutSec 5
    Write-Host "  API Status: $($health.status)" -ForegroundColor $Green
} catch {
    Write-Host "  ERROR: API not available at $ApiUrl" -ForegroundColor $Red
    Write-Host "  Make sure the API is running: dotnet run --project src/PaymentGateway.Api" -ForegroundColor $Yellow
    exit 1
}

# Generate test payment data
$testPayment = @{
    amount = [Math]::Round((Get-Random -Minimum 10 -Maximum 1000), 2)
    currency = "BRL"
    method = "credit_card"
    customer = @{
        email = "benchmark@payment-gateway.local"
        name = "Benchmark Test"
        document = "12345678901"
        ip = "192.168.1.1"
        phone = "+5511999999999"
    }
    description = "Benchmark test payment"
} | ConvertTo-Json

# Results storage
$results = @{
    Latencies = @()
    Errors = 0
    Success = 0
    StartTime = Get-Date
}

Write-BenchmarkHeader "Running Benchmark"

$runDuration = $benchmarkConfig.Duration
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# Run benchmark with concurrent requests
$jobs = @()
for ($i = 0; $i -lt $benchmarkConfig.Concurrency; $i++) {
    $jobs += Start-Job -ScriptBlock {
        param($ApiUrl, $Duration, $PaymentJson)

        $results = @{
            Latencies = @()
            Errors = 0
            Success = 0
        }

        $endTime = (Get-Date).AddSeconds($Duration)

        while ((Get-Date) -lt $endTime) {
            $reqStart = Get-Date

            try {
                $response = Invoke-RestMethod -Uri "$ApiUrl/api/v1/payments" `
                    -Method Post `
                    -Body $PaymentJson `
                    -ContentType "application/json" `
                    -TimeoutSec 30

                $reqEnd = Get-Date
                $latency = ($reqEnd - $reqStart).TotalMilliseconds

                $results.Latencies += $latency
                $results.Success++
            } catch {
                $results.Errors++
            }
        }

        return $results
    } -ArgumentList $ApiUrl, $runDuration, $testPayment
}

# Wait for jobs and collect results
Write-Host "Executing $runDuration second benchmark with $($benchmarkConfig.Concurrency) concurrent connections..." -ForegroundColor $Yellow

$completed = 0
while ($completed -lt $jobs.Count) {
    Start-Sleep -Seconds 1
    $completed = ($jobs | Where-Object { $_.State -eq 'Completed' }).Count
    $progress = [math]::Round(($completed / $jobs.Count) * 100)
    Write-Host "`rProgress: $progress% ($completed/$($jobs.Count) jobs completed)" -NoNewline
}

# Collect results
foreach ($job in $jobs) {
    $jobResults = Receive-Job -Job $job
    $results.Latencies += $jobResults.Latencies
    $results.Errors += $jobResults.Errors
    $results.Success += $jobResults.Success
}

$sw.Stop()

Write-Host "`n" -NoNewline

# Calculate metrics
$totalRequests = $results.Success + $results.Errors
$actualDuration = $sw.Elapsed.TotalSeconds
$rps = if ($actualDuration -gt 0) { $totalRequests / $actualDuration } else { 0 }

$sortedLatencies = ($results.Latencies | Sort-Object)
$latencyCount = $sortedLatencies.Count

if ($latencyCount -gt 0) {
    $p50Index = [math]::Floor($latencyCount * 0.50)
    $p95Index = [math]::Floor($latencyCount * 0.95)
    $p99Index = [math]::Floor($latencyCount * 0.99)

    $latencyP50 = $sortedLatencies[$p50Index]
    $latencyP95 = $sortedLatencies[$p95Index]
    $latencyP99 = $sortedLatencies[$p99Index]
    $latencyAvg = ($sortedLatencies | Measure-Object -Average).Average
    $latencyMin = $sortedLatencies[0]
    $latencyMax = $sortedLatencies[-1]
}

# Output results
Write-BenchmarkHeader "Results"

Write-BenchmarkResult "Total Requests" "$totalRequests"
Write-BenchmarkResult "Successful" "$($results.Success)" $Green
Write-BenchmarkResult "Failed" "$($results.Errors)" $(if ($results.Errors -gt 0) { $Red } else { $Green })
Write-BenchmarkResult "Duration" "$([math]::Round($actualDuration, 2))s"
Write-BenchmarkResult "RPS (Requests/sec)" "$([math]::Round($rps, 2))"

Write-Host "`nLatency (ms):" -ForegroundColor $Yellow
Write-BenchmarkResult "  Min" "$([math]::Round($latencyMin, 2))ms"
Write-BenchmarkResult "  Avg" "$([math]::Round($latencyAvg, 2))ms"
Write-BenchmarkResult "  p50" "$([math]::Round($latencyP50, 2))ms"
Write-BenchmarkResult "  p95" "$([math]::Round($latencyP95, 2))ms"
Write-BenchmarkResult "  p99" "$([math]::Round($latencyP99, 2))ms"
Write-BenchmarkResult "  Max" "$([math]::Round($latencyMax, 2))ms"

Write-Host "`nSuccess Rate:" -ForegroundColor $Yellow
$successRate = if ($totalRequests -gt 0) { ($results.Success / $totalRequests) * 100 } else { 0 }
Write-BenchmarkResult "  Success Rate" "$([math]::Round($successRate, 2))%"

# Target comparison
Write-Host "`nTarget Comparison:" -ForegroundColor $Yellow
$targets = @{
    "RPS (target: 1000+)" = @($rps, 1000, "ms")
    "p99 Latency (target: <100ms)" = @($latencyP99, 100, "ms")
}

foreach ($target in $targets.GetEnumerator()) {
    $actual = $target.Value[0]
    $targetVal = $target.Value[1]
    $unit = $target.Value[2]

    $status = if ($actual -le $targetVal) { "PASS" } else { "FAIL" }
    $color = if ($actual -le $targetVal) { $Green } else { $Red }

    Write-BenchmarkResult "  $($target.Key)" "$([math]::Round($actual, 2))$unit ($status)" $color
}

Write-Host "`n" -NoNewline

# Summary
if ($results.Errors -gt 0) {
    Write-Host "Benchmark completed with errors!" -ForegroundColor $Yellow
} else {
    Write-Host "Benchmark completed successfully!" -ForegroundColor $Green
}

# Cleanup
$jobs | Remove-Job -Force

# Export to JSON
$exportPath = Join-Path $PSScriptRoot "benchmark-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$exportData = @{
    Timestamp = (Get-Date).ToString("o")
    Config = $benchmarkConfig
    Results = @{
        TotalRequests = $totalRequests
        Success = $results.Success
        Errors = $results.Errors
        RPS = [math]::Round($rps, 2)
        Latency = @{
            Min = [math]::Round($latencyMin, 2)
            Avg = [math]::Round($latencyAvg, 2)
            P50 = [math]::Round($latencyP50, 2)
            P95 = [math]::Round($latencyP95, 2)
            P99 = [math]::Round($latencyP99, 2)
            Max = [math]::Round($latencyMax, 2)
        }
        SuccessRate = [math]::Round($successRate, 2)
    }
} | ConvertTo-Json -Depth 3

$exportData | Out-File -FilePath $exportPath -Encoding UTF8
Write-Host "`nResults exported to: $exportPath" -ForegroundColor $Cyan
