using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Contas;

public sealed record CriarContaRequest(
    string Nome,
    decimal ValorEstimado,
    int DiaVencimento,
    Recorrencia Recorrencia,
    int? NumeroParcelas,
    string? Categoria,
    string? Descricao);

public sealed record AtualizarContaRequest(
    string Nome,
    decimal ValorEstimado,
    int DiaVencimento,
    Recorrencia Recorrencia,
    int? NumeroParcelas,
    string? Categoria,
    string? Descricao,
    bool StatusAtivo);

public sealed record ContaResponse(
    Guid Id,
    string Nome,
    decimal ValorEstimado,
    int DiaVencimento,
    Recorrencia Recorrencia,
    int? NumeroParcelas,
    string? Categoria,
    string? Descricao,
    bool StatusAtivo);

public sealed record ContaFiltro(bool? StatusAtivo);

public interface IContaRepository
{
    /// <summary>Carrega Faturas junto — necessário para checar antes de remover.</summary>
    Task<Conta?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default);

    Task<(IReadOnlyList<Conta> Itens, int Total)> ListarAsync(
        Guid usuarioId, ContaFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default);

    /// <summary>Usado pela regra 7 (geração mensal de fatura).</summary>
    Task<IReadOnlyList<Conta>> ListarAtivasDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task AdicionarAsync(Conta conta, CancellationToken ct = default);
    Task AtualizarAsync(Conta conta, CancellationToken ct = default);
    Task RemoverAsync(Conta conta, CancellationToken ct = default);

    /// <summary>Apaga Pagamento, Fatura e Conta do usuário — usado por "zerar base" (seção 8).</summary>
    Task ZerarContasDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}
