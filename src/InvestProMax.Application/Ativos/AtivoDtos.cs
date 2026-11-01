using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Ativos;

public sealed record CriarAtivoRequest(
    string Nome,
    TipoAtivo TipoAtivo,
    TipoOrigemAtivo TipoOrigem,
    TipoPapel? TipoPapelVinculado,
    string? Descricao);

public sealed record AtualizarAtivoRequest(
    string Nome,
    TipoAtivo TipoAtivo,
    TipoOrigemAtivo TipoOrigem,
    TipoPapel? TipoPapelVinculado,
    string? Descricao,
    bool StatusAtivo);

public sealed record AtivoResponse(
    Guid Id,
    string Nome,
    TipoAtivo TipoAtivo,
    TipoOrigemAtivo TipoOrigem,
    TipoPapel? TipoPapelVinculado,
    string? Descricao,
    bool StatusAtivo);

public sealed record AtivoFiltro(TipoAtivo? TipoAtivo, bool? StatusAtivo);

public interface IAtivoRepository
{
    /// <summary>Carrega HistoricoInventario junto — necessário para checar antes de remover.</summary>
    Task<Ativo?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default);

    Task<(IReadOnlyList<Ativo> Itens, int Total)> ListarAsync(
        Guid usuarioId, AtivoFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default);

    /// <summary>Usado pela regra 6 (sugestão calculada) para achar os ativos Calculado do usuário.</summary>
    Task<IReadOnlyList<Ativo>> ListarPorOrigemAsync(Guid usuarioId, TipoOrigemAtivo tipoOrigem, CancellationToken ct = default);

    Task AdicionarAsync(Ativo ativo, CancellationToken ct = default);
    Task AtualizarAsync(Ativo ativo, CancellationToken ct = default);
    Task RemoverAsync(Ativo ativo, CancellationToken ct = default);

    /// <summary>Apaga Inventario e Ativo do usuário — usado por "zerar base" (seção 8).</summary>
    Task ZerarPatrimonioDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}
