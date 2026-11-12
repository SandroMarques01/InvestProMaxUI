using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;

namespace InvestProMax.Application.Dashboard;

/// <summary>
/// Ordem dos blocos do Dashboard escolhida pelo usuário (drag and drop).
/// Kpis: ordem dos cards de indicador. Cards: ordem dos gráficos e grids na área de cards.
/// </summary>
public sealed record DashboardLayoutDto(IReadOnlyList<string> Kpis, IReadOnlyList<string> Cards);

/// <summary>Persistência do layout do Dashboard por usuário (collection IPM_DashboardLayout no MongoDB).</summary>
public interface IDashboardLayoutRepository
{
    Task<DashboardLayoutDto?> ObterAsync(Guid usuarioId, CancellationToken ct = default);
    Task SalvarAsync(Guid usuarioId, DashboardLayoutDto layout, CancellationToken ct = default);
}

public interface IDashboardLayoutService
{
    Task<DashboardLayoutDto> ObterAsync(CancellationToken ct = default);
    Task<DashboardLayoutDto> SalvarAsync(DashboardLayoutDto layout, CancellationToken ct = default);
}

public sealed class DashboardLayoutService : IDashboardLayoutService
{
    /// <summary>Identificadores dos blocos na ordem original do Dashboard (layout padrão).</summary>
    public static readonly IReadOnlyList<string> KpisPadrao =
        ["patrimonio-total", "total-atual", "total-investido", "ganho-perda", "dividendos-mes"];

    public static readonly IReadOnlyList<string> CardsPadrao =
        ["alocacao-tipo", "dividendos-recebidos", "aumento-capital", "dividendos-por-tipo", "top10-papeis"];

    public static DashboardLayoutDto LayoutPadrao => new(KpisPadrao, CardsPadrao);

    private readonly IDashboardLayoutRepository _repository;
    private readonly IUsuarioLogado _usuarioLogado;

    public DashboardLayoutService(IDashboardLayoutRepository repository, IUsuarioLogado usuarioLogado)
    {
        _repository = repository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<DashboardLayoutDto> ObterAsync(CancellationToken ct = default)
    {
        var salvo = await _repository.ObterAsync(_usuarioLogado.UsuarioId, ct);
        return salvo ?? LayoutPadrao;
    }

    public async Task<DashboardLayoutDto> SalvarAsync(DashboardLayoutDto layout, CancellationToken ct = default)
    {
        Validar(layout.Kpis, KpisPadrao, "KPIs");
        Validar(layout.Cards, CardsPadrao, "gráficos e grids");

        await _repository.SalvarAsync(_usuarioLogado.UsuarioId, layout, ct);
        return layout;
    }

    // O layout precisa ser uma permutação exata dos blocos existentes: sem faltar, repetir ou inventar blocos.
    private static void Validar(IReadOnlyList<string>? informados, IReadOnlyList<string> esperados, string grupo)
    {
        if (informados is null
            || informados.Count != esperados.Count
            || !informados.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(esperados.OrderBy(x => x, StringComparer.Ordinal)))
        {
            throw new RegraDeNegocioException(
                $"Layout inválido para {grupo}: informe exatamente os blocos {string.Join(", ", esperados)}.");
        }
    }
}

