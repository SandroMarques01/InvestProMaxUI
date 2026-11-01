using InvestProMax.SharedUI.Auth;
using Microsoft.JSInterop;

namespace InvestProMax.Web;

/// <summary>
/// Decisão explícita do dono do produto (2026-09-12): "é uma aplicação caseira, faça o mais
/// simples" — localStorage para o refresh token, com o risco de XSS ciente e aceito (ver
/// comentário em ITokenStorage.cs sobre a alternativa mais segura, cookie HttpOnly, que exigiria
/// mudar a Api). Chama localStorage.getItem/setItem/removeItem via JS interop — não precisa de
/// nenhum arquivo .js próprio porque esses são métodos globais padrão do navegador.
/// </summary>
public sealed class LocalStorageTokenStorage : ITokenStorage
{
    private const string Chave = "investpromax.refreshToken";
    private readonly IJSRuntime _js;

    public LocalStorageTokenStorage(IJSRuntime js) => _js = js;

    public async Task<string?> ObterRefreshTokenAsync() =>
        await _js.InvokeAsync<string?>("localStorage.getItem", Chave);

    public async Task SalvarRefreshTokenAsync(string refreshToken) =>
        await _js.InvokeVoidAsync("localStorage.setItem", Chave, refreshToken);

    public async Task LimparAsync() =>
        await _js.InvokeVoidAsync("localStorage.removeItem", Chave);
}
