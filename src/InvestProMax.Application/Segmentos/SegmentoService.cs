using InvestProMax.Application.Common;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Segmentos;

public interface ISegmentoService
{
    Task<PaginaResultado<SegmentoResponse>> ListarAsync(bool? ativo, ParametrosPaginacao paginacao, CancellationToken ct = default);
    Task<SegmentoResponse> ObterAsync(Guid id, CancellationToken ct = default);
    Task<SegmentoResponse> CriarAsync(CriarSegmentoRequest request, CancellationToken ct = default);
    Task<SegmentoResponse> AtualizarAsync(Guid id, AtualizarSegmentoRequest request, CancellationToken ct = default);
    Task RemoverAsync(Guid id, CancellationToken ct = default);
}

public sealed class SegmentoService : ISegmentoService
{
    private readonly ISegmentoRepository _repository;

    public SegmentoService(ISegmentoRepository repository) => _repository = repository;

    public async Task<PaginaResultado<SegmentoResponse>> ListarAsync(
        bool? ativo, ParametrosPaginacao paginacao, CancellationToken ct = default)
    {
        var (itens, total) = await _repository.ListarAsync(ativo, paginacao.Skip, paginacao.TamanhoEfetivo, ct);
        return new PaginaResultado<SegmentoResponse>(itens.Select(ParaResponse).ToList(), total, paginacao.PaginaEfetiva, paginacao.TamanhoEfetivo);
    }

    public async Task<SegmentoResponse> ObterAsync(Guid id, CancellationToken ct = default)
    {
        var segmento = await _repository.ObterPorIdAsync(id, ct) ?? throw new NaoEncontradoException("Segmento não encontrado.");
        return ParaResponse(segmento);
    }

    public async Task<SegmentoResponse> CriarAsync(CriarSegmentoRequest request, CancellationToken ct = default)
    {
        var segmento = new Segmento { Nome = request.Nome, Descricao = request.Descricao, Ativo = true };
        await _repository.AdicionarAsync(segmento, ct);
        return ParaResponse(segmento);
    }

    public async Task<SegmentoResponse> AtualizarAsync(Guid id, AtualizarSegmentoRequest request, CancellationToken ct = default)
    {
        var segmento = await _repository.ObterPorIdAsync(id, ct) ?? throw new NaoEncontradoException("Segmento não encontrado.");

        segmento.Nome = request.Nome;
        segmento.Descricao = request.Descricao;
        segmento.Ativo = request.Ativo;

        await _repository.AtualizarAsync(segmento, ct);
        return ParaResponse(segmento);
    }

    public async Task RemoverAsync(Guid id, CancellationToken ct = default)
    {
        var segmento = await _repository.ObterPorIdAsync(id, ct) ?? throw new NaoEncontradoException("Segmento não encontrado.");
        await _repository.RemoverAsync(segmento, ct);
    }

    private static SegmentoResponse ParaResponse(Segmento s) => new(s.Id, s.Nome, s.Descricao, s.Ativo);
}
