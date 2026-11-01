using InvestProMax.Application.Common;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Contas;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Faturas;

public interface IFaturaService
{
    Task<PaginaResultado<FaturaResponse>> ListarAsync(FaturaFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default);
    Task<GerarFaturasResponse> GerarFaturasAsync(CancellationToken ct = default);
    Task<FaturaResponse> PagarAsync(Guid faturaId, PagarFaturaRequest request, CancellationToken ct = default);
}

public sealed class FaturaService : IFaturaService
{
    private readonly IFaturaRepository _faturaRepository;
    private readonly IContaRepository _contaRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public FaturaService(IFaturaRepository faturaRepository, IContaRepository contaRepository, IUsuarioLogado usuarioLogado)
    {
        _faturaRepository = faturaRepository;
        _contaRepository = contaRepository;
        _usuarioLogado = usuarioLogado;
    }

    /// <summary>
    /// ASSUNÇÃO SINALIZADA: o filtro `status` é aplicado contra o Status *persistido* no
    /// banco (para poder paginar em SQL), mas o `Status` retornado em cada FaturaResponse é
    /// sempre o *efetivo* (Pendente vencida aparece como Atrasada, mesmo antes do job diário
    /// rodar — ver GeradorDeFaturas.EfetivarStatus). Consequência: filtrar
    /// `status=Atrasada` só retorna o que já foi persistido como tal; uma fatura vencida hoje
    /// mas ainda Pendente no banco só aparece nesse filtro depois do job rodar (uma vez por
    /// dia). Filtrar em memória para os dois convergirem sempre exigiria abrir mão de paginar
    /// no banco — troca que não valeu a pena para o porte deste produto.
    /// </summary>
    public async Task<PaginaResultado<FaturaResponse>> ListarAsync(
        FaturaFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default)
    {
        var (itens, total) = await _faturaRepository.ListarAsync(
            _usuarioLogado.UsuarioId, filtro, paginacao.Skip, paginacao.TamanhoEfetivo, ct);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        return new PaginaResultado<FaturaResponse>(
            itens.Select(f => ParaResponse(f, hoje)).ToList(), total, paginacao.PaginaEfetiva, paginacao.TamanhoEfetivo);
    }

    public async Task<GerarFaturasResponse> GerarFaturasAsync(CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var competencia = new DateOnly(hoje.Year, hoje.Month, 1);

        var criadas = await GeradorDeFaturas.GerarParaUsuarioAsync(
            _usuarioLogado.UsuarioId, competencia, _contaRepository, _faturaRepository, ct);

        return new GerarFaturasResponse(criadas);
    }

    public async Task<FaturaResponse> PagarAsync(Guid faturaId, PagarFaturaRequest request, CancellationToken ct = default)
    {
        var fatura = await _faturaRepository.ObterPorIdAsync(faturaId, _usuarioLogado.UsuarioId, ct)
            ?? throw new NaoEncontradoException("Fatura não encontrada.");

        if (fatura.Status == StatusFatura.Paga)
        {
            throw new RegraDeNegocioException("Esta fatura já está paga.");
        }
        if (fatura.Status == StatusFatura.Cancelada)
        {
            throw new RegraDeNegocioException("Uma fatura cancelada não pode ser paga.");
        }

        var pagamento = new Pagamento
        {
            FaturaId = fatura.Id,
            ValorPago = request.ValorPago,
            Data = request.Data,
            FormaPagamento = request.FormaPagamento,
            Descricao = request.Descricao
        };
        await _faturaRepository.AdicionarPagamentoAsync(pagamento, ct);

        // Regra 8: baixa da fatura.
        fatura.Status = StatusFatura.Paga;
        fatura.Pagamento = pagamento;
        await _faturaRepository.AtualizarAsync(fatura, ct);

        return ParaResponse(fatura, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    private static FaturaResponse ParaResponse(Fatura f, DateOnly hoje) => new(
        f.Id,
        f.ContaId,
        f.Conta?.Nome ?? string.Empty,
        f.Competencia,
        f.DataVencimento,
        f.ValorPrevisto,
        GeradorDeFaturas.EfetivarStatus(f, hoje),
        f.Pagamento is null ? null : new PagamentoResponse(f.Pagamento.Id, f.Pagamento.ValorPago, f.Pagamento.Data, f.Pagamento.FormaPagamento, f.Pagamento.Descricao));
}
