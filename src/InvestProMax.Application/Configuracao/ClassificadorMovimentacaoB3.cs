using InvestProMax.Domain.Enums;
using TipoDividendoEnum = InvestProMax.Domain.Enums.TipoDividendo;
using TipoTransacaoEnum = InvestProMax.Domain.Enums.TipoTransacao;

namespace InvestProMax.Application.Configuracao;

public enum TipoRegistroImportado
{
    Transacao,
    Dividendo,
    Ignorar
}

/// <summary>
/// Resultado da classificação de uma linha do extrato B3: o que criar e como preencher os
/// campos a partir dos dados brutos da linha.
/// </summary>
public sealed record ClassificacaoB3(
    TipoRegistroImportado Tipo,
    TipoTransacao? TipoTransacao = null,
    TipoDividendo? TipoDividendo = null,
    bool ValorUnitarioZerado = false);

/// <summary>
/// Regra 5: interpreta os tipos de movimentação do extrato da B3 listados na especificação
/// (Bonificação em Ativos, Solicitação de Subscrição, Transferência - Liquidação, JSCP,
/// Rendimento, Dividendo, Leilão de Fração, Desdobro, Grupamento, Restituição de Capital).
///
/// ASSUNÇÃO SINALIZADA: a especificação não define, para cada tipo, se ele vira Transacao ou
/// Dividendo, nem a direção (compra/venda) ou o custo unitário — só lista os nomes. As decisões
/// abaixo seguem o formato real do extrato "Movimentação" exportado pela Área do Investidor da
/// B3 (não incluído na especificação, nem havia uma planilha de exemplo disponível):
///
/// - "Transferência - Liquidação" é como a B3 registra a liquidação de compra/venda nesse
///   relatório (não "Compra"/"Venda" diretamente) — Entrada = Compra, Saída = Venda.
/// - "Bonificação em Ativos" e "Desdobro" só aumentam a quantidade em carteira sem custo
///   novo — tratados como Compra com ValorUnitario = 0, o que dilui corretamente o preço
///   médio (regra 1) sem inventar um custo que não existiu.
/// - "Grupamento" e "Leilão de Fração" reduzem a quantidade — tratados como Venda. No
///   Leilão de Fração normalmente há dinheiro envolvido (venda da fração), então o valor da
///   operação é usado; no Grupamento, ValorUnitario = 0 pelo mesmo motivo da Bonificação.
/// - "Solicitação de Subscrição" tem custo real (o preço de exercício do direito) — Compra
///   usando o valor da linha.
/// - "Dividendo", "Juros Sobre Capital Próprio"/"JSCP" e "Rendimento" mapeiam direto para o
///   TipoDividendo homônimo; "Restituição de Capital" cai em TipoDividendo.Outro por não ter
///   equivalente mais específico no enum.
///
/// Qualquer tipo de movimentação fora dessa lista é ignorado (não reconhecido), e a linha
/// entra na contagem de "não reconhecidas" da resposta da importação — nunca falha o processo
/// inteiro por causa de uma linha inesperada.
/// </summary>
public static class ClassificadorMovimentacaoB3
{
    public static ClassificacaoB3 Classificar(string movimentacao)
    {
        var chave = movimentacao.Trim();

        return chave switch
        {
            "Transferência - Liquidação" => new ClassificacaoB3(TipoRegistroImportado.Transacao),
            "Solicitação de Subscrição" => new ClassificacaoB3(TipoRegistroImportado.Transacao, TipoTransacao: TipoTransacaoEnum.Compra),
            "Bonificação em Ativos" => new ClassificacaoB3(TipoRegistroImportado.Transacao, TipoTransacao: TipoTransacaoEnum.Compra, ValorUnitarioZerado: true),
            "Desdobro" => new ClassificacaoB3(TipoRegistroImportado.Transacao, TipoTransacao: TipoTransacaoEnum.Compra, ValorUnitarioZerado: true),
            "Grupamento" => new ClassificacaoB3(TipoRegistroImportado.Transacao, TipoTransacao: TipoTransacaoEnum.Venda, ValorUnitarioZerado: true),
            "Leilão de Fração" => new ClassificacaoB3(TipoRegistroImportado.Transacao, TipoTransacao: TipoTransacaoEnum.Venda),
            "Dividendo" => new ClassificacaoB3(TipoRegistroImportado.Dividendo, TipoDividendo: TipoDividendoEnum.Dividendo),
            "Juros Sobre Capital Próprio" or "JSCP" => new ClassificacaoB3(TipoRegistroImportado.Dividendo, TipoDividendo: TipoDividendoEnum.JSCP),
            "Rendimento" => new ClassificacaoB3(TipoRegistroImportado.Dividendo, TipoDividendo: TipoDividendoEnum.Rendimento),
            "Empréstimo" => new ClassificacaoB3(TipoRegistroImportado.Dividendo, TipoDividendo: TipoDividendoEnum.Aluguel),
            "Restituição de Capital" => new ClassificacaoB3(TipoRegistroImportado.Dividendo, TipoDividendo: TipoDividendoEnum.Outro),
            _ => new ClassificacaoB3(TipoRegistroImportado.Ignorar)
        };
    }

    /// <summary>
    /// "Empréstimo" no extrato B3 aparece em duas formas: (1) o lançamento de custódia da saída
    /// para aluguel (sem valor — já representado pelo MovimentoAluguel gerado pela T-L de débito)
    /// e (2) a remuneração do aluguel (valor pequeno). Só (2) vira provento. Heurística: valor
    /// positivo e, se há quantidade, menos de R$ 1,00 por ação — valores por ação maiores se
    /// parecem com preço/garantia, não com remuneração, e ficam como "não reconhecidos".
    /// </summary>
    public static bool EhRemuneracaoDeAluguel(LinhaExtratoB3 linha) =>
        linha.ValorOperacao > 0 && (linha.Quantidade == 0 || linha.ValorOperacao / linha.Quantidade < 1m);

    /// <summary>
    /// "Transferência - Liquidação" é o único tipo cuja direção (compra/venda) depende da
    /// coluna Entrada/Saída da linha, em vez de ser fixa pelo tipo de movimentação.
    /// </summary>
    public static TipoTransacao DirecaoPorEntradaSaida(string entradaSaida) =>
        entradaSaida.Trim().Equals("Credito", StringComparison.OrdinalIgnoreCase) ? TipoTransacaoEnum.Compra : TipoTransacaoEnum.Venda;
}
