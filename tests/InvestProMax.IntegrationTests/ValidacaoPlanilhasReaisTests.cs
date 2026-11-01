using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Configuracao;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Papeis;
using InvestProMax.Application.Transacoes;
using InvestProMax.Domain.Enums;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InvestProMax.IntegrationTests;

public class ValidacaoPlanilhasReaisTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private const string PastaExtratos = @"C:\MinhasFinancas\InvestidorProMax_Claude";

    public ValidacaoPlanilhasReaisTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ImportacaoDos7ExtratosReais_ConfereComMetricasEsperadasDaSecao7()
    {
        if (!Directory.Exists(PastaExtratos))
        {
            return;
        }

        var arquivos = new[]
        {
            Path.Combine(PastaExtratos, "movimentacao20.xlsx"),
            Path.Combine(PastaExtratos, "movimentacao21.xlsx"),
            Path.Combine(PastaExtratos, "movimentacao22.xlsx"),
            Path.Combine(PastaExtratos, "movimentacao23.xlsx"),
            Path.Combine(PastaExtratos, "movimentacao24.xlsx"),
            Path.Combine(PastaExtratos, "movimentacao25.xlsx"),
            Path.Combine(PastaExtratos, "movimentacao26.xlsx")
        };

        foreach (var arq in arquivos)
        {
            File.Exists(arq).Should().BeTrue($"O arquivo {arq} precisa existir.");
        }

        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var usuarioId = Guid.NewGuid();

        var usuarioLogadoMock = new MockUsuarioLogado(usuarioId);
        var reader = sp.GetRequiredService<IExtratoB3Reader>();
        var papelRepo = sp.GetRequiredService<IPapelRepository>();
        var transacaoRepo = sp.GetRequiredService<ITransacaoRepository>();
        var dividendoRepo = sp.GetRequiredService<IDividendoRepository>();
        var aluguelRepo = sp.GetRequiredService<IMovimentoAluguelRepository>();

        var importacaoService = new ImportacaoB3Service(
            reader, papelRepo, transacaoRepo, dividendoRepo, aluguelRepo, usuarioLogadoMock);

        var papelService = new PapelService(
            papelRepo, transacaoRepo, aluguelRepo, dividendoRepo,
            sp.GetRequiredService<InvestProMax.Application.Segmentos.ISegmentoRepository>(),
            usuarioLogadoMock);

        int totalPapeisCriados = 0;
        int totalTransacoesCriadas = 0;
        int totalDividendosCriados = 0;
        int totalLinhasIgnoradas = 0;
        int totalAlugueisCriados = 0;

        foreach (var arquivo in arquivos)
        {
            using var stream = File.OpenRead(arquivo);
            var resp = await importacaoService.ImportarAsync(stream);
            totalPapeisCriados += resp.PapeisCriados;
            totalTransacoesCriadas += resp.TransacoesCriadas;
            totalDividendosCriados += resp.DividendosCriados;
            totalLinhasIgnoradas += resp.LinhasIgnoradasNaoReconhecidas;
            totalAlugueisCriados += resp.MovimentosAluguelCriados;
        }

        // Validações da Seção 7
        totalPapeisCriados.Should().Be(75, "devem ser criados 75 papéis");
        totalTransacoesCriadas.Should().Be(1107, "devem ser criadas 1107 transações");
        totalDividendosCriados.Should().Be(1499, "devem ser criados 1499 proventos");
        totalLinhasIgnoradas.Should().Be(283, "devem ser 283 linhas não reconhecidas/ignoradas");

        // Nenhum código terminado em 12 ou 13
        var todosPapeis = await papelRepo.ListarTodosDoUsuarioAsync(usuarioId);
        todosPapeis.Should().NotContain(p => p.Codigo.EndsWith("12") || p.Codigo.EndsWith("13"));

        // Conferências gerais de papéis em carteira
        var geral = await papelService.ObterPapeisGeralAsync(somenteAtivos: null);
        var emCarteira = geral.Itens.Where(i => i.QuantidadeTotal > 0).ToList();
        emCarteira.Should().HaveCount(50, "50 papéis com quantidade > 0");

        var acoes = geral.Itens.Where(i => i.TipoPapel == TipoPapel.Acao && i.QuantidadeTotal > 0).ToList();
        acoes.Should().HaveCount(14);
        acoes.Sum(a => a.QuantidadeTotal).Should().Be(1857.41m, "1857 inteiras + 0.41 fração CMIG4 conforme seção 10");
        acoes.Sum(a => a.QuantidadeEmAluguel).Should().Be(1354m);

        var bdrs = geral.Itens.Where(i => i.TipoPapel == TipoPapel.BDR && i.QuantidadeTotal > 0).ToList();
        bdrs.Should().HaveCount(15);
        bdrs.Sum(b => b.QuantidadeTotal).Should().Be(309m);
        bdrs.Sum(b => b.QuantidadeEmAluguel).Should().Be(36m);

        var fiis = geral.Itens.Where(i => i.TipoPapel == TipoPapel.FII && i.QuantidadeTotal > 0).ToList();
        fiis.Should().HaveCount(21);
        fiis.Sum(f => f.QuantidadeTotal).Should().Be(1518m);
        fiis.Sum(f => f.QuantidadeEmAluguel).Should().Be(40m);

        var emAluguelHoje = geral.Itens.Where(i => i.QuantidadeEmAluguel > 0).ToList();
        emAluguelHoje.Should().HaveCount(11, "11 papéis em aluguel hoje");
        emAluguelHoje.Sum(a => a.QuantidadeEmAluguel).Should().Be(1430m, "1430 unidades em aluguel");

        geral.TotalDividendos.Should().Be(20228.50m);

        // Conferências pontuais
        var bbse3 = geral.Itens.Single(i => i.Codigo == "BBSE3");
        bbse3.QuantidadeTotal.Should().Be(109m);
        Math.Round(bbse3.PrecoMedio, 2).Should().Be(28.74m);
        bbse3.QuantidadeEmAluguel.Should().Be(109m);
        bbse3.TotalDividendos.Should().Be(1184.59m);

        var itub4 = geral.Itens.Single(i => i.Codigo == "ITUB4");
        itub4.QuantidadeTotal.Should().Be(124m);
        Math.Round(itub4.PrecoMedio, 2).Should().Be(26.02m);
        itub4.QuantidadeEmAluguel.Should().Be(124m);
        itub4.TotalDividendos.Should().Be(834.16m);

        var dirr3 = geral.Itens.Single(i => i.Codigo == "DIRR3");
        dirr3.QuantidadeTotal.Should().Be(331m);
        Math.Round(dirr3.PrecoMedio, 2).Should().Be(6.54m);
        dirr3.QuantidadeEmAluguel.Should().Be(331m);
        dirr3.TotalDividendos.Should().Be(1115.23m);

        var xpca11 = geral.Itens.Single(i => i.Codigo == "XPCA11");
        xpca11.QuantidadeTotal.Should().Be(313m);
        Math.Round(xpca11.PrecoMedio, 2).Should().Be(8.02m);
        xpca11.QuantidadeEmAluguel.Should().Be(0m);
        xpca11.TotalDividendos.Should().Be(955.85m);

        // Reimportar 2026 deve produzir apenas duplicadas
        using (var reimportStream = File.OpenRead(Path.Combine(PastaExtratos, "movimentacao26.xlsx")))
        {
            var reimportResp = await importacaoService.ImportarAsync(reimportStream);
            reimportResp.PapeisCriados.Should().Be(0);
            reimportResp.TransacoesCriadas.Should().Be(0);
            reimportResp.DividendosCriados.Should().Be(0);
            reimportResp.MovimentosAluguelCriados.Should().Be(0);
            reimportResp.LinhasIgnoradasDuplicadas.Should().BeGreaterThan(0);
        }
    }

    private sealed class MockUsuarioLogado : IUsuarioLogado
    {
        public MockUsuarioLogado(Guid usuarioId) => UsuarioId = usuarioId;
        public Guid UsuarioId { get; }
        public bool EstaAutenticado => true;
    }
}
