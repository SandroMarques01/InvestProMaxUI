using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Dashboard;

public sealed record DividendoPorMes(int Ano, int Mes, decimal Total);

public sealed record DividendoMensalPorTipo(
    int Ano,
    int Mes,
    decimal Acoes,
    decimal Fiis,
    decimal Bdrs,
    decimal Etfs,
    decimal Total);

public sealed record EvolucaoCapitalMes(
    int Ano,
    int Mes,
    decimal ComprasNoMes,
    decimal VendasNoMes,
    decimal AporteLiquidoNoMes,
    decimal CapitalAcumulado);

public sealed record AlocacaoPorTipoPapel(TipoPapel TipoPapel, decimal Valor);

public sealed record DashboardResponse(
    decimal PatrimonioTotal,
    int? PatrimonioAno,
    int? PatrimonioMes,
    decimal TotalInvestidoCarteira,
    decimal TotalAtualCarteira,
    decimal GanhoTotalCarteira,
    decimal TotalDividendosHistorico,
    IReadOnlyList<DividendoPorMes> DividendosPorMes,
    IReadOnlyList<DividendoMensalPorTipo> DividendosPorMesETipo,
    IReadOnlyList<EvolucaoCapitalMes> EvolucaoCapital,
    IReadOnlyList<AlocacaoPorTipoPapel> AlocacaoPorTipoPapel,
    decimal DividendosNoMes = 0m);

