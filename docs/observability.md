# Payment Gateway - Observabilidade

> **Data:** 2026-03-19
> **Status:** Implementado

## 1. Escopo

Todos os projetos backend do `PaymentGateway` passaram a compartilhar o mesmo bootstrap de observabilidade:

- `Serilog` para logging estruturado
- `Serilog.Sinks.Elasticsearch` para envio de logs ao Elasticsearch
- `OpenTelemetry` para traces e metrics
- `ActivitySource` e `Meter` próprios do domínio em `PaymentGateway.Domain`

Projetos cobertos:

- `PaymentGateway.Api`
- `PaymentGateway.Services.PaymentProcessor`
- `PaymentGateway.Services.FraudDetector`
- `PaymentGateway.Services.Notification`
- `PaymentGateway.Services.Settlement`
- `PaymentGateway.Services.TransactionHistory`
- `PaymentGateway.Services.MongoSync`

## 2. Componentes

Implementação compartilhada:

- [TelemetryOptions.cs](../src/PaymentGateway.Infrastructure/Configuration/TelemetryOptions.cs)
- [ObservabilityServiceCollectionExtensions.cs](../src/PaymentGateway.Infrastructure/Observability/ObservabilityServiceCollectionExtensions.cs)
- [ActivityEnricher.cs](../src/PaymentGateway.Infrastructure/Observability/ActivityEnricher.cs)
- [SensitiveDataSanitizer.cs](../src/PaymentGateway.Infrastructure/Observability/SensitiveDataSanitizer.cs)
- [PaymentGatewayTelemetry.cs](../src/PaymentGateway.Domain/Observability/PaymentGatewayTelemetry.cs)

## 3. Logging

O logging agora é estruturado e correlacionado com trace/span:

- `ServiceName`
- `Environment`
- `ServiceVersion`
- `TraceId`
- `SpanId`

A API deixou de gerar correlation id ad hoc no middleware. O `RequestLoggingMiddleware` passou a usar o `TraceId` atual e a devolver `X-Request-Id`.

## 4. Elasticsearch

Configuração por `appsettings.json`:

```json
"Telemetry": {
  "EnableElasticsearchLogging": true,
  "ElasticsearchUrl": "https://elasticsearch.home.arpa/",
  "SkipTlsValidation": true
}
```

Formato de índice:

```text
{service-name-normalizado}-{environment}-{yyyy.MM}
```

Exemplo validado:

```text
paymentgateway-api-development-2026.03
```

## 5. OpenTelemetry

Configuração por `Telemetry`:

```json
"Telemetry": {
  "EnableOtlp": false,
  "OtelEndpoint": "http://localhost:4317",
  "EnableConsoleExporter": false
}
```

Quando `EnableOtlp=true`, traces e métricas são exportados via OTLP gRPC.

Instrumentação compartilhada:

- `HttpClient`
- `Runtime`
- `AspNetCore` na API
- `PaymentGatewayTelemetry.ActivitySource`
- `PaymentGatewayTelemetry.Meter`

## 6. Métricas e Traces de Domínio

Foram adicionados spans e medições em pontos críticos:

- criação e consulta de pagamentos
- antifraude
- webhooks
- Redis/cache
- repositórios PostgreSQL e Mongo
- publicação e assinatura DDS
- idempotência
- rate limiting

## 7. Redação de Dados Sensíveis

Payload bruto deixou de ser logado nos fluxos principais. Campos sensíveis passam por sanitização, incluindo:

- `email`
- `document`
- `phone`
- `ip`
- `customerEmail`
- `customerDocument`
- `customerIp`
- `idempotencyKey`

Referência:

- [SensitiveDataSanitizer.cs](../src/PaymentGateway.Infrastructure/Observability/SensitiveDataSanitizer.cs)

## 8. Validação Executada

Validações concluídas no WSL:

- `dotnet build PaymentGateway.sln` -> OK
- `./scripts/verify-wsl.sh` -> OK
- `./scripts/smoke-e2e-wsl.sh --api-port 5011 --stop-started-containers` -> OK
- Elasticsearch real acessível em `https://elasticsearch.home.arpa/`
- índice real criado:
  - `paymentgateway-api-development-2026.03`

## 9. Limitação Atual

O caminho de `DDS real` ainda tem um problema de runtime nativo em pelo menos um worker:

- `PaymentGateway.Services.PaymentProcessor`

Com `Dds__UseRealDds=true`, o processo sobe, inicializa publisher/subscriber real e aborta logo após a assinatura dos tópicos.  
Com `Dds__UseRealDds=false`, o serviço permanece estável e a observabilidade continua funcional.

Conclusão:

- o pacote de observabilidade está saudável
- o risco remanescente está no runtime nativo DDS real, não em `Serilog`/`OpenTelemetry`

## 10. Operação

Comandos úteis:

```bash
# Ver índices do PaymentGateway
curl -k -s https://elasticsearch.home.arpa/_cat/indices?v | grep paymentgateway

# Verificar saúde do cluster
curl -k -s https://elasticsearch.home.arpa/_cluster/health?pretty
```

## 11. Próximo Passo Recomendado

Antes de avançar para CI/APM mais profundo, o próximo passo correto é estabilizar o caminho de `DDS real` nos workers para que logs, traces e métricas possam ser validados ponta a ponta em execução distribuída real.
