using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Inventario;
using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardResponse> ObterAsync(CancellationToken ct = default);
}

public sealed class DashboardService : IDashboardService
{
    private readonly IPapelRepository _papelRepository;
    private readonly IInventarioRepository _inventarioRepository;
    private readonly IDividendoRepository _dividendoRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public DashboardService(
        IPapelRepository papelRepository,
        IInventarioRepository inventarioRepository,
        IDividendoRepository dividendoRepository,
        IUsuarioLogado usuarioLogado)
    {
        _papelRepository = papelRepository;
        _inventarioRepository = inventarioRepository;
        _dividendoRepository = dividendoRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<DashboardResponse> ObterAsync(CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;

        var papeis = await _papelRepository.ListarTodosDoUsuarioAsync(usuarioId, ct);
        var dividendos = await _dividendoRepository.ListarTodosDoUsuarioAsync(usuarioId, ct);
        var inventario = await _inventarioRepository.ListarUltimoMesDoUsuarioAsync(usuarioId, ct);

        // Carteira de Ações/FIIs/BDRs/ETFs
        decimal totalInvestidoCarteira = 0m;
        decimal totalAtualCarteira = 0m;
        var valoresAlocacao = new List<(TipoPapel Tipo, decimal Valor)>();
        var dividendosPorPapel = dividendos
            .GroupBy(d => d.PapelId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.ValorRecebido));
        var candidatosTopPapeis = new List<TopPapelDashboard>();

        foreach (var papel in papeis)
        {
            var posicao = CalculadoraPosicao.Calcular(papel.Transacoes);
            if (posicao.QuantidadeAtual > 0)
            {
                var valorInvestido = posicao.QuantidadeAtual * posicao.PrecoMedio;
                var valorAtual = posicao.QuantidadeAtual * papel.CotacaoAtual;

                totalInvestidoCarteira += valorInvestido;
                totalAtualCarteira += valorAtual;
                valoresAlocacao.Add((papel.TipoPapel, valorAtual));

                var dividendosPapel = dividendosPorPapel.TryGetValue(papel.Id, out var somaDiv)
                    ? somaDiv
                    : papel.Dividendos.Sum(d => d.ValorRecebido);

                candidatosTopPapeis.Add(new TopPapelDashboard(
                    PapelId: papel.Id,
                    Codigo: papel.Codigo,
                    ValorTotalAtual: valorAtual,
                    DividendosTotal: dividendosPapel));
            }
        }

        var topPapeis = candidatosTopPapeis
            .OrderByDescending(p => p.ValorTotalAtual)
            .ThenBy(p => p.Codigo)
            .Take(10)
            .ToList();

        var ganhoTotalCarteira = totalAtualCarteira - totalInvestidoCarteira;

        // Patrimônio Total (Inventário tem prioridade; se não houver registros, fallback na carteira)
        decimal patrimonioTotal;
        int? patrimonioAno = null;
        int? patrimonioMes = null;

        if (inventario.Count > 0)
        {
            patrimonioTotal = inventario.Sum(i => i.Valor);
            patrimonioAno = inventario[0].Data.Year;
            patrimonioMes = inventario[0].Data.Month;
        }
        else
        {
            patrimonioTotal = totalAtualCarteira;
        }

        // Dividendos
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var totalDividendosHistorico = dividendos.Sum(d => d.ValorRecebido);
        var dividendosNoMes = dividendos
            .Where(d => d.Data.Year == hoje.Year && d.Data.Month == hoje.Month)
            .Sum(d => d.ValorRecebido);
        var dividendosPorMes = CalcularDividendosUltimos12Meses(dividendos);
        var dividendosPorMesETipo = CalcularDividendosPorMesETipo(dividendos, papeis);

        // Evolução de Capital pelas Compras/Vendas
        var evolucaoCapital = CalcularEvolucaoCapital(papeis);

        // Alocação por Tipo de Papel
        var alocacao = valoresAlocacao
            .GroupBy(x => x.Tipo)
            .Select(g => new AlocacaoPorTipoPapel(g.Key, g.Sum(x => x.Valor)))
            .OrderByDescending(a => a.Valor)
            .ToList();

        return new DashboardResponse(
            PatrimonioTotal: patrimonioTotal,
            PatrimonioAno: patrimonioAno,
            PatrimonioMes: patrimonioMes,
            TotalInvestidoCarteira: totalInvestidoCarteira,
            TotalAtualCarteira: totalAtualCarteira,
            GanhoTotalCarteira: ganhoTotalCarteira,
            TotalDividendosHistorico: totalDividendosHistorico,
            DividendosPorMes: dividendosPorMes,
            DividendosPorMesETipo: dividendosPorMesETipo,
            EvolucaoCapital: evolucaoCapital,
            AlocacaoPorTipoPapel: alocacao,
            DividendosNoMes: dividendosNoMes,
            TopPapeis: topPapeis);
    }

    private static IReadOnlyList<DividendoPorMes> CalcularDividendosUltimos12Meses(IReadOnlyList<Dividendo> dividendos)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var mesesJanela = Enumerable.Range(0, 12)
            .Select(offset => new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-offset))
            .OrderBy(d => d)
            .ToList();

        var somasPorMes = dividendos
            .GroupBy(d => new DateOnly(d.Data.Year, d.Data.Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(d => d.ValorRecebido));

        return mesesJanela
            .Select(mes => new DividendoPorMes(mes.Year, mes.Month, somasPorMes.GetValueOrDefault(mes, 0)))
            .ToList();
    }

    private static IReadOnlyList<DividendoMensalPorTipo> CalcularDividendosPorMesETipo(
        IReadOnlyList<Dividendo> dividendos,
        IReadOnlyList<Papel> papeis)
    {
        if (dividendos.Count == 0)
        {
            return [];
        }

        var tipoPorPapel = papeis.ToDictionary(p => p.Id, p => p.TipoPapel);

        return dividendos
            .GroupBy(d => new { d.Data.Year, d.Data.Month })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(g =>
            {
                var acoes = g.Where(d => tipoPorPapel.TryGetValue(d.PapelId, out var t) && t == TipoPapel.Acao)
                    .Sum(d => d.ValorRecebido);
                var fiis = g.Where(d => tipoPorPapel.TryGetValue(d.PapelId, out var t) && t == TipoPapel.FII)
                    .Sum(d => d.ValorRecebido);
                var bdrs = g.Where(d => tipoPorPapel.TryGetValue(d.PapelId, out var t) && t == TipoPapel.BDR)
                    .Sum(d => d.ValorRecebido);
                var etfs = g.Where(d => tipoPorPapel.TryGetValue(d.PapelId, out var t) && t == TipoPapel.ETF)
                    .Sum(d => d.ValorRecebido);
                var total = g.Sum(d => d.ValorRecebido);

                return new DividendoMensalPorTipo(
                    Ano: g.Key.Year,
                    Mes: g.Key.Month,
                    Acoes: acoes,
                    Fiis: fiis,
                    Bdrs: bdrs,
                    Etfs: etfs,
                    Total: total);
            })
            .ToList();
    }

    private static IReadOnlyList<EvolucaoCapitalMes> CalcularEvolucaoCapital(IReadOnlyList<Papel> papeis)
    {
        var todasTransacoes = papeis
            .SelectMany(p => p.Transacoes)
            .ToList();

        if (todasTransacoes.Count == 0)
        {
            return [];
        }

        var grupos = todasTransacoes
            .GroupBy(t => new { t.Data.Year, t.Data.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .ToList();

        var resultado = new List<EvolucaoCapitalMes>();
        decimal capitalAcumulado = 0m;

        foreach (var grupo in grupos)
        {
            var compras = grupo
                .Where(t => t.TipoTransacao == TipoTransacao.Compra)
                .Sum(t => t.Quantidade * t.ValorUnitario);

            var vendas = grupo
                .Where(t => t.TipoTransacao == TipoTransacao.Venda)
                .Sum(t => t.Quantidade * t.ValorUnitario);

            var aporteLiquido = compras - vendas;
            capitalAcumulado += aporteLiquido;
            if (capitalAcumulado < 0)
            {
                capitalAcumulado = 0;
            }

            resultado.Add(new EvolucaoCapitalMes(
                Ano: grupo.Key.Year,
                Mes: grupo.Key.Month,
                ComprasNoMes: compras,
                VendasNoMes: vendas,
                AporteLiquidoNoMes: aporteLiquido,
                CapitalAcumulado: capitalAcumulado));
        }

        return resultado;
    }
}
