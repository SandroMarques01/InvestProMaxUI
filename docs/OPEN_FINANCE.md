# Open Finance no Invest Pro Max — Guia de Implementação

> **Status:** proposta / documentação de planejamento. Nenhum código foi implementado.
> **Pesquisa realizada em:** outubro/2026, com base na documentação oficial da Pluggy (`docs.pluggy.ai`), do Open Finance Brasil e das páginas de preço da Pluggy.
> Itens marcados com **(a confirmar)** não foram verificados em fonte oficial e precisam ser validados antes de qualquer decisão.

---

## 1. Objetivo e escopo

Permitir que o Invest Pro Max leia, **com autorização do próprio usuário**, dados financeiros que ele já possui em bancos e corretoras (posições de investimentos, saldos e, opcionalmente, transações), reduzindo a digitação manual.

**Dentro do escopo desta documentação**

- Como o Open Finance funciona e quais caminhos existem para integrá-lo.
- Qual caminho é viável para este sistema, custos e limitações.
- Proposta de arquitetura que respeita as camadas atuais (Domain / Application / Infrastructure / Api / SharedUI).
- Fluxo de conexão, mapeamento de dados, segurança e roteiro em fases.

**Fora do escopo**

- Iniciação de pagamentos (Pix via Open Finance).
- Implementação de código (depende de aprovação das decisões da seção 13).

---

## 2. O que é o Open Finance Brasil

Sistema regulado pelo Banco Central do Brasil (BCB) que permite ao cliente **compartilhar seus dados entre instituições** mediante consentimento. Regras principais:

- O compartilhamento só acontece com **consentimento explícito** do cliente, escolhido por ele: quais dados, para qual finalidade e por quanto tempo.
- O consentimento pode ser **revogado a qualquer momento** pelo cliente (no app do banco).
- A validade do consentimento deve ser compatível com a finalidade (Resolução Conjunta nº 7/2023); não há prazo fixo único. Quando expira, os dados deixam de ser retornados e o usuário precisa renovar.
- Dados compartilháveis relevantes para este projeto (APIs de investimentos): renda variável, renda fixa bancária, renda fixa de crédito, títulos do Tesouro e fundos.

---

## 3. Por que não conectar direto ao Open Finance

Para consumir as APIs diretamente, o sistema precisaria ser uma instituição participante, o que exige:

| Requisito | Observação |
|---|---|
| Autorização do BCB como instituição (ITP/IP) | Pessoa jurídica regulada |
| Certificado ICP-Brasil + mTLS | Custo e operação de PKI |
| Certificação FAPI 1.0 Advanced (OpenID Foundation) | Processo formal de homologação |
| Cadastro no Diretório de Participantes | Governança do ecossistema |

Isso é **inviável para um sistema pessoal/pequeno**. O caminho prático é usar um **agregador** (intermediário já certificado) que expõe uma API única e simples.

---

## 4. Opções de agregadores

| Opção | Situação nesta pesquisa |
|---|---|
| **Pluggy** | Pesquisada em profundidade (docs oficiais, preços, SDK .NET). **Recomendada como base deste guia.** |
| Belvo | Alternativa conhecida no mercado; **não foi verificada** neste estudo **(a confirmar)** |
| Conexão direta | Inviável (seção 3) |

O restante do documento descreve a integração com a **Pluggy**. A arquitetura proposta usa uma interface própria (seção 6), de modo que trocar de agregador no futuro não afete o Domain/Application.

---

## 5. Pluggy: conceitos, custos e planos

### 5.1 Conceitos

| Conceito | Significado |
|---|---|
| **Connector** | Instituição que pode ser conectada. Os conectores de Open Finance têm ID ≥ 600 (ex.: Itaú 601, XP 602, Nubank 612, BTG Investimentos 614, C6 626, Inter 823). |
| **Item** | Conexão de **um usuário** com **uma instituição**. Ao criar, busca até os últimos 365 dias; nas atualizações, busca desde a última coleta + 4 dias de sobreposição. |
| **API Key** | Gerada por `POST /auth` com `CLIENT_ID` + `CLIENT_SECRET`; válida por **2 horas**; acesso total; **somente no servidor**. |
| **Connect Token** | Gerado por `POST /connect_token`; válido por **30 minutos**; escopo limitado (endpoints de detalhe retornam 403); usado só pelo widget no front-end. Gerar um por conexão. |
| **Connect Widget** | Componente de UI (web, React Native, Flutter, webview) que conduz o usuário: escolha do banco, CPF, redirecionamento ao banco (QR code / link do app), consentimento. Ao concluir, dispara `onSuccess` com o `itemId`. |
| **Webhook** | Notificações da Pluggy para o seu sistema quando algo muda. |

### 5.2 Planos e custos

| Plano | Custo | Uso |
|---|---|---|
| Sandbox | Gratuito, sem limite de tempo | Desenvolvimento e testes com dados fictícios |
| Trial de produção | 15 dias, sem cartão | Avaliação |
| **Meu Pluggy** (`meu.pluggy.ai`) | **Gratuito** para pessoa física lendo as próprias contas | Uso pessoal, não comercial |
| Dados (produção) | A partir de **R$ 2.500/mês** (até 500 conexões ativas) | Produtos que atendem clientes |
| Pagamentos (Pix) | A partir de R$ 500/mês | Fora do escopo |

**Pontos sobre o Meu Pluggy**

- A documentação da Pluggy indica o conector **MeuPluggy** como o caminho gratuito para quem quer ler as **próprias** contas.
- Atualização automática a cada 24h, sem custo.
- Existe documentação de acesso por API em `meu.pluggy.ai/api-guide`.
- Com o MeuPluggy existem **dois Items**: o original (no portal Meu Pluggy) e um **Item proxy** (no seu app). O proxy espelha cada atualização do original, **não tem sincronização própria nem atualização manual**. Cada banco precisa ser autorizado separadamente no seu app.
- **(a confirmar)** Limite de "até 5 conexões" para uso pessoal: veio de fontes secundárias, não da documentação oficial.
- Uso **comercial** ou para **terceiros** exige plano pago.

> [!IMPORTANT]
> Se o Invest Pro Max for usado **apenas por você** (ou pessoas que cada uma use a própria conta Meu Pluggy), o custo pode ser zero. Se for aberto a outros usuários como produto, o plano pago passa a ser necessário. Ver decisão D1 (seção 13).

---

## 6. Arquitetura proposta no Invest Pro Max

Respeitando a estrutura atual da solução e o isolamento por usuário (`IUsuarioLogado`).

```text
SharedUI (Blazor)  ──►  Api (Controllers)  ──►  Application (Services)  ──►  Domain
   │                          │                         │                       ▲
   │ Connect Widget           │ /openfinance/*          │ IOpenFinanceProvider  │
   │ (itemId)                 │ webhook                 ▼                       │
   │                          └────────────────►  Infrastructure ───────────────┘
                                                   PluggyOpenFinanceProvider
                                                   (NuGet Pluggy.SDK)
```

### 6.1 Responsabilidades por camada

| Camada | O que entra |
|---|---|
| **Domain** | Entidade `ConexaoOpenFinance` (por usuário): `UsuarioId`, `ItemId`, `InstituicaoNome`, `Status`, `UltimaSincronizacao`, `ConsentimentoExpiraEm` (se informado). Possível vínculo opcional do `Ativo`/`Papel` com a conexão de origem. |
| **Application** | Interface `IOpenFinanceProvider` (gerar connect token, listar investimentos de um item, obter status). Serviço `OpenFinanceService` (registrar item, sincronizar, reconciliar). DTOs próprios — **sem tipos da Pluggy vazando** para fora da Infrastructure. |
| **Infrastructure** | `PluggyOpenFinanceProvider` usando o pacote NuGet oficial `Pluggy.SDK` (`new PluggyAPI(clientId, clientSecret)`). Repositório de `ConexaoOpenFinance`. Configuração `Pluggy:ClientId` / `Pluggy:ClientSecret`. Migration para a nova tabela. |
| **Api** | `OpenFinanceController`: `POST connect-token`, `POST conexoes` (registra `itemId` após o `onSuccess`), `GET conexoes`, `DELETE conexoes/{id}`, `POST conexoes/{id}/sincronizar`. `OpenFinanceWebhookController` (público, validado — ver 9). |
| **SharedUI** | Página/seção de "Conexões Open Finance" na Configuração: botão **Conectar**, lista de conexões, status, ação de sincronizar/remover. O widget é carregado via JS interop. |
| **Web / Maui** | O widget precisa de HTTPS e redirecionamento de volta ao app (`oauthRedirectUri`). No Web (WASM) é direto; no **MAUI** exige tratamento específico de deep link / webview **(a validar na PoC)**. |

> [!NOTE]
> O fluxo de dados é sempre **front → sua Api → Pluggy**. O front nunca recebe a API Key; só o Connect Token de escopo limitado.

### 6.2 Segredos

- `ClientId`/`ClientSecret` **fora do git**: `appsettings.json` com valores vazios; valores reais em variável de ambiente / user-secrets (mesma regra já aplicada ao restante do projeto).
- A API Key (2h) é obtida/renovada no servidor e nunca é persistida em banco nem enviada ao cliente.

---

## 7. Fluxo de conexão (passo a passo)

```text
Usuário        SharedUI           Api               Pluggy             Banco
   │ Conectar ──►│                  │                   │                 │
   │             │ POST connect-token│                  │                 │
   │             │─────────────────►│ POST /auth        │                 │
   │             │                  │──────────────────►│                 │
   │             │                  │ POST /connect_token (options aninhado)
   │             │                  │──────────────────►│                 │
   │             │◄─ connectToken ──│                   │                 │
   │             │ abre Connect Widget                  │                 │
   │ escolhe banco, informa CPF ───────────────────────►│ redireciona ───►│
   │ autoriza compartilhamento no app/site do banco ────────────────────►│
   │             │◄─ onSuccess({ item.id }) ────────────│                 │
   │             │ POST conexoes (itemId)               │                 │
   │             │─────────────────►│ persiste ConexaoOpenFinance         │
   │             │                  │ GET investments?itemId=...          │
   │             │                  │──────────────────►│                 │
   │             │◄─ resultado da sincronização ────────│                 │
```

**Detalhes importantes**

1. O corpo de `POST /connect_token` deve ter as opções **aninhadas** em `options`:
   `{"options": {"webhookUrl": "...", "clientUserId": "<id do usuário>", "oauthRedirectUri": "...", "avoidDuplicates": true}}`.
   Campos enviados na raiz são **ignorados silenciosamente**.
2. `clientUserId` deve ser o id do usuário **no seu sistema** (ajuda a correlacionar webhooks).
3. `avoidDuplicates` **não existe** para a maioria dos conectores Open Finance (só Nubank) — a deduplicação precisa ser feita por você.
4. O `itemId` retornado no `onSuccess` **precisa ser persistido** imediatamente.
5. Usuário concede consentimento no **banco**; o sistema não vê nem guarda credenciais bancárias.

---

## 8. Mapeamento de dados e limitações

### 8.1 Como encaixa no modelo atual

O Invest Pro Max é **orientado a transações**: posição e preço médio são **calculados** (`CalculadoraPosicao`) a partir de `Transacao`; a importação B3 usa a planilha "Movimentação" com deduplicação por hash (`OrigemImportacaoHash`); aluguel é `MovimentoAluguel`; proventos são `Dividendo`.

O Open Finance entrega, principalmente, **posições** (quanto você tem hoje), não o histórico completo de operações. Por isso a integração se encaixa melhor como **conferência e alimentação de patrimônio**, e não como substituto do histórico:

| Dado Open Finance | Destino no Invest Pro Max | Observação |
|---|---|---|
| `EQUITY` (STOCK, BDR, REAL_ESTATE_FUND) — `code`, `quantity`, `value` | **Conferência** com a posição calculada de um `Papel` (`quantidade` OF × `CalculadoraPosicao`) | Divergência vira alerta para o usuário; não altera transações sozinha |
| `value` / `amount` do ativo de renda variável | `Papel.CotacaoAtual` (opcional) | Decisão D3 |
| `FIXED_INCOME` (CDB, LCI, LCA, CRI, CRA, Debêntures, Tesouro) | **Inventário** — `Ativo` Manual (`TipoAtivo.RendaFixa`) + lançamento de `Inventario` (`Valor`, `Data`) | Alimenta o patrimônio do mês |
| `MUTUAL_FUND`, `SECURITY` (previdência), `COE` | **Inventário** (`Ativo` Manual) | Idem |
| `ETF` | Hoje o enum `TipoPapel` possui Ação, FII, BDR — tratamento de ETF depende de decisão | Decisão D3 |
| Saldos de conta corrente / caixinhas | **Inventário** (reserva/saldos) | Caixinhas aparecem como CDB |
| Transações agendadas (PENDING) | Possível apoio ao módulo **Contas a pagar** | Fase futura; não faz parte do MVP |
| Dividendos | Não há endpoint dedicado de proventos documentado para investimentos OF | Continuam via importação B3/manual |
| Aluguel (BTC) | **Não documentado** na Pluggy | Continua via `MovimentoAluguel` / importação B3 |

### 8.2 Campos de investimento retornados

`code` (ticker), `isin`, `quantity`, `value`, `amount`, `balance`, `date`, `status` (`ACTIVE`, `PENDING`, `TOTAL_WITHDRAWAL`), `amountOriginal`, `taxes`.

### 8.3 Limitações verificadas

- **Rentabilidades vêm nulas via Open Finance:** `amountProfit`, `annualRate`, `lastMonthRate`, `lastTwelveMonthsRate`. O sistema deve calcular ganho/perda a partir das suas próprias transações, **não** desses campos.
- **Investimentos totalmente resgatados não são retornados** na primeira conexão (sem histórico de posições encerradas).
- **Histórico de transações de investimento limitado:** ex.: XP regulado = 1 ano (conector direto = 4 anos).
- **Cobertura varia por instituição.** Exemplos: Bradesco e Mercado Pago **não** retornam renda variável; Clear retorna apenas renda variável e Tesouro; Itaú, XP, Rico, BB, Nubank, BTG, Inter, C6, Safra, Ágora, Toro retornam renda variável. Verificar a matriz de cobertura da Pluggy para **as instituições que você usa** antes de investir na implementação.
- Contas correntes têm limite de 260 recursos por consulta.
- Consentimento expirado: os metadados do Item permanecem, mas **os dados deixam de ser retornados** até renovar.
- Conexões via Meu Pluggy: atualização somente pelo espelhamento do Item original (24h).

---

## 9. Webhooks e estratégia de sincronização

### 9.1 Eventos

`item/created`, `item/updated`, `item/deleted`, `item/error`, `item/waiting_user_input`, `item/waiting_user_action`, `item/login_succeeded`, `connector/status_updated`, `transactions/created`, `transactions/updated`, `transactions/deleted`.

Payload: `event`, `eventId`, `triggeredBy` (`USER`, `CLIENT`, `SYNC`, `INTERNAL`) e `itemId`. O `clientUserId` aparece **apenas em eventos `item/*`** — por isso é obrigatório manter o mapeamento próprio `itemId → usuário` (a tabela `ConexaoOpenFinance`).

### 9.2 Regras

- URL **HTTPS obrigatória**; `localhost` não é aceito. Em desenvolvimento usar túnel (ngrok ou similar).
- O endpoint de webhook precisa ser **anônimo** (a Pluggy não envia o JWT do seu sistema), portanto deve ser protegido por: validação do `itemId` contra conexões existentes, idempotência por `eventId` e, preferencialmente, um segredo na URL ou header configurado em `options.webhookUrl` **(mecanismo de assinatura a confirmar na documentação da Pluggy)**.
- Responder rápido (HTTP 2xx) e processar a sincronização em segundo plano. O projeto já tem um padrão de `BackgroundService` (`FaturaGeracaoHostedService`) que pode ser reutilizado como referência.
- Idempotência: ignorar `eventId` já processado.

### 9.3 Estratégia de sincronização

| Gatilho | Ação |
|---|---|
| `onSuccess` do widget | Primeira sincronização completa (até 365 dias) |
| `item/updated` | Buscar investimentos atualizados e reconciliar |
| Botão "Sincronizar" | Atualização manual (não disponível para Item proxy do Meu Pluggy) |
| Atualização automática | Recurso premium da Pluggy em produção (a cada 6/8/12/24h); no Meu Pluggy é a cada 24h gratuitamente |
| `item/error`, `item/waiting_user_action` | Marcar conexão com status de atenção e mostrar ação ao usuário |

---

## 10. Consentimento, estados e erros

| Situação | Comportamento sugerido na interface |
|---|---|
| Conexão ativa | "Atualizado em dd/mm hh:mm" |
| Aguardando ação do usuário | Botão "Reautorizar" abre o widget com o `itemId` existente |
| Consentimento expirado / revogado | Aviso claro + botão "Renovar" |
| Erro temporário | Mostrar último sucesso e permitir tentar novamente |
| Usuário remove a conexão | `DELETE` do Item na Pluggy **e** da tabela local |

---

## 11. Segurança e LGPD

- **Minimização:** guardar apenas `itemId`, instituição, status e datas. Não armazenar credenciais, CPF do titular na conexão nem payloads brutos da Pluggy.
- **Isolamento:** toda consulta/escrita filtrada pelo usuário logado (`IUsuarioLogado`), como o restante do sistema. Webhook mapeia `itemId` → usuário pela tabela local.
- **Segredos:** `ClientId`/`ClientSecret` somente por configuração segura; nunca no front-end nem no repositório.
- **Transparência:** tela deve explicar quais dados são lidos e permitir **desconectar** a qualquer momento (e orientar a revogar o consentimento no banco).
- **Logs:** não registrar tokens nem dados financeiros brutos.
- **Retenção:** ao remover a conexão, decidir se os dados já importados permanecem (D5).
- **LGPD:** o tratamento é baseado no consentimento do titular; o agregador (Pluggy) atua como operador/subprocessador — para uso além do pessoal, revisar contrato e política de privacidade.

---

## 12. Roteiro de implementação em fases

| Fase | Entrega | Critério de pronto |
|---|---|---|
| **0 — PoC (Sandbox)** | Criar conta em `dashboard.pluggy.ai`; testar o fluxo `auth → connect_token → widget → investments` com dados fictícios; validar o widget no Web e no MAUI | `itemId` recebido e investimentos lidos no sandbox |
| **1 — Conexão pessoal (somente leitura)** | Entidade `ConexaoOpenFinance`, `IOpenFinanceProvider`, adapter Pluggy, `OpenFinanceController`, tela de conexões, uso do **MeuPluggy** com suas instituições reais | Listar posições reais dentro do app, sem alterar dados |
| **2 — Conferência da carteira** | Comparar posição OF × `CalculadoraPosicao` por `Papel`; tela de divergências | Divergências visíveis; nenhuma alteração automática |
| **3 — Alimentar Inventário** | Criar/atualizar `Ativo` Manual e `Inventario` mensal a partir de renda fixa, fundos e saldos | Patrimônio do mês reflete dados OF |
| **4 — Webhooks e automação** | Webhook com idempotência, sincronização em segundo plano, status/renovação de consentimento | Atualização sem ação manual |
| **5 — (Opcional)** | Contas a pagar a partir de transações agendadas; criação de `Transacao` a partir do OF | Somente se decidido em D4 |

Cada fase deve incluir testes (Application com repositórios em memória/fakes, como o padrão atual; integração para os controllers) e validação de build.

---

## 13. Decisões em aberto

Estas decisões **não foram assumidas** e precisam ser definidas antes da Fase 1:

| # | Decisão | Impacto |
|---|---|---|
| **D1** | Uso **pessoal** (Meu Pluggy, gratuito) ou **multiusuário/comercial** (plano pago a partir de R$ 2.500/mês)? | Custo, contrato, arquitetura de conexões |
| **D2** | **Quais instituições** você usa? (conferir na matriz de cobertura da Pluggy) | Define se o valor da integração compensa |
| **D3** | A integração deve apenas **conferir** posição, **atualizar cotação** (`Papel.CotacaoAtual`) ou **cadastrar papéis** novos? Como tratar ETFs (não existem em `TipoPapel`)? | Escopo da Fase 2 |
| **D4** | O Open Finance pode **criar `Transacao`**? (Risco de conflito com o histórico importado da B3 e com desdobramentos) | Recomendação: **não** no MVP |
| **D5** | O que acontece com os dados importados quando a conexão é removida? | LGPD / consistência |
| **D6** | O MAUI precisa suportar o widget desde o início ou só o Web? | Esforço da Fase 0/1 |

---

## 14. Riscos

| Risco | Mitigação |
|---|---|
| Dependência de terceiro (Pluggy) | Interface `IOpenFinanceProvider` isola o fornecedor |
| Cobertura desigual entre bancos | Validar na Fase 0/1 com suas instituições reais |
| Dados de rentabilidade nulos | Calcular ganho/perda internamente |
| Mudança de plano/preço ou política de uso pessoal | Revalidar `meu.pluggy.ai` e a página de preços antes de começar |
| Consentimento expira e dados somem | Alertas e fluxo de renovação (seção 10) |
| Duplicidade com importação B3 | Open Finance não cria transações no MVP (D4) |

---

## 15. Referências

- Pluggy — documentação: <https://docs.pluggy.ai> (páginas de Authentication, Connect Widget, Item, Webhooks, Open Finance e Investments; acrescentar `.md` à URL retorna o texto limpo)
- Pluggy — preços: <https://pluggy.ai/pricing>
- Meu Pluggy: <https://meu.pluggy.ai> e guia de API em <https://meu.pluggy.ai/api-guide>
- SDK .NET oficial: NuGet `Pluggy.SDK` — repositório `pluggyai/pluggy-net`
- Open Finance Brasil: <https://openfinancebrasil.org.br>
- Resolução Conjunta nº 7/2023 (BCB/CMN) — consentimento no Open Finance
