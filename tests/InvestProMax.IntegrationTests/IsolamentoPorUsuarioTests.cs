using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvestProMax.Application.Ativos;
using InvestProMax.Application.Common;
using InvestProMax.Application.Contas;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Faturas;
using InvestProMax.Application.Inventario;
using InvestProMax.Application.Papeis;
using InvestProMax.Application.Transacoes;
using InvestProMax.Domain.Enums;
using Xunit;

namespace InvestProMax.IntegrationTests;

/// <summary>
/// Regra 10 (seção 6): "toda consulta que envolve dados de Papel, Ativo ou Conta (e suas
/// entidades filhas) deve ser filtrada pelo UsuarioId extraído do token JWT autenticado".
/// Estes testes exercitam o pipeline real (HTTP + autenticação JWT + repositórios), não só a
/// camada de serviço isolada — é o que a seção 11 pede explicitamente e que os testes
/// unitários existentes (Application.Tests) não cobrem.
/// </summary>
public class IsolamentoPorUsuarioTests : IsolamentoTestBase
{
    public IsolamentoPorUsuarioTests(CustomWebApplicationFactory fabrica) : base(fabrica) { }

    [Fact]
    public async Task Usuario_B_nao_consegue_obter_Papel_criado_pelo_Usuario_A()
    {
        var (_, clienteA) = await CriarUsuarioAutenticadoAsync("usuarioA_papel");
        var (_, clienteB) = await CriarUsuarioAutenticadoAsync("usuarioB_papel");

        var criarResponse = await clienteA.PostAsJsonAsync("/api/papeis", new CriarPapelRequest(
            "PETR4", "Petrobras PN", TipoPapel.Acao, 38.50m, null, null));
        criarResponse.EnsureSuccessStatusCode();
        var papelDeA = await criarResponse.Content.ReadFromJsonAsync<PapelResponse>();

        var obterComoB = await clienteB.GetAsync($"/api/papeis/{papelDeA!.Id}");

        obterComoB.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Listagem_de_Papeis_do_Usuario_B_nao_inclui_papeis_do_Usuario_A()
    {
        var (_, clienteA) = await CriarUsuarioAutenticadoAsync("usuarioA_lista");
        var (_, clienteB) = await CriarUsuarioAutenticadoAsync("usuarioB_lista");

        await clienteA.PostAsJsonAsync("/api/papeis", new CriarPapelRequest(
            "VALE3", "Vale ON", TipoPapel.Acao, 60.00m, null, null));

        var listaComoB = await clienteB.GetFromJsonAsync<PaginaResultado<PapelResponse>>("/api/papeis");

        listaComoB!.Itens.Should().BeEmpty();
    }

    [Fact]
    public async Task Usuario_B_nao_consegue_obter_Fatura_de_Conta_do_Usuario_A()
    {
        // Caso de "join": Fatura não tem UsuarioId direto — o isolamento depende de filtrar
        // por Conta.UsuarioId (ver FaturaRepository), então vale testar separado do caso Papel.
        var (_, clienteA) = await CriarUsuarioAutenticadoAsync("usuarioA_fatura");
        var (_, clienteB) = await CriarUsuarioAutenticadoAsync("usuarioB_fatura");

        var criarContaResponse = await clienteA.PostAsJsonAsync("/api/contas", new CriarContaRequest(
            "Internet", 120.00m, 10, Recorrencia.Mensal, null, null, null));
        criarContaResponse.EnsureSuccessStatusCode();

        var gerarFaturasResponse = await clienteA.PostAsync("/api/faturas/gerar", content: null);
        gerarFaturasResponse.EnsureSuccessStatusCode();

        var faturasDeA = await clienteA.GetFromJsonAsync<PaginaResultado<FaturaResponse>>("/api/faturas");
        var faturaDeA = faturasDeA!.Itens.Should().ContainSingle().Subject;

        var pagarComoB = await clienteB.PostAsJsonAsync(
            $"/api/faturas/{faturaDeA.Id}/pagar",
            new PagarFaturaRequest(faturaDeA.ValorPrevisto, DateOnly.FromDateTime(DateTime.UtcNow), null, null));

        pagarComoB.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ZerarCarteira_apaga_apenas_carteira_do_usuario_solicitante_e_preserva_ativos_contas_e_outro_usuario()
    {
        var (_, clienteA) = await CriarUsuarioAutenticadoAsync("usuarioA_zerar_carteira");
        var (_, clienteB) = await CriarUsuarioAutenticadoAsync("usuarioB_zerar_carteira");
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        // Popula carteira + ativo + conta do usuário A
        var papelAResp = await clienteA.PostAsJsonAsync("/api/papeis", new CriarPapelRequest(
            "ITUB4", "Itaú Unibanco PN", TipoPapel.Acao, 35.00m, null, null));
        papelAResp.EnsureSuccessStatusCode();
        var papelA = await papelAResp.Content.ReadFromJsonAsync<PapelResponse>();

        (await clienteA.PostAsJsonAsync("/api/transacoes", new CriarTransacaoRequest(
            papelA!.Id, TipoTransacao.Compra, 100m, 30m, hoje, null))).EnsureSuccessStatusCode();
        (await clienteA.PostAsJsonAsync("/api/dividendos", new CriarDividendoRequest(
            papelA.Id, TipoDividendo.Dividendo, 50m, 100m, hoje, null))).EnsureSuccessStatusCode();

        var ativoAResp = await clienteA.PostAsJsonAsync("/api/ativos", new CriarAtivoRequest(
            "Tesouro Selic A", TipoAtivo.RendaFixa, TipoOrigemAtivo.Manual, null, null));
        ativoAResp.EnsureSuccessStatusCode();

        var contaAResp = await clienteA.PostAsJsonAsync("/api/contas", new CriarContaRequest(
            "Energia A", 200m, 10, Recorrencia.Mensal, null, null, null));
        contaAResp.EnsureSuccessStatusCode();

        // Popula carteira + ativo + conta do usuário B
        var papelBResp = await clienteB.PostAsJsonAsync("/api/papeis", new CriarPapelRequest(
            "BBDC4", "Bradesco PN", TipoPapel.Acao, 15.00m, null, null));
        papelBResp.EnsureSuccessStatusCode();
        var papelB = await papelBResp.Content.ReadFromJsonAsync<PapelResponse>();

        (await clienteB.PostAsJsonAsync("/api/transacoes", new CriarTransacaoRequest(
            papelB!.Id, TipoTransacao.Compra, 50m, 14m, hoje, null))).EnsureSuccessStatusCode();
        (await clienteB.PostAsJsonAsync("/api/dividendos", new CriarDividendoRequest(
            papelB.Id, TipoDividendo.Dividendo, 25m, 50m, hoje, null))).EnsureSuccessStatusCode();

        (await clienteB.PostAsJsonAsync("/api/ativos", new CriarAtivoRequest(
            "Tesouro Selic B", TipoAtivo.RendaFixa, TipoOrigemAtivo.Manual, null, null))).EnsureSuccessStatusCode();
        (await clienteB.PostAsJsonAsync("/api/contas", new CriarContaRequest(
            "Energia B", 300m, 15, Recorrencia.Mensal, null, null, null))).EnsureSuccessStatusCode();

        // Usuário A zera apenas sua carteira
        var zerarResp = await clienteA.DeleteAsync("/api/configuracao/zerar-carteira");
        zerarResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Carteira do Usuário A zerada; ativos e contas preservados
        (await clienteA.GetFromJsonAsync<PaginaResultado<PapelResponse>>("/api/papeis"))!.Itens.Should().BeEmpty();
        (await clienteA.GetFromJsonAsync<PaginaResultado<TransacaoResponse>>("/api/transacoes"))!.Itens.Should().BeEmpty();
        (await clienteA.GetFromJsonAsync<PaginaResultado<DividendoResponse>>("/api/dividendos"))!.Itens.Should().BeEmpty();
        (await clienteA.GetFromJsonAsync<PaginaResultado<AtivoResponse>>("/api/ativos"))!.Itens.Should().ContainSingle();
        (await clienteA.GetFromJsonAsync<PaginaResultado<ContaResponse>>("/api/contas"))!.Itens.Should().ContainSingle();

        // Dados do Usuário B completamente preservados
        (await clienteB.GetFromJsonAsync<PaginaResultado<PapelResponse>>("/api/papeis"))!.Itens.Should().ContainSingle();
        (await clienteB.GetFromJsonAsync<PaginaResultado<TransacaoResponse>>("/api/transacoes"))!.Itens.Should().ContainSingle();
        (await clienteB.GetFromJsonAsync<PaginaResultado<DividendoResponse>>("/api/dividendos"))!.Itens.Should().ContainSingle();
        (await clienteB.GetFromJsonAsync<PaginaResultado<AtivoResponse>>("/api/ativos"))!.Itens.Should().ContainSingle();
        (await clienteB.GetFromJsonAsync<PaginaResultado<ContaResponse>>("/api/contas"))!.Itens.Should().ContainSingle();
    }

    [Fact]
    public async Task ZerarBase_apaga_todos_os_dados_do_usuario_solicitante_e_preserva_outro_usuario()
    {
        var (_, clienteA) = await CriarUsuarioAutenticadoAsync("usuarioA_zerar_base");
        var (_, clienteB) = await CriarUsuarioAutenticadoAsync("usuarioB_zerar_base");
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        // Popula toda a base do usuário A
        var papelAResp = await clienteA.PostAsJsonAsync("/api/papeis", new CriarPapelRequest(
            "WEGE3", "WEG ON", TipoPapel.Acao, 50m, null, null));
        papelAResp.EnsureSuccessStatusCode();
        var papelA = await papelAResp.Content.ReadFromJsonAsync<PapelResponse>();
        (await clienteA.PostAsJsonAsync("/api/transacoes", new CriarTransacaoRequest(
            papelA!.Id, TipoTransacao.Compra, 10m, 48m, hoje, null))).EnsureSuccessStatusCode();

        var ativoAResp = await clienteA.PostAsJsonAsync("/api/ativos", new CriarAtivoRequest(
            "CDB Banco A", TipoAtivo.RendaFixa, TipoOrigemAtivo.Manual, null, null));
        ativoAResp.EnsureSuccessStatusCode();
        var ativoA = await ativoAResp.Content.ReadFromJsonAsync<AtivoResponse>();
        (await clienteA.PostAsJsonAsync("/api/inventario/lote", new LancamentoLoteRequest(
            hoje.Year, hoje.Month, [new ItemLancamentoLoteRequest(ativoA!.Id, 1000m, null)]))).EnsureSuccessStatusCode();

        (await clienteA.PostAsJsonAsync("/api/contas", new CriarContaRequest(
            "Aluguel A", 1500m, 5, Recorrencia.Mensal, null, null, null))).EnsureSuccessStatusCode();
        (await clienteA.PostAsync("/api/faturas/gerar", null)).EnsureSuccessStatusCode();

        // Popula toda a base do usuário B
        var papelBResp = await clienteB.PostAsJsonAsync("/api/papeis", new CriarPapelRequest(
            "ABEV3", "Ambev ON", TipoPapel.Acao, 13m, null, null));
        papelBResp.EnsureSuccessStatusCode();
        var ativoBResp = await clienteB.PostAsJsonAsync("/api/ativos", new CriarAtivoRequest(
            "CDB Banco B", TipoAtivo.RendaFixa, TipoOrigemAtivo.Manual, null, null));
        ativoBResp.EnsureSuccessStatusCode();
        var ativoB = await ativoBResp.Content.ReadFromJsonAsync<AtivoResponse>();
        (await clienteB.PostAsJsonAsync("/api/inventario/lote", new LancamentoLoteRequest(
            hoje.Year, hoje.Month, [new ItemLancamentoLoteRequest(ativoB!.Id, 2000m, null)]))).EnsureSuccessStatusCode();
        (await clienteB.PostAsJsonAsync("/api/contas", new CriarContaRequest(
            "Aluguel B", 1800m, 5, Recorrencia.Mensal, null, null, null))).EnsureSuccessStatusCode();

        // Usuário A zera toda a base
        var zerarBaseResp = await clienteA.DeleteAsync("/api/configuracao/zerar-base");
        zerarBaseResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Toda a base do Usuário A apagada
        (await clienteA.GetFromJsonAsync<PaginaResultado<PapelResponse>>("/api/papeis"))!.Itens.Should().BeEmpty();
        (await clienteA.GetFromJsonAsync<PaginaResultado<TransacaoResponse>>("/api/transacoes"))!.Itens.Should().BeEmpty();
        (await clienteA.GetFromJsonAsync<PaginaResultado<AtivoResponse>>("/api/ativos"))!.Itens.Should().BeEmpty();
        (await clienteA.GetFromJsonAsync<PaginaResultado<InventarioResponse>>("/api/inventario"))!.Itens.Should().BeEmpty();
        (await clienteA.GetFromJsonAsync<PaginaResultado<ContaResponse>>("/api/contas"))!.Itens.Should().BeEmpty();
        (await clienteA.GetFromJsonAsync<PaginaResultado<FaturaResponse>>("/api/faturas"))!.Itens.Should().BeEmpty();

        // Toda a base do Usuário B preservada
        (await clienteB.GetFromJsonAsync<PaginaResultado<PapelResponse>>("/api/papeis"))!.Itens.Should().ContainSingle();
        (await clienteB.GetFromJsonAsync<PaginaResultado<AtivoResponse>>("/api/ativos"))!.Itens.Should().ContainSingle();
        (await clienteB.GetFromJsonAsync<PaginaResultado<InventarioResponse>>("/api/inventario"))!.Itens.Should().ContainSingle();
        (await clienteB.GetFromJsonAsync<PaginaResultado<ContaResponse>>("/api/contas"))!.Itens.Should().ContainSingle();
    }
}
