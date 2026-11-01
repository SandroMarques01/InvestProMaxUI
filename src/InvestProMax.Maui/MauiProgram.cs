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

        builder.Services.AddHttpClient<IInvestProMaxApiClient, InvestProMaxApiClient>(cliente =>
        {
            // ASSUNÇÃO SINALIZADA: emulador Android usa 10.0.2.2 pra alcançar o localhost da
            // máquina host; iOS/MacCatalyst (e dispositivo físico Android) usam o IP real da
            // máquina rodando a Api na rede local. DeviceInfo.Platform decide em tempo de
            // execução; ajuste os dois valores abaixo pro seu ambiente antes de rodar.
            var baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                ? "https://10.0.2.2:7001/"
                : "https://localhost:7001/";
            cliente.BaseAddress = new Uri(baseUrl);
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        return app;
    }
}
