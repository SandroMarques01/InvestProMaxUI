using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvestProMax.Application.Common;
using InvestProMax.Application.Contas;
using InvestProMax.Application.Faturas;
using InvestProMax.Application.Papeis;
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
}
