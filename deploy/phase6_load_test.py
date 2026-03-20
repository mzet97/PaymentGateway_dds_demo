#!/usr/bin/env python3
"""
Phase 6: Load Testing with k6
Executa testes de carga via k6 na VM3
"""

import subprocess
import json
import sys
from datetime import datetime
from pathlib import Path

class LoadTester:
    def __init__(self, host, username, password, api_url):
        self.host = host
        self.username = username
        self.password = password
        self.api_url = api_url
        self.results = {
            "timestamp": datetime.now().isoformat(),
            "host": host,
            "api_url": api_url,
            "scenarios": []
        }

    def execute_ssh(self, command):
        """Executa comando via SSH (usando sshpass)"""
        try:
            ssh_cmd = f'sshpass -p "{self.password}" ssh -o StrictHostKeyChecking=no {self.username}@{self.host} "{command}"'
            result = subprocess.run(ssh_cmd, shell=True, capture_output=True, text=True, timeout=600)
            return result.returncode == 0, result.stdout, result.stderr
        except subprocess.TimeoutExpired:
            return False, "", "Command timeout"
        except Exception as e:
            return False, "", str(e)

    def upload_k6_script(self):
        """Faz upload do script k6"""
        step_name = "Upload k6 Test Script"

        try:
            # Create k6 script locally
            k6_script = '''import http from 'k6/http';
import { check, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

const BASE_URL = __ENV.API_URL || 'http://192.168.1.61:5000';

export const errorRate = new Rate('errors');
export const http_req_duration = new Trend('http_req_duration');
export const payments_created = new Counter('payments_created');

export const options = {
  vus: __ENV.VUS || 1,
  duration: __ENV.DURATION || '30s',
  thresholds: {
    errors: ['rate<0.1'],
    'http_req_duration': ['p(95)<500', 'p(99)<1000'],
  },
};

export default function () {
  group('Health Check', () => {
    const res = http.get(`${BASE_URL}/health`);
    check(res, {
      'status is 200': (r) => r.status === 200,
    }) || errorRate.add(1);
  });

  group('Create Payment', () => {
    const payload = JSON.stringify({
      amount: Math.floor(Math.random() * 100000) + 1000,
      currency: 'BRL',
      description: `Load Test ${Date.now()}`,
      customer_email: `customer-${Date.now()}@test.local`,
      customer_name: 'Load Test Customer',
    });

    const params = {
      headers: { 'Content-Type': 'application/json' },
    };

    const res = http.post(`${BASE_URL}/api/v1/payments`, payload, params);
    check(res, {
      'status is 200 or 201': (r) => r.status === 200 || r.status === 201,
    }) || errorRate.add(1);

    if (res.status === 200 || res.status === 201) {
      payments_created.add(1);
    }
  });

  group('Get Payments', () => {
    const res = http.get(`${BASE_URL}/api/v1/payments`);
    check(res, {
      'status is 200': (r) => r.status === 200,
    }) || errorRate.add(1);
  });
}
'''

            with open("/tmp/load-test.js", "w") as f:
                f.write(k6_script)

            # Upload script
            scp_cmd = f'sshpass -p "{self.password}" scp -o StrictHostKeyChecking=no /tmp/load-test.js {self.username}@{self.host}:~/'
            result = subprocess.run(scp_cmd, shell=True, capture_output=True, text=True, timeout=30)

            if result.returncode == 0:
                subprocess.run('rm /tmp/load-test.js', shell=True)
                return True, "k6 script uploaded"
            else:
                return False, result.stderr[:200]

        except Exception as e:
            return False, str(e)[:200]

    def run_load_test_scenario(self, name, vus, duration):
        """Executa um cenario de teste de carga"""
        scenario_name = f"Load Test: {name} ({vus} VU, {duration}s)"

        try:
            print(f"    [Running {name} scenario...]", flush=True)

            cmd = f'cd ~ && k6 run load-test.js --vus {vus} --duration {duration}s --env API_URL={self.api_url} 2>&1'
            success, out, err = self.execute_ssh(cmd)

            # Parse k6 output
            metrics = self._parse_k6_output(out)

            result = {
                "name": scenario_name,
                "success": success,
                "vus": vus,
                "duration_seconds": duration,
                "metrics": metrics
            }

            self.results["scenarios"].append(result)

            return success

        except Exception as e:
            self.results["scenarios"].append({
                "name": scenario_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def _parse_k6_output(self, output):
        """Extrai metricas do output k6"""
        metrics = {}

        # Extract key metrics from k6 output
        lines = output.split('\n')
        for line in lines:
            if 'http_req_duration' in line:
                metrics['http_req_duration'] = line.strip()
            elif 'payments_created' in line:
                metrics['payments_created'] = line.strip()
            elif 'errors' in line:
                metrics['errors'] = line.strip()
            elif 'RPS' in line or 'req/s' in line:
                metrics['rps'] = line.strip()

        return metrics

    def run_all(self):
        """Executa todos os cenarios de teste"""
        print(f"\n{'='*70}")
        print(f"Phase 6: Load Testing - {self.host}")
        print(f"{'='*70}\n")

        # Upload script
        print(f"-> Upload k6 Script...", end=" ", flush=True)
        success, msg = self.upload_k6_script()
        print("[OK]" if success else "[FAIL]")

        if not success:
            self.results["upload_error"] = msg
            self._save_results()
            return self.results

        # Run scenarios
        scenarios = [
            ("Baseline (1 VU)", 1, 30),
            ("Light Load (10 VU)", 10, 60),
            ("Heavy Load (100 VU)", 100, 120),
        ]

        for name, vus, duration in scenarios:
            print(f"-> {name}...", end=" ", flush=True)
            success = self.run_load_test_scenario(name, vus, duration)
            print("[OK]" if success else "[FAIL]")

        self._save_results()

        return self.results

    def _save_results(self):
        """Salva resultados"""
        with open("phase6_results.json", "w") as f:
            json.dump(self.results, f, indent=2)

        print(f"\n[OK] Results saved: phase6_results.json\n")

if __name__ == "__main__":
    # Load testing from VM3 targeting VM1 API
    tester = LoadTester(
        host="192.168.1.63",
        username="oldds",
        password="Admin@123",
        api_url="http://192.168.1.61:5000"
    )

    results = tester.run_all()

    print(f"{'='*70}")
    print("Phase 6 Summary:")
    print(f"{'='*70}")

    passed = sum(1 for s in results.get("scenarios", []) if s.get("success", False))
    total = len(results.get("scenarios", []))

    print(f"  Scenarios completed: {passed}/{total}")

    for scenario in results.get("scenarios", []):
        status = "[PASS]" if scenario.get("success") else "[FAIL]"
        name = scenario.get("name", "Unknown")
        print(f"    {status} - {name}")

        # Print metrics if available
        metrics = scenario.get("metrics", {})
        if metrics:
            for metric_name, metric_value in metrics.items():
                print(f"      {metric_name}: {metric_value[:80]}")

    print()
