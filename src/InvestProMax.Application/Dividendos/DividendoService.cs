using InvestProMax.Application.Common;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Dividendos;

public interface IDividendoService
{
    Task<PaginaResultado<DividendoResponse>> ListarAsync(DividendoFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default);
    Task<DividendoResponse> ObterAsync(Guid id, CancellationToken ct = default);
    Task<DividendoResponse> CriarAsync(CriarDividendoRequest request, CancellationToken ct = default);
    Task<DividendoResponse> AtualizarAsync(Guid id, AtualizarDividendoRequest request, CancellationToken ct = default);
    Task RemoverAsync(Guid id, CancellationToken ct = default);
}

public sealed class DividendoService : IDividendoService
{
    private readonly IDividendoRepository _dividendoRepository;
    private readonly IPapelRepository _papelRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public DividendoService(
        IDividendoRepository dividendoRepository, IPapelRepository papelRepository, IUsuarioLogado usuarioLogado)
    {
        _dividendoRepository = dividendoRepository;
        _papelRepository = papelRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<PaginaResultado<DividendoResponse>> ListarAsync(
        DividendoFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default)
    {
        var (itens, total) = await _dividendoRepository.ListarAsync(
            _usuarioLogado.UsuarioId, filtro, paginacao.Skip, paginacao.TamanhoEfetivo, ct);

        return new PaginaResultado<DividendoResponse>(
            itens.Select(ParaResponse).ToList(), total, paginacao.PaginaEfetiva, paginacao.TamanhoEfetivo);
    }

    public async Task<DividendoResponse> ObterAsync(Guid id, CancellationToken ct = default) =>
        ParaResponse(await ObterOuFalharAsync(id, ct));

    public async Task<DividendoResponse> CriarAsync(CriarDividendoRequest request, CancellationToken ct = default)
    {
        var papel = await _papelRepository.ObterPorIdAsync(request.PapelId, _usuarioLogado.UsuarioId, ct: ct)
            ?? throw new NaoEncontradoException("Papel não encontrado.");

        var dividendo = new Dividendo
        {
            PapelId = papel.Id,
            TipoDividendo = request.TipoDividendo,
            ValorRecebido = request.ValorRecebido,
            Quantidade = request.Quantidade,
            Data = request.Data,
            Descricao = request.Descricao,
            Historico = false
        };

        await _dividendoRepository.AdicionarAsync(dividendo, ct);
        return ParaResponse(dividendo);
    }

    public async Task<DividendoResponse> AtualizarAsync(Guid id, AtualizarDividendoRequest request, CancellationToken ct = default)
    {
        var dividendo = await ObterOuFalharAsync(id, ct);

        dividendo.TipoDividendo = request.TipoDividendo;
        dividendo.ValorRecebido = request.ValorRecebido;
        dividendo.Quantidade = request.Quantidade;
        dividendo.Data = request.Data;
        dividendo.Descricao = request.Descricao;

        await _dividendoRepository.AtualizarAsync(dividendo, ct);
        return ParaResponse(dividendo);
    }

    public async Task RemoverAsync(Guid id, CancellationToken ct = default)
    {
        var dividendo = await ObterOuFalharAsync(id, ct);
        await _dividendoRepository.RemoverAsync(dividendo, ct);
    }

    private async Task<Dividendo> ObterOuFalharAsync(Guid id, CancellationToken ct) =>
        await _dividendoRepository.ObterPorIdAsync(id, _usuarioLogado.UsuarioId, ct)
            ?? throw new NaoEncontradoException("Dividendo não encontrado.");

    private static DividendoResponse ParaResponse(Dividendo d) => new(
        d.Id, d.PapelId, d.TipoDividendo, d.ValorRecebido, d.Quantidade, d.Data, d.Descricao, d.Historico);
}
