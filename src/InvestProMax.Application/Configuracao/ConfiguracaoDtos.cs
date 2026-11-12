using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Configuracao;

public sealed record TransacaoImportadaDto(
    Guid Id,
    Guid PapelId,
    string Codigo,
    string? NomePapel,
    TipoTransacao TipoTransacao,
    decimal Quantidade,
    decimal ValorUnitario,
    decimal ValorTotal,
    DateOnly Data,
    string? Descricao);

public sealed record DividendoImportadoDto(
    Guid Id,
    Guid PapelId,
    string Codigo,
    string? NomePapel,
    TipoDividendo TipoDividendo,
    decimal Quantidade,
    decimal ValorRecebido,
    DateOnly Data,
    string? Descricao);

public sealed record ImportarB3Response(
    int PapeisCriados,
    int TransacoesCriadas,
    int DividendosCriados,
    int LinhasIgnoradasDuplicadas,
    int LinhasIgnoradasNaoReconhecidas,
    int MovimentosAluguelCriados = 0,
    IReadOnlyList<TransacaoImportadaDto>? Transacoes = null,
    IReadOnlyList<DividendoImportadoDto>? Dividendos = null);

public sealed record ImportarCotacoesResponse(int CotacoesAtualizadas, IReadOnlyList<string> CodigosNaoEncontrados);

public sealed record RestaurarBackupResponse(
    int PapeisCriados, int PapeisAtualizados,
    int TransacoesCriadas, int TransacoesAtualizadas,
    int DividendosCriados, int DividendosAtualizados,
    int AlugueisCriados = 0, int AlugueisAtualizados = 0);

public sealed record InfoAmbienteResponse(
    string ProvedorBanco,
    string ServidorBanco,
    string NomeBanco,
    bool Conectado,
    string Ambiente,
    DateTime DataHoraServidorUtc);

public sealed record BackupMongoTabelaItem(string NomeTabela, string NomeColecao, int TotalRegistros);

public sealed record BackupMongoResponse(
    bool Sucesso,
    string Mensagem,
    DateTime DataHoraUtc,
    string BancoOrigem,
    string BancoDestino,
    IReadOnlyList<BackupMongoTabelaItem> Tabelas,
    int TotalGeralRegistros);

public sealed record StatusMongoResponse(
    bool Conectado,
    string Servidor,
    string Banco,
    IReadOnlyList<BackupMongoTabelaItem> Colecoes,
    int TotalDocumentos);

public sealed record BackupTransacaoEventoItem(
    Guid TransacaoId,
    Guid PapelAnteriorId,
    Guid PapelAtualId,
    string CodigoAnterior,
    string CodigoAtual,
    DateOnly DataAcao,
    string TipoAcao,
    decimal? FatorMultiplicador,
    DateOnly DataTransacao,
    string TipoTransacao,
    decimal QuantidadeAnterior,
    decimal QuantidadeAtual,
    decimal ValorAnterior,
    decimal ValorAtual,
    string? Descricao);

public sealed record BackupDividendoEventoItem(
    Guid DividendoId,
    Guid PapelAnteriorId,
    Guid PapelAtualId,
    string CodigoAnterior,
    string CodigoAtual,
    DateOnly DataAcao,
    string TipoAcao,
    decimal? FatorMultiplicador,
    DateOnly DataDividendo,
    string TipoDividendo,
    decimal QuantidadeAnterior,
    decimal QuantidadeAtual,
    decimal ValorAnterior,
    decimal ValorAtual,
    string? Descricao);
