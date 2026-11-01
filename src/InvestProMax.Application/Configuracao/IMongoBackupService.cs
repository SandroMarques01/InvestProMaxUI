namespace InvestProMax.Application.Configuracao;

public interface IMongoBackupService
{
    Task<BackupMongoResponse> ExecutarBackupAsync(CancellationToken ct = default);
    Task<StatusMongoResponse> ObterStatusAsync(CancellationToken ct = default);
}

