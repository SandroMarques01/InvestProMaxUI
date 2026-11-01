using InventarioEntidade = InvestProMax.Domain.Entidades.Inventario;

namespace InvestProMax.Application.Inventario;

public sealed record InventarioResponse(Guid Id, Guid AtivoId, decimal Valor, DateOnly Data, string? Descricao, bool StatusAtivo);

public sealed record InventarioFiltro(Guid? AtivoId, int? Ano, int? Mes);

public sealed record ItemLancamentoLoteRequest(Guid AtivoId, decimal Valor, string? Descricao);

/// <summary>
/// ASSUNÇÃO SINALIZADA sobre o formato do body: a especificação (seção 8) só diz "mês + lista
/// { ativoId, valor }" — Ano/Mes explícitos em vez de uma DateOnly genérica, porque o
/// lançamento é sempre do mês inteiro (dia 1), não de uma data específica.
/// </summary>
public sealed record LancamentoLoteRequest(int Ano, int Mes, IReadOnlyList<ItemLancamentoLoteRequest> Itens);

public sealed record LancamentoLoteResponse(int Criados, int Atualizados);

public sealed record ItemSugestaoCalculada(
    Guid AtivoId, string AtivoNome, decimal ValorSugerido, decimal QuantidadeTotal = 0, decimal QuantidadeEmAluguel = 0);

public sealed record SugestaoCalculadaResponse(int Ano, int Mes, IReadOnlyList<ItemSugestaoCalculada> Itens);

public interface IInventarioRepository
{
    Task<(IReadOnlyList<InventarioEntidade> Itens, int Total)> ListarAsync(
        Guid usuarioId, InventarioFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default);

    /// <summary>Usado pelo lançamento em lote para decidir se cria ou atualiza (upsert por
    /// AtivoId + mês) — ver observação em Inventario.cs (Domain) sobre por que a busca é por
    /// Data, não pelas colunas-sombra Ano/Mes.</summary>
    Task<InventarioEntidade?> ObterPorAtivoEMesAsync(Guid ativoId, int ano, int mes, CancellationToken ct = default);

    Task AdicionarAsync(InventarioEntidade inventario, CancellationToken ct = default);
    Task AtualizarAsync(InventarioEntidade inventario, CancellationToken ct = default);

    /// <summary>
    /// Usado pelo dashboard (seção 8) para o "patrimônio total": lançamentos do mês mais
    /// recente que tiver algum registro, entre os ativos ainda ativos (StatusAtivo) do
    /// usuário. Retorna vazio se o usuário nunca lançou nada em Inventario.
    /// </summary>
    Task<IReadOnlyList<InventarioEntidade>> ListarUltimoMesDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}
