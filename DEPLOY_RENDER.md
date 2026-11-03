# Guia de Publicação do InvestProMax no Render Free

Este documento descreve o passo a passo completo para publicar a API do **InvestProMax** no plano gratuito do **Render** utilizando container Docker, conectada ao banco de dados **PostgreSQL no Supabase** e preparada para atender ao front-end Blazor WebAssembly hospedado no **Cloudflare Pages** (`https://investpromaxui.pages.dev`).

---

## 1. Visão Geral da Arquitetura

```
+-----------------------------------+
|  Front-end (Blazor WebAssembly)   |  -> Cloudflare Pages (https://investpromaxui.pages.dev)
+-----------------------------------+
                 |
                 | HTTPS (CORS configurado)
                 v
+-----------------------------------+
|     Back-end (.NET 10 API)        |  -> Render Free (Web Service Docker)
+-----------------------------------+
                 |
                 | TLS / SSL Require (Porta 5432)
                 v
+-----------------------------------+
|  Banco de Dados (PostgreSQL)      |  -> Supabase (Cloud)
+-----------------------------------+
```

---

## 2. Pré-requisitos

1. **Conta no Render** ([render.com](https://render.com)).
2. **Repositório GitHub conectado ao Render**: O repositório [`InvestProMaxUI`](https://github.com/SandroMarques01/InvestProMaxUI) já possui os arquivos `Dockerfile` e `.dockerignore` na raiz.
3. **Credenciais do Supabase PostgreSQL**:
   - Host do projeto Supabase (ex: `db.wgdrkvwduldywznsnqzj.supabase.co` ou pooler `aws-0-...pooler.supabase.com`).
   - Senha do banco PostgreSQL.
4. **Chave JWT de Produção**: Uma chave aleatória com no mínimo 32 caracteres (256 bits) para assinatura dos tokens HS256.

---

## 3. Passo a Passo de Criação do Serviço no Render

1. Acesse o painel do Render: [dashboard.render.com](https://dashboard.render.com).
2. Clique no botão **New +** no canto superior direito e selecione **Web Service**.
3. Selecione a opção **Build and deploy from a Git repository** e clique em **Next**.
4. Conecte sua conta do GitHub (caso ainda não tenha conectado) e selecione o repositório **`SandroMarques01/InvestProMaxUI`**.
5. Preencha as configurações do serviço:
   - **Name**: `investpromax-api` (ou outro nome de sua preferência).
   - **Region**: Escolha a região mais próxima (ex: `Ohio (US East)` ou `Oregon (US West)`).
   - **Branch**: `main`.
   - **Root Directory**: Deixe em branco (o Dockerfile está na raiz da solução).
   - **Runtime**: `Docker`.
   - **Dockerfile Path**: `./Dockerfile` (ou `Dockerfile`).
   - **Docker Context**: `.`
   - **Instance Type**: **Free** ($0/mês).

---

## 4. Configuração do Health Check

Na seção **Advanced** (ou nas abas de configuração do serviço):
- **Health Check Path**: `/health`

> O Render enviará requisições para `/health` a fim de saber se a aplicação está pronta para receber tráfego. Nossa API responde `HTTP 200 OK` com `{"status":"Healthy"}` sem exigir autenticação.

---

## 5. Variáveis de Ambiente (Environment Variables)

Ainda na tela de criação ou na aba **Environment** do serviço, cadastre as seguintes variáveis:

| Nome da Variável | Valor Recomendado / Descrição | Obrigatória? |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Sim (padrão no Dockerfile) |
| `ConnectionStrings__SupabaseDb` | `Host=db.xxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=SUA_SENHA;Ssl Mode=Require;` | **Sim** |
| `Jwt__ChaveSecreta` | `SUA_CHAVE_SECRETA_COM_PELO_MENOS_32_CARACTERES_AQUI!` | **Sim** |
| `ENABLE_SWAGGER` | `true` | Recomendado para testar endpoints |
| `Cors__OrigensPermitidas__0` | `https://investpromaxui.pages.dev` | Sim (já embutido como padrão na API) |
| `ConnectionStrings__MongoDb` | `mongodb+srv://...` (string do Atlas) | Opcional (apenas para backup Mongo) |

> ⚠️ **Atenção:** No .NET, seções aninhadas do `appsettings.json` são mapeadas em variáveis de ambiente utilizando dois underscores (`__`). Exemplo: `ConnectionStrings__SupabaseDb` preenche a seção `ConnectionStrings:SupabaseDb`.

---

## 6. Particularidades do PostgreSQL no Supabase

### Formato da Connection String
A API aceita tanto o formato padrão de parâmetros quanto o formato URI (`postgres://...` ou `postgresql://...`):
- **Formato com parâmetros (recomendado):**
  ```
  Host=db.wgdrkvwduldywznsnqzj.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=SUA_SENHA;Ssl Mode=Require;
  ```
- **Se o Supabase exigir Connection Pooling (IPv4 / Porta 6543 ou 5432):**
  No painel do Supabase (`Project Settings` > `Database` > `Connection Pooling`):
  Copie a string no modo **Session** (porta 5432) ou **Transaction** (porta 6543) caso queira compatibilidade total com redes puramente IPv4.

---

## 7. Acompanhamento dos Logs e Deploy

1. Clique em **Create Web Service**.
2. O Render iniciará automaticamente o processo de build do Docker:
   - Baixará a imagem oficial `.NET 10 SDK`.
   - Executará o `dotnet restore` e `dotnet publish` em modo Release.
   - Montará a imagem final baseada em `.NET 10 ASP.NET Runtime`.
3. Na aba **Logs**, você verá as mensagens de inicialização:
   ```
   Now listening on: http://[::]:10000 (ou a porta atribuída pelo Render)
   Application started.
   Hosting environment: Production
   ```
4. Quando o deploy for concluído, o status mudará para **Live** e o Render exibirá a URL pública da API (ex: `https://investpromax-api.onrender.com`).

---

## 8. Testes Pós-Publicação

Abra o navegador ou terminal e execute os seguintes testes substituindo `<SUA_URL_RENDER>`:

1. **Teste do Health Check:**
   ```bash
   curl -i https://<SUA_URL_RENDER>.onrender.com/health
   ```
   *Resposta esperada:* `HTTP/1.1 200 OK` com `{"status":"Healthy","timestamp":"..."}`.

2. **Teste do Swagger UI:**
   Acesse no navegador:
   ```
   https://<SUA_URL_RENDER>.onrender.com/swagger
   ```
   *Resultado esperado:* Interface gráfica com todos os endpoints documentados.

3. **Teste do CORS:**
   ```bash
   curl -i -X OPTIONS https://<SUA_URL_RENDER>.onrender.com/api/auth/login \
     -H "Origin: https://investpromaxui.pages.dev" \
     -H "Access-Control-Request-Method: POST" \
     -H "Access-Control-Request-Headers: content-type"
   ```
   *Resposta esperada:* `HTTP/1.1 204 No Content` contendo `Access-Control-Allow-Origin: https://investpromaxui.pages.dev`.

---

## 9. Diagnóstico de Problemas Comuns

| Sintoma | Causa Mais Provável | Solução |
| :--- | :--- | :--- |
| **API demora 50s na primeira requisição** | Cold start do Render Free (o container hiberna após 15 min de inatividade). | Comportamento normal do plano gratuito. Pode-se utilizar um pinger gratuito (como UptimeRobot) chamando `/health` a cada 14 minutos se quiser mantê-la ativa. |
| **Erro 502 Bad Gateway no Render** | A aplicação não subiu na porta `$PORT` fornecida pelo Render. | Nosso entrypoint já resolve isso automaticamente via `ASPNETCORE_URLS=http://+:${PORT:-8080}`. |
| **Loop de redirecionamento HTTPS (ERR_TOO_MANY_REDIRECTS)** | O proxy reverso do Render envia HTTP interno e o app não reconhece o cabeçalho `X-Forwarded-Proto`. | Já configurado no `Program.cs` com `UseForwardedHeaders` e no Dockerfile com `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. |
| **Exceção de conexão com Supabase (`SocketException` / `Timeout`)** | Senha com caracteres especiais não encodados ou bloqueio de IP. | Verifique se a senha do banco está correta na variável de ambiente do Render e se o projeto do Supabase não está pausado. |
| **Erro 401 Unauthorized imediato no Login** | `Jwt:ChaveSecreta` incorreta ou não fornecida. | Cadastre a variável de ambiente `Jwt__ChaveSecreta` com 32+ caracteres no Render. |

