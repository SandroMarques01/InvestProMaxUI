using InvestProMax.SharedUI.Auth;

namespace InvestProMax.Maui;

/// <summary>Seção 9: "MAUI: SecureStorage" — Keychain no iOS, Keystore no Android.</summary>
public sealed class SecureStorageTokenStorage : ITokenStorage
{
    private const string Chave = "investpromax.refreshToken";

    public Task<string?> ObterRefreshTokenAsync() => SecureStorage.Default.GetAsync(Chave);

    public Task SalvarRefreshTokenAsync(string refreshToken) => SecureStorage.Default.SetAsync(Chave, refreshToken);

    public Task LimparAsync()
    {
        SecureStorage.Default.Remove(Chave);
        return Task.CompletedTask;
    }
}
