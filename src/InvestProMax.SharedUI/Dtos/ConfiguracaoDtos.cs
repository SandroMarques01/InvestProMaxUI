namespace InvestProMax.SharedUI.Dtos;

public sealed record ImportarB3Response(
    int PapeisCriados, int TransacoesCriadas, int DividendosCriados,
    int LinhasIgnoradasDuplicadas, int LinhasIgnoradasNaoReconhecidas,
    int MovimentosAluguelCriados = 0);

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
