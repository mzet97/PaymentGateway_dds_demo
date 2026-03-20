#!/usr/bin/env python3
"""
Master Orchestrator - Complete PaymentGateway Deployment
Executa todas as 8 fases de deployment nas VMs
"""

import subprocess
import json
import sys
import os
from datetime import datetime
from pathlib import Path

class PaymentGatewayOrchestrator:
    def __init__(self):
        self.start_time = datetime.now()
        self.results = {
            "timestamp": self.start_time.isoformat(),
            "phases": {},
            "vms": {
                "api_host": "192.168.1.61",
                "orchestrator_host": "192.168.1.62",
                "client_host": "192.168.1.63"
            },
            "external_services": {
                "postgres": "spsql.home.arpa:5432",
                "mongodb": "mongodb.home.arpa:27017",
                "redis": "redis.home.arpa:6379",
                "minio": "minio-s3.home.arpa:443",
                "elasticsearch": "elasticsearch.home.arpa:443"
            }
        }

    def print_banner(self, text):
        """Imprime banner formatado"""
        print(f"\n{'='*70}")
        print(f"  {text}")
        print(f"{'='*70}\n")

    def run_phase(self, phase_num, phase_name, script_name):
        """Executa uma fase de deployment"""
        print(f"\n→ Fase {phase_num}: {phase_name}...", flush=True)

        script_path = Path(__file__).parent / script_name

        if not script_path.exists():
            print(f"  ✗ Script não encontrado: {script_path}")
            self.results["phases"][f"phase_{phase_num}"] = {
                "name": phase_name,
                "success": False,
                "error": f"Script not found: {script_name}"
            }
            return False

        try:
            result = subprocess.run(
                [sys.executable, str(script_path)],
                capture_output=True,
                text=True,
                timeout=3600
            )

            success = result.returncode == 0

            self.results["phases"][f"phase_{phase_num}"] = {
                "name": phase_name,
                "success": success,
                "duration": "...",
                "output": result.stdout[:500] if result.stdout else "",
                "error": result.stderr[:500] if result.stderr else ""
            }

            print(f"  {'✓ OK' if success else '✗ FAILED'}")
            return success

        except subprocess.TimeoutExpired:
            print(f"  ✗ TIMEOUT")
            self.results["phases"][f"phase_{phase_num}"] = {
                "name": phase_name,
                "success": False,
                "error": "Timeout"
            }
            return False
        except Exception as e:
            print(f"  ✗ ERROR: {e}")
            self.results["phases"][f"phase_{phase_num}"] = {
                "name": phase_name,
                "success": False,
                "error": str(e)
            }
            return False

    def generate_final_report(self):
        """Gera relatório final"""
        self.print_banner("RELATÓRIO FINAL")

        total_phases = len(self.results["phases"])
        successful_phases = sum(
            1 for p in self.results["phases"].values() if p.get("success", False)
        )

        print(f"Fases completadas: {successful_phases}/{total_phases}")
        print(f"Tempo total: {datetime.now() - self.start_time}")
        print()

        for phase_key, phase_data in self.results["phases"].items():
            status = "✓ PASS" if phase_data.get("success") else "✗ FAIL"
            print(f"  {status} - Fase {phase_data['name']}")

        # Salvar relatório JSON
        report_file = "PaymentGateway-DeploymentResults.json"
        with open(report_file, "w") as f:
            json.dump(self.results, f, indent=2)

        print(f"\n✓ Relatório salvo: {report_file}")

        # Status geral
        if successful_phases == total_phases:
            print(f"\n🎉 DEPLOYMENT COMPLETO - SISTEMA PRONTO PARA PRODUÇÃO")
            return 0
        else:
            print(f"\n⚠️  {total_phases - successful_phases} fases falharam")
            return 1

    def run_all(self):
        """Executa todas as fases"""
        self.print_banner("PaymentGateway Deployment Orchestrator")
        print("Status: INICIANDO\n")

        phases = [
            (1, "VM Setup + DNS Configuration", "phase1_vm_setup.py"),
            (2, "EF Core Migrations", "phase2_migrations.py"),
            (3, "Docker Build + Deploy", "phase3_deploy.py"),
            (4, "Kibana Dashboards", "phase4_kibana.py"),
            (5, "Backend API Tests", "phase5_test_backend.py"),
            (6, "Load Testing (k6)", "phase6_load_test.py"),
            (7, "Frontend Next.js", "phase7_frontend.py"),
            (8, "Final Report", "phase8_report.py"),
        ]

        for phase_num, phase_name, script_name in phases:
            self.run_phase(phase_num, phase_name, script_name)

        # Gerar relatório
        exit_code = self.generate_final_report()

        return exit_code

if __name__ == "__main__":
    orchestrator = PaymentGatewayOrchestrator()
    sys.exit(orchestrator.run_all())
