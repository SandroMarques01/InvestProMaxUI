using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Papeis;

/// <summary>
/// Quantidade atualmente emprestada (aluguel) de um papel. Informativa: a posição do papel
/// (<see cref="CalculadoraPosicao"/>) já inclui essas ações, pois saída/devolução de aluguel
/// não geram Transacao.
/// </summary>
public static class CalculadoraAluguel
{
    public static decimal QuantidadeEmAluguel(IEnumerable<MovimentoAluguel> movimentos, DateOnly? ate = null)
    {
        var saldo = movimentos
            .Where(m => ate is null || m.Data <= ate.Value)
            .Sum(m => m.Tipo == TipoMovimentoAluguel.Saida ? m.Quantidade : -m.Quantidade);

        return Math.Max(0, saldo);
    }

    /// <summary>Nunca maior que a posição: não dá para ter mais em aluguel do que em carteira.</summary>
    public static decimal QuantidadeEmAluguel(Papel papel, decimal quantidadeEmCarteira, DateOnly? ate = null) =>
        Math.Min(QuantidadeEmAluguel(papel.MovimentosAluguel, ate), quantidadeEmCarteira);
}
