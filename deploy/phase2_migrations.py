#!/usr/bin/env python3
"""
Phase 2: EF Core Migrations
Configura banco de dados PostgreSQL com migrações EF Core
"""

import subprocess
import json
import sys
from datetime import datetime
from pathlib import Path

class MigrationRunner:
    def __init__(self, host, username, password):
        self.host = host
        self.username = username
        self.password = password
        self.results = {
            "timestamp": datetime.now().isoformat(),
            "host": host,
            "steps": []
        }

    def execute_ssh(self, command):
        """Executa comando via SSH (usando sshpass)"""
        try:
            ssh_cmd = f'sshpass -p "{self.password}" ssh -o StrictHostKeyChecking=no {self.username}@{self.host} "{command}"'
            result = subprocess.run(ssh_cmd, shell=True, capture_output=True, text=True, timeout=300)
            return result.returncode == 0, result.stdout, result.stderr
        except Exception as e:
            return False, "", str(e)

    def install_dotnet_sdk(self):
        """Instala .NET 8 SDK"""
        step_name = "Install .NET 8 SDK"

        commands = [
            "sudo apt-get update",
            "sudo apt-get install -y dotnet-sdk-8.0",
            "dotnet --version"
        ]

        for cmd in commands:
            success, out, err = self.execute_ssh(cmd)
            if not success:
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "command": cmd,
                    "error": err[:200]
                })
                return False

        self.results["steps"].append({
            "name": step_name,
            "success": True,
            "message": ".NET 8 SDK installed successfully"
        })

        return True

    def test_database_connectivity(self):
        """Testa conexao com PostgreSQL"""
        step_name = "Test PostgreSQL Connectivity"

        # Test connection string
        cmd = 'psql -h spsql.home.arpa -U app -d demo-gateway -c "SELECT version();" 2>&1'
        success, out, err = self.execute_ssh(cmd)

        self.results["steps"].append({
            "name": step_name,
            "success": success,
            "output": out[:200] if out else "",
            "error": err[:200] if err else ""
        })

        return success

    def run_migrations(self):
        """Executa migrações EF Core"""
        step_name = "Run EF Core Migrations"

        # Assumindo que o monorepo está em /home/oldds/PaymentGateway ou similar
        cmd = '''
        cd ~/PaymentGateway && \
        dotnet ef database update \
          --project src/PaymentGateway.Persistence/PaymentGateway.Persistence.csproj \
          --startup-project src/PaymentGateway.Migrations/PaymentGateway.Migrations.csproj \
          --connection "Host=spsql.home.arpa;Port=5432;Database=demo-gateway;Username=app;Password=Admin@123;" \
          --verbose
        '''

        success, out, err = self.execute_ssh(cmd)

        self.results["steps"].append({
            "name": step_name,
            "success": success,
            "output": out[:500] if out else "",
            "error": err[:500] if err else ""
        })

        return success

    def verify_tables(self):
        """Verifica se as tabelas foram criadas"""
        step_name = "Verify Database Tables"

        cmd = '''
        psql -h spsql.home.arpa -U app -d demo-gateway -c \
        "SELECT table_name FROM information_schema.tables WHERE table_schema='public';" 2>&1
        '''

        success, out, err = self.execute_ssh(cmd)

        # Verificar se esperadas tabelas existem
        tables_found = []
        if out:
            tables_found = [
                "merchants" in out,
                "payments" in out,
                "transactions" in out
            ]

        self.results["steps"].append({
            "name": step_name,
            "success": success and all(tables_found),
            "tables_found": sum(tables_found),
            "output": out[:300] if out else "",
            "error": err[:300] if err else ""
        })

        return success and all(tables_found)

    def run_all(self):
        """Executa todas as etapas de migração"""
        print(f"\n{'='*70}")
        print(f"Phase 2: EF Core Migrations - {self.host}")
        print(f"{'='*70}\n")

        steps = [
            ("Install .NET SDK", self.install_dotnet_sdk),
            ("Test Database Connectivity", self.test_database_connectivity),
            ("Run EF Core Migrations", self.run_migrations),
            ("Verify Tables", self.verify_tables)
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
                    "error": str(e)
                })

        # Salvar resultados
        with open(f"phase2_results_{self.host}.json", "w") as f:
            json.dump(self.results, f, indent=2)

        print(f"\n[OK] Results saved: phase2_results_{self.host}.json\n")

        return self.results

if __name__ == "__main__":
    # Configuration
    vms = [
        {"host": "192.168.1.61", "username": "oldds", "password": "Admin@123"}
    ]

    all_results = {}

    for vm_config in vms:
        runner = MigrationRunner(**vm_config)
        results = runner.run_all()
        all_results[vm_config["host"]] = results

    # Save summary
    with open("phase2_summary.json", "w") as f:
        json.dump(all_results, f, indent=2)

    print(f"{'='*70}")
    print("Phase 2 Summary:")
    print(f"{'='*70}")
    for host, results in all_results.items():
        success_count = sum(1 for s in results.get("steps", []) if s.get("success", False))
        total_count = len(results.get("steps", []))
        print(f"  {host}: {success_count}/{total_count} steps completed")
    print()
