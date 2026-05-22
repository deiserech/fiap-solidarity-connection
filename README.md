# SolidarityConnection

Plataforma principal do projeto Solidarity Connection. Este repositorio contem a API Core, responsavel por autenticacao, campanhas, recebimento da solicitacao de doacao e consumo do evento de doacao processada.

## Visao geral

O Core funciona integrado com o repositório de Donations. No fluxo principal:

1. O usuario se autentica no Core.
2. O Core publica `DonationRequestedEvent` no Azure Service Bus.
3. A API de Donations consome o evento, processa a doacao e grava o resultado.
4. A API de Donations publica `DonationProcessedEvent`.
5. O Core consome o evento e atualiza o total arrecadado da campanha.

## Estrutura do repositorio

- `src/SolidarityConnection.Api`: API principal da aplicacao
- `src/SolidarityConnection.Application`: regras de negocio e casos de uso
- `src/SolidarityConnection.Domain`: entidades, enums e eventos de dominio
- `src/SolidarityConnection.Infrastructure`: persistencia, repositarios e integracoes
- `src/SolidarityConnection.Shared`: contratos e utilitarios compartilhados
- `tests/SolidarityConnection.Tests`: testes automatizados
- `others/docs`: documentacao de apoio e validacoes
- `k8s/`: manifests Kubernetes
- `pipeline/`: Azure Pipelines

## Requisitos

- .NET SDK 8.x
- Docker Desktop
- SQL Server local ou em Docker para a validacao local
- Azure Service Bus acessivel para a validacao fim a fim
- `kubectl` e Azure CLI somente se for publicar no AKS

## Exemplo de variáveis de ambiente locais

Defina as variáveis abaixo na sua sessão de terminal ou em um arquivo local fora do repositório. Substitua todos os valores de exemplo pelos seus dados locais ou de ambiente.

```env
SQL_SA_PASSWORD=<SUA_SENHA_FORTE>
CORE_DB_CONNECTION=Server=sqlserver,1433;Database=core-db;User Id=SA;Password=<SUA_SENHA_FORTE>;TrustServerCertificate=True;
DONATIONS_DB_CONNECTION=Server=sqlserver,1433;Database=donations-db;User Id=SA;Password=<SUA_SENHA_FORTE>;TrustServerCertificate=True;
SERVICEBUS_CONNECTION=Endpoint=sb://<NAMESPACE>.servicebus.windows.net/;SharedAccessKeyName=<NOME_DA_CHAVE>;SharedAccessKey=<SUA_CHAVE>;
JWT_SECRET=<SUA_CHAVE_JWT_COM_32_OU_MAIS_CARACTERES>
JWT_ISSUER=solidarityconnection-core-api
JWT_AUDIENCE=solidarityconnection
NEW_RELIC_LICENSE_KEY=<SUA_CHAVE_NEW_RELIC>
```

## Como subir localmente

### Opcao recomendada 

Ambos os servicos fazem bootstrap automatico do banco ao iniciar: se o banco ainda nao existir, ele e criado e as migracoes EF Core sao aplicadas antes da API ficar disponivel.

A forma mais simples de validar a solucao inteira e manter os dois repositorios lado a lado:

- `c:\Repos\HACKATON\fiap-solidarity-connection`
- `c:\Repos\HACKATON\fiap-solidarity-connection-donations`

Depois siga o guia de validacao local do Core:

- `others/docs/validacao-local-docker-compose.md`

Passo a passo:

1. Crie um arquivo `.env` local com os valores do bloco acima.
2. Verifique se o repositório `fiap-solidarity-connection-donations` esta no mesmo nivel de pasta do Core.
3. Na raiz do Core, suba a stack passando o arquivo como parametro:

```bash
docker compose --env-file .env -f docker-compose.validation.yml up -d --build
```

4. Aguarde os containers subirem e valide os health checks:

```bash
curl http://localhost:8080/health
curl http://localhost:8081/health
```

5. Use o Core para registrar usuario, autenticar e enviar uma doacao.

## Execucao local somente do Core

Use esta opcao se quiser subir apenas a API principal para debug.

1. Restore das dependencias:

```bash
dotnet restore SolidarityConnection.sln
```

2. Inicie um SQL Server local ou em Docker. Exemplo com Docker:

```bash
docker run -d --name solidarity-core-sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=<SUA_SENHA_FORTE> -p 1433:1433 mcr.microsoft.com/mssql/server:2025-latest
```

3. Configure a connection string do banco e a connection string do Service Bus por variavel de ambiente ou user-secrets. A aplicacao tambem carrega `appsettings.Development.json` quando executada em desenvolvimento.

Exemplo com user-secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=core-db;User Id=SA;Password=<SUA_SENHA_FORTE>;TrustServerCertificate=True;Encrypt=False;" --project src/SolidarityConnection.Api/SolidarityConnection.Api.csproj
dotnet user-secrets set "ServiceBus:ConnectionString" "Endpoint=sb://<NAMESPACE>.servicebus.windows.net/;SharedAccessKeyName=<NOME_DA_CHAVE>;SharedAccessKey=<SUA_CHAVE>;" --project src/SolidarityConnection.Api/SolidarityConnection.Api.csproj
```

4. Execute a aplicacao:

```bash
dotnet run --project src/SolidarityConnection.Api/SolidarityConnection.Api.csproj
```

5. Abra o health check para confirmar a subida:

```bash
curl http://localhost:5060/health
```

Observacao: no perfil de desenvolvimento, a aplicacao expõe `http://localhost:5060` e `https://localhost:5050`.

## Validacao integrada com Donations

O fluxo esperado entre os repositorios e:

1. O Core publica `DonationRequestedEvent`.
2. Donations consome e processa a doacao.
3. Donations publica `DonationProcessedEvent`.
4. O Core atualiza `total_raised_amount` da campanha.

Se o objetivo for correção manual dos professores, o caminho recomendado continua sendo o compose de validacao no repositorio Core, porque ele sobe o SQL Server e os dois servicos com um unico comando.

## Testes

```bash
dotnet test SolidarityConnection.sln -c Release
```

## Docker

Build da imagem:

```bash
docker build -f src/SolidarityConnection.Api/Dockerfile -t solidarity-connection:local .
```

Executar localmente:

```bash
docker run -p 8080:80 solidarity-connection:local
```

## Kubernetes (AKS)

Os manifests estao em `k8s/`.

Aplicar:

```bash
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/shared-secret.yaml
kubectl apply -f k8s/users-secret.yaml
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/hpa.yaml
```

Verificar rollout:

```bash
kubectl rollout status deployment/solidarity-connection
```

## Configuracoes importantes

As variaveis mais relevantes sao:

- `ConnectionStrings__DefaultConnection`
- `ServiceBus__ConnectionString`
- `DONATION_TOPIC`
- `DONATION_PROCESSED_TOPIC`
- `DONATION_PROCESSED_SUBSCRIPTION`
- `JwtSettings__SecretKey`
- `JwtSettings__Issuer`
- `JwtSettings__Audience`

No ambiente local, a connection string normalmente aponta para `localhost,1433` e um banco como `core-db`, mas voce pode usar qualquer SQL Server acessivel desde que ajuste a string.

## Pipelines

O pipeline esta em `pipeline/azure-pipelines.yml` e executa:

- Build
- Testes
- Build e publish da imagem
- Deploy no AKS nas branches `develop` e `main`

## Documentacao adicional

- `others/docs/arquitetura-microsservicos-doacoes.md`
- `others/docs/plano-implementacao-mvp.md`
- `others/docs/validacao-local-docker-compose.md`
- `others/docs/validacao-kubernetes-docker-desktop.md`
- `others/docs/roteiro-video-apresentacao-15min.md`