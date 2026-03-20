#!/usr/bin/env python3
"""
Starter Script - Inicia o deployment completo do PaymentGateway nas VMs
Pode ser executado direto do Windows/WSL
"""

import subprocess
import sys
import os
import json
from pathlib import Path
from datetime import datetime

class DeploymentStarter:
    def __init__(self):
        self.repo_root = Path(__file__).parent.parent.parent.parent
        self.deployment_dir = Path(__file__).parent
        self.vms = {
            "api_server": {"ip": "192.168.1.61", "user": "oldds", "pass": "Admin@123", "role": "API + Microservices"},
            "orchestrator": {"ip": "192.168.1.62", "user": "oldds", "pass": "Admin@123", "role": "Coordinator"},
            "client": {"ip": "192.168.1.63", "user": "oldds", "pass": "Admin@123", "role": "Load Testing"}
        }

    def print_header(self):
        output = """
=================================================================
    PaymentGateway Deployment - Complete Test Suite
              CycloneDDS.NET Demo Application
=================================================================

Status: READY TO DEPLOY
Timestamp: """ + datetime.now().isoformat() + """

Target VMs:
  * 192.168.1.61 (oldds) - API Server + Microservices
  * 192.168.1.62 (oldds) - Orchestrator
  * 192.168.1.63 (oldds) - Client (Load Testing)

External Infrastructure (192.168.1.51):
  [OK] PostgreSQL (spsql.home.arpa:5432)
  [OK] MongoDB (mongodb.home.arpa:27017)
  [OK] Redis (redis.home.arpa:6379)
  [OK] MinIO S3 (minio-s3.home.arpa:443)
  [OK] Elasticsearch+Kibana (elasticsearch.home.arpa:443)

Deployment Phases:
  1. VM Setup + DNS Configuration
  2. EF Core Migrations (PostgreSQL)
  3. Docker Build + Deploy (docker-compose up)
  4. Kibana Dashboards + APM Setup
  5. Backend API Tests
  6. Load Testing (k6)
  7. Frontend Next.js Deployment
  8. Final Report Generation

=================================================================
"""
        print(output)

    def check_prerequisites(self):
        """Verifica pre-requisitos locais"""
        print("\n-> Verificando pre-requisitos locais...\n")

        checks = {
            "Python 3.6+": lambda: sys.version_info >= (3, 6),
            "PaymentGateway.sln": lambda: (self.repo_root / "demo/PaymentGateway/PaymentGateway.sln").exists(),
            "docker-compose.yml": lambda: (self.deployment_dir.parent / "docker-compose.yml").exists(),
            ".env": lambda: (self.deployment_dir.parent / ".env").exists(),
            "deploy/ scripts": lambda: (self.deployment_dir / "run_all.py").exists(),
        }

        all_ok = True
        for check_name, check_func in checks.items():
            try:
                result = check_func()
                status = "[OK]" if result else "[FAIL]"
                print("  " + status + " " + check_name)
                if not result:
                    all_ok = False
            except Exception as e:
                print("  [FAIL] " + check_name + " (Error: " + str(e) + ")")
                all_ok = False

        return all_ok

    def check_ssh_connectivity(self):
        """Testa conectividade SSH para as VMs"""
        print("\n-> Testando conectividade SSH para as VMs...\n")

        all_ok = True
        for vm_name, vm_info in self.vms.items():
            try:
                cmd = 'sshpass -p "' + vm_info["pass"] + '" ssh -o ConnectTimeout=5 -o StrictHostKeyChecking=no ' + vm_info["user"] + '@' + vm_info["ip"] + ' "echo OK"'
                result = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=10)

                if result.returncode == 0:
                    print("  [OK] " + vm_info['ip'] + " (" + vm_name + "): SSH OK")
                else:
                    print("  [FAIL] " + vm_info['ip'] + " (" + vm_name + "): SSH FAILED")
                    print("    Error: " + result.stderr[:100])
                    all_ok = False
            except Exception as e:
                print("  [FAIL] " + vm_info['ip'] + " (" + vm_name + "): " + str(e))
                all_ok = False

        return all_ok

    def create_deployment_plan(self):
        """Cria plano de deployment"""
        plan = {
            "timestamp": datetime.now().isoformat(),
            "deployment_dir": str(self.deployment_dir),
            "repo_root": str(self.repo_root),
            "vms": self.vms,
            "phases": [
                {
                    "phase": 1,
                    "name": "VM Setup + DNS",
                    "target_vms": ["all"],
                    "estimated_duration": "5 min"
                },
                {
                    "phase": 2,
                    "name": "EF Core Migrations",
                    "target_vms": ["api_server"],
                    "estimated_duration": "5 min"
                },
                {
                    "phase": 3,
                    "name": "Docker Build + Deploy",
                    "target_vms": ["api_server"],
                    "estimated_duration": "35 min"
                },
                {
                    "phase": 4,
                    "name": "Kibana Setup",
                    "target_vms": ["orchestrator"],
                    "estimated_duration": "5 min"
                },
                {
                    "phase": 5,
                    "name": "Backend Tests",
                    "target_vms": ["api_server"],
                    "estimated_duration": "5 min"
                },
                {
                    "phase": 6,
                    "name": "Load Testing",
                    "target_vms": ["client"],
                    "estimated_duration": "10 min"
                },
                {
                    "phase": 7,
                    "name": "Frontend Deploy",
                    "target_vms": ["api_server"],
                    "estimated_duration": "10 min"
                },
                {
                    "phase": 8,
                    "name": "Final Report",
                    "target_vms": ["all"],
                    "estimated_duration": "1 min"
                }
            ],
            "total_estimated_duration": "86 min"
        }

        plan_file = self.deployment_dir / "DEPLOYMENT_PLAN.json"
        with open(plan_file, "w") as f:
            json.dump(plan, f, indent=2)

        print("\n[OK] Plano de deployment criado: " + str(plan_file))
        return plan

    def run(self):
        """Executa verificacoes e prepara deployment"""
        self.print_header()

        # Checklist
        print("\n" + "="*70)
        print("VERIFICACOES PRE-DEPLOYMENT")
        print("="*70)

        checks_ok = self.check_prerequisites()
        if not checks_ok:
            print("\n[WARNING] Alguns pre-requisitos locais nao foram encontrados!")
            print("    Verifique se todos os arquivos estao presentes.")

        ssh_ok = self.check_ssh_connectivity()
        if not ssh_ok:
            print("\n[WARNING] Conectividade SSH nao funcionou!")
            print("    Verifique as credenciais e se as VMs estao acessiveis.")
            print("    Certifique-se de que 'sshpass' esta instalado.")

        # Criar plano
        plan = self.create_deployment_plan()

        # Status final
        print("\n" + "="*70)
        print("PROXIMOS PASSOS")
        print("="*70)

        if checks_ok and ssh_ok:
            print("""
[SUCCESS] Todos os pre-requisitos foram validados!

Para iniciar o deployment:
  1. Windows/WSL: python3 deploy/run_all.py
  2. Aguarde ~86 minutos para conclusao
  3. Verifique PaymentGateway-DeploymentResults.json para status

Detalhes do deployment:
  * Fase 1-8 serao executadas sequencialmente
  * Cada fase gera um arquivo JSON com resultados
  * Em caso de falha, o script para e reporta o erro

Endpoints da aplicacao:
  * API: http://192.168.1.61:5000
  * Frontend: http://192.168.1.61:3000
  * Kibana: https://elasticsearch.home.arpa:5601
  * Prometheus: http://192.168.1.61:9090
            """)
            return 0
        else:
            print("""
[WARNING] Existem problemas com os pre-requisitos.

Por favor, verifique:
  1. Todos os scripts estao em deploy/
  2. VMs estao acessiveis via SSH
  3. sshpass esta instalado (para autenticacao SSH)
  4. Credenciais estao corretas

Apos corrigir, execute:
  python3 deploy/DEPLOYMENT_START.py
            """)
            return 1

if __name__ == "__main__":
    starter = DeploymentStarter()
    sys.exit(starter.run())
