#!/usr/bin/env python3
"""
Phase 3: Docker Build and Deployment
Faz upload do monorepo para VM1, executa docker compose build/up
"""

import subprocess
import json
import sys
import os
from datetime import datetime
from pathlib import Path

class DockerDeployer:
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
            result = subprocess.run(ssh_cmd, shell=True, capture_output=True, text=True, timeout=600)
            return result.returncode == 0, result.stdout, result.stderr
        except subprocess.TimeoutExpired:
            return False, "", "Command timeout (10 min)"
        except Exception as e:
            return False, "", str(e)

    def upload_monorepo(self):
        """Faz upload do monorepo via tar + SSH"""
        step_name = "Upload Monorepo via SSH"

        try:
            # Create tar excluding unnecessary files
            print("    [Compressing monorepo...]", flush=True)
            repo_root = Path(__file__).parent.parent.parent.parent

            tar_cmd = f'tar --exclude=.git --exclude=models --exclude=*.gguf --exclude=bin --exclude=obj --exclude=.vs -czf /tmp/paymentgateway.tar.gz -C {repo_root.parent} {repo_root.name}/'
            result = subprocess.run(tar_cmd, shell=True, capture_output=True, text=True, timeout=120)

            if result.returncode != 0:
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": "Failed to compress monorepo"
                })
                return False

            # Upload tar
            print("    [Uploading to VM (this may take 5-10 minutes)...]", flush=True)
            scp_cmd = f'sshpass -p "{self.password}" scp -o StrictHostKeyChecking=no /tmp/paymentgateway.tar.gz {self.username}@{self.host}:~/'
            result = subprocess.run(scp_cmd, shell=True, capture_output=True, text=True, timeout=600)

            if result.returncode != 0:
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": f"SCP failed: {result.stderr[:200]}"
                })
                return False

            # Extract on VM
            print("    [Extracting on VM...]", flush=True)
            extract_cmd = 'cd ~ && tar -xzf paymentgateway.tar.gz && rm paymentgateway.tar.gz'
            success, out, err = self.execute_ssh(extract_cmd)

            if success:
                # Clean up local tar
                subprocess.run('rm /tmp/paymentgateway.tar.gz', shell=True)
                self.results["steps"].append({
                    "name": step_name,
                    "success": True,
                    "message": "Monorepo uploaded and extracted successfully"
                })
            else:
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": err[:200]
                })

            return success

        except Exception as e:
            self.results["steps"].append({
                "name": step_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def create_env_file(self):
        """Cria arquivo .env na VM"""
        step_name = "Create .env Configuration"

        env_content = '''POSTGRES_HOST=spsql.home.arpa
POSTGRES_PORT=5432
POSTGRES_DB=demo-gateway
POSTGRES_USER=app
POSTGRES_PASSWORD=Admin@123
MONGO_HOST=mongodb.home.arpa
MONGO_PORT=27017
MONGO_USER=admin
MONGO_PASSWORD=Admin@123
REDIS_HOST=redis.home.arpa
REDIS_PORT=6379
REDIS_PASSWORD=Admin@123
MINIO_ENDPOINT=minio-s3.home.arpa
MINIO_ACCESS_KEY=admin
MINIO_SECRET_KEY=Admin@123
MINIO_BUCKET=demo-gateway
MINIO_USE_SSL=true
ELASTICSEARCH_URL=https://elasticsearch.home.arpa
ELASTICSEARCH_USER=elastic
ELASTICSEARCH_PASSWORD=
OPENROUTER_API_KEY=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
OPENROUTER_BASE_URL=https://openrouter.ai/api/v1
OPENROUTER_MODEL=minimax/minimax-m2.5
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000
ENABLE_AUTH=false
DDS_USE_REAL_DDS=false
DDS_ENABLE_GRACEFUL_FALLBACK=true
ENABLE_PROMETHEUS_METRICS=true
ENABLE_SWAGGER=true
'''

        try:
            # Write to temp file
            with open("/tmp/.env", "w") as f:
                f.write(env_content)

            # Upload .env
            scp_cmd = f'sshpass -p "{self.password}" scp -o StrictHostKeyChecking=no /tmp/.env {self.username}@{self.host}:~/CycloneDds.NET/demo/PaymentGateway/'
            result = subprocess.run(scp_cmd, shell=True, capture_output=True, text=True, timeout=30)

            if result.returncode == 0:
                subprocess.run('rm /tmp/.env', shell=True)
                self.results["steps"].append({
                    "name": step_name,
                    "success": True,
                    "message": ".env created successfully"
                })
                return True
            else:
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": result.stderr[:200]
                })
                return False

        except Exception as e:
            self.results["steps"].append({
                "name": step_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def docker_compose_build(self):
        """Executa docker compose build"""
        step_name = "Docker Compose Build"

        print("    [Building Docker images (this will take 15-30 minutes)...]", flush=True)

        cmd = 'cd ~/CycloneDds.NET/demo/PaymentGateway && docker compose build 2>&1'
        success, out, err = self.execute_ssh(cmd)

        self.results["steps"].append({
            "name": step_name,
            "success": success,
            "output": out[-300:] if out else "",
            "error": err[-300:] if err else ""
        })

        return success

    def docker_compose_up(self):
        """Executa docker compose up -d"""
        step_name = "Docker Compose Up"

        cmd = 'cd ~/CycloneDds.NET/demo/PaymentGateway && docker compose up -d'
        success, out, err = self.execute_ssh(cmd)

        self.results["steps"].append({
            "name": step_name,
            "success": success,
            "output": out[:300] if out else "",
            "error": err[:300] if err else ""
        })

        return success

    def wait_for_health_checks(self):
        """Aguarda health checks dos servicos"""
        step_name = "Wait for Health Checks"

        # Wait up to 2 minutes for services to be healthy
        cmd = '''
        for i in {1..24}; do
          STATUS=$(cd ~/CycloneDds.NET/demo/PaymentGateway && docker compose ps 2>&1 | grep -c "healthy")
          if [ "$STATUS" = "1" ]; then
            echo "Services healthy"
            exit 0
          fi
          sleep 5
        done
        echo "Timeout waiting for health checks"
        exit 1
        '''

        success, out, err = self.execute_ssh(cmd)

        self.results["steps"].append({
            "name": step_name,
            "success": success,
            "message": "Health checks passed" if success else "Health check timeout"
        })

        return success

    def verify_services(self):
        """Verifica status dos servicos"""
        step_name = "Verify Services Running"

        cmd = 'cd ~/CycloneDds.NET/demo/PaymentGateway && docker compose ps'
        success, out, err = self.execute_ssh(cmd)

        # Count running services
        running_count = out.count("Up") if out else 0

        self.results["steps"].append({
            "name": step_name,
            "success": success,
            "running_services": running_count,
            "output": out[-500:] if out else "",
            "error": err[-200:] if err else ""
        })

        return success and running_count >= 7

    def run_all(self):
        """Executa todas as etapas de deployment"""
        print(f"\n{'='*70}")
        print(f"Phase 3: Docker Build and Deploy - {self.host}")
        print(f"{'='*70}\n")

        steps = [
            ("Upload Monorepo", self.upload_monorepo),
            ("Create .env", self.create_env_file),
            ("Docker Compose Build", self.docker_compose_build),
            ("Docker Compose Up", self.docker_compose_up),
            ("Wait for Health Checks", self.wait_for_health_checks),
            ("Verify Services", self.verify_services)
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
        with open(f"phase3_results_{self.host}.json", "w") as f:
            json.dump(self.results, f, indent=2)

        print(f"\n[OK] Results saved: phase3_results_{self.host}.json\n")

        return self.results

if __name__ == "__main__":
    vms = [
        {"host": "192.168.1.61", "username": "oldds", "password": "Admin@123"}
    ]

    all_results = {}

    for vm_config in vms:
        deployer = DockerDeployer(**vm_config)
        results = deployer.run_all()
        all_results[vm_config["host"]] = results

    # Save summary
    with open("phase3_summary.json", "w") as f:
        json.dump(all_results, f, indent=2)

    print(f"{'='*70}")
    print("Phase 3 Summary:")
    print(f"{'='*70}")
    for host, results in all_results.items():
        success_count = sum(1 for s in results.get("steps", []) if s.get("success", False))
        total_count = len(results.get("steps", []))
        print(f"  {host}: {success_count}/{total_count} steps completed")
    print()
