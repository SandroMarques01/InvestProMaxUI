using InvestProMax.SharedUI.Api;
using InvestProMax.SharedUI.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace InvestProMax.SharedUI;

public static class DependencyInjection
{
    /// <summary>
    /// Registra tudo que a SharedUI precisa. O head chamador ainda precisa:
    /// 1) registrar sua própria implementação de <see cref="ITokenStorage"/> antes de chamar
    ///    isto (ex.: services.AddScoped&lt;ITokenStorage, SecureStorageTokenStorage&gt;()), e
    /// 2) configurar o BaseAddress do HttpClient tipado depois:
    ///    services.AddHttpClient&lt;IInvestProMaxApiClient, InvestProMaxApiClient&gt;(c =&gt;
    ///        c.BaseAddress = new Uri("https://.../"));
    /// Isso fica fora da SharedUI de propósito (seção 3): a URL da Api varia por ambiente/head
    /// (emulador Android x dispositivo físico x navegador), e a SharedUI não deve hardcodar
    /// nem decidir isso.
    /// </summary>
    public static IServiceCollection AddSharedUI(this IServiceCollection services)
    {
        services.AddScoped<AuthState>();
        services.AddScoped<AuthenticationStateProvider, InvestProMaxAuthStateProvider>();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();

        return services;
    }
}
