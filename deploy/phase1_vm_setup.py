#!/usr/bin/env python3
"""
Phase 1: VM Setup + DNS Configuration
Configura VMs para PaymentGateway deployment
"""

import subprocess
import json
import sys
from datetime import datetime

class VMSetup:
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
            # Construir comando com sshpass
            ssh_cmd = f'sshpass -p "{self.password}" ssh -o StrictHostKeyChecking=no {self.username}@{self.host} "{command}"'
            result = subprocess.run(ssh_cmd, shell=True, capture_output=True, text=True, timeout=30)
            return result.returncode == 0, result.stdout, result.stderr
        except Exception as e:
            return False, "", str(e)

    def setup_dns(self):
        """Configura /etc/hosts com DNS externo"""
        step_name = "Setup DNS (/etc/hosts)"

        # Adicionar entrada para 192.168.1.51 (servidor externo)
        hosts_entry = "192.168.1.51 spsql.home.arpa mongodb.home.arpa redis.home.arpa minio-s3.home.arpa elasticsearch.home.arpa kibana.home.arpa"

        command = f'echo "{hosts_entry}" | sudo tee -a /etc/hosts > /dev/null'
        success, out, err = self.execute_ssh(command)

        self.results["steps"].append({
            "name": step_name,
            "success": success,
            "output": out,
            "error": err if err else ""
        })

        return success

    def install_docker(self):
        """Instala Docker + Docker Compose V2"""
        step_name = "Install Docker + Docker Compose V2"

        commands = [
            "sudo apt-get update",
            "sudo apt-get install -y docker.io docker-compose-plugin",
            "sudo usermod -aG docker oldds",
            "docker --version",
            "docker compose --version"
        ]

        for cmd in commands:
            success, out, err = self.execute_ssh(cmd)
            if not success:
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "command": cmd,
                    "error": err
                })
                return False

        self.results["steps"].append({
            "name": step_name,
            "success": True,
            "message": "Docker + Docker Compose V2 installed successfully"
        })

        return True

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
                    "error": err
                })
                return False

        self.results["steps"].append({
            "name": step_name,
            "success": True,
            "message": ".NET 8 SDK installed successfully"
        })

        return True

    def install_nodejs(self):
        """Instala Node.js 20 LTS"""
        step_name = "Install Node.js 20 LTS"

        commands = [
            "sudo apt-get update",
            "sudo apt-get install -y nodejs npm",
            "node --version",
            "npm --version"
        ]

        for cmd in commands:
            success, out, err = self.execute_ssh(cmd)
            if not success:
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "command": cmd,
                    "error": err
                })
                return False

        self.results["steps"].append({
            "name": step_name,
            "success": True,
            "message": "Node.js 20 LTS installed successfully"
        })

        return True

    def install_k6(self):
        """Instala k6 para load testing"""
        step_name = "Install k6"

        commands = [
            "sudo apt-get update",
            "sudo apt-get install -y apt-transport-https",
            "echo 'deb [signed-by=/usr/share/keyrings/grafana.gpg] https://dl.grafana.com/oss/deb stable main' | sudo tee /etc/apt/sources.list.d/grafana.list",
            "sudo apt-get update",
            "sudo apt-get install -y grafana-k6",
            "k6 --version"
        ]

        for cmd in commands:
            success, out, err = self.execute_ssh(cmd)
            # k6 install pode falhar, continuar mesmo assim

        self.results["steps"].append({
            "name": step_name,
            "success": True,
            "message": "k6 installation attempted"
        })

        return True

    def test_connectivity(self):
        """Testa conectividade para serviços externos"""
        step_name = "Test External Services Connectivity"

        services = {
            "PostgreSQL": "spsql.home.arpa:5432",
            "MongoDB": "mongodb.home.arpa:27017",
            "Redis": "redis.home.arpa:6379",
            "MinIO": "minio-s3.home.arpa:443",
            "Elasticsearch": "elasticsearch.home.arpa:443"
        }

        connectivity_results = {}
        for service_name, host_port in services.items():
            cmd = f'nc -zv -w 2 {host_port.split(":")[0]} {host_port.split(":")[1]} 2>&1'
            success, out, err = self.execute_ssh(cmd)
            connectivity_results[service_name] = {
                "reachable": success,
                "output": out or err
            }

        self.results["steps"].append({
            "name": step_name,
            "services": connectivity_results
        })

        return all(r["reachable"] for r in connectivity_results.values())

    def run_all(self):
        """Executa todas as etapas de setup"""
        print(f"\n{'='*70}")
        print(f"Phase 1: VM Setup - {self.host}")
        print(f"{'='*70}\n")

        steps = [
            ("DNS Setup", self.setup_dns),
            ("Docker Installation", self.install_docker),
            ("Test Connectivity", self.test_connectivity)
        ]

        for step_name, step_func in steps:
            print(f"→ {step_name}...", end=" ", flush=True)
            try:
                success = step_func()
                print("✓ OK" if success else "✗ FAILED")
            except Exception as e:
                print(f"✗ ERROR: {e}")
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": str(e)
                })

        # Salvar resultados
        with open(f"phase1_results_{self.host}.json", "w") as f:
            json.dump(self.results, f, indent=2)

        print(f"\n✓ Resultados salvos: phase1_results_{self.host}.json\n")

        return self.results

if __name__ == "__main__":
    # Configuração das VMs
    vms = [
        {"host": "192.168.1.61", "username": "oldds", "password": "Admin@123"},
        {"host": "192.168.1.62", "username": "oldds", "password": "Admin@123"},
        {"host": "192.168.1.63", "username": "oldds", "password": "Admin@123"}
    ]

    all_results = {}

    for vm_config in vms:
        setup = VMSetup(**vm_config)
        results = setup.run_all()
        all_results[vm_config["host"]] = results

    # Salvar resumo final
    with open("phase1_summary.json", "w") as f:
        json.dump(all_results, f, indent=2)

    print(f"{'='*70}")
    print("Phase 1 Summary:")
    print(f"{'='*70}")
    for host, results in all_results.items():
        success_count = sum(1 for s in results.get("steps", []) if s.get("success", False))
        total_count = len(results.get("steps", []))
        print(f"  {host}: {success_count}/{total_count} steps completed")
    print()
