# LinkedIn Post - CycloneDDS.NET Linux Support

Minha primeira grande contribuicao open-source: adicionando suporte Linux ao CycloneDDS.NET

Nos ultimos meses, como parte do meu mestrado em Engenharia Eletronica na UERJ, desenvolvi o CycloneDDS.NET - bindings .NET de alta performance para o Eclipse Cyclone DDS com zero alocacao de memoria.

A lib ja funcionava no Windows, mas para validar minha tese sobre middleware DDS para orquestracao de agentes LLM, eu precisava que rodasse em Linux. Entao decidi contribuir isso como minha primeira grande contribuicao open-source.

O que foi adicionado:

- Build nativo CMake para Linux (libddsc.so, idlc)
- Correcao critica no P/Invoke: a struct iovec tem campo order diferente entre Windows (WSABUF: len+buf) e Linux (POSIX: iov_base+iov_len) - um bug sutil que causava segfault silencioso
- CI/CD com GitHub Actions matrix (Windows + Linux)
- NuGet package multiplataforma com runtimes/win-x64 e runtimes/linux-x64
- Smoke test end-to-end que valida roundtrip DDS real em Linux

Para testar a lib de verdade, criei uma demo completa: um Payment Gateway com arquitetura CQRS + Event Sourcing + DDD, usando CycloneDDS.NET como message broker entre 7 microservicos .NET 8.

Stack da demo:
- .NET 8 Minimal API + MediatR
- CycloneDDS.NET (pub/sub entre servicos)
- MongoDB (write buffer) + PostgreSQL (read DB)
- Redis (cache) + MinIO (arquivos)
- OpenRouter/MiniMax M2.5 (deteccao de fraude com IA)
- Next.js 16 (frontend) + NestJS (webhook receiver)
- Elasticsearch + Kibana (observabilidade)

Resultado do benchmark k6 (single instance, WSL2, 100 VUs concorrentes):
- POST /payments: 2,813 req/s | med 16ms | p99 77ms | 100% success (281k payments criados)
- GET reads: 6,628 req/s | med 8ms | p99 23ms | 100% success (662k requests)
- Cada POST faz: validacao, MongoDB insert, DDS publish, idempotency check no Redis
- Rate limiter tiered (Free/Basic/Pro/Enterprise), HMAC webhooks, observabilidade com OpenTelemetry

O mais legal de contribuir para open-source e que voce e forcado a pensar em edge cases que nunca encontraria no seu proprio codigo. Aquele bug do iovec? So apareceu quando tentei rodar o NuGet package numa distro limpa.

Se voce usa DDS em .NET e precisa de suporte Linux, a lib esta disponivel. Se esta pensando em fazer sua primeira contribuicao open-source, meu conselho: crie uma demo real que force a lib a funcionar de verdade. Testes unitarios sao bons, mas nada substitui um sistema completo rodando em producao.

Repo: github.com/matheuslaidler/CycloneDds.NET

#OpenSource #DotNET #DDS #CycloneDDS #Linux #CQRS #EventSourcing #Microservices #PaymentGateway #Mestrado #UERJ
