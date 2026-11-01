# InvestProMax — Documento de Implantação: Cotações Automáticas (n8n + InvestProMax)

**Versão:** Módulo de Cotações Automáticas e Integração com n8n / brapi  
**Data:** 27/10/2026 · **Ambiente:** SQL Server / PostgreSQL  

---

## 1. Visão Geral e Responsabilidades

O fluxo de cotações automáticas divide as responsabilidades claramente:
- **n8n:** responsável pelo agendamento (19h seg-sex, fuso `America/Sao_Paulo`), webhook acionado pelo botão da interface, orquestração das chamadas à API da **brapi**, espera de 300 ms entre chamadas (rate limit) e alerta em caso de falha.
- **InvestProMax:** decide quais papéis precisam ser atualizados (`GET api/integracao/cotacoes/tickers`), valida os valores e regras de negócio, e persiste `CotacaoAtual`, `CotacaoAtualizadaEm` e `FonteCotacao` (`POST api/integracao/cotacoes/lote`). Não chama a API de mercado diretamente e não utiliza agendador interno.

---

## 2. Configurações Necessárias

### 2.1 InvestProMax (Api)

Configurado via `appsettings.json`, variáveis de ambiente ou user-secrets:

```json
"Integracao": {
  "Cotacoes": {
    "ApiKey": "<CHAVE_ALEATORIA_MINIMO_32_CARACTERES>",
    "UsuarioId": "<GUID_DO_USUARIO_NA_TABELA_USUARIOS>"
  },
  "N8n": {
    "WebhookUrl": "https://<HOST_DO_N8N>/webhook/cotacoes-atualizar",
    "Segredo": "<SEGREDO_COMPARTILHADO_PARA_O_HEADER_X_SEGREDO>"
  }
}
```

> **Validação de inicialização:** Se `ApiKey` estiver preenchida, ela deve ter no mínimo 32 caracteres e `UsuarioId` deve ser um Guid válido. Chaves vazias mantêm a integração desabilitada sem impedir a inicialização.

### 2.2 n8n

O workflow exportado em `docs/n8n/atualizar-cotacoes.json` utiliza as seguintes variáveis de ambiente ou parâmetros globais:

| Variável | Descrição | Exemplo |
|---|---|---|
| `API_BASE_URL` | URL base da API do InvestProMax (sem barra final) | `https://api.investpromax.com` |
| `INVESTPROMAX_API_KEY` | Chave de integração idêntica a `Integracao:Cotacoes:ApiKey` | `chave-aleatoria-com-mais-de-32-caracteres` |
| `BRAPI_TOKEN` | Token de autenticação da brapi.dev | `token_brapi_xyz` |

---

## 3. Banco de Dados e Migrations

A migration adiciona duas colunas nuláveis na tabela `Papeis`:
- `CotacaoAtualizadaEm` (`datetime2`, null)
- `FonteCotacao` (`nvarchar(30)`, null)

### Aplicar em produção / staging:
```bash
dotnet ef database update --project src/InvestProMax.Infrastructure --startup-project src/InvestProMax.Api
```

---

## 4. Endpoints de Integração

Base: `https://<host-da-api>`

### 4.1 `GET /api/integracao/cotacoes/tickers`
- **Autenticação:** Header `X-Api-Key`.
- **Query param opcional:** `desatualizadasHaMinutos` (`0` para todos os papéis com posição > 0; `30` para filtrar recém-atualizados).
- **Resposta 200:**
  ```json
  {
    "geradoEm": "2026-10-27T22:00:00Z",
    "tickers": ["PETR4", "VALE3", "BBSE3"]
  }
  ```

### 4.2 `POST /api/integracao/cotacoes/lote`
- **Autenticação:** Header `X-Api-Key`.
- **Payload:**
  ```json
  {
    "fonte": "brapi",
    "cotacoes": [
      { "codigo": "PETR4", "cotacao": 38.52, "dataHoraCotacao": "2026-10-27T20:58:00Z" }
    ],
    "falhas": [
      { "codigo": "XYZW3", "motivo": "HTTP 404" }
    ]
  }
  ```
- **Resposta 200:**
  ```json
  {
    "recebidas": 50,
    "atualizadas": 48,
    "naoEncontradas": ["ABCD3"],
    "ignoradas": [ { "codigo": "EFGH4", "motivo": "cotação inválida" } ],
    "falhasInformadas": 1,
    "atualizadoEm": "2026-10-27T22:00:03Z"
  }
  ```

### 4.3 `POST /api/cotacoes/atualizar`
- **Autenticação:** JWT Bearer do usuário autenticado (`[Authorize]`).
- Dispara o webhook do n8n com payload `{ "forcar": false, "solicitadoEm": "..." }` e header `X-Segredo`.
- Retorna `202 Accepted` com `{ "solicitadoEm": "..." }`.
- Se o webhook não estiver configurado: `503 Service Unavailable`.
- Se já foi solicitado há menos de 60 segundos: `429 Too Many Requests`.

---

## 5. Passos de Implantação e Validação

1. **Compilar e rodar a suíte de testes:**
   ```bash
   dotnet build
   dotnet test
   ```
2. **Aplicar a migration no banco de dados:**
   ```bash
   dotnet ef database update --project src/InvestProMax.Infrastructure --startup-project src/InvestProMax.Api
   ```
3. **Configurar as credenciais da API** (`ApiKey`, `UsuarioId`, `WebhookUrl`, `Segredo`).
4. **Importar o workflow no n8n:**
   - No n8n: *Workflows -> Import from File -> `docs/n8n/atualizar-cotacoes.json`*.
   - Configurar o webhook path `cotacoes-atualizar` com autenticação por header `X-Segredo`.
   - Preencher as variáveis `API_BASE_URL`, `INVESTPROMAX_API_KEY` e `BRAPI_TOKEN`.
   - Ativar o workflow.
5. **Validação na interface:**
   - Acessar a tela **Papéis** ou **Papéis Geral**.
   - Clicar no botão **Atualizar cotações**.
   - Conferir mensagem informativa de atualização solicitada, bloqueio de 60s do botão, execução do webhook e recarregamento da tela.
   - Conferir exibição da data/hora no horário de Brasília (`dd/MM/yyyy HH:mm`) e da fonte da cotação nos Detalhes do papel.

---

## 6. Procedimento de Rollback

Caso seja necessário reverter a funcionalidade:
1. **Desativar o workflow no n8n:** nenhuma chamada de cotação será disparada.
2. **Remover a `ApiKey` do `appsettings`:** desativa imediatamente a autenticação dos endpoints de integração.
3. As colunas `CotacaoAtualizadaEm` e `FonteCotacao` adicionadas à tabela `Papeis` são nuláveis e não afetam nenhuma funcionalidade anterior do sistema caso permaneçam no banco. Se a remoção for estritamente necessária:
   ```bash
   dotnet ef database update 20261005003628_AluguelDeAcoes --project src/InvestProMax.Infrastructure --startup-project src/InvestProMax.Api
   dotnet ef migrations remove --project src/InvestProMax.Infrastructure --startup-project src/InvestProMax.Api
   ```
