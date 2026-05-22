# Arquitetura Atual - Solidarity Connection

Este documento descreve a arquitetura atual dos dois repositórios do projeto: `fiap-solidarity-connection` e `fiap-solidarity-connection-donations`.

## Visão geral

O sistema está dividido em dois serviços principais:

- Core: autenticação, campanhas, solicitação de doação e consumo do evento de doação processada.
- Donations: consumo do evento de doação solicitada, processamento assíncrono e publicação do evento de processamento.

Os dois serviços se comunicam por Azure Service Bus e persistem dados em seus respectivos bancos SQL Server.

## Fluxo funcional

```mermaid
flowchart LR
  U[Usuário / Doador] --> C[Core API]
  ONG[Gestor ONG] --> C

  C --> CDB[(SQL Server Core)]
  C --> SB[(Azure Service Bus)]

  SB --> D[Donations API]
  D --> DDB[(SQL Server Donations)]

  D --> SB
  SB --> C

  C --> OBS1[Health / Metrics / Logs]
  D --> OBS2[Health / Metrics / Logs]

  OBS1 --> NR[New Relic]
  OBS2 --> NR
```

## Fluxo de requisição e processamento

```mermaid
sequenceDiagram
  participant User as Usuário / Doador
  participant Core as Core API
  participant SB as Azure Service Bus
  participant Donations as Donations API

  User->>Core: Login / criar doação
  Core->>Core: Validar regra de negócio
  Core->>SB: Publicar DonationRequestedEvent
  SB->>Donations: Entregar DonationRequestedEvent
  Donations->>Donations: Processar doação e persistir
  Donations->>SB: Publicar DonationProcessedEvent
  SB->>Core: Entregar DonationProcessedEvent
  Core->>Core: Atualizar total arrecadado
```

## Visão de implantação no AKS

```mermaid
flowchart TB
  subgraph Azure[Azure Subscription]
    ACR[Azure Container Registry]
    ASB[Azure Service Bus]
    SQLC[(SQL Core)]
    SQLD[(SQL Donations)]

    subgraph AKS[AKS Cluster]
      subgraph SVC[Services]
        Csvc[Core Service\nLoadBalancer 8080:80]
        Dsvc[Donations Service\nLoadBalancer 8081:80]
      end

      subgraph PODS[Pods / Deployments]
        Cpod[Core Pods\nDeployment + HPA]
        Dpod[Donations Pods\nDeployment + HPA]
      end

      subgraph CM[ConfigMaps]
        Ccfg[core-api-config]
        Dcfg[donations-api-config]
      end

      subgraph SEC[Secrets]
        Csec[core-secret\nJWT / Service Bus / DB]
        Dsec[donations-secret\nService Bus / DB]
        Ssec[shared-secret\nService Bus / Observability]
      end
    end
  end

  Client[Client / Browser / Front-end] --> Csvc
  Client --> Dsvc

  Csvc --> Cpod
  Dsvc --> Dpod

  Cpod --> Ccfg
  Dpod --> Dcfg
  Cpod --> Csec
  Dpod --> Dsec
  Cpod --> Ssec
  Dpod --> Ssec

  Cpod --> ACR
  Dpod --> ACR
  Cpod --> ASB
  Dpod --> ASB
  Cpod --> SQLC
  Dpod --> SQLD
```

## Observações

- O Core não escreve no banco da Donations e a Donations não escreve no banco do Core.
- O Azure Service Bus é o ponto de integração assíncrona entre os dois serviços.
- O `health` de cada aplicação é usado para validação de subida local e monitoração básica.