using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Segmentos;

public sealed record CriarSegmentoRequest(string Nome, string? Descricao);

public sealed record AtualizarSegmentoRequest(string Nome, string? Descricao, bool Ativo);

public sealed record SegmentoResponse(Guid Id, string Nome, string? Descricao, bool Ativo);

/// <summary>
/// Sem filtro por UsuarioId de propósito: Segmento não tem UsuarioId no modelo de domínio
/// (seção 5) e não está entre Papel/Ativo/Conta na lista de entidades isoladas por usuário
/// (regra 10, seção 6) — é uma taxonomia compartilhada entre todos os usuários, como uma
/// lista de categorias global.
/// </summary>
public interface ISegmentoRepository
{
    Task<Segmento?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Usado pela restauração de backup (regra 9) para reaproveitar um Segmento já
    /// existente em vez de criar um duplicado pelo nome.</summary>
    Task<Segmento?> ObterPorNomeAsync(string nome, CancellationToken ct = default);
    Task<(IReadOnlyList<Segmento> Itens, int Total)> ListarAsync(bool? ativo, int skip, int tamanhoPagina, CancellationToken ct = default);
    Task<bool> ExistemTodosAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task AdicionarAsync(Segmento segmento, CancellationToken ct = default);
    Task AtualizarAsync(Segmento segmento, CancellationToken ct = default);
    Task RemoverAsync(Segmento segmento, CancellationToken ct = default);
}
