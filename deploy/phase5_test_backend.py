#!/usr/bin/env python3
"""
Phase 5: Backend API Functional Tests
Executa testes completos da API PaymentGateway
"""

import requests
import json
import sys
import time
from datetime import datetime
import uuid

class BackendTester:
    def __init__(self, api_url):
        self.api_url = api_url
        self.results = {
            "timestamp": datetime.now().isoformat(),
            "api_url": api_url,
            "tests": []
        }
        self.merchant_id = None
        self.payment_id = None
        self.session = requests.Session()

    def test_health_check(self):
        """Testa GET /health"""
        test_name = "Health Check"

        try:
            response = self.session.get(f"{self.api_url}/health", timeout=10)

            success = response.status_code == 200
            result = {
                "name": test_name,
                "success": success,
                "status_code": response.status_code
            }

            if success:
                result["response"] = response.json()

            self.results["tests"].append(result)
            return success

        except Exception as e:
            self.results["tests"].append({
                "name": test_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def test_create_merchant(self):
        """Testa POST /api/v1/merchants"""
        test_name = "Create Merchant"

        try:
            payload = {
                "name": f"TestMerchant-{uuid.uuid4().hex[:8]}",
                "email": f"merchant-{uuid.uuid4().hex[:8]}@test.local",
                "website": "https://test.merchant.local"
            }

            response = self.session.post(
                f"{self.api_url}/api/v1/merchants",
                json=payload,
                timeout=10
            )

            success = response.status_code in [200, 201]

            result = {
                "name": test_name,
                "success": success,
                "status_code": response.status_code
            }

            if success:
                data = response.json()
                self.merchant_id = data.get("id")
                result["merchant_id"] = self.merchant_id
                result["response"] = data

            self.results["tests"].append(result)
            return success

        except Exception as e:
            self.results["tests"].append({
                "name": test_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def test_create_payment(self):
        """Testa POST /api/v1/payments"""
        test_name = "Create Payment"

        if not self.merchant_id:
            self.results["tests"].append({
                "name": test_name,
                "success": False,
                "error": "No merchant_id from previous test"
            })
            return False

        try:
            payload = {
                "merchant_id": self.merchant_id,
                "amount": 10000,
                "currency": "BRL",
                "description": f"Test Payment {uuid.uuid4().hex[:8]}",
                "customer_email": "customer@test.local",
                "customer_name": "Test Customer"
            }

            response = self.session.post(
                f"{self.api_url}/api/v1/payments",
                json=payload,
                timeout=10
            )

            success = response.status_code in [200, 201]

            result = {
                "name": test_name,
                "success": success,
                "status_code": response.status_code
            }

            if success:
                data = response.json()
                self.payment_id = data.get("id")
                result["payment_id"] = self.payment_id
                result["response"] = data

            self.results["tests"].append(result)
            return success

        except Exception as e:
            self.results["tests"].append({
                "name": test_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def test_get_payment(self):
        """Testa GET /api/v1/payments/{id}"""
        test_name = "Get Payment by ID"

        if not self.payment_id:
            self.results["tests"].append({
                "name": test_name,
                "success": False,
                "error": "No payment_id from previous test"
            })
            return False

        try:
            response = self.session.get(
                f"{self.api_url}/api/v1/payments/{self.payment_id}",
                timeout=10
            )

            success = response.status_code == 200

            result = {
                "name": test_name,
                "success": success,
                "status_code": response.status_code
            }

            if success:
                result["response"] = response.json()

            self.results["tests"].append(result)
            return success

        except Exception as e:
            self.results["tests"].append({
                "name": test_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def test_list_payments(self):
        """Testa GET /api/v1/payments"""
        test_name = "List Payments"

        try:
            response = self.session.get(
                f"{self.api_url}/api/v1/payments",
                timeout=10
            )

            success = response.status_code == 200

            result = {
                "name": test_name,
                "success": success,
                "status_code": response.status_code
            }

            if success:
                data = response.json()
                result["payment_count"] = len(data.get("items", []))

            self.results["tests"].append(result)
            return success

        except Exception as e:
            self.results["tests"].append({
                "name": test_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def test_payment_flow(self):
        """Testa fluxo completo de pagamento"""
        test_name = "Payment Flow (Create -> Get)"

        try:
            # Create payment
            create_payload = {
                "merchant_id": self.merchant_id or str(uuid.uuid4()),
                "amount": 5000,
                "currency": "BRL",
                "description": "Flow Test",
                "customer_email": "flow@test.local",
                "customer_name": "Flow Test"
            }

            create_response = self.session.post(
                f"{self.api_url}/api/v1/payments",
                json=create_payload,
                timeout=10
            )

            if create_response.status_code not in [200, 201]:
                self.results["tests"].append({
                    "name": test_name,
                    "success": False,
                    "error": f"Create failed: {create_response.status_code}"
                })
                return False

            payment_id = create_response.json().get("id")

            # Wait briefly
            time.sleep(1)

            # Get payment
            get_response = self.session.get(
                f"{self.api_url}/api/v1/payments/{payment_id}",
                timeout=10
            )

            success = get_response.status_code == 200

            result = {
                "name": test_name,
                "success": success,
                "status_code": get_response.status_code
            }

            if success:
                payment = get_response.json()
                result["payment_status"] = payment.get("status", "unknown")

            self.results["tests"].append(result)
            return success

        except Exception as e:
            self.results["tests"].append({
                "name": test_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def test_latency(self):
        """Testa latencia da API"""
        test_name = "API Latency Measurement"

        try:
            import time

            latencies = []

            for _ in range(5):
                start = time.time()
                response = self.session.get(f"{self.api_url}/health", timeout=10)
                latency = (time.time() - start) * 1000  # ms

                if response.status_code == 200:
                    latencies.append(latency)

            if latencies:
                result = {
                    "name": test_name,
                    "success": True,
                    "min_ms": min(latencies),
                    "max_ms": max(latencies),
                    "avg_ms": sum(latencies) / len(latencies),
                    "samples": len(latencies)
                }
            else:
                result = {
                    "name": test_name,
                    "success": False,
                    "error": "No successful latency samples"
                }

            self.results["tests"].append(result)
            return result["success"]

        except Exception as e:
            self.results["tests"].append({
                "name": test_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def run_all(self):
        """Executa todos os testes"""
        print(f"\n{'='*70}")
        print(f"Phase 5: Backend API Tests - {self.api_url}")
        print(f"{'='*70}\n")

        tests = [
            ("Health Check", self.test_health_check),
            ("Create Merchant", self.test_create_merchant),
            ("Create Payment", self.test_create_payment),
            ("Get Payment", self.test_get_payment),
            ("List Payments", self.test_list_payments),
            ("Payment Flow", self.test_payment_flow),
            ("Latency Measurement", self.test_latency),
        ]

        for test_name, test_func in tests:
            print(f"-> {test_name}...", end=" ", flush=True)
            try:
                success = test_func()
                print("[OK]" if success else "[FAIL]")
            except Exception as e:
                print(f"[ERROR]: {e}")

        # Save results
        with open("phase5_results.json", "w") as f:
            json.dump(self.results, f, indent=2)

        print(f"\n[OK] Results saved: phase5_results.json\n")

        return self.results

if __name__ == "__main__":
    api_url = "http://192.168.1.61:5000"

    tester = BackendTester(api_url)
    results = tester.run_all()

    print(f"{'='*70}")
    print("Phase 5 Summary:")
    print(f"{'='*70}")

    passed = sum(1 for t in results.get("tests", []) if t.get("success", False))
    total = len(results.get("tests", []))

    print(f"  Tests passed: {passed}/{total}")

    for test in results.get("tests", []):
        status = "[PASS]" if test.get("success") else "[FAIL]"
        print(f"    {status} - {test['name']}")

    if passed == total:
        print(f"\n[SUCCESS] All backend tests passed!")
    print()
