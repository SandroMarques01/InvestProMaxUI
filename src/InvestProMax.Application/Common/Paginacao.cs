namespace InvestProMax.Application.Common;

public sealed record ParametrosPaginacao(int Page = 1, int PageSize = 20)
{
    private const int TamanhoMaximo = 100;

    public int PaginaEfetiva => Page < 1 ? 1 : Page;
    public int TamanhoEfetivo => PageSize is < 1 or > TamanhoMaximo ? Math.Clamp(PageSize, 1, TamanhoMaximo) : PageSize;
    public int Skip => (PaginaEfetiva - 1) * TamanhoEfetivo;
}

public sealed record PaginaResultado<T>(IReadOnlyList<T> Itens, int Total, int Page, int PageSize);
