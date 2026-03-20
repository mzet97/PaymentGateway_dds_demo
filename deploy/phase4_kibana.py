#!/usr/bin/env python3
"""
Phase 4: Kibana Setup
Configura data views e dashboards no Kibana
"""

import requests
import json
import sys
import time
from datetime import datetime
from urllib3.exceptions import InsecureRequestWarning

# Suppress SSL warnings for self-signed certificates
requests.packages.urllib3.disable_warnings(InsecureRequestWarning)

class KibanaSetup:
    def __init__(self, kibana_url, username="elastic", password=""):
        self.kibana_url = kibana_url
        self.username = username
        self.password = password
        self.results = {
            "timestamp": datetime.now().isoformat(),
            "kibana_url": kibana_url,
            "steps": []
        }
        self.session = self._create_session()

    def _create_session(self):
        """Cria sessao HTTP com autenticacao"""
        session = requests.Session()
        if self.username:
            session.auth = (self.username, self.password)
        session.verify = False
        return session

    def create_data_view(self, name, index_pattern):
        """Cria data view no Kibana"""
        step_name = f"Create Data View: {name}"

        try:
            # First check if data view exists
            url = f"{self.kibana_url}/api/data_views/data_views"
            headers = {
                "kbn-xsrf": "true",
                "Content-Type": "application/json"
            }

            # Get existing data views
            response = self.session.get(url, headers=headers, timeout=10)

            # Create new data view
            create_url = f"{self.kibana_url}/api/data_views/data_views"
            payload = {
                "data_view": {
                    "title": index_pattern,
                    "name": name,
                    "timeFieldName": "@timestamp"
                }
            }

            response = self.session.post(create_url, json=payload, headers=headers, timeout=30)

            if response.status_code in [200, 201]:
                self.results["steps"].append({
                    "name": step_name,
                    "success": True,
                    "index_pattern": index_pattern
                })
                return True
            else:
                # Data view might already exist (409), which is OK
                if response.status_code == 409:
                    self.results["steps"].append({
                        "name": step_name,
                        "success": True,
                        "message": "Data view already exists"
                    })
                    return True

                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": f"HTTP {response.status_code}: {response.text[:200]}"
                })
                return False

        except Exception as e:
            self.results["steps"].append({
                "name": step_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def create_dashboard(self):
        """Cria dashboard no Kibana"""
        step_name = "Create PaymentGateway Dashboard"

        try:
            dashboard_url = f"{self.kibana_url}/api/saved_objects/dashboard"
            headers = {
                "kbn-xsrf": "true",
                "Content-Type": "application/json"
            }

            dashboard_payload = {
                "title": "PaymentGateway Overview",
                "description": "Payment Gateway System Monitoring",
                "panels": [],
                "refreshInterval": {
                    "pause": False,
                    "value": 30000
                },
                "timeRestore": True,
                "timeFrom": "now-7d",
                "timeTo": "now",
                "version": 1
            }

            response = self.session.post(
                dashboard_url,
                json=dashboard_payload,
                headers=headers,
                timeout=30
            )

            if response.status_code in [200, 201]:
                dashboard_id = response.json().get("id", "unknown")
                self.results["steps"].append({
                    "name": step_name,
                    "success": True,
                    "dashboard_id": dashboard_id,
                    "dashboard_url": f"{self.kibana_url}/app/dashboards#/view/{dashboard_id}"
                })
                return True
            elif response.status_code == 409:
                # Dashboard already exists
                self.results["steps"].append({
                    "name": step_name,
                    "success": True,
                    "message": "Dashboard already exists"
                })
                return True
            else:
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": f"HTTP {response.status_code}: {response.text[:200]}"
                })
                return False

        except Exception as e:
            self.results["steps"].append({
                "name": step_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def create_visualization(self, name, index_pattern, viz_type="line"):
        """Cria visualizacao no Kibana"""
        step_name = f"Create Visualization: {name}"

        try:
            viz_url = f"{self.kibana_url}/api/saved_objects/visualization"
            headers = {
                "kbn-xsrf": "true",
                "Content-Type": "application/json"
            }

            viz_payload = {
                "title": name,
                "type": viz_type,
                "params": {},
                "kibanaSavedObjectMeta": {
                    "searchSourceJSON": json.dumps({
                        "index": index_pattern,
                        "filter": [],
                        "query": {
                            "match_all": {}
                        }
                    })
                }
            }

            response = self.session.post(
                viz_url,
                json=viz_payload,
                headers=headers,
                timeout=30
            )

            if response.status_code in [200, 201]:
                self.results["steps"].append({
                    "name": step_name,
                    "success": True,
                    "visualization_id": response.json().get("id", "unknown")
                })
                return True
            elif response.status_code == 409:
                self.results["steps"].append({
                    "name": step_name,
                    "success": True,
                    "message": "Visualization already exists"
                })
                return True
            else:
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": f"HTTP {response.status_code}"
                })
                return False

        except Exception as e:
            self.results["steps"].append({
                "name": step_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def test_connectivity(self):
        """Testa conectividade com Kibana"""
        step_name = "Test Kibana Connectivity"

        try:
            url = f"{self.kibana_url}/api/status"
            response = self.session.get(url, timeout=10)

            if response.status_code == 200:
                status = response.json().get("state", "unknown")
                self.results["steps"].append({
                    "name": step_name,
                    "success": True,
                    "kibana_status": status
                })
                return True
            else:
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": f"HTTP {response.status_code}"
                })
                return False

        except Exception as e:
            self.results["steps"].append({
                "name": step_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def run_all(self):
        """Executa todas as etapas de setup Kibana"""
        print(f"\n{'='*70}")
        print(f"Phase 4: Kibana Setup - {self.kibana_url}")
        print(f"{'='*70}\n")

        steps = [
            ("Test Kibana Connectivity", self.test_connectivity),
            ("Create Logs Data View", lambda: self.create_data_view("PaymentGateway Logs", "paymentgateway-*")),
            ("Create APM Data View", lambda: self.create_data_view("PaymentGateway APM", "apm-*")),
            ("Create Visualizations", lambda: self.create_visualization("Payment RPS", "paymentgateway-*", "line")),
            ("Create Dashboard", self.create_dashboard),
        ]

        for step_name, step_func in steps:
            print(f"-> {step_name}...", end=" ", flush=True)
            try:
                success = step_func()
                print("[OK]" if success else "[FAIL]")
            except Exception as e:
                print(f"[ERROR]: {e}")
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": str(e)[:200]
                })

        # Save results
        with open("phase4_results.json", "w") as f:
            json.dump(self.results, f, indent=2)

        print(f"\n[OK] Results saved: phase4_results.json\n")

        return self.results

if __name__ == "__main__":
    kibana_url = "https://elasticsearch.home.arpa:5601"

    setup = KibanaSetup(kibana_url, username="elastic", password="")
    results = setup.run_all()

    print(f"{'='*70}")
    print("Phase 4 Summary:")
    print(f"{'='*70}")
    success_count = sum(1 for s in results.get("steps", []) if s.get("success", False))
    total_count = len(results.get("steps", []))
    print(f"  Kibana Setup: {success_count}/{total_count} steps completed")

    if success_count == total_count:
        print(f"\n[SUCCESS] Kibana dashboard available at:")
        print(f"  {kibana_url}")
    print()
