using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Configuracao;

/// <summary>
/// Uma linha do extrato de movimentação da B3 (regra 5), já com os tipos primitivos
/// convertidos, mas sem nenhuma interpretação de negócio — isso é feito pelo
/// <see cref="ImportacaoB3Service"/>. Os nomes de campo seguem as colunas do extrato real
/// exportado pela Área do Investidor da B3 (Movimentação): Entrada/Saída, Data, Movimentação,
/// Produto, Quantidade, Preço unitário, Valor da Operação.
/// </summary>
public sealed record LinhaExtratoB3(
    string EntradaSaida,
    DateOnly Data,
    string Movimentacao,
    string Produto,
    decimal Quantidade,
    decimal? PrecoUnitario,
    decimal ValorOperacao);

public interface IExtratoB3Reader
{
    IReadOnlyList<LinhaExtratoB3> Ler(Stream planilha);
}

public sealed record LinhaCotacao(string Codigo, decimal Cotacao);

public interface ICotacoesReader
{
    IReadOnlyList<LinhaCotacao> Ler(Stream planilha);
}

/// <summary>Regra 9: abas Papel, Transação, Dividendo. Id incluído para que a restauração seja
/// um upsert (atualiza se o Id já existe e pertence ao usuário, cria caso contrário) em vez de
/// uma inserção cega que duplicaria tudo a cada restore.</summary>
public sealed record LinhaBackupPapel(
    Guid Id, string Codigo, string Nome, TipoPapel TipoPapel, decimal CotacaoAtual,
    string? Descricao, bool Ativo, IReadOnlyList<string> SegmentoNomes);

public sealed record LinhaBackupTransacao(
    Guid Id, string PapelCodigo, TipoTransacao TipoTransacao, decimal Quantidade,
    decimal ValorUnitario, DateOnly Data, string? Descricao, bool Historico);

public sealed record LinhaBackupDividendo(
    Guid Id, string PapelCodigo, TipoDividendo TipoDividendo, decimal ValorRecebido,
    decimal Quantidade, DateOnly Data, string? Descricao, bool Historico);

public sealed record LinhaBackupAluguel(
    Guid Id, string PapelCodigo, TipoMovimentoAluguel Tipo, decimal Quantidade, DateOnly Data, string? Descricao);

public sealed record BackupDados(
    IReadOnlyList<LinhaBackupPapel> Papeis,
    IReadOnlyList<LinhaBackupTransacao> Transacoes,
    IReadOnlyList<LinhaBackupDividendo> Dividendos,
    IReadOnlyList<LinhaBackupAluguel>? Alugueis = null);

public interface IBackupExcelWriter
{
    byte[] Gerar(BackupDados dados);
}

public interface IBackupExcelReader
{
    BackupDados Ler(Stream planilha);
}
