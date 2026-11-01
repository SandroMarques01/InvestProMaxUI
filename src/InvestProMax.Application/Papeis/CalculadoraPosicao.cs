using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Papeis;

/// <summary>
/// Regras de negócio 1 e 2 (seção 6): preço médio ponderado e posição líquida do papel,
/// sempre recalculados sob demanda a partir do histórico de <see cref="Transacao"/> — nunca
/// persistidos, conforme regra 1 exige explicitamente.
/// </summary>
public static class CalculadoraPosicao
{
    public sealed record Posicao(decimal QuantidadeAtual, decimal PrecoMedio);

    /// <summary>
    /// ASSUNÇÃO SINALIZADA: a especificação não trata venda a descoberto (posição negativa).
    /// Uma venda que exceda a quantidade em carteira zera a posição em vez de deixá-la
    /// negativa — comportamento mais seguro para um controle financeiro pessoal.
    /// </summary>
    public static Posicao Calcular(IEnumerable<Transacao> transacoes)
    {
        decimal quantidade = 0;
        decimal custoTotal = 0;
        decimal ultimoPrecoMedio = 0; // sistema antigo: posição zerada mantém o último preço médio

        foreach (var transacao in transacoes.OrderBy(t => t.Data).ThenBy(t => t.TipoTransacao).ThenBy(t => t.Id))
        {
            if (transacao.TipoTransacao == TipoTransacao.Compra)
            {
                custoTotal += transacao.Quantidade * transacao.ValorUnitario;
                quantidade += transacao.Quantidade;
                continue;
            }

            // Venda: reduz a quantidade pelo preço médio vigente até aqui. O preço médio em si
            // não muda numa venda — só a quantidade e, proporcionalmente, o custo total.
            var precoMedioAtual = quantidade > 0 ? custoTotal / quantidade : ultimoPrecoMedio;
            var quantidadeVendida = Math.Min(transacao.Quantidade, quantidade);

            custoTotal -= quantidadeVendida * precoMedioAtual;
            quantidade -= quantidadeVendida;

            if (quantidade <= 0)
            {
                // Regra 2: posição líquida zerada.
                quantidade = 0;
                custoTotal = 0;
                ultimoPrecoMedio = precoMedioAtual;
            }
        }

        var precoMedio = quantidade > 0 ? custoTotal / quantidade : ultimoPrecoMedio;
        return new Posicao(quantidade, precoMedio);
    }
}
