using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Papeis;

public interface IPapelRepository
{
    /// <summary>Sempre carrega Transacoes, Dividendos e PapelSegmentos junto — necessário para
    /// calcular posição/preço médio (regras 1/2), checar antes de remover, e listar os
    /// SegmentoIds na resposta. Dado o porte do produto (uso pessoal), o custo de sempre
    /// incluir é aceitável.</summary>
    Task<Papel?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default);

    Task<(IReadOnlyList<Papel> Itens, int Total)> ListarAsync(
        Guid usuarioId, PapelFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default);

    Task<bool> ExisteCodigoAsync(Guid usuarioId, string codigo, Guid? ignorarId = null, CancellationToken ct = default);

    /// <summary>Usado pela importação B3 (regra 5) para localizar um Papel existente antes de criar um novo.</summary>
    Task<Papel?> ObterPorCodigoAsync(Guid usuarioId, string codigo, CancellationToken ct = default);

    Task AdicionarAsync(Papel papel, CancellationToken ct = default);
    Task AtualizarAsync(Papel papel, CancellationToken ct = default);
    Task RemoverAsync(Papel papel, CancellationToken ct = default);

    /// <summary>Sem paginação — usado pela regra 6 (sugestão calculada) para somar a posição
    /// de todos os papéis do usuário de um TipoPapel específico.</summary>
    Task<IReadOnlyList<Papel>> ListarPorTipoAsync(Guid usuarioId, TipoPapel tipoPapel, CancellationToken ct = default);

    /// <summary>Sem paginação — usado por backup (regra 9) e "zerar base" (seção 8).</summary>
    Task<IReadOnlyList<Papel>> ListarTodosDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Apaga todo o histórico de investimentos do usuário (Transacao, Dividendo,
    /// PapelSegmento e Papel). Chamado por ConfiguracaoService.ZerarBaseAsync junto com os
    /// métodos análogos de IAtivoRepository/IContaRepository — juntos cobrem "todos os dados
    /// do usuário" (seção 8). Nome mantido de quando isso ainda era o único módulo existente
    /// (Fase 3); só apaga a parte de investimentos, como o nome já deixa claro agora.
    /// </summary>
    Task ZerarCarteiraDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}
