using InvestProMax.SharedUI;
using InvestProMax.SharedUI.Api;
using InvestProMax.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

builder.RootComponents.Add<InvestProMax.SharedUI.AppRoot>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSharedUI();
builder.Services.AddScoped<InvestProMax.SharedUI.Auth.ITokenStorage, LocalStorageTokenStorage>();

// Registra o cliente HTTP tipado com a URL-base configurada centralizadamente
builder.Services.AddHttpClient<IInvestProMaxApiClient, InvestProMaxApiClient>(cliente =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
    if (string.IsNullOrWhiteSpace(apiBaseUrl))
    {
        apiBaseUrl = builder.HostEnvironment.IsDevelopment()
            ? "https://localhost:7199/"
            : "https://investpromax-api-902552407127.us-central1.run.app/";
    }

    if (!apiBaseUrl.EndsWith('/'))
    {
        apiBaseUrl += "/";
    }

    cliente.BaseAddress = new Uri(apiBaseUrl);
});

// Datas dd/MM/yyyy e números no padrão brasileiro, independente do idioma do navegador.
var culturaBr = new System.Globalization.CultureInfo("pt-BR");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culturaBr;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culturaBr;

var host = builder.Build();

await host.RunAsync();
