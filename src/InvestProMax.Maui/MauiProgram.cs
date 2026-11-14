using InvestProMax.SharedUI;
using InvestProMax.SharedUI.Api;
using InvestProMax.SharedUI.Auth;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;

namespace InvestProMax.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var culturaBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culturaBr;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culturaBr;
        System.Globalization.CultureInfo.CurrentCulture = culturaBr;
        System.Globalization.CultureInfo.CurrentUICulture = culturaBr;

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // Sem fonte customizada pedida pela especificação — mantém a padrão da
                // plataforma. Se quiser uma fonte própria, adicione o arquivo em
                // Resources/Fonts/ e registre aqui com fonts.AddFont(...).
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSharedUI();
        builder.Services.AddSingleton<ITokenStorage, SecureStorageTokenStorage>();
        builder.Services.AddSingleton<IBiometricAuthService, MauiBiometricAuthService>();

        builder.Services.AddHttpClient<IInvestProMaxApiClient, InvestProMaxApiClient>(cliente =>
        {
            cliente.BaseAddress = new Uri("https://investpromax-api-902552407127.us-central1.run.app/");
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        return app;
    }
}
