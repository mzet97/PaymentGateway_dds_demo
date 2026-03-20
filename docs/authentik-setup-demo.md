# Configuração do Authentik (Servidor Zerado) para a Demo PaymentGateway

Este guia configura o Authentik do zero para rodar a demo com autenticação/autorização reais (OIDC), mantendo o contrato:

- merchant acessa apenas os próprios recursos
- admin pode acessar endpoints administrativos

## 1. Pré-requisitos

- Authentik acessível em `https://authentik.home.arpa`
- Backend da demo em `http://localhost:5000`
- Frontend em `http://localhost:3000`
- DNS/hosts resolvendo `authentik.home.arpa`

## 2. Criar Provider OIDC no Authentik (campo a campo)

No Authentik 2024.12.x:

1. `Applications` -> `Providers` -> `Create`
2. Tipo: `OAuth2/OpenID Provider`
3. Preencha os campos abaixo (se não listado, deixe padrão):

| Campo | Valor |
|------|------|
| Name | `payment-gateway` |
| Slug | `payment-gateway` |
| Client type | `Confidential` |
| Client ID | `payment-gateway` |
| Client Secret | gerar/copiar (você vai usar no frontend) |
| Redirect URIs (Strict) | `http://localhost:3000/api/auth/callback/authentik` |
| Redirect URIs (Strict) | `http://127.0.0.1:3000/api/auth/callback/authentik` |
| Signing Key | padrão do Authentik |
| Scopes | `openid`, `profile`, `email` |

4. Salve o provider e copie:
   - `Client ID`
   - `Client Secret`
   - `Issuer` (deve ficar com o slug do provider)

Issuer esperado para este projeto:

`https://authentik.home.arpa/application/o/payment-gateway/`

OpenID metadata esperado:

`https://authentik.home.arpa/application/o/payment-gateway/.well-known/openid-configuration`

## 3. Criar Application no Authentik

1. `Applications` -> `Create`
2. Configure:
   - Name: `payment-gateway-web`
   - Slug: `payment-gateway-web`
   - Provider: `payment-gateway`
   - Launch URL: `http://localhost:3000/auth/signin`

## 4. Configurar Groups, Roles e Claims (obrigatório)

### 4.1 Groups

Crie os grupos:

- `admin`
- `Merchant`

`admin` precisa existir exatamente com esse nome para casar com `AdminRoles` do backend (`Admin`, `admin`).

### 4.2 Criar mapping para claim `merchant_id`

No usuário merchant, defina atributos (JSON) com um GUID válido:

```json
{
  "merchant_id": "11111111-1111-1111-1111-111111111111"
}
```

Onde isso fica na UI:

1. Menu lateral: `Customization` -> `Property Mappings`
2. Clique em `Create`
3. Escolha o tipo `Scope Mapping`
4. Preencha:
   - `Name`: `pgw-merchant-id`
   - `Scope name`: `openid`
   - `Description`: `Expose merchant_id claim for PaymentGateway`
5. No campo `Expression`, cole:

```python
merchant_id = request.user.attributes.get("merchant_id")
if merchant_id:
    return {"merchant_id": merchant_id}
return {}
```

6. Salve.

Depois disso, o mapping ainda nao esta sendo usado pelo provider. Voce precisa vincular:

1. `Applications` -> `Providers`
2. Abra o provider `payment-gateway`
3. Clique em `Edit`
4. Abra `Advanced protocol settings`
5. Na area de `Scopes` ou `Property mappings`, adicione `pgw-merchant-id`
6. Salve

Resultado esperado:
- o token emitido pelo Authentik passa a carregar a claim `merchant_id`
- o backend usa essa claim para limitar o merchant aos proprios recursos

### 4.3 Criar mapping para `roles` e `groups`

O backend aceita roles vindas de `roles`, `role` ou `groups`.

Onde isso fica na UI:

1. `Customization` -> `Property Mappings` -> `Create`
2. Escolha `Scope Mapping`
3. Preencha:
   - `Name`: `pgw-roles-groups`
   - `Scope name`: `openid`
   - `Description`: `Expose groups and roles for PaymentGateway`
4. No campo `Expression`, cole:

```python
group_names = [g.name for g in request.user.ak_groups.all()]
return {
    "groups": group_names,
    "roles": group_names
}
```

5. Salve.
6. Volte em `Applications` -> `Providers` -> `payment-gateway` -> `Edit`
7. Em `Advanced protocol settings`, associe `pgw-roles-groups`

Resultado esperado:
- usuario no grupo `admin` recebe `admin` em `groups` e `roles`
- usuario no grupo `Merchant` recebe `Merchant` em `groups` e `roles`

Observação importante:
- O backend aceita roles em `roles`, `role` ou `groups`.
- Para liberar endpoints administrativos, o usuário precisa receber role `admin` (ou `Admin`).

## 5. Criar Usuários no Authentik

Crie dois usuários:

1. Usuário admin
   - adiciona ao grupo `admin`
2. Usuário merchant
   - adiciona ao grupo `Merchant`
   - define atributo `merchant_id` com GUID do merchant

### 5.1 Onde colocar o `merchant_id` no usuario

No Authentik:

1. Menu lateral: `Directory` -> `Users`
2. Abra o usuario merchant que voce criou
3. Clique em `Edit`
4. Procure a secao `Attributes`
5. No editor JSON/YAML, coloque:

```json
{
  "merchant_id": "11111111-1111-1111-1111-111111111111"
}
```

6. Salve

Se o usuario ja tiver outros atributos, nao sobrescreva tudo. Apenas adicione a chave `merchant_id`. Exemplo:

```json
{
  "department": "sales",
  "merchant_id": "11111111-1111-1111-1111-111111111111"
}
```

Depois disso:

1. Ainda no usuario, abra a aba `Groups`
2. Adicione o grupo `Merchant`
3. Salve

Resultado esperado:
- o usuario continua sendo um usuario comum do Authentik
- o token OIDC desse usuario passa a carregar `merchant_id`, desde que o mapping `pgw-merchant-id` esteja vinculado ao provider

## 6. Configurar Backend (.NET API)

Arquivo: `src/PaymentGateway.Api/appsettings.Development.json`

Confirme bloco `Authentik`:

```json
"Authentik": {
  "Authority": "https://authentik.home.arpa/application/o/payment-gateway/",
  "Audience": "payment-gateway",
  "ValidAudiences": ["account"],
  "RequireHttpsMetadata": true,
  "MerchantIdClaim": "merchant_id",
  "RoleClaims": ["roles", "role", "groups"],
  "AdminRoles": ["Admin", "admin"]
}
```

Observação: se houver problema de trust TLS no ambiente local, use temporariamente:

```json
"RequireHttpsMetadata": false
```

(apenas desenvolvimento)

## 7. Configurar Frontend (NextAuth)

Arquivo: `web/payment_gateway_web/.env.local`

Você pode copiar de `.env.local.example` e ajustar:

```bash
AUTHENTIK_CLIENT_ID=payment-gateway
AUTHENTIK_CLIENT_SECRET=<client-secret-do-provider>
AUTHENTIK_ISSUER=https://authentik.home.arpa/application/o/payment-gateway/

NEXT_PUBLIC_API_URL=http://localhost:5000
API_URL=http://localhost:5000

NEXTAUTH_URL=http://localhost:3000
NEXTAUTH_SECRET=<gerar-com-openssl-rand-base64-32>
```

Gerar segredo:

```bash
openssl rand -base64 32
```

Se Node não confiar no certificado local (somente dev):

```bash
export NODE_TLS_REJECT_UNAUTHORIZED=0
```

## 8. Merchant da Demo (rápido)

Para um ambiente local limpo, rode o smoke uma vez para garantir seed de merchant:

```bash
cd demo/PaymentGateway
./scripts/smoke-e2e-wsl.sh --stop-started-containers
```

Seed padrão utilizado no smoke:

- Merchant ID: `11111111-1111-1111-1111-111111111111`
- API Key: `sk_test_smoke_merchant`

Use o mesmo `merchant_id` no atributo do usuário merchant no Authentik.

## 9. Subir a demo

Backend:

```bash
cd demo/PaymentGateway
dotnet run --project src/PaymentGateway.Api
```

Serviços:

```bash
dotnet run --project src/services/PaymentGateway.Services.PaymentProcessor
dotnet run --project src/services/PaymentGateway.Services.FraudDetector
dotnet run --project src/services/PaymentGateway.Services.Notification
dotnet run --project src/services/PaymentGateway.Services.Settlement
dotnet run --project src/services/PaymentGateway.Services.TransactionHistory
```

Frontend:

```bash
cd web/payment_gateway_web
npm install
npm run dev
```

## 10. Validação funcional (checklist)

1. Acesse `http://localhost:3000/auth/signin` e faça login com Authentik.
2. Em `Settings`, salve `merchantId` + `apiKey` do merchant.
3. Abra `Payments`, `Merchants`, `Webhooks`, `Analytics`.
4. Confirme que:
   - sem token: API retorna `401`
   - merchant tentando outro merchant: `403`
   - admin consegue endpoint administrativo (ex.: criação de merchant)

## 11. Troubleshooting rápido

- `401 invalid_token`:
  - `Authority`/`AUTHENTIK_ISSUER` incorretos
  - `Audience` não corresponde ao client/provider
- `403` em rotas de merchant:
  - token sem claim `merchant_id`
  - `merchant_id` não é GUID válido
  - `merchant_id` do token difere do recurso solicitado
- `403` em rotas admin:
  - token sem role `admin`/`Admin`
- erro no callback do NextAuth:
  - redirect URI não cadastrada no provider
- falha TLS local:
  - cert local não confiável no runtime (Node/.NET)

## 12. Endpoints úteis de verificação

Well-known OIDC:

```bash
curl -k https://authentik.home.arpa/application/o/payment-gateway/.well-known/openid-configuration
```

Health da API:

```bash
curl http://localhost:5000/health
```
