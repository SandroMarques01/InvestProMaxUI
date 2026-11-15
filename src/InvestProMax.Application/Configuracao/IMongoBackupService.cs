namespace InvestProMax.Application.Configuracao;

public interface IMongoBackupService
{
    Task<BackupMongoResponse> ExecutarBackupAsync(CancellationToken ct = default);
    Task<StatusMongoResponse> ObterStatusAsync(CancellationToken ct = default);
    Task RegistrarBackupEventoPapelAsync(
        IReadOnlyList<BackupTransacaoEventoItem> transacoes,
        IReadOnlyList<BackupDividendoEventoItem> dividendos,
        CancellationToken ct = default);
    Task ZerarCarteiraDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
    Task ZerarBaseDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}

