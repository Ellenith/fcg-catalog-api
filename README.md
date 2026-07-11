
# FIAP Cloud Gaming - Fase 02

Tech Challenge Pos-graduacao FIAP.

# FCG — Catalog API

Microsserviço de Catálogo da FIAP Cloud Games. Responsável pelo CRUD de
jogos, promoções e por iniciar e concluir o fluxo de compra.

Parte da Fase 2 do Tech Challenge — arquitetura de microsserviços orientada
a eventos.

---

## Responsabilidades

- CRUD de jogos e promoções
- Cálculo de preço atual considerando promoções ativas
- Inicia o fluxo de compra publicando `OrderPlacedEvent`
- Consome `PaymentProcessedEvent` e adiciona o jogo à biblioteca do
  usuário caso o pagamento seja aprovado
- Consulta da biblioteca de jogos adquiridos por usuário

---

## Tecnologias

- .NET 9
- Entity Framework Core + SQLite
- MassTransit + RabbitMQ (publica e consome eventos)
- JWT Bearer Authentication (valida tokens emitidos pelo UsersAPI)
- Serilog (logs em arquivo)
- Swagger / OpenAPI

---

## Endpoints

| Método | Endpoint | Descrição | Acesso |
|--------|----------|-----------|--------|
| POST | `/jogos` | Cadastrar jogo | Admin |
| GET | `/jogos` | Listar todos os jogos | Público |
| GET | `/jogos/{id}` | Buscar jogo por Id | Público |
| PUT | `/jogos/{id}` | Atualizar preço | Admin |
| DELETE | `/jogos/{id}` | Remover jogo | Admin |
| GET | `/jogos/promocoes` | Listar promoções ativas | Público |
| POST | `/jogos/{id}/promocoes` | Criar promoção | Admin |
| POST | `/jogos/{id}/comprar` | Iniciar compra de um jogo | Autenticado |
| GET | `/biblioteca` | Listar jogos adquiridos | Autenticado |

---

## Fluxo de compra (orientado a eventos)

1. Cliente chama POST /jogos/{id}/comprar
2. CatalogAPI publica OrderPlacedEvent (OrderId, UsuarioId, JogoId, Preco)
3. PaymentsAPI consome, simula o processamento e publica
PaymentProcessedEvent (Status: Approved | Rejected)
4. CatalogAPI consome PaymentProcessedEvent

→ se Approved: adiciona o jogo à biblioteca do usuário  
→ se Rejected: nenhuma ação, apenas log

> Importante: o jogo NÃO é adicionado à biblioteca no momento da chamada
> de `/comprar`. A resposta é `202 Accepted` — o cliente deve consultar
> `GET /biblioteca` posteriormente para confirmar a aquisição.

---

## Eventos publicados

### `OrderPlacedEvent`

```csharp
public record OrderPlacedEvent(
    Guid     OrderId,
    Guid     UsuarioId,
    Guid     JogoId,
    string   TituloJogo,
    decimal  Preco,
    DateTime CriadoEm);
```

## Eventos consumidos

### `PaymentProcessedEvent`

```csharp
public record PaymentProcessedEvent(
    Guid     OrderId,
    Guid     UsuarioId,
    Guid     JogoId,
    decimal  Preco,
    string   Status,     // "Approved" ou "Rejected"
    string   Mensagem,
    DateTime ProcessadoEm);
```

---

## Variáveis de ambiente

| Variável | Descrição | Exemplo |
|----------|-----------|---------|
| `ConnectionStrings__DefaultConnection` | Connection string do SQLite | `Data Source=/data/catalog.db` |
| `Jwt__Secret` | Mesma chave usada pelo UsersAPI | `fcg-super-secret-key-min-32-chars!!` |
| `Jwt__Issuer` | Emissor do token (deve bater com UsersAPI) | `FCG.UsersAPI` |
| `Jwt__Audience` | Audiência do token | `FCG.Client` |
| `RabbitMQ__Host` | Host do RabbitMQ | `rabbitmq` (Docker) ou `localhost` |
| `RabbitMQ__Usuario` | Usuário do RabbitMQ | `guest` |
| `RabbitMQ__Senha` | Senha do RabbitMQ | `guest` |

> ⚠️ O `Jwt__Secret` e `Jwt__Issuer` devem ser **idênticos** aos
> configurados no `UsersAPI` — o CatalogAPI valida tokens emitidos
> por aquele serviço, mas não os gera.

---

## Como rodar localmente

**Pré-requisitos:**
- .NET 9 SDK
- RabbitMQ rodando (local ou via Docker)
- UsersAPI rodando (para gerar tokens JWT válidos)

```bash
dotnet restore
dotnet run --project FCG.CatalogAPI
```

Acesse o Swagger em `https://localhost:{porta}/swagger`.

---

## Como rodar com Docker

```bash
docker build -t fcg-catalog-api .
docker run -p 5002:8080 \
  -e RabbitMQ__Host=host.docker.internal \
  -e Jwt__Secret=fcg-super-secret-key-min-32-chars!! \
  fcg-catalog-api
```

> Recomendado: use o `docker-compose.yml` do repositório de orquestração
> para subir todos os serviços juntos.

---

## Limitações conhecidas

### Condição de corrida na validação de duplicidade

O endpoint `POST /jogos/{id}/comprar` valida previamente se o usuário
já possui o jogo, evitando iniciar o fluxo de pagamento para compras
que sabidamente falhariam. Essa validação, no entanto, **não elimina
por completo** uma condição de corrida: duas requisições de compra
para o mesmo jogo, disparadas quase simultaneamente pelo mesmo
usuário, podem ambas passar pela validação antes que a primeira seja
concluída — resultando em dois `OrderPlacedEvent` publicados e,
potencialmente, dois pagamentos aprovados para o mesmo jogo.

**Defesa em profundidade:** mesmo nesse cenário extremo, a duplicidade
**nunca chega a ser persistida** graças ao índice único composto
`(UsuarioId, JogoId)` na tabela `Biblioteca` — o segundo `INSERT`
falharia a nível de banco de dados. O usuário não seria cobrado
duas vezes na biblioteca, mas o segundo pagamento aprovado não
seria automaticamente estornado.

**Solução completa (fora do escopo desta fase):** o tratamento
correto para este tipo de cenário em um sistema de produção seria
um padrão de **Saga com compensação** — se o `CatalogAPI` detectar
a duplicidade após o pagamento já aprovado, ele publicaria um evento
de estorno (`PaymentRefundRequestedEvent`) para o `PaymentsAPI`
reverter a cobrança automaticamente. Essa solução foi
conscientemente deixada de fora do MVP da Fase 2 por não ser
exigida pelo Tech Challenge e por representar um esforço de
implementação desproporcional ao benefício nesta etapa do projeto.

---

## Regras de negócio

- Somente Admin cadastra, atualiza e remove jogos
- Título deve ser único no catálogo
- Preço não pode ser negativo (jogos gratuitos são permitidos)
- Desconto de promoção entre 1% e 100%
- Preço atual é calculado em tempo real via `GetCurrentPrice()`
- Um usuário não pode adquirir o mesmo jogo duas vezes

---

*FIAP Cloud Games · Tech Challenge Fase 2 · PosTech FIAP*