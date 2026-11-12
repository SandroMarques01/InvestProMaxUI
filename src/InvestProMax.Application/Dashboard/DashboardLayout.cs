using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;

namespace InvestProMax.Application.Dashboard;

/// <summary>
/// Dimensão individual de um bloco do Dashboard (KPI, gráfico ou grid).
/// Colunas: largura na grade de 12 colunas (2 = 20%/1-5, 3 = 25%/1-4, 4 = 33%/1-3, 6 = 50%/1-2, 8 = 67%/2-3, 9 = 75%/3-4, 12 = 100%).
/// Altura: "auto", "sm", "md", "lg" ou "xl".
/// </summary>
public sealed record DashboardItemDimensaoDto(int Colunas, string Altura);

/// <summary>
/// Configuração de layout do Dashboard escolhida pelo usuário.
/// Kpis: KPIs visíveis na ordem escolhida.
/// Cards: Gráficos e grids visíveis na ordem escolhida.
/// Dimensoes: Largura (Colunas) e Altura por identificador do bloco.
/// </summary>
public sealed record DashboardLayoutDto(
    IReadOnlyList<string> Kpis,
    IReadOnlyList<string> Cards,
    IReadOnlyDictionary<string, DashboardItemDimensaoDto>? Dimensoes = null);

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

    public static readonly IReadOnlyList<int> ColunasPermitidas = [2, 3, 4, 6, 8, 9, 12];

    public static readonly IReadOnlyList<string> AlturasPermitidas = ["auto", "sm", "md", "lg", "xl"];

    public static IReadOnlyDictionary<string, DashboardItemDimensaoDto> ObterDimensoesPadrao() =>
        new Dictionary<string, DashboardItemDimensaoDto>(StringComparer.Ordinal)
        {
            ["patrimonio-total"] = new(2, "auto"),
            ["total-atual"] = new(2, "auto"),
            ["total-investido"] = new(2, "auto"),
            ["ganho-perda"] = new(2, "auto"),
            ["dividendos-mes"] = new(2, "auto"),
            ["alocacao-tipo"] = new(6, "auto"),
            ["dividendos-recebidos"] = new(6, "auto"),
            ["aumento-capital"] = new(6, "auto"),
            ["dividendos-por-tipo"] = new(6, "auto"),
            ["top10-papeis"] = new(12, "auto")
        };

    public static DashboardLayoutDto LayoutPadrao => new(KpisPadrao, CardsPadrao, ObterDimensoesPadrao());

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
        ValidarLista(layout.Kpis, KpisPadrao, "KPIs");
        ValidarLista(layout.Cards, CardsPadrao, "gráficos e grids");
        ValidarDimensoes(layout.Dimensoes);

        await _repository.SalvarAsync(_usuarioLogado.UsuarioId, layout, ct);
        return layout;
    }

    // Permite subconjunto (itens ocultos ficam fora da lista), sem repetição e sem IDs desconhecidos.
    private static void ValidarLista(IReadOnlyList<string>? informados, IReadOnlyList<string> permitidos, string grupo)
    {
        if (informados is null
            || informados.Count > permitidos.Count
            || informados.Distinct(StringComparer.Ordinal).Count() != informados.Count
            || informados.Any(id => !permitidos.Contains(id, StringComparer.Ordinal)))
        {
            throw new RegraDeNegocioException(
                $"Layout inválido para {grupo}: informe apenas blocos válidos ({string.Join(", ", permitidos)}) sem duplicidade.");
        }
    }

    private static void ValidarDimensoes(IReadOnlyDictionary<string, DashboardItemDimensaoDto>? dimensoes)
    {
        if (dimensoes is null) return;

        foreach (var (id, dim) in dimensoes)
        {
            var idConhecido = KpisPadrao.Contains(id, StringComparer.Ordinal)
                              || CardsPadrao.Contains(id, StringComparer.Ordinal);
            if (!idConhecido)
            {
                throw new RegraDeNegocioException($"Bloco desconhecido nas dimensões do layout: '{id}'.");
            }

            if (dim is null || !ColunasPermitidas.Contains(dim.Colunas))
            {
                throw new RegraDeNegocioException(
                    $"Largura inválida para o bloco '{id}'. Valores permitidos: {string.Join(", ", ColunasPermitidas)}.");
            }

            if (string.IsNullOrWhiteSpace(dim.Altura) || !AlturasPermitidas.Contains(dim.Altura, StringComparer.Ordinal))
            {
                throw new RegraDeNegocioException(
                    $"Altura inválida para o bloco '{id}'. Valores permitidos: {string.Join(", ", AlturasPermitidas)}.");
            }
        }
    }
}

