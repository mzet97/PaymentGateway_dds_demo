#!/usr/bin/env python3
"""
Phase 7: Frontend Next.js Deployment
Faz deploy da aplicacao Next.js na VM1
"""

import subprocess
import json
import sys
from datetime import datetime
from pathlib import Path

class FrontendDeployer:
    def __init__(self, host, username, password, api_url):
        self.host = host
        self.username = username
        self.password = password
        self.api_url = api_url
        self.results = {
            "timestamp": datetime.now().isoformat(),
            "host": host,
            "api_url": api_url,
            "steps": []
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

    def upload_frontend(self):
        """Faz upload do frontend via SCP"""
        step_name = "Upload Frontend Code"

        try:
            print("    [Compressing frontend...]", flush=True)

            # Create tar of frontend
            repo_root = Path(__file__).parent.parent.parent.parent
            frontend_path = repo_root / "demo" / "PaymentGateway" / "web" / "payment_gateway_web"

            if not frontend_path.exists():
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": f"Frontend path not found: {frontend_path}"
                })
                return False

            tar_cmd = f'tar -czf /tmp/frontend.tar.gz -C {frontend_path.parent} {frontend_path.name}/'
            result = subprocess.run(tar_cmd, shell=True, capture_output=True, text=True, timeout=60)

            if result.returncode != 0:
                self.results["steps"].append({
                    "name": step_name,
                    "success": False,
                    "error": "Failed to compress frontend"
                })
                return False

            # Upload tar
            print("    [Uploading frontend (2-5 minutes)...]", flush=True)
            scp_cmd = f'sshpass -p "{self.password}" scp -o StrictHostKeyChecking=no /tmp/frontend.tar.gz {self.username}@{self.host}:~/'
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
            extract_cmd = 'mkdir -p ~/applications/PaymentGateway && cd ~/applications/PaymentGateway && tar -xzf ~/frontend.tar.gz && mv payment_gateway_web web && rm ~/frontend.tar.gz'
            success, out, err = self.execute_ssh(extract_cmd)

            if success:
                subprocess.run('rm /tmp/frontend.tar.gz', shell=True)
                self.results["steps"].append({
                    "name": step_name,
                    "success": True,
                    "message": "Frontend uploaded and extracted"
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

    def create_env_local(self):
        """Cria arquivo .env.local na VM"""
        step_name = "Create .env.local"

        try:
            env_content = f'''NEXT_PUBLIC_API_URL=http://{self.api_url.replace('http://', '')}
API_URL=http://{self.api_url.replace('http://', '')}
NEXTAUTH_URL=http://{self.host}:3000
NEXTAUTH_SECRET=paymentgateway-secret-2026
SKIP_AUTH=true
NODE_ENV=production
'''

            with open("/tmp/.env.local", "w") as f:
                f.write(env_content)

            # Upload .env.local
            scp_cmd = f'sshpass -p "{self.password}" scp -o StrictHostKeyChecking=no /tmp/.env.local {self.username}@{self.host}:~/applications/PaymentGateway/web/'
            result = subprocess.run(scp_cmd, shell=True, capture_output=True, text=True, timeout=30)

            if result.returncode == 0:
                subprocess.run('rm /tmp/.env.local', shell=True)
                self.results["steps"].append({
                    "name": step_name,
                    "success": True,
                    "message": ".env.local created"
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

    def install_dependencies(self):
        """Instala dependencias do Next.js"""
        step_name = "Install Dependencies"

        print("    [Installing npm packages (5-10 minutes)...]", flush=True)

        cmd = 'cd ~/applications/PaymentGateway/web && npm install 2>&1'
        success, out, err = self.execute_ssh(cmd)

        self.results["steps"].append({
            "name": step_name,
            "success": success,
            "output": out[-300:] if out else "",
            "error": err[-300:] if err else ""
        })

        return success

    def build_nextjs(self):
        """Compila aplicacao Next.js"""
        step_name = "Build Next.js App"

        print("    [Building Next.js (5-10 minutes)...]", flush=True)

        cmd = 'cd ~/applications/PaymentGateway/web && npm run build 2>&1'
        success, out, err = self.execute_ssh(cmd)

        self.results["steps"].append({
            "name": step_name,
            "success": success,
            "output": out[-300:] if out else "",
            "error": err[-300:] if err else ""
        })

        return success

    def start_frontend_server(self):
        """Inicia servidor Next.js em background"""
        step_name = "Start Frontend Server"

        cmd = 'cd ~/applications/PaymentGateway/web && nohup npm start -- -p 3000 > ~/frontend.log 2>&1 &'
        success, out, err = self.execute_ssh(cmd)

        self.results["steps"].append({
            "name": step_name,
            "success": success,
            "message": "Frontend server started on port 3000"
        })

        return success

    def verify_frontend(self):
        """Verifica se o frontend esta acessivel"""
        step_name = "Verify Frontend Accessibility"

        # Wait a bit for server to start
        import time
        time.sleep(5)

        # Test HTTP endpoints
        try:
            import requests
            base_url = f"http://{self.host}:3000"

            routes = ["/", "/payments", "/merchants", "/analytics"]
            results = {}

            for route in routes:
                try:
                    response = requests.get(f"{base_url}{route}", timeout=10)
                    results[route] = response.status_code
                except:
                    results[route] = "unreachable"

            all_accessible = all(v == 200 for v in results.values())

            self.results["steps"].append({
                "name": step_name,
                "success": all_accessible,
                "route_status": results,
                "message": f"Frontend accessible at {base_url}"
            })

            return all_accessible

        except Exception as e:
            self.results["steps"].append({
                "name": step_name,
                "success": False,
                "error": str(e)[:200]
            })
            return False

    def run_all(self):
        """Executa todas as etapas de deploy"""
        print(f"\n{'='*70}")
        print(f"Phase 7: Frontend Deployment - {self.host}")
        print(f"{'='*70}\n")

        steps = [
            ("Upload Frontend", self.upload_frontend),
            ("Create .env.local", self.create_env_local),
            ("Install Dependencies", self.install_dependencies),
            ("Build Next.js", self.build_nextjs),
            ("Start Server", self.start_frontend_server),
            ("Verify Accessibility", self.verify_frontend),
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
        with open("phase7_results.json", "w") as f:
            json.dump(self.results, f, indent=2)

        print(f"\n[OK] Results saved: phase7_results.json\n")

        return self.results

if __name__ == "__main__":
    deployer = FrontendDeployer(
        host="192.168.1.61",
        username="oldds",
        password="Admin@123",
        api_url="http://192.168.1.61:5000"
    )

    results = deployer.run_all()

    print(f"{'='*70}")
    print("Phase 7 Summary:")
    print(f"{'='*70}")

    passed = sum(1 for s in results.get("steps", []) if s.get("success", False))
    total = len(results.get("steps", []))

    print(f"  Steps completed: {passed}/{total}")

    for step in results.get("steps", []):
        status = "[OK]" if step.get("success") else "[FAIL]"
        print(f"    {status} - {step['name']}")

    if passed == total:
        print(f"\n[SUCCESS] Frontend available at:")
        print(f"  http://192.168.1.61:3000")

    print()
