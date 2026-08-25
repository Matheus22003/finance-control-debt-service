# Finance Control - Debt Service

Microserviço responsável por pessoas, despesas compartilhadas, divisões, pagamentos, histórico e simplificação de transferências.

## Stack

- .NET `10`
- Minimal APIs
- Entity Framework Core `10.0.10`
- Npgsql EF Core Provider `10.0.3`
- PostgreSQL
- OpenAPI nativo 3.1
- Scalar e Swagger UI
- ProblemDetails
- Docker

## Modelo

`Debt` representa uma despesa compartilhada e é o aggregate root. Cada dívida possui:

- uma pessoa pagadora;
- uma ou mais participações (`DebtShare`);
- pagamentos aplicados às participações;
- histórico append-only das alterações;
- status `OPEN` ou `PAID`.

A pessoa pagadora pode ou não participar da divisão. Quando participa, sua própria cota já nasce quitada. A soma das cotas deve ser exatamente igual ao valor total.

Uma única pessoa pode ser marcada como `isCurrentUser`. O resumo usa essa pessoa para calcular quanto ela deve e quanto tem a receber.

## Endpoints

| Método | Caminho | Descrição |
|---|---|---|
| `GET` | `/health` | Estado da aplicação |
| `GET` | `/api/v1/people` | Lista pessoas |
| `GET` | `/api/v1/people/{id}` | Consulta uma pessoa |
| `POST` | `/api/v1/people` | Cria uma pessoa |
| `PUT` | `/api/v1/people/{id}` | Atualiza uma pessoa |
| `DELETE` | `/api/v1/people/{id}` | Exclui uma pessoa sem vínculos |
| `GET` | `/api/v1/debts` | Lista despesas compartilhadas |
| `GET` | `/api/v1/debts/{id}` | Consulta uma despesa |
| `POST` | `/api/v1/debts` | Cria uma despesa e suas cotas |
| `PUT` | `/api/v1/debts/{id}` | Atualiza descrição, categoria e vencimento |
| `DELETE` | `/api/v1/debts/{id}` | Exclui o aggregate |
| `GET` | `/api/v1/debts/summary` | Resumo relativo à pessoa atual |
| `GET` | `/api/v1/debts/settlements/simplified` | Sugere transferências líquidas |
| `GET` | `/api/v1/debts/settlements/simplified/transfers` | Lista o plano simplificado ativo |
| `GET` | `/api/v1/debts/settlements/simplified/transfers/pending-confirmation` | Lista transferências aguardando confirmação do usuário |
| `POST` | `/api/v1/debts/settlements/simplified/transfers` | Registra uma transferência sugerida |
| `POST` | `/api/v1/debts/settlements/simplified/transfers/{transferId}/confirm` | Confirma o recebimento |
| `POST` | `/api/v1/debts/settlements/simplified/transfers/{transferId}/reject` | Recusa a transferência e cancela o plano |
| `GET` | `/api/v1/debts/{id}/payments` | Lista pagamentos da dívida |
| `POST` | `/api/v1/debts/{id}/shares/{shareId}/payments` | Registra pagamento em uma cota |
| `PUT` | `/api/v1/debts/{id}/payments/{paymentId}` | Corrige um pagamento |
| `DELETE` | `/api/v1/debts/{id}/payments/{paymentId}` | Remove um pagamento e reabre a dívida quando necessário |
| `GET` | `/api/v1/debts/{id}/history` | Consulta o histórico da dívida |
| `GET` | `/api/v1/debts/analysis-context` | Calcula totais, vencimentos e concentrações para análise pelo BFF |

Endpoints internos de ciclo de vida:

- `GET /api/v1/internal/account-data/deletion-eligibility`: verifica pendências que bloqueiam a exclusão da conta;
- `DELETE /api/v1/internal/account-data`: remove vínculos privados e anonimiza históricos concluídos.

Os endpoints internos recebem o usuário no header `X-Finance-Control-User-Id` enviado pelo BFF e ficam restritos à rede interna no Docker Compose. A remoção é idempotente.

Em `Development`, também estão disponíveis:

- OpenAPI: `/openapi/v1.json`
- Scalar: `/scalar/v1`
- Swagger UI: `/swagger`

## Contexto analítico de dívidas

O endpoint `/api/v1/debts/analysis-context` calcula de forma determinística a posição do usuário em todas as dívidas acessíveis: total a pagar, total a receber, itens quitados, atrasados, próximos do vencimento e concentração por categoria e grupo. O Debt Service continua responsável por todos os cálculos; o BFF sanitiza o resultado antes de utilizá-lo com qualquer provedor de IA.

## Simplificação de transferências

O serviço calcula o saldo líquido de cada pessoa considerando todas as cotas pendentes. Um algoritmo determinístico combina devedores e credores e produz no máximo `n - 1` transferências para `n` pessoas com saldo diferente de zero.

O cálculo continua sendo uma projeção sem efeitos colaterais. Quando o pagador registra uma sugestão, o serviço cria um plano que guarda uma fotografia das cotas originais e todas as transferências simplificadas necessárias. Cada destinatário confirma o recebimento; somente depois de todas as confirmações o plano distribui os pagamentos entre as cotas originais e quita as dívidas de forma atômica.

Se alguma dívida mudar enquanto o plano estiver ativo, ele é cancelado e deve ser recalculado. Uma recusa também cancela o plano inteiro, evitando liquidação parcial ou saldos inconsistentes.

## Persistência

O schema é controlado pelas migrations do Entity Framework Core. A aplicação executa `Database.MigrateAsync()` ao iniciar fora do ambiente de testes.

Variável obrigatória fora de Development:

```text
ConnectionStrings__DebtDatabase
```

Valor local padrão em `appsettings.Development.json`:

```text
Host=localhost;Port=5432;Database=finance_control_debt;Username=debt_app;Password=local_debt_password
```

## Autenticação e acesso

O Debt Service não emite nem valida JWT. O BFF permanece como único ponto de autenticação e o serviço fica em rede interna no Docker Compose.

## Observabilidade

Cada requisição recebe um UUID no header `X-Correlation-ID`. O serviço preserva
um valor válido propagado pelo BFF ou gera um novo, devolve-o na resposta e o
inclui nas respostas ProblemDetails. Os logs são emitidos em JSON com o ID de
correlação, método, caminho, status HTTP e duração, sem registrar payloads ou
informações financeiras sensíveis.

## Execução local

O SDK é fixado em `10.0.301` pelo `global.json` e a ferramenta `dotnet-ef` em `10.0.10` pelo manifest local.

```powershell
Set-Location FinanceControl.DebtService
dotnet tool restore
dotnet restore FinanceControl.DebtService.sln --locked-mode
dotnet run --project src/FinanceControl.DebtService/FinanceControl.DebtService.csproj
```

A aplicação usa `http://localhost:5085` pelo perfil local.

## Migrations

```powershell
dotnet ef migrations add NomeDaMigration `
  --project src/FinanceControl.DebtService/FinanceControl.DebtService.csproj `
  --startup-project src/FinanceControl.DebtService/FinanceControl.DebtService.csproj `
  --output-dir Persistence/Migrations
```

## Testes

Os testes usam EF Core InMemory `10.0.10`. A migration e o provider Npgsql são validados adicionalmente no PostgreSQL real pelo Docker Compose.

```powershell
dotnet test FinanceControl.DebtService.sln --configuration Release
```

## Integração contínua

O workflow `.github/workflows/ci.yml` é executado em pushes e pull requests para
`main` e `develop`, além de permitir execução manual. A pipeline restaura as
dependências pelo lock file, executa os testes em `Release` e valida a imagem
Docker do Debt Service.

O workflow `.github/workflows/publish-image.yml` publica no GHCR uma imagem
multiarch `linux/amd64` e `linux/arm64` quando uma tag `v*` é criada ou por
execução manual. A publicação inclui SBOM e proveniência do build.

## Pacotes com versão direta

Aplicação:

- `Microsoft.AspNetCore.OpenApi` `10.0.10`
- `Microsoft.EntityFrameworkCore` `10.0.10`
- `Microsoft.EntityFrameworkCore.Relational` `10.0.10`
- `Microsoft.EntityFrameworkCore.Design` `10.0.10`
- `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.3`
- `Microsoft.OpenApi` `2.11.0`
- `Scalar.AspNetCore` `2.16.17`
- `Swashbuckle.AspNetCore.SwaggerUI` `10.2.3`

Testes:

- `Microsoft.AspNetCore.Mvc.Testing` `10.0.10`
- `Microsoft.EntityFrameworkCore.InMemory` `10.0.10`
- `Microsoft.NET.Test.Sdk` `18.8.1`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `3.1.5`
