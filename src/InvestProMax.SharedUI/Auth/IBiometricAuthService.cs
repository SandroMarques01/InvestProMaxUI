namespace InvestProMax.SharedUI.Auth;

public sealed record CredenciaisBiometricas(string NomeUsuario, string Senha);

public interface IBiometricAuthService
{
    Task<bool> EstaDisponivelAsync();
    Task<bool> PossuiCredenciaisSalvasAsync();
    Task SalvarCredenciaisAsync(string nomeUsuario, string senha);
    Task<bool> AutenticarAsync(string motivo = "Use sua digital ou reconhecimento facial para acessar o InvestProMax");
    Task<CredenciaisBiometricas?> ObterCredenciaisSalvasAsync();
}

public sealed class NullBiometricAuthService : IBiometricAuthService
{
    public Task<bool> EstaDisponivelAsync() => Task.FromResult(false);
    public Task<bool> PossuiCredenciaisSalvasAsync() => Task.FromResult(false);
    public Task SalvarCredenciaisAsync(string nomeUsuario, string senha) => Task.CompletedTask;
    public Task<bool> AutenticarAsync(string motivo = "") => Task.FromResult(false);
    public Task<CredenciaisBiometricas?> ObterCredenciaisSalvasAsync() => Task.FromResult<CredenciaisBiometricas?>(null);
}
