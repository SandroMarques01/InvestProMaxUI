# Invest Pro Max — Fases 1 a 5

Este pacote contém:

- **Fase 1 — Fundação**: Domain + Infrastructure (EF Core + migrations) + Api com autenticação JWT funcionando.
- **Fase 2 — Núcleo de investimentos**: CRUD de `Papel`/`Transação`/`Dividendo`/`Segmento` + regras de negócio 1, 2, 3 e 4, com testes.
- **Fase 3 — Importação e backup**: importação do extrato B3, importação de cotações, backup/restauração em Excel e "zerar base" (regras 5 e 9), com testes.
- **Fase 4 — Patrimônio**: CRUD de `Ativo`/`Inventário`, lançamento mensal em lote e sugestão de valor calculado (regra 6), com testes.
- **Fase 5 — Contas a pagar**: CRUD de `Conta`, geração mensal de `Fatura` e baixa de `Pagamento` (regras 7 e 8), com job diário automático, com testes.

As Fases 6 e 7 (front-end, polimento) ainda não foram implementadas.

## Revisão pós-entrega (2026-09-11)

Durante revisão de arquitetura deste pacote, duas lacunas foram identificadas e corrigidas:

1. **Detecção de reuso de refresh token** (`AuthService.RefreshAsync`): um token já revogado
   sendo reapresentado agora revoga todos os refresh tokens ativos do usuário (`
   IRefreshTokenRepository.RevogarTodosDoUsuarioAsync`), não só recusa a tentativa isolada —
   mitiga o cenário de um refresh token roubado sendo usado depois que o dono já rotacionou.
   Não estava na especificação original nem nas assunções sinalizadas; adicionado a pedido
   explícito do dono do produto.
2. **Teste de integração de isolamento por usuário** (seção 11, exigido e ausente do pacote
   original): novo projeto `tests/InvestProMax.IntegrationTests`, com `WebApplicationFactory`
   real (login via `/auth/login`, chamadas HTTP autenticadas de ponta a ponta). Usa o provider
   EF Core InMemory em vez de SQL Server — mesma limitação de ambiente do restante deste
   pacote (sem SQL Server disponível para gerar/aplicar migrations reais). Validar contra SQL
   Server real antes de considerar a cobertura da seção 11 definitiva; ver comentário em
   `CustomWebApplicationFactory.cs`.

## Fase 6 (2026-09-11): GET /dashboard + SharedUI completa

**Back-end**: novo módulo `Dashboard` (`DashboardService`/`DashboardController`,
`GET /api/dashboard`). A seção 8 não detalhava os campos do dashboard — assunções sinalizadas
em `DashboardDtos.cs`: patrimônio total = soma de `Inventario.Valor` do mês mais recente
lançado (não soma a carteira de ações separadamente, pois um Ativo Calculado já a inclui —
evita contar em dobro); dividendos por mês = últimos 12 meses corridos, com zero explícito
nos meses sem recebimento; alocação por tipo de papel = posição × cotação atual, agrupada por
`TipoPapel`.

**Front-end**: projeto `src/InvestProMax.SharedUI` (Razor Class Library) com as 10 telas da
seção 10 — Login, Dashboard, Papéis (com trocar/desdobramento), Transações, Dividendos,
Segmentos, Ativos, Inventário (lançamento em lote com sugestão calculada), Contas a pagar
(Conta + Fatura + pagamento) e Configurações (importar B3/cotações, backup/restaurar, zerar
base, alterar senha). Inclui:

- `IInvestProMaxApiClient`/`InvestProMaxApiClient`: cliente HTTP tipado com renovação
  silenciosa de access token via refresh token, e uma segunda tentativa automática se ainda
  assim vier 401 (cobre o caso de detecção de reuso revogar a sessão no meio de uma chamada).
- `AuthState` + `InvestProMaxAuthStateProvider`: ponte para `AuthorizeView`/`[Authorize]` do
  Blazor, sem acoplar a nenhum runtime específico (Web ou MAUI).
- `ITokenStorage`: interface pura — **cada head da Fase 7 precisa fornecer sua própria
  implementação** (SecureStorage no MAUI; cookie HttpOnly+Secure recomendado no Web — ver
  comentário em `ITokenStorage.cs` sobre por que localStorage é desaconselhado para o refresh
  token) e chamar `IInvestProMaxApiClient.RestaurarSessaoAsync()` uma vez na inicialização.
- `ILembreteService`: interface declarada (seção 9), sem implementação — fica para os heads.
- DTOs e enums são cópias explícitas dos do back-end (a SharedUI não referencia
  `Domain`/`Infrastructure` — seção 3); os valores dos enums têm que continuar batendo com
  `Domain/Enums` manualmente, já que a Api não serializa enum como string.
- Nenhum uso de `localStorage`/`sessionStorage` do navegador diretamente na SharedUI (só um
  pequeno `download.js` para o botão de backup, via `IJSRuntime` — funciona tanto em Blazor
  Web quanto em MAUI Blazor Hybrid, já que ambos rodam sobre um WebView com DOM).

**Ainda não compilado nem executado neste ambiente** (sem SDK do .NET, sem rede) — rode
`dotnet restore && dotnet build && dotnet test` localmente antes de seguir para a Fase 7.

## Fase 7 (2026-09-12): heads Web (Blazor WebAssembly) e MAUI Blazor Hybrid

**Web** (`src/InvestProMax.Web`): head Blazor WebAssembly standalone. `LocalStorageTokenStorage`
guarda o refresh token em `localStorage` — decisão explícita do dono do produto ("aplicação
caseira, faça o mais simples"), ciente do risco de XSS que isso implica (documentado também
em `ITokenStorage.cs`); a alternativa mais segura seria cookie `HttpOnly+Secure`, mas exigiria
mudar a Api (`Set-Cookie` em vez de devolver o token no corpo JSON de `/auth/login` e
`/auth/refresh`), não é só uma troca de storage no head. URL da Api configurável em
`wwwroot/appsettings.json` (`ApiBaseUrl`) — ajuste antes de rodar.

**MAUI** (`src/InvestProMax.Maui`): head MAUI Blazor Hybrid (Android/iOS/MacCatalyst).
`SecureStorageTokenStorage` usa `Microsoft.Maui.Storage.SecureStorage` (Keystore/Keychain).
URL da Api hardcoded em `MauiProgram.cs` com um switch simples por plataforma (10.0.2.2 pro
emulador Android, localhost pros demais) — ajuste pro seu ambiente real (IP da máquina rodando
a Api, se for testar em dispositivo físico).

**⚠️ AVISO IMPORTANTE sobre o head MAUI**: este pacote traz só as peças multiplataforma em C#/
Razor (`MauiProgram.cs`, `App.xaml(.cs)`, `MainPage.xaml(.cs)`, `SecureStorageTokenStorage.cs`,
o `.csproj`) — **NÃO inclui** as pastas `Platforms/Android`, `Platforms/iOS`,
`Platforms/MacCatalyst` nem `Resources/` (ícone, splash screen, `AndroidManifest.xml`,
`Info.plist`, `MainActivity`/`MainApplication`, etc.). Gerar esses arquivos à mão, sem o SDK
do MAUI disponível para validar (nome de pacote, permissões, entitlements), é alto risco de
erro sutil e difícil de depurar a partir de uma base que nem compila. O caminho seguro:

```
dotnet new maui-blazor -n InvestProMax.MauiTemp
```
em uma pasta temporária, e copiar de lá para `src/InvestProMax.Maui/` as pastas `Platforms/`
e `Resources/` inteiras (ajustando o `ApplicationId`/nome do pacote pra
`com.investpromax.app`, já definido no `.csproj` deste pacote) — sem sobrescrever o
`MauiProgram.cs`, `App.xaml(.cs)`, `MainPage.xaml(.cs)` nem o `.csproj` que já vieram prontos
aqui.

**Compartilhado pelos dois heads**: `AppRoot.razor` (novo na SharedUI) — componente raiz que
tenta restaurar a sessão via `ITokenStorage`/`RestaurarSessaoAsync()` de forma assíncrona antes
de renderizar qualquer rota, evitando bloquear a thread de UI do MAUI com
`.GetAwaiter().GetResult()` no bootstrap nativo.

**Ainda não compilado nem executado** — mesma ressalva de sempre, agravada aqui pela
observação acima sobre o MAUI.

## Fase 8 (2026-09-12): polimento — dashboard (já entregue na Fase 6), tratamento de erro
padronizado, revisão de segurança contra a seção 7

O dashboard consolidado já tinha sido adiantado para a Fase 6 (decisão sua, para resolver o
conflito de roteiro apontado naquela hora) — nada pendente aí.

**Revisão de segurança (seção 7), item a item:**

| Requisito da seção 7 | Situação |
|---|---|
| Hash adaptativo de senha (BCrypt) | Já conforme desde a Fase 1 (`BCryptPasswordHasher`) |
| JWT curto + refresh longo, storage seguro | Já conforme (Fases 6/7); localStorage no Web é concessão documentada, exatamente como a seção 7 pede |
| Query parametrizada, zero SQL concatenado | Já conforme — nenhum `FromSqlRaw`/`ExecuteSqlRaw` em todo o código |
| Nenhuma credencial versionada | Já conforme — `ChaveSecreta`/`ConnectionStrings` vazias em `appsettings.json`, populadas só via `appsettings.Development.json` (gitignored) ou variável de ambiente |
| Autorização sempre contra o `UsuarioId` do token | Já conforme — nenhum controller aceita `UsuarioId` do corpo/rota; todos usam `IUsuarioLogado.UsuarioId` |
| HTTPS obrigatório inclusive em dev | **Corrigido nesta fase** — `launchSettings.json` tinha uma URL `http://` junto da `https://`; removida. Adicionado `app.UseHsts()` fora do Development, complementando o `UseHttpsRedirection` já existente |

**Dois reforços adicionais, fora da lista literal da seção 7 mas descobertos durante a revisão:**

1. **Tamanho mínimo da chave JWT**: nada impedia subir o ambiente com `Jwt:ChaveSecreta` curta
   o suficiente para enfraquecer o HS256 na prática. `Program.cs` agora falha no startup se a
   chave tiver menos de 32 bytes (256 bits).
2. **ProblemDetails também no 401/403 do próprio pipeline de autenticação**: a seção 8 exige
   `ProblemDetails` em "erros" sem qualificar só os de negócio, mas um token ausente/inválido
   nunca passava pelo `ExceptionHandlingMiddleware` (que só captura exceções lançadas) — saía
   como um 401 sem corpo. Adicionados `JwtBearerEvents.OnChallenge`/`OnForbidden` em
   `Program.cs` para escrever o mesmo formato `ProblemDetails` também nesse caso. Cobertura em
   `tests/InvestProMax.IntegrationTests/ErroPadronizadoTests.cs`.

**Sinalizado, não implementado** (fora da seção 7, então não decidi por conta própria — seção
13/princípio 2, "não faça suposições"): nenhum rate limiting no `/auth/login` contra força
bruta de senha. Não é um requisito explícito do documento; se quiser, é um `AddRateLimiter`
relativamente pequeno de adicionar.

**Ainda não compilado nem executado** — roda `dotnet restore && dotnet build && dotnet test`
antes de considerar o MVP completo; são 8 fases de código acumulado sem nenhuma checagem de
compilador real até agora.

---

## ⚠️ Antes de tudo: sobre este pacote

Este código foi escrito em um ambiente sem o SDK do .NET instalado e sem acesso à internet —
não foi possível compilar, restaurar pacotes NuGet ou gerar migrations reais aqui. Isso tem duas
consequências práticas:

1. **As versões dos pacotes NuGet nos `.csproj` são um chute plausível** (segui o padrão
   histórico de "versão da lib major = versão do runtime .NET", ex.: `10.0.0` para pacotes
   `Microsoft.EntityFrameworkCore.*` no .NET 10). Rode `dotnet restore` e, se algum pacote não
   for encontrado nessa versão, `dotnet add package <Nome>` sem especificar versão para pegar a
   mais recente compatível.
2. **As migrations do EF Core ainda não existem** — o comando para gerá-las está no passo 4
   abaixo. Elas dependem do SDK real para serem geradas corretamente.

Nada disso afeta a arquitetura ou as regras implementadas — é só a parte que exige uma máquina
com o SDK instalado.

---

## Pré-requisitos

- **.NET 10 SDK**
- **SQL Server** acessível (local, Docker, ou Azure SQL). Exemplo via Docker:
  ```bash
  docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Trocar@Esta123" \
    -p 1433:1433 --name investpromax-sql -d mcr.microsoft.com/mssql/server:2022-latest
  ```
- **ferramenta `dotnet-ef`** (para gerar/aplicar migrations):
  ```bash
  dotnet tool install --global dotnet-ef
  ```

---

## Passo a passo para rodar localmente

```bash
# 1. Restaurar pacotes
cd InvestProMax
dotnet restore

# 2. Build
dotnet build

# 3. Conferir/ajustar appsettings.Development.json (já vem preenchido com valores de dev —
#    ver seção "Segredos" abaixo antes de usar em qualquer ambiente real)

# 4. Gerar e aplicar a primeira migration
cd src/InvestProMax.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../InvestProMax.Api
cd ../InvestProMax.Api
dotnet ef database update --project ../InvestProMax.Infrastructure

# 5. Rodar a Api (em Development, aplica migrations pendentes e semeia o usuário de dev automaticamente)
dotnet run --project src/InvestProMax.Api
```

A Api sobe em `https://localhost:7199` (ver `Properties/launchSettings.json`) com Swagger em
`/swagger`.

### Testando o login

Em `Development`, um usuário é semeado automaticamente (ver `SeedUsuarioDev` em
`appsettings.Development.json` — por padrão `admin` / `Troque@Esta123`):

```bash
curl -k -X POST https://localhost:7199/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"nomeUsuario":"admin","senha":"Troque@Esta123"}'
```

Deve retornar `accessToken`, `refreshToken` e as respectivas expirações.

### Testando a importação B3

`POST /api/configuracao/importar-b3` espera um Excel (.xlsx) com os cabeçalhos exatos:
`Entrada/Saída`, `Data`, `Movimentação`, `Produto`, `Quantidade`, `Preço unitário` (opcional),
`Valor da Operação` — mesmo layout do relatório "Movimentação" exportável pela Área do
Investidor da B3. Exemplo de chamada autenticada:

```bash
curl -k -X POST https://localhost:7199/api/configuracao/importar-b3 \
  -H "Authorization: Bearer SEU_ACCESS_TOKEN" \
  -F "arquivo=@extrato-b3.xlsx"
```



```bash
dotnet test
```

---

## Segredos — leia antes de usar fora do seu laptop

`appsettings.Development.json` está no `.gitignore` e vem com valores de exemplo (senha de
banco, chave JWT, usuário seed). Eles servem só para rodar localmente. Antes de qualquer deploy:

- Gere uma `Jwt:ChaveSecreta` aleatória de verdade (32+ bytes).
- Nunca reutilize a senha de banco de exemplo.
- Em produção, connection string e chave JWT vêm de variável de ambiente ou secret manager —
  nunca de um `appsettings.*.json` versionado (isso é requisito não-negociável da seção 7 do
  documento original).

---

## O que foi implementado nesta fase

- **Domain**: as 11 entidades da seção 5, todos os enums, sem nenhuma dependência externa.
- **Application**: feature de autenticação completa (login, refresh com rotação, alterar senha),
  interfaces de repositório/serviço, validação com FluentValidation, exceções mapeáveis para
  ProblemDetails.
- **Infrastructure**: `DbContext` com todos os `DbSet`s e configurações Fluent API (índices,
  precisão decimal, relacionamentos, `OnDelete`) para as 12 tabelas (11 entidades + o
  `RefreshToken` adicionado — ver assunções abaixo); repositórios de `Usuario` e `RefreshToken`;
  hasher BCrypt; serviço de JWT.
- **Api**: `Program.cs` com autenticação JWT Bearer, `AuthController` (`/auth/login`,
  `/auth/refresh`, `/auth/alterar-senha`), middleware de erro traduzindo exceções para
  ProblemDetails (RFC 7807, seção 8), Swagger com suporte a Bearer token, Serilog, HTTPS
  obrigatório, CORS já preparado (mas sem origens liberadas ainda — chega na Fase 6), seed de
  usuário de desenvolvimento.
- **Testes**: projeto `Domain.Tests` (placeholder — regras reais chegam nas próximas fases) e
  `Application.Tests` com cobertura do `AuthService` (login, refresh, troca de senha, casos de
  sucesso e falha).

## Assunções sinalizadas (a especificação não decidia isso — seção 13 do documento original)

1. **`Inventario.Ano`/`Mes`**: a especificação pede um índice único em `(AtivoId, Ano, Mes)`,
   mas o modelo de domínio só define `Data`. `Ano`/`Mes` foram implementados como *shadow
   properties* computadas pelo SQL Server a partir de `Data`, só para viabilizar o índice —
   `Data` continua sendo a única fonte de verdade no domínio.
2. **Entidade `RefreshToken`**: não existia na seção 5, mas a seção 7 exige refresh token de
   vida longa "armazenado de forma segura". Sem persistência não dá para revogar um token
   comprometido nem impedir reuso após rotação — por isso foi adicionada, guardando só o hash
   do token (nunca o valor em texto puro).
3. **Sem endpoint de auto-registro (`/auth/registrar`)**: a seção 8 não lista um, e a seção 1
   descreve o produto como de uso pessoal/pequeno grupo, não multi-tenant público. Assumi
   provisionamento manual de usuário — daí o `DbSeeder` de desenvolvimento. Para produção, o
   dono do produto precisa decidir como novos usuários (amigos) serão criados: seed manual via
   script, endpoint admin, ou algo mais.
4. **CORS**: a especificação não fala de CORS porque o front-end ainda não existe nesta fase.
   Já deixei a política registrada (restrita por configuração, nunca `AllowAnyOrigin`), pronta
   para receber as origens do Blazor WebAssembly na Fase 6.

## O que foi implementado na Fase 2

- **`Papeis`**: CRUD completo (`GET/POST /api/papeis`, `GET/PUT/DELETE /api/papeis/{id}`), com
  filtro por tipo/ativo e paginação; associação N:N com `Segmento` via `SegmentoIds` no
  request; `PapelResponse` traz `QuantidadeAtual`/`PrecoMedio` sempre recalculados na hora
  (regra 1 — nunca persistidos).
- **`Transacoes`** e **`Dividendos`**: CRUD completo, isolados por usuário através do `Papel`
  ao qual pertencem (regra 10). Toda mutação de `Transacao` recalcula a posição do `Papel` e
  ajusta `Ativo` automaticamente (regra 2).
- **`Segmentos`**: CRUD completo, **sem** isolamento por usuário — é taxonomia compartilhada
  (a especificação não lista `Segmento` entre as entidades da regra 10, e a entidade não tem
  `UsuarioId`).
- **Regra 3 — Troca de papel**: `POST /api/papeis/{id}/trocar` realoca todas as `Transacao` e
  `Dividendo` do papel de origem para o destino e desativa a origem.
- **Regra 4 — Desdobramento/Grupamento**: `POST /api/papeis/{id}/desdobramento` recalcula
  quantidade/valor unitário dos registros anteriores à data do evento e marca `Historico = true`.
- **Testes**: `CalculadoraPosicaoTests` (preço médio ponderado, venda parcial, posição
  zerada — regra 1/2), `PapelServiceTests` (troca de papel e desdobramento/grupamento — regras
  3/4) e `TransacaoServiceTests` (desativação/reativação automática do papel — regra 2).

### Assunções adicionais sinalizadas nesta fase

5. **Fórmula do desdobramento/grupamento**: para `Desdobramento`, `Quantidade` é multiplicada
   por `Fator` e `ValorUnitario` dividido por `Fator` (o inverso para `Grupamento`); `Fator` é
   sempre um número maior que 1, e a direção do ajuste vem do campo `Tipo`. A especificação
   não define a fórmula exata, só descreve o comportamento em palavras.
6. **Ajuste em memória, não duplicação**: o desdobramento/grupamento *modifica* os registros
   de `Transacao`/`Dividendo` existentes anteriores à data do evento (em vez de criar cópias
   ajustadas mantendo as originais). A especificação fala em "recalcula... os registros
   anteriores", que foi lida como edição in-place.
7. **Reativação automática do papel**: a regra 2 só descreve a desativação quando a posição
   zera; a reativação automática quando uma nova compra volta a deixar a posição positiva foi
   adicionada por simetria (do contrário o usuário precisaria reativar manualmente toda vez).
8. **`Codigo` e `TipoPapel` não são editáveis** via `PUT /papeis/{id}` — mudança de ticker
   passa pelo fluxo de troca de papel (regra 3), não por edição direta.
9. **Um `Papel` com `Transacao`/`Dividendo` vinculados não pode ser removido** (só desativado
   ou trocado) — a especificação não decide isso, mas remover apagaria histórico financeiro
   silenciosamente.

## O que foi implementado na Fase 3

- **Importação do extrato B3** (`POST /api/configuracao/importar-b3`, regra 5): lê o relatório
  "Movimentação" exportado pela Área do Investidor da B3 (Excel), classifica cada linha pelo
  tipo de movimentação, cria `Papel` (se necessário), `Transacao` ou `Dividendo`, e nunca
  duplica uma linha já importada — mesmo que o mesmo arquivo seja reenviado depois de um
  desdobramento ter alterado os valores originais (ver campo novo `OrigemImportacaoHash`
  abaixo).
- **Importação de cotações** (`POST /api/configuracao/importar-cotacoes`): planilha simples
  com colunas `Codigo`/`Cotacao`, atualiza `Papel.CotacaoAtual`.
- **Backup/restauração** (`GET /api/configuracao/backup`, `POST /api/configuracao/backup/restaurar`,
  regra 9): exporta/importa 3 abas (Papel, Transação, Dividendo) em Excel; a restauração faz
  *upsert* por `Id` (atualiza se já existe, cria se não existe) em vez de apagar tudo antes.
- **Zerar base** (`DELETE /api/configuracao/zerar-base`): apaga a carteira inteira do usuário
  autenticado (Papel, Transacao, Dividendo, PapelSegmento).
- **Testes**: `ClassificadorMovimentacaoB3Tests` (mapeamento de cada tipo de movimentação),
  `ImportacaoB3ServiceTests` (criação/reaproveitamento de Papel, direção da transação,
  deduplicação — inclusive duas linhas idênticas no mesmo arquivo e uma linha já importada
  antes) e `BackupServiceTests` (upsert por Id, erro ao referenciar papel fora da aba Papel).

### Mudança de schema nesta fase

- **`Transacao.OrigemImportacaoHash`** e **`Dividendo.OrigemImportacaoHash`** (novos campos,
  string? nullable): hash SHA-256 dos valores brutos da linha da planilha B3, usado para
  deduplicação de reimportações. Ver comentário no código para o porquê de não dar para usar
  Quantidade/ValorUnitario/Data para isso (a regra 4 pode alterar esses campos depois).

### Assunções adicionais sinalizadas nesta fase

10. **Layout do extrato B3**: a especificação lista os tipos de movimentação mas não o layout
    da planilha nem uma fórmula de mapeamento tipo→registro/direção/custo — segui o formato
    real do relatório "Movimentação" da B3 (Entrada/Saída, Data, Movimentação, Produto,
    Quantidade, Preço unitário, Valor da Operação) e documentei cada decisão de mapeamento
    diretamente em `ClassificadorMovimentacaoB3.cs`. Sem uma planilha de exemplo real para
    validar contra, há uma chance real de precisar de ajustes finos quando testado com um
    extrato de verdade — a lógica está isolada e testada exatamente para tornar esse ajuste
    barato.
11. **Inferência de `TipoPapel` na criação automática de um Papel novo pela importação B3**:
    heurística por sufixo do código (`34`/`35` → BDR, `11` → FII, resto → Ação), sabidamente
    imprecisa (ETFs também terminam em 11). Por isso `TipoPapel` foi tornado editável no PUT
    de Papel nesta fase (ver revisão da suposição 8 da Fase 2, README acima).
12. **Layout da planilha de cotações**: não definido na especificação — adotei duas colunas,
    `Codigo` e `Cotacao`.
13. **Restauração de backup é upsert, não substituição destrutiva** — ver docstring de
    `BackupService`.
14. **Escopo do "zerar base"**: restrito à carteira de investimentos (Papel/Transacao/
    Dividendo/PapelSegmento) — `Ativo` e `Conta` ainda não existem no sistema (chegam nas
    Fases 4/5) então não há o que zerar ainda; revisar esse método quando essas fases
    existirem, porque hoje ele não vai apagar dados dessas entidades futuras.

## O que foi implementado na Fase 4

- **`Ativos`**: CRUD completo (`GET/POST /api/ativos`, `GET/PUT/DELETE /api/ativos/{id}`), com
  filtro por tipo/status. Validação garante consistência entre `TipoOrigem` e
  `TipoPapelVinculado`: `Calculado` exige o vínculo, `Manual` proíbe.
- **`Inventario`**: `GET /api/inventario` (filtros `ativoId`/`ano`/`mes`) e
  `POST /api/inventario/lote` — lançamento mensal em lote que faz *upsert* por
  (AtivoId, Ano, Mes): reenviar o lote do mesmo mês corrige os valores em vez de duplicar.
- **Regra 6 — Sugestão calculada**: `GET /api/inventario/sugestao-calculada?ano=&mes=` soma
  `Quantidade × CotacaoAtual` de todos os papéis do usuário cujo `TipoPapel` bate com
  `Ativo.TipoPapelVinculado`, para cada `Ativo` de origem `Calculado`. Reaproveita a
  `CalculadoraPosicao` da Fase 2 — mesma fonte de verdade da posição usada no módulo de
  investimentos, sem duplicar lógica.
- **Guarda de remoção**: um `Ativo` com lançamentos de `Inventario` não pode ser removido (só
  desativado via `StatusAtivo`), mesma lógica defensiva aplicada a `Papel` na Fase 2.
- **Testes**: `InventarioServiceTests` (upsert do lote, ativo inexistente, cálculo da regra 6
  somando múltiplos papéis, lista vazia quando não há ativo Calculado) e
  `AtivoServiceTests`/`AtivoValidatorsTests` (guarda de remoção, consistência TipoOrigem×TipoPapelVinculado).

### Assunções adicionais sinalizadas nesta fase

15. **Formato do body de `POST /inventario/lote`**: `{ ano, mes, itens: [{ ativoId, valor,
    descricao }] }` — a especificação só diz "mês + lista { ativoId, valor }"; usei
    Ano/Mes explícitos (não uma `Data` genérica) porque o lançamento é sempre do mês inteiro.
16. **Parâmetros de `GET /inventario/sugestao-calculada`**: usei `ano` e `mes` como dois
    parâmetros de query (consistente com os filtros de `GET /inventario`), não um único texto
    `mes=yyyy-MM` — a especificação só escreve `?mes=` sem detalhar o formato.
17. **`Ano`/`Mes` da sugestão calculada são só informativos**: o cálculo em si sempre usa a
    posição e `CotacaoAtual` *correntes* dos papéis (não há cotação histórica por mês no
    sistema), então pedir a sugestão de um mês passado dá o mesmo valor que pedir a do mês
    atual — documentado na docstring de `ObterSugestaoCalculadaAsync`.
18. **Sem endpoint dedicado para editar/desativar um único registro de `Inventario`** — a
    especificação (seção 8) só lista `GET`/`POST lote` para este recurso; correções a um
    lançamento específico passam por relançar o lote daquele mês (upsert), não por um
    `PUT /inventario/{id}` que não existe na tabela de rotas.

## O que foi implementado na Fase 5

- **`Contas`**: CRUD completo (`GET/POST /api/contas`, `GET/PUT/DELETE /api/contas/{id}`).
  Validação garante consistência entre `Recorrencia` e `NumeroParcelas` (mesmo padrão do
  `TipoOrigem`/`TipoPapelVinculado` de `Ativo` na Fase 4): `Parcelada` exige o número de
  parcelas, as demais recorrências proíbem.
- **Regra 7 — Geração mensal de Fatura**: `POST /api/faturas/gerar` dispara manualmente para o
  usuário autenticado; o job diário (abaixo) faz o mesmo automaticamente para todos os
  usuários. Idempotente — reexecutar no mesmo mês não duplica fatura (reforçado por índice
  único `(ContaId, Competencia)` desde a Fase 1). `DataVencimento` ajusta corretamente para o
  último dia do mês quando `DiaVencimento` (ex.: 31) não existe naquele mês — caso
  explicitamente exigido pela seção 11 e coberto por teste parametrizado (incluindo ano
  bissexto).
- **Regra 8 — Baixa de Fatura**: `POST /api/faturas/{id}/pagar` cria o `Pagamento` e muda
  `Fatura.Status` para `Paga`; bloqueia pagar uma fatura já paga ou cancelada. A
  reclassificação `Pendente` → `Atrasada` de faturas vencidas é feita das duas formas que a
  especificação permite: persistida pelo job diário **e** calculada em tempo de leitura em
  `GET /api/faturas` (então o status exibido nunca está desatualizado, mesmo entre execuções
  do job) — ver a assunção sinalizada sobre a consequência disso no filtro `status=`.
- **Job diário** (`FaturaGeracaoHostedService`, seção 9): roda a cada 24h a partir do start da
  Api, aplicando as regras 7 e 8 para **todos** os usuários — não só o autenticado. O núcleo
  do algoritmo (`GeradorDeFaturas`) é compartilhado entre o job e o endpoint manual, para não
  duplicar a lógica das regras em dois lugares.
- **"Zerar base" agora cobre tudo**: revisei a implementação das Fases 3/4 (que só apagava a
  carteira de investimentos) para também apagar patrimônio (`Ativo`/`Inventario`) e contas a
  pagar (`Conta`/`Fatura`/`Pagamento`) — a leitura mais fiel de "apaga todos os dados do
  usuário autenticado" (seção 8), agora que esses módulos existem.
- **Testes**: `GeradorDeFaturasTests` (cálculo de vencimento incluindo o caso do dia 31 e ano
  bissexto, geração idempotente, reclassificação de atrasadas, projeção do status efetivo),
  `FaturaServiceTests` (pagamento, bloqueio de pagamento duplicado/de fatura cancelada),
  `FaturaGeracaoJobServiceTests` (execução para múltiplos usuários) e `ContaServiceTests`
  (guarda de remoção).

### Assunções adicionais sinalizadas nesta fase

19. **Filtro `status=` de `GET /faturas` usa o Status persistido, não o efetivo** — uma fatura
    vencida hoje mas ainda `Pendente` no banco só aparece em `status=Atrasada` depois do job
    diário rodar, mesmo que o `Status` retornado no corpo de cada item já mostre `Atrasada`
    corretamente. Documentado na docstring de `FaturaService.ListarAsync`.
20. **Sem endpoint para cancelar uma Fatura**: `StatusFatura.Cancelada` existe no enum (seção
    5) mas nenhuma regra ou rota da especificação diz quando/como uma fatura chega nesse
    estado — o código já trata esse status corretamente onde importa (bloqueia pagamento), mas
    nada nesta fase o define.
21. **"Diariamente" (seção 9) implementado como um loop de 24 em 24 horas a partir do start da
    Api**, não agendado para um horário fixo — ver comentário em `FaturaGeracaoHostedService`
    sobre trocar por um agendador de verdade em produção se o horário exato importar.
22. **"Zerar base" revisado para cobrir Ativo/Conta** (ver acima) — supersede a assunção 14 da
    Fase 4.

## Próximos passos (Fase 6 do roteiro, seção 14)

`SharedUI` + app MAUI + app Web (Blazor WebAssembly) — é aqui que o CORS já preparado desde a
Fase 1 entra em uso de verdade. Me avise quando quiser que eu continue.
