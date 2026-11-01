using InvestProMax.Domain.Common;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Domain.Entidades;

public class Transacao : EntidadeBase
{
    public Guid PapelId { get; set; }
    public Papel? Papel { get; set; }

    public TipoTransacao TipoTransacao { get; set; }
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public DateOnly Data { get; set; }
    public string? Descricao { get; set; }

    /// <summary>
    /// Marca registros gerados por desdobramento/grupamento (regra 4), evitando reprocessamento
    /// em importações futuras (regra 5).
    /// </summary>
    public bool Historico { get; set; }

    /// <summary>
    /// NOVO CAMPO — ASSUNÇÃO SINALIZADA (Fase 3, regra 5): a especificação pede para a
    /// importação do extrato B3 evitar "duplicar lançamentos já importados", mas não define
    /// como identificar uma linha já importada. Casar por Quantidade/ValorUnitario/Data não é
    /// confiável porque um desdobramento (regra 4) altera exatamente esses campos depois da
    /// importação original — o próprio motivo pelo qual a regra 4 exige o flag Historico.
    /// Por isso, cada linha importada grava aqui o hash (SHA-256) dos valores *brutos* da
    /// linha da planilha (antes de qualquer ajuste), que nunca muda mesmo que a Transacao em
    /// si seja modificada depois. Nulo para transações criadas manualmente pelo usuário.
    /// </summary>
    public string? OrigemImportacaoHash { get; set; }
}
