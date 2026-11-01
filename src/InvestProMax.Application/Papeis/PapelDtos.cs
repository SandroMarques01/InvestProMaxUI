using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Papeis;

public sealed record CriarPapelRequest(
    string Codigo,
    string Nome,
    TipoPapel TipoPapel,
    decimal CotacaoAtual,
    string? Descricao,
    IReadOnlyList<Guid>? SegmentoIds);

/// <summary>
/// Codigo não é editável aqui de propósito — ASSUNÇÃO SINALIZADA: a especificação não decide
/// isso explicitamente, mas a regra 3 ("troca de papel") descreve exatamente o fluxo para
/// quando o código de um ticker muda na B3 (cria um Papel novo e migra o histórico), então
/// deixar Codigo editável em PUT abriria um segundo caminho inconsistente para o mesmo problema.
/// TipoPapel, ao contrário, É editável (revisão em relação à Fase 2): a importação do extrato
/// B3 (Fase 3, regra 5) precisa adivinhar o TipoPapel de um Papel novo a partir do código
/// (heurística, não é confiável para todos os casos — ver ImportacaoB3Service), então o usuário
/// precisa de um jeito de corrigir esse palpite sem usar o fluxo de troca, que é para mudança
/// de ticker, não de classificação.
/// </summary>
public sealed record AtualizarPapelRequest(
    string Nome,
    TipoPapel TipoPapel,
    decimal CotacaoAtual,
    string? Descricao,
    IReadOnlyList<Guid>? SegmentoIds);

public sealed record PapelResponse(
    Guid Id,
    string Codigo,
    string Nome,
    TipoPapel TipoPapel,
    decimal CotacaoAtual,
    string? Descricao,
    bool Ativo,
    decimal QuantidadeAtual,
    decimal PrecoMedio,
    IReadOnlyList<Guid> SegmentoIds,
    decimal QuantidadeEmAluguel = 0,
    DateTime? CotacaoAtualizadaEm = null,
    string? FonteCotacao = null);

public sealed record PapelFiltro(TipoPapel? TipoPapel, bool? Ativo);

public sealed record TrocarPapelRequest(Guid PapelDestinoId);

/// <summary>
/// ASSUNÇÃO SINALIZADA sobre a fórmula: para Desdobramento, Quantidade é multiplicada por
/// Fator e ValorUnitario dividido por Fator (o valor total da posição não muda, só quantas
/// partes ela tem); para Grupamento, o inverso. Fator é sempre informado como número > 1
/// (ex.: desdobro 1:2 → Fator = 2; grupamento 10:1 → Fator = 10) — a direção do ajuste vem do
/// campo Tipo, não do sinal de Fator.
/// </summary>
public sealed record DesdobramentoRequest(decimal Fator, DateOnly Data, TipoEventoPapel Tipo);

public enum TipoEventoPapel
{
    Desdobramento = 1,
    Grupamento = 2
}
