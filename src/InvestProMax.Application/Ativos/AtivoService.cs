using InvestProMax.Application.Common;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Ativos;

public interface IAtivoService
{
    Task<PaginaResultado<AtivoResponse>> ListarAsync(AtivoFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default);
    Task<AtivoResponse> ObterAsync(Guid id, CancellationToken ct = default);
    Task<AtivoResponse> CriarAsync(CriarAtivoRequest request, CancellationToken ct = default);
    Task<AtivoResponse> AtualizarAsync(Guid id, AtualizarAtivoRequest request, CancellationToken ct = default);
    Task RemoverAsync(Guid id, CancellationToken ct = default);
}

public sealed class AtivoService : IAtivoService
{
    private readonly IAtivoRepository _repository;
    private readonly IUsuarioLogado _usuarioLogado;

    public AtivoService(IAtivoRepository repository, IUsuarioLogado usuarioLogado)
    {
        _repository = repository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<PaginaResultado<AtivoResponse>> ListarAsync(
        AtivoFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default)
    {
        var (itens, total) = await _repository.ListarAsync(
            _usuarioLogado.UsuarioId, filtro, paginacao.Skip, paginacao.TamanhoEfetivo, ct);

        return new PaginaResultado<AtivoResponse>(itens.Select(ParaResponse).ToList(), total, paginacao.PaginaEfetiva, paginacao.TamanhoEfetivo);
    }

    public async Task<AtivoResponse> ObterAsync(Guid id, CancellationToken ct = default) =>
        ParaResponse(await ObterOuFalharAsync(id, ct));

    public async Task<AtivoResponse> CriarAsync(CriarAtivoRequest request, CancellationToken ct = default)
    {
        var ativo = new Ativo
        {
            UsuarioId = _usuarioLogado.UsuarioId,
            Nome = request.Nome,
            TipoAtivo = request.TipoAtivo,
            TipoOrigem = request.TipoOrigem,
            TipoPapelVinculado = request.TipoPapelVinculado,
            Descricao = request.Descricao,
            StatusAtivo = true
        };

        await _repository.AdicionarAsync(ativo, ct);
        return ParaResponse(ativo);
    }

    public async Task<AtivoResponse> AtualizarAsync(Guid id, AtualizarAtivoRequest request, CancellationToken ct = default)
    {
        var ativo = await ObterOuFalharAsync(id, ct);

        ativo.Nome = request.Nome;
        ativo.TipoAtivo = request.TipoAtivo;
        ativo.TipoOrigem = request.TipoOrigem;
        ativo.TipoPapelVinculado = request.TipoPapelVinculado;
        ativo.Descricao = request.Descricao;
        ativo.StatusAtivo = request.StatusAtivo;

        await _repository.AtualizarAsync(ativo, ct);
        return ParaResponse(ativo);
    }

    /// <summary>
    /// ASSUNÇÃO SINALIZADA: mesma lógica aplicada a Papel na Fase 2 — um Ativo com histórico
    /// de Inventario não pode ser removido (só desativado via StatusAtivo), para não apagar
    /// série histórica de patrimônio silenciosamente.
    /// </summary>
    public async Task RemoverAsync(Guid id, CancellationToken ct = default)
    {
        var ativo = await ObterOuFalharAsync(id, ct);

        if (ativo.HistoricoInventario.Count > 0)
        {
            throw new RegraDeNegocioException(
                "Este ativo tem lançamentos de inventário e não pode ser removido. Desative-o em vez disso (StatusAtivo = false).");
        }

        await _repository.RemoverAsync(ativo, ct);
    }

    private async Task<Ativo> ObterOuFalharAsync(Guid id, CancellationToken ct) =>
        await _repository.ObterPorIdAsync(id, _usuarioLogado.UsuarioId, ct)
            ?? throw new NaoEncontradoException("Ativo não encontrado.");

    private static AtivoResponse ParaResponse(Ativo a) => new(
        a.Id, a.Nome, a.TipoAtivo, a.TipoOrigem, a.TipoPapelVinculado, a.Descricao, a.StatusAtivo);
}
