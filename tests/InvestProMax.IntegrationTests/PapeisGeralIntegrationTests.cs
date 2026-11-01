using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InvestProMax.IntegrationTests;

public class PapeisGeralIntegrationTests : IsolamentoTestBase
{
    public PapeisGeralIntegrationTests(CustomWebApplicationFactory fabrica) : base(fabrica) { }

    [Fact]
    public async Task ObterPapeisGeral_RetornaSucessoComCalculosConsolidados()
    {
        var (usuario, cliente) = await CriarUsuarioAutenticadoAsync("usuario_papeis_geral");

        using (var scope = Fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvestProMaxDbContext>();

            var papel = new Papel
            {
                UsuarioId = usuario.Id,
                Codigo = "BBDC4",
                Nome = "Bradesco PN",
                TipoPapel = TipoPapel.Acao,
                CotacaoAtual = 15.00m,
                Ativo = true,
                Transacoes = new List<Transacao>
                {
                    new() { TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 12.00m, Data = new DateOnly(2026, 1, 10) }
                },
                Dividendos = new List<Dividendo>
                {
                    new() { Quantidade = 100, ValorRecebido = 10.00m, Data = new DateOnly(2026, 2, 1) }
                }
            };

            db.Papeis.Add(papel);
            await db.SaveChangesAsync();
        }

        var response = await cliente.GetAsync("/api/papeis/geral?somenteAtivos=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<PapeisGeralResponse>();
        resultado.Should().NotBeNull();
        resultado!.Itens.Should().Contain(i => i.Codigo == "BBDC4");

        var item = resultado.Itens.First(i => i.Codigo == "BBDC4");
        item.QuantidadeTotal.Should().Be(100);
        item.PrecoMedio.Should().Be(12.00m);
        item.TotalCompra.Should().Be(1200.00m);
        item.TotalAtual.Should().Be(1500.00m);
        item.GanhoTotal.Should().Be(300.00m);
        item.TotalDividendos.Should().Be(10.00m);
    }

    [Fact]
    public async Task ObterPapeisGeral_AplicaIsolamentoPorUsuario()
    {
        var (usuarioA, clienteA) = await CriarUsuarioAutenticadoAsync("usuario_a_papeis_geral");
        var (usuarioB, clienteB) = await CriarUsuarioAutenticadoAsync("usuario_b_papeis_geral");

        using (var scope = Fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvestProMaxDbContext>();

            db.Papeis.Add(new Papel
            {
                UsuarioId = usuarioA.Id,
                Codigo = "PAPE_A",
                Nome = "Papel do Usuário A",
                TipoPapel = TipoPapel.Acao,
                CotacaoAtual = 10m,
                Ativo = true,
                Transacoes = new List<Transacao> { new() { TipoTransacao = TipoTransacao.Compra, Quantidade = 10, ValorUnitario = 10m } }
            });

            db.Papeis.Add(new Papel
            {
                UsuarioId = usuarioB.Id,
                Codigo = "PAPE_B",
                Nome = "Papel do Usuário B",
                TipoPapel = TipoPapel.FII,
                CotacaoAtual = 20m,
                Ativo = true,
                Transacoes = new List<Transacao> { new() { TipoTransacao = TipoTransacao.Compra, Quantidade = 20, ValorUnitario = 20m } }
            });

            await db.SaveChangesAsync();
        }

        var responseA = await clienteA.GetFromJsonAsync<PapeisGeralResponse>("/api/papeis/geral");
        responseA!.Itens.Should().Contain(i => i.Codigo == "PAPE_A");
        responseA.Itens.Should().NotContain(i => i.Codigo == "PAPE_B");

        var responseB = await clienteB.GetFromJsonAsync<PapeisGeralResponse>("/api/papeis/geral");
        responseB!.Itens.Should().Contain(i => i.Codigo == "PAPE_B");
        responseB.Itens.Should().NotContain(i => i.Codigo == "PAPE_A");
    }
}
