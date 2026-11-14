# 📱 InvestProMax — Guia de Build, Distribuição e Instalação Mobile (Android e iOS)

Guia passo a passo para compilar, atualizar, instalar em outros celulares e distribuir o aplicativo móvel `.NET MAUI Blazor Hybrid` (`src/InvestProMax.Maui`) do **InvestProMax**, cobrindo **Android (`.apk` / `.aab`)** e **iOS (`.ipa` / PWA)**.

---

## 1. Como Gerar o APK Android (`Release`)

Sempre que fizer alterações no `InvestProMax.SharedUI` ou no `InvestProMax.Maui`, gere o pacote assinado de produção com o comando abaixo na raiz do repositório (`c:\MinhasFinancas\InvestProMax`):

```powershell
dotnet publish "src\InvestProMax.Maui\InvestProMax.Maui.csproj" -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```

O arquivo final assinado e pronto para instalação será gerado em:
`src\InvestProMax.Maui\bin\Release\net10.0-android\publish\com.investpromax.app-Signed.apk`

> **Dica de Versionamento**: Antes de publicar uma nova versão, incremente `<ApplicationDisplayVersion>` (ex.: `1.2`) e `<ApplicationVersion>` (ex.: `3`, sempre número inteiro crescente) em `src/InvestProMax.Maui/InvestProMax.Maui.csproj`. Isso permite atualizar o app por cima da versão anterior sem perder os dados salvos (como a biometria e a sessão).

---

## 2. Como Instalar em Outros Celulares Android

Existem **3 formas práticas** de instalar o APK em outros aparelhos Android:

### Opção A — Enviar ou Hospedar o Arquivo `.apk` (Mais Simples para Outros Telefones)
Você **não precisa conectar outros celulares no computador** nem ativar Opções do Desenvolvedor neles. Basta disponibilizar o arquivo `com.investpromax.app-Signed.apk`:

1. **Onde publicar/enviar o `.apk`**:
   - **Cloudflare Pages (Direto pelo seu site atual)**: Copie o arquivo `com.investpromax.app-Signed.apk` para `src/InvestProMax.Web/wwwroot/downloads/investpromax.apk`. Ao publicar o front-end Web, qualquer pessoa poderá baixar acessando `https://investpromaxui.pages.dev/downloads/investpromax.apk`.
   - **GitHub Releases**: Na página do seu repositório no GitHub, clique em **Releases -> Draft a new release**, crie uma tag (ex.: `v1.1.0`) e anexe o arquivo `com.investpromax.app-Signed.apk`. Você terá um link direto de download.
   - **Firebase App Distribution** (Gratuito do Google): Permite cadastrar os e-mails dos convidados; eles recebem um aviso sempre que você subir uma versão nova do APK.
   - **WhatsApp / Google Drive / OneDrive / Telegram**: Envie o arquivo `com.investpromax.app-Signed.apk` diretamente.
2. **Como o usuário instala no celular Android**:
   - Toca no arquivo `.apk` baixado.
   - Se for a primeira vez, o Android pedirá permissão para **"Instalar apps desconhecidos"** daquele navegador/app — basta autorizar e tocar em **Instalar**.
   - Se o Google Play Protect exibir o aviso *"App não reconhecido pelo Play Protect"* (normal para apps pessoais fora da loja), basta tocar em **Mais detalhes -> Instalar assim mesmo**.

---

### Opção B — Instalação Técnica via ADB Wi-Fi (Sem Cabo USB)
Ideal para instalar e depurar diretamente do seu PC quando o celular está na mesma rede Wi-Fi:

1. **No celular Android**:
   - Vá em **Configurações -> Sobre o telefone -> Informações do software** e toque 7 vezes em **Número de compilação** para ativar as **Opções do desenvolvedor**.
   - Vá em **Configurações -> Opções do desenvolvedor -> Depuração por Wi-Fi** e ative a chave.
   - Toque em **Parear o dispositivo com um código de pareamento** (anote o `IP:Porta` de pareamento e o código de 6 dígitos).
2. **No PowerShell do PC**:
   ```powershell
   $adb = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
   $apk = "C:\MinhasFinancas\InvestProMax\src\InvestProMax.Maui\bin\Release\net10.0-android\publish\com.investpromax.app-Signed.apk"

   # 1. Descobrir IP e portas de pareamento/conexão do celular na rede:
   & $adb mdns services

   # 2. Parear (necessário apenas na 1ª vez por celular):
   & $adb pair <IP>:<PORTA_PAIRING> <CODIGO_6_DIGITOS>

   # 3. Desativar auto-connect mDNS (evita colisão de transporte TLS quando há duas entradas mDNS na rede)
   #    e conectar/instalar diretamente (-r preserva os dados do usuário):
   $env:ADB_MDNS = "0"
   & $adb kill-server
   & $adb connect <IP>:<PORTA_CONNECT>
   & $adb -s <IP>:<PORTA_CONNECT> install -r $apk
   & $adb kill-server
   ```

---

### Opção C — Publicação Oficial na Google Play Store
Se quiser que o app fique disponível na loja oficial do Android:
1. Crie uma conta no **Google Play Console** (taxa única de US$ 25).
2. Gere um pacote no formato **Android App Bundle (`.aab`)** exigido pela Play Store:
   ```powershell
   dotnet publish "src\InvestProMax.Maui\InvestProMax.Maui.csproj" -f net10.0-android -c Release -p:AndroidPackageFormat=aab
   ```
3. Crie uma faixa de **Teste Interno** ou **Teste Fechado** no Google Play Console e faça upload do `.aab`. Você pode liberar para até 100 testadores apenas pelo e-mail do Gmail deles, sem precisar aguardar revisão pública.

---

## 3. Como Fazer para iOS (iPhone / iPad)

Diferente do Android, a Apple **não permite** baixar um arquivo equivalente ao `.apk` pelo navegador e instalar livremente. Além disso, a compilação nativa de apps iOS (`.ipa`) exige o **Xcode**, que roda apenas no **macOS**.

Você tem **3 caminhos** para disponibilizar o **InvestProMax** no iPhone:

### Caminho 1 — Instalar como Web App (PWA) no iOS (Gratuito, Sem Mac e Imediato)
Como o **InvestProMax** também possui o front-end `InvestProMax.Web` hospedado no Cloudflare Pages (`https://investpromaxui.pages.dev`) compartilhando 100% das telas do `InvestProMax.SharedUI`:
1. Abra o **Safari** no iPhone e acesse `https://investpromaxui.pages.dev`.
2. Toque no botão **Compartilhar** (ícone do quadrado com seta para cima na barra inferior).
3. Role para baixo e toque em **Adicionar à Tela de Início** (*Add to Home Screen*).
4. O InvestProMax ganhará um ícone próprio na tela inicial do iPhone e abrirá em tela cheia como um aplicativo nativo.

---

### Caminho 2 — Compilar `.ipa` Nativo sem ter um Mac (Usando GitHub Actions) + TestFlight
O projeto `InvestProMax.Maui.csproj` já está configurado com `<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>`.
Mesmo usando um PC Windows, você pode compilar a versão nativa de iOS usando os servidores macOS do **GitHub Actions** (`runs-on: macos-latest`):

1. **Requisitos da Apple**:
   - Conta no **Apple Developer Program** (US$ 99/ano) para assinar o app e usar o **TestFlight**.
   - Certificado de Distribuição Apple (`.p12`) e Provisioning Profile (`.mobileprovision`) gerados no portal *developer.apple.com*.
2. **Comando de Build no macOS / GitHub Actions**:
   ```bash
   dotnet publish src/InvestProMax.Maui/InvestProMax.Maui.csproj \
     -f net10.0-ios \
     -c Release \
     -p:ArchiveOnBuild=true \
     -p:RuntimeIdentifier=ios-arm64
   ```
3. **Distribuição via TestFlight (Sem precisar publicar na App Store pública)**:
   - O arquivo `.ipa` gerado é enviado para o **App Store Connect** (via step do GitHub Actions ou app *Transporter*).
   - No **TestFlight**, você adiciona o e-mail (Apple ID) das pessoas que terão acesso (até 100 membros internos ou 10.000 externos).
   - No iPhone, a pessoa baixa o app oficial **TestFlight** na App Store, aceita o convite e instala o **InvestProMax** com 1 toque.

---

### Caminho 3 — Instalação Local em um iPhone Pessoal (Sideload via AltStore / Sideloadly)
Se você quiser instalar o `.ipa` apenas no seu próprio iPhone sem pagar a assinatura anual de US$ 99 da Apple:
1. Gere o `.ipa` usando uma máquina virtual macOS ou runner `macos-latest` do GitHub Actions (`-p:BuildIpa=true`).
2. No seu PC Windows, instale o **Sideloadly** ou **AltStore Server** (com iTunes e iCloud instalados).
3. Conecte o iPhone ao PC e assine o `.ipa` com o seu Apple ID gratuito.
   - *Limitação da conta gratuita da Apple*: Apps assinados sem conta paga de desenvolvedor expiram a cada **7 dias**, exigindo renovação automática via Wi-Fi pelo AltStore.

