using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace InvestProMax.SharedUI.Auth;

public sealed class InvestProMaxAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly AuthState _authState;

    public InvestProMaxAuthStateProvider(AuthState authState)
    {
        _authState = authState;
        _authState.MudancaDeEstado += NotificarMudanca;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identidade = _authState.EstaAutenticado
            ? new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, _authState.NomeUsuario ?? string.Empty)],
                authenticationType: "InvestProMax")
            : new ClaimsIdentity();

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identidade)));
    }

    private void NotificarMudanca() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose() => _authState.MudancaDeEstado -= NotificarMudanca;
}
