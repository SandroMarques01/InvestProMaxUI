# InvestProMax — Documento de implantação
**Versão:** módulo de Ações com aluguel (BTC), detalhe do papel e paridade com o MinhasFinancas
**Data:** 04/10/2026 · **Responsável:** Sandro · **Ambiente alvo:** SQL Server (LocalDB em desenvolvimento)

> **Status do código:** escrito sem acesso a um SDK .NET. Nada foi compilado nem executado.
> O primeiro passo da implantação é `dotnet build` e `dotnet test`. Os valores esperados da
> seção 7 vêm de uma simulação em Python que reproduz a lógica; confirme-os no sistema real.

---

## 1. O que muda nesta versão

| Área | Mudança |
|---|---|
| **Aluguel de ações** | Saída para aluguel e devolução **não** viram venda/compra. O papel continua na carteira; a quantidade emprestada é informativa (nova entidade `MovimentoAluguel`). |
| **Importação B3** | Ordena por data antes de processar; pareia saída × devolução; "Empréstimo" com valor pequeno vira provento do tipo **Aluguel**; códigos terminados em 12/13 viram 11. |
| **Tela de Papéis / Papéis geral** | Coluna **Em aluguel**; **DY total** (regra do sistema antigo); código do papel vira link para o detalhe. |
| **Detalhe do papel** (novo) | Rota `/papeis/{id}`, endpoint `GET api/papeis/{id}/detalhe`. Fórmulas idênticas às de `Papel/Detalhes` do MinhasFinancas. |
| **Inventário** | O item calculado mostra "N em carteira (X em aluguel, incluídas)". O valor sugerido já incluía as ações emprestadas, pois não há venda. |
| **Preço médio** | Com posição zerada, mantém o último preço médio (como no sistema antigo). |
| **Desdobramento / grupamento** | Não pode ser aplicado duas vezes à mesma data (marcador `[Desdobramento dd/MM/yyyy]` na descrição). |
| **Backup Excel** | Nova aba opcional **Aluguel**. Backups antigos (sem a aba) continuam restaurando. |
| **Cultura** | `pt-BR` definida no Web e no MAUI: datas `dd/MM/yyyy`, vírgula decimal. |
| **Configurações** | Novo botão **Zerar só a carteira de ações** (preserva ativos, inventário e contas). |
| **Banco** | Nova tabela `MovimentosAluguel`. Coluna `TipoDividendo` aceita o valor `Aluguel` (já é texto, máx. 20). |

## 2. Pré-requisitos

- .NET SDK 10 e a ferramenta `dotnet-ef` (`dotnet tool install --global dotnet-ef`).
- SQL Server acessível pela connection string `InvestProMaxDb` (`src/InvestProMax.Api/appsettings*.json`).
- Os 7 extratos da B3 (`movimentacao20.xlsx` … `movimentacao26.xlsx`).
- Em desenvolvimento a migration é aplicada automaticamente ao subir a Api (`Program.cs`). Em produção ela **não** é: aplique com `dotnet ef database update` ou pelo pipeline.

## 3. Antes de começar (backup)

1. Faça backup do banco (`BACKUP DATABASE InvestProMax01DB TO DISK = '...'`).
2. Na tela **Configurações**, gere também o **backup Excel** e guarde o arquivo.
3. Copie a pasta atual do projeto (ou crie uma branch `pre-aluguel`).

## 4. Aplicar o código

1. Substitua o projeto pelo conteúdo de `InvestProMax_com_aluguel.zip` (ou aplique como branch/merge).
2. Compile e rode os testes:
   ```bash
   dotnet restore
   dotnet build
   dotnet test
   ```
3. Se houver erro de compilação, corrija antes de seguir. Pontos mais prováveis de ajuste:
   construtores de `PapelService`, `ImportacaoB3Service` e `BackupService` (ganharam `IMovimentoAluguelRepository`)
   e os DTOs duplicados em `InvestProMax.SharedUI/Dtos`.

## 5. Migration do banco

```bash
dotnet ef migrations add AluguelDeAcoes ^
  --project src/InvestProMax.Infrastructure ^
  --startup-project src/InvestProMax.Api
```
(No PowerShell use `` ` `` no lugar de `^`; no bash use `\`.)

Confira o arquivo gerado em `src/InvestProMax.Infrastructure/Migrations/`. Ele deve conter **apenas**:

- `CreateTable` de `MovimentosAluguel` (colunas `Id`, `PapelId`, `Tipo` nvarchar(20), `Quantidade` decimal(18,6), `Data`, `Descricao` nvarchar(500), `OrigemImportacaoHash` nvarchar(64));
- índices em `(PapelId, Data)` e `OrigemImportacaoHash`;
- chave estrangeira para `Papeis` com `ReferentialAction.Restrict`.

Se aparecer qualquer alteração em outras tabelas, pare e investigue. Depois aplique:

```bash
dotnet ef database update --project src/InvestProMax.Infrastructure --startup-project src/InvestProMax.Api
```

## 6. Migrar os dados de ações

Os lançamentos já importados contêm as "vendas falsas" de aluguel. Não há como corrigir isso por migration; reimporte.

1. Confirme que o backup da seção 3 foi feito.
2. **Configurações → Zerar só a carteira de ações** (apaga papéis, transações, dividendos, movimentos de aluguel e o vínculo papel × segmento).
   **Não use "Zerar toda a base":** ela também apaga ativos, inventário, contas e faturas, e o backup Excel **não** cobre esses dados.
3. Suba a Api e a interface. Confira que datas aparecem como `31/12/2025`.
4. Importe os extratos **na ordem 2020 → 2026**, um por vez, em **Configurações → Importar B3**.
   Não importe duas vezes o mesmo arquivo seguidamente: a segunda vez deve resultar só em "duplicadas".
5. Reaplique o vínculo com segmentos e importe as cotações atuais, pois os papéis foram recriados.
6. Gere um novo backup Excel (agora com a aba Aluguel).

## 7. Validação (valores esperados para os seus 7 extratos)

Simulação da lógica final sobre os arquivos enviados. Se o sistema real divergir, trate como defeito até provar o contrário.

| Indicador | Esperado |
|---|---|
| Papéis criados | **75** (nenhum código terminado em 12/13) |
| Transações criadas | **1.107** |
| Proventos criados | **1.499** (inclui 17 de remuneração de aluguel, R$ 13,41) |
| Linhas ignoradas (não reconhecidas) | **283** (inclui o "Empréstimo" do BPAC11 de 01/09/2026, R$ 2.184,80) |
| Papéis em carteira (qtd > 0) | **50** |
| Tipo Ação: papéis / unidades / em aluguel | **14 / 1.857 / 1.354** |
| Tipo BDR: papéis / unidades / em aluguel | 15 / 309 / 36 |
| Tipo FII (inclui Units): papéis / unidades / em aluguel | 21 / 1.518 / 40 |
| Total em aluguel hoje | **11 papéis, 1.430 unidades** |
| Total de proventos | **R$ 20.228,50** |

Papéis em aluguel hoje (quantidade emprestada = quantidade em carteira): DIRR3 331, GOAU4 281, BBAS3 245, ITUB4 124, BBSE3 109, B3SA3 106, CSMG3 70, VALE3 52, TAEE11 40, AAPL34 36, LEVE3 36.

Conferências pontuais:

| Papel | Qtd | Preço médio | Em aluguel | Proventos |
|---|---|---|---|---|
| BBSE3 | 109 | R$ 28,74 | 109 | R$ 1.184,59 |
| ITUB4 | 124 | R$ 26,02 | 124 | R$ 834,16 |
| DIRR3 | 331 | R$ 6,54 | 331 | R$ 1.115,23 |
| XPCA11 | 313 | R$ 8,02 | 0 | R$ 955,85 |

Para a quantidade total de ações no inventário: **Inventário → competência → item calculado de Ações**
deve exibir "1.857 em carteira (1.354 em aluguel, incluídas)" (considerando o tipo Ação).

## 8. Roteiro de aceite

- [ ] `dotnet build` e `dotnet test` sem erros.
- [ ] Migration criou só `MovimentosAluguel` + índices + FK.
- [ ] Importação dos 7 arquivos bate com a seção 7.
- [ ] Reimportar o arquivo de 2026 não cria nada novo (tudo "duplicada").
- [ ] Ativos, inventário e contas a pagar continuam intactos após "Zerar só a carteira de ações".
- [ ] **Papéis geral**: BBSE3 mostra quantidade 109 e "Em aluguel" 109; **DY total** aparece.
- [ ] **Detalhe** (`/papeis/{id}`): tabelas de Transação e Dividendos com datas `dd/MM/yyyy` e valores em R$.
- [ ] **Inventário**: texto "em carteira / em aluguel" no item calculado de Ações.
- [ ] **Backup → Restaurar** em base vazia devolve os movimentos de aluguel.
- [ ] Desdobramento manual repetido na mesma data é recusado com mensagem.

## 9. Rollback

1. Parar a Api.
2. `dotnet ef database update InitialCreate` (remove a tabela `MovimentosAluguel`) ou restaurar o backup do banco da seção 3.
3. Voltar o código para a branch/pasta anterior.
4. Restaurar o backup Excel, se necessário (o formato antigo continua válido).

## 10. Decisões e limitações conhecidas

| Tema | Situação |
|---|---|
| **Regra de aluguel** | Inferida do padrão dos extratos: débito "Transferência - Liquidação" sem preço = saída; crédito com preço enquanto há emprestado = devolução. Confirme com o extrato da corretora. |
| **"Empréstimo" como provento** | Só quando valor > 0 e, havendo quantidade, menos de R$ 1,00 por ação. O lançamento do BPAC11 (R$ 2.184,80 / 40 ações) fica fora e conta como não reconhecido. |
| **Units** | ALUP11, BPAC11 e TAEE11 (e outras units/ETFs) terminam em 11 e são classificadas como FII. Corrija o tipo manualmente em Papéis. |
| **Fórmulas herdadas** | O detalhe repete peculiaridades do antigo: "Evolução" soma as vendas; o "$ Médio" do dividendo divide pela quantidade do provento. Divisão por zero retorna 0 em vez de NaN. |
| **Código 12/13** | O antigo trocava em qualquer posição do código; aqui só o sufixo. |
| **Leilão de fração** | CMIG4 fica com 0,41 de posição porque o extrato traz o leilão com quantidade 0. Ajuste manual. |
| **Ordem no mesmo dia** | `CalculadoraPosicao` desempata por `Id` (Guid). Compra e venda no mesmo dia podem ser ordenadas de forma arbitrária; com os seus dados não houve efeito. |
| **Aluguel × data final** | O filtro de data final dos Papéis considera os movimentos de aluguel até aquela data. |
| **Backup restaurado** | Não preserva `OrigemImportacaoHash`; reimportar extratos depois de restaurar duplica lançamentos. |
| **Interface** | Layout ainda não definido; as telas novas usam o CSS atual. |

## 11. Arquivos alterados (referência)

**Novos:** `MovimentoAluguel`, `TipoMovimentoAluguel`, `MovimentoAluguelConfiguration`, `MovimentoAluguelRepository`,
`IMovimentoAluguelRepository`, `CalculadoraAluguel`, `PapelDetalheDtos` (Application e SharedUI), `PapelDetalhe.razor`,
`CalculadoraAluguelTests`, `InferenciaPapelTests`.

**Alterados:** `Papel`, `TipoDividendo` (+ cópia no SharedUI), `InvestProMaxDbContext`, `PapelRepository` (Include + AsSplitQuery + zerar carteira),
`DependencyInjection`, `PapelService`, `PapeisController`, `ImportacaoB3Service`, `ClassificadorMovimentacaoB3`, `InferenciaPapel`,
`CalculadoraPosicao`, `InventarioService`, `BackupService`, `ExcelIo`, leitor e gravador de backup, DTOs (Papel, PapelGeral, Inventário, Configuração),
`Papeis.razor`, `PapeisGeral.razor`, `Inventario.razor`, `Configuracoes.razor` (botão "Zerar só a carteira de ações"), `ConfiguracaoService` e `ConfiguracaoController` (`DELETE api/configuracao/zerar-carteira`), `Program.cs` (Web), `MauiProgram.cs`,
e os testes `ImportacaoB3ServiceTests`, `PapelServiceTests`, `PapelGeralServiceTests`, `BackupServiceTests`, `CalculadoraPosicaoTests`.
