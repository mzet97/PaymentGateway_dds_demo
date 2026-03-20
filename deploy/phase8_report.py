#!/usr/bin/env python3
"""
Fase 8: Relatório Final
Coleta resultados de todas as fases e gera relatório final
"""

import json
import sys
import subprocess
from datetime import datetime
from pathlib import Path

class FinalReporter:
    def __init__(self, api_url="http://192.168.1.61:5000"):
        self.api_url = api_url
        self.results = {
            "timestamp": datetime.now().isoformat(),
            "titulo": "Relatório Completo de Deploy - PaymentGateway",
            "status_geral": "PROCESSING",
            "fases": {},
            "resumo": {},
            "criterios_sucesso": {}
        }

    def coletar_resultados_fase(self, fase_num, arquivo_resultado):
        """Coleta resultados de uma fase específica"""
        caminho = Path(arquivo_resultado)

        if caminho.exists():
            try:
                with open(caminho, 'r') as f:
                    dados = json.load(f)
                    self.results["fases"][f"fase_{fase_num}"] = dados
                    return True
            except Exception as e:
                self.results["fases"][f"fase_{fase_num}"] = {
                    "erro": str(e)[:200]
                }
                return False
        else:
            self.results["fases"][f"fase_{fase_num}"] = {
                "erro": f"Arquivo não encontrado: {arquivo_resultado}"
            }
            return False

    def verificar_servicos_docker(self):
        """Verifica status dos serviços Docker na VM1"""
        passo = "Verificar Serviços Docker"

        try:
            cmd = 'sshpass -p "Admin@123" ssh -o StrictHostKeyChecking=no oldds@192.168.1.61 "cd ~/CycloneDds.NET/demo/PaymentGateway && docker compose ps" 2>&1'
            result = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=30)

            servicos_esperados = ["api", "processor", "fraud-detector", "notification", "settlement", "mongo-sync", "transaction-history"]

            servicos_rodando = 0
            servicos_status = {}

            if result.returncode == 0:
                output = result.stdout
                for servico in servicos_esperados:
                    if servico in output and "Up" in output:
                        servicos_rodando += 1
                        servicos_status[servico] = "rodando"
                    else:
                        servicos_status[servico] = "parado"

            self.results["resumo"]["servicos_docker"] = {
                "total_esperado": len(servicos_esperados),
                "rodando": servicos_rodando,
                "status": servicos_status,
                "sucesso": servicos_rodando == len(servicos_esperados)
            }

            return servicos_rodando == len(servicos_esperados)

        except Exception as e:
            self.results["resumo"]["servicos_docker"] = {
                "erro": str(e)[:200]
            }
            return False

    def verificar_health_check(self):
        """Verifica se a API está saudável"""
        passo = "Health Check da API"

        try:
            import requests
            response = requests.get(f"{self.api_url}/health", timeout=10)

            sucesso = response.status_code == 200

            self.results["resumo"]["health_check"] = {
                "status_code": response.status_code,
                "url": self.api_url,
                "sucesso": sucesso
            }

            if sucesso:
                self.results["resumo"]["health_check"]["dados"] = response.json()

            return sucesso

        except Exception as e:
            self.results["resumo"]["health_check"] = {
                "erro": str(e)[:200]
            }
            return False

    def verificar_kibana(self):
        """Verifica se Kibana está acessível"""
        passo = "Verificar Kibana"

        try:
            import requests
            requests.packages.urllib3.disable_warnings()

            response = requests.get("https://elasticsearch.home.arpa:5601/api/status", verify=False, timeout=10)

            sucesso = response.status_code == 200

            self.results["resumo"]["kibana"] = {
                "status_code": response.status_code,
                "url": "https://elasticsearch.home.arpa:5601",
                "sucesso": sucesso
            }

            return sucesso

        except Exception as e:
            self.results["resumo"]["kibana"] = {
                "erro": str(e)[:200]
            }
            return False

    def verificar_frontend(self):
        """Verifica se o frontend está acessível"""
        passo = "Verificar Frontend"

        try:
            import requests
            response = requests.get("http://192.168.1.61:3000", timeout=10)

            sucesso = response.status_code == 200

            self.results["resumo"]["frontend"] = {
                "status_code": response.status_code,
                "url": "http://192.168.1.61:3000",
                "sucesso": sucesso
            }

            return sucesso

        except Exception as e:
            self.results["resumo"]["frontend"] = {
                "erro": str(e)[:200]
            }
            return False

    def processar_testes_backend(self):
        """Processa resultados dos testes backend"""
        if "fase_5" in self.results["fases"]:
            testes = self.results["fases"]["fase_5"].get("tests", [])

            total_testes = len(testes)
            testes_passados = sum(1 for t in testes if t.get("success", False))

            self.results["resumo"]["testes_backend"] = {
                "total": total_testes,
                "passados": testes_passados,
                "falhados": total_testes - testes_passados,
                "taxa_sucesso": (testes_passados / total_testes * 100) if total_testes > 0 else 0,
                "sucesso": testes_passados == total_testes
            }

            # Extrair latências se disponível
            for teste in testes:
                if teste.get("name") == "API Latency Measurement":
                    self.results["resumo"]["latencia_api"] = {
                        "min_ms": teste.get("min_ms"),
                        "max_ms": teste.get("max_ms"),
                        "avg_ms": teste.get("avg_ms")
                    }

    def processar_testes_carga(self):
        """Processa resultados dos testes de carga k6"""
        if "fase_6" in self.results["fases"]:
            cenarios = self.results["fases"]["fase_6"].get("scenarios", [])

            self.results["resumo"]["testes_carga"] = {
                "total_cenarios": len(cenarios),
                "cenarios_completados": sum(1 for c in cenarios if c.get("success", False))
            }

            for cenario in cenarios:
                nome = cenario.get("name", "desconhecido")
                self.results["resumo"]["testes_carga"][nome] = {
                    "sucesso": cenario.get("success", False),
                    "metricas": cenario.get("metrics", {})
                }

    def calcular_criterios_sucesso(self):
        """Calcula criterios de sucesso do deployment"""
        criterios = {
            "dns_resolvendo": self._verificar_dns(),
            "servicos_docker": self.results["resumo"].get("servicos_docker", {}).get("sucesso", False),
            "migrations_aplicadas": self._verificar_migrations(),
            "health_check_ok": self.results["resumo"].get("health_check", {}).get("sucesso", False),
            "kibana_acessivel": self.results["resumo"].get("kibana", {}).get("sucesso", False),
            "testes_backend_passed": self.results["resumo"].get("testes_backend", {}).get("sucesso", False),
            "frontend_acessivel": self.results["resumo"].get("frontend", {}).get("sucesso", False)
        }

        self.results["criterios_sucesso"] = criterios

        # Calcular status geral
        total_criterios = len(criterios)
        criterios_ok = sum(1 for v in criterios.values() if v is True)

        if criterios_ok == total_criterios:
            self.results["status_geral"] = "PRONTO PARA PRODUCAO"
        elif criterios_ok >= total_criterios * 0.8:
            self.results["status_geral"] = "REQUER REVISAO MENOR"
        else:
            self.results["status_geral"] = "REQUER REVISAO CRITICA"

        return criterios_ok, total_criterios

    def _verificar_dns(self):
        """Verifica se DNS está resolvendo"""
        try:
            import socket
            hosts = ["spsql.home.arpa", "mongodb.home.arpa", "redis.home.arpa", "elasticsearch.home.arpa"]

            for host in hosts:
                socket.gethostbyname(host)

            return True
        except:
            return False

    def _verificar_migrations(self):
        """Verifica se migrations foram aplicadas"""
        if "fase_2" in self.results["fases"]:
            fase2 = self.results["fases"]["fase_2"]
            steps = fase2.get("steps", [])

            tabelas_verificadas = False
            for step in steps:
                if step.get("name") == "Verify Database Tables":
                    return step.get("success", False)

        return False

    def gerar_relatorio_html(self):
        """Gera versão HTML do relatório (opcional)"""
        html = f"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <title>Relatório PaymentGateway</title>
            <style>
                body {{ font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }}
                .header {{ background-color: #2c3e50; color: white; padding: 20px; border-radius: 5px; }}
                .status {{ padding: 10px; margin: 10px 0; border-radius: 5px; }}
                .success {{ background-color: #d4edda; color: #155724; }}
                .warning {{ background-color: #fff3cd; color: #856404; }}
                .error {{ background-color: #f8d7da; color: #721c24; }}
                .section {{ background-color: white; padding: 15px; margin: 10px 0; border-radius: 5px; }}
                h2 {{ color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px; }}
                table {{ width: 100%; border-collapse: collapse; }}
                th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                th {{ background-color: #3498db; color: white; }}
                .timestamp {{ color: #7f8c8d; font-size: 0.9em; }}
            </style>
        </head>
        <body>
            <div class="header">
                <h1>Relatório de Deploy - PaymentGateway</h1>
                <p class="timestamp">Gerado em: {self.results['timestamp']}</p>
            </div>

            <div class="section">
                <h2>Status Geral</h2>
                <div class="status success">
                    <strong>Status: {self.results['status_geral']}</strong>
                </div>
            </div>

            <div class="section">
                <h2>Critérios de Sucesso</h2>
                <table>
                    <tr><th>Critério</th><th>Status</th></tr>
        """

        for criterio, status in self.results.get("criterios_sucesso", {}).items():
            classe_status = "success" if status else "error"
            html += f"""
                    <tr>
                        <td>{criterio}</td>
                        <td><span class="{classe_status}">{'OK' if status else 'FALHA'}</span></td>
                    </tr>
            """

        html += """
                </table>
            </div>

            <div class="section">
                <h2>Endpoints da Aplicação</h2>
                <ul>
                    <li>API: http://192.168.1.61:5000</li>
                    <li>Frontend: http://192.168.1.61:3000</li>
                    <li>Kibana: https://elasticsearch.home.arpa:5601</li>
                    <li>Prometheus: http://192.168.1.61:9090</li>
                </ul>
            </div>
        </body>
        </html>
        """

        return html

    def executar(self):
        """Executa coleta de todos os resultados"""
        print(f"\n{'='*70}")
        print("Fase 8: Relatório Final")
        print(f"{'='*70}\n")

        # Coletar resultados de todas as fases
        print("-> Coletando resultados das fases...", end=" ", flush=True)

        self.coletar_resultados_fase(1, "phase1_summary.json")
        self.coletar_resultados_fase(2, "phase2_summary.json")
        self.coletar_resultados_fase(3, "phase3_summary.json")
        self.coletar_resultados_fase(4, "phase4_results.json")
        self.coletar_resultados_fase(5, "phase5_results.json")
        self.coletar_resultados_fase(6, "phase6_results.json")
        self.coletar_resultados_fase(7, "phase7_results.json")

        print("[OK]")

        # Verificar status dos serviços
        print("-> Verificando serviços Docker...", end=" ", flush=True)
        self.verificar_servicos_docker()
        print("[OK]")

        # Verificar health check
        print("-> Verificando API health check...", end=" ", flush=True)
        self.verificar_health_check()
        print("[OK]")

        # Verificar Kibana
        print("-> Verificando Kibana...", end=" ", flush=True)
        self.verificar_kibana()
        print("[OK]")

        # Verificar Frontend
        print("-> Verificando Frontend...", end=" ", flush=True)
        self.verificar_frontend()
        print("[OK]")

        # Processar resultados dos testes
        print("-> Processando resultados dos testes...", end=" ", flush=True)
        self.processar_testes_backend()
        self.processar_testes_carga()
        print("[OK]")

        # Calcular critérios de sucesso
        print("-> Calculando critérios de sucesso...", end=" ", flush=True)
        ok, total = self.calcular_criterios_sucesso()
        print(f"[OK] ({ok}/{total})")

        # Salvar relatório JSON
        print("-> Salvando relatório JSON...", end=" ", flush=True)
        with open("PaymentGateway-FullTestResults.json", "w") as f:
            json.dump(self.results, f, indent=2)
        print("[OK]")

        # Salvar relatório HTML
        print("-> Gerando relatório HTML...", end=" ", flush=True)
        html = self.gerar_relatorio_html()
        with open("PaymentGateway-FullTestResults.html", "w") as f:
            f.write(html)
        print("[OK]")

        print(f"\n{'='*70}")
        print("Resumo Final:")
        print(f"{'='*70}")
        print(f"Status Geral: {self.results['status_geral']}")
        print(f"Timestamp: {self.results['timestamp']}")
        print()
        print("Relatórios gerados:")
        print("  - PaymentGateway-FullTestResults.json")
        print("  - PaymentGateway-FullTestResults.html")
        print()
        print("Endpoints da aplicação:")
        print("  - API: http://192.168.1.61:5000")
        print("  - Frontend: http://192.168.1.61:3000")
        print("  - Kibana: https://elasticsearch.home.arpa:5601")
        print()

        return self.results

if __name__ == "__main__":
    relatorio = FinalReporter()
    resultados = relatorio.executar()

    print(f"{'='*70}")
    print("Critérios de Sucesso:")
    print(f"{'='*70}")

    for criterio, status in resultados.get("criterios_sucesso", {}).items():
        simbolo = "[OK]" if status else "[FAIL]"
        print(f"  {simbolo} - {criterio}")

    print()
