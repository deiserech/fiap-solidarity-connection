# Validacao Local com Docker Compose (Core + Donations)

## Objetivo

Subir os dois servicos localmente com Docker Compose para validar o fluxo fim a fim:

1. Core API publica `DonationRequestedEvent`.
2. Donations consome, processa e publica `DonationProcessedEvent`.
3. Core API consome e atualiza `total_raised_amount`.

## Pre-requisitos

1. Docker Desktop com Compose habilitado.
2. Dois repositorios lado a lado:
   - `c:/Repos/HACKATON/fiap-solidarity-connection`
   - `c:/Repos/HACKATON/fiap-solidarity-connection-donations`
3. SQL Server acessivel (local ou cloud).
4. Azure Service Bus acessivel.
5. (Opcional) New Relic License Key para telemetria.

Se voce estiver usando o SQL Server trazido pelo proprio Docker Compose desta validacao, o client no Windows deve conectar em `localhost,1434`.

## Passo 1 - Criar arquivo .env

No repositorio `fiap-solidarity-connection`, crie um arquivo `.env` na raiz com este modelo:

```env
CORE_DB_CONNECTION=Server=<server>;Database=<core_db>;User Id=<user>;Password=<password>;TrustServerCertificate=True;Encrypt=False;
DONATIONS_DB_CONNECTION=Server=<server>;Database=<donations_db>;User Id=<user>;Password=<password>;TrustServerCertificate=True;Encrypt=False;
SERVICEBUS_CONNECTION=Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<name>;SharedAccessKey=<key>
JWT_SECRET=<secret_min_32_chars>
JWT_ISSUER=solidarity-connection
JWT_AUDIENCE=solidarity-connection-clients
NEW_RELIC_LICENSE_KEY=
```

## Passo 2 - Criar arquivo docker-compose.validation.yml

Ainda na raiz de `fiap-solidarity-connection`, crie `docker-compose.validation.yml`:

```yaml
services:
  core-api:
    build:
      context: .
      dockerfile: src/SolidarityConnection.Api/Dockerfile
    container_name: solidarity-core-api
    ports:
      - "8080:80"
    environment:
      ConnectionStrings__DefaultConnection: ${CORE_DB_CONNECTION}
      ServiceBus__ConnectionString: ${SERVICEBUS_CONNECTION}
      DONATION_PROCESSED_TOPIC: donation-processed
      DONATION_PROCESSED_SUBSCRIPTION: solidarity-connection-core-api
      JwtSettings__SecretKey: ${JWT_SECRET}
      JwtSettings__Issuer: ${JWT_ISSUER}
      JwtSettings__Audience: ${JWT_AUDIENCE}
      NewRelic__OtlpEndpoint: https://otlp.nr-data.net:4317
      NewRelic__Protocol: grpc
      NEW_RELIC_LICENSE_KEY: ${NEW_RELIC_LICENSE_KEY}

  donations-api:
    build:
      context: ../fiap-solidarity-connection-donations
      dockerfile: src/SolidarityConnection.Donations.Api/Dockerfile
    container_name: solidarity-donations-api
    ports:
      - "8081:80"
    environment:
      ConnectionStrings__DefaultConnection: ${DONATIONS_DB_CONNECTION}
      ServiceBus__ConnectionString: ${SERVICEBUS_CONNECTION}
      DONATION_TOPIC: donation-requested
      DONATION_SUBSCRIPTION: solidarity-connection-donations-api
      DonationProcessing__MaxAttempts: "3"
      NewRelic__OtlpEndpoint: https://otlp.nr-data.net:4317
      NewRelic__Protocol: grpc
      NEW_RELIC_LICENSE_KEY: ${NEW_RELIC_LICENSE_KEY}
```

## Passo 3 - Build e subida

Na raiz de `fiap-solidarity-connection`:

```bash
docker compose --env-file .env -f docker-compose.validation.yml build 
```

```bash
docker compose --env-file .env -f docker-compose.validation.yml up -d 
```

Verificar status:

```bash
docker compose --env-file .env -f docker-compose.validation.yml ps 
```

Verificar health:

```bash
curl http://localhost:8080/health
curl http://localhost:8081/health
```

## Passo 4 - Validacao funcional minima

## 4.1 Registrar doador

```bash
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "fullName":"Doador Demo",
    "email":"doador.demo@fiap.com",
    "cpf":"12345678901",
    "password":"P@ssword123!"
  }'
```

## 4.2 Login do doador e obter token

```bash
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email":"doador.demo@fiap.com",
    "password":"P@ssword123!"
  }'
```

Guarde o token JWT retornado.

## 4.3 Buscar campanha ativa

```bash
curl http://localhost:8080/api/campaigns/public
```

Pegue um `campaignId` de campanha ativa.

## 4.4 Enviar intencao de doacao

```bash
curl -X POST http://localhost:8080/api/donations \
  -H "Authorization: Bearer <TOKEN_JWT>" \
  -H "Content-Type: application/json" \
  -d '{
    "campaignId":"<CAMPAIGN_ID>",
    "donationAmount":50.00
  }'
```

Resposta esperada: `202 Accepted` com `status = "Received"`.

## 4.5 Confirmar atualizacao de arrecadacao

```bash
curl http://localhost:8080/api/campaigns/public
```

Resultado esperado: aumento de `total_raised_amount` na campanha alvo.

## Passo 5 - Diagnostico rapido

Logs do Core:

```bash
docker logs solidarity-core-api --tail 200
```

Logs do Donations:

```bash
docker logs solidarity-donations-api --tail 200
```

Parar stack:

```bash
docker compose -f docker-compose.validation.yml down
```

## Criterio de aceite desta validacao local

1. Dois health checks respondendo.
2. Doacao aceita pelo Core (`202`).
3. Core e Donations processando eventos no Service Bus.
4. `total_raised_amount` atualizado sem acesso cruzado de banco.
