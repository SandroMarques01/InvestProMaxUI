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

// ASSUNÇÃO SINALIZADA (decisão explícita do dono do produto, 2026-09-12): aplicação caseira,
// sem exigência de mitigação de XSS além do que o próprio Blazor já dá por padrão — refresh
// token fica em localStorage (ver comentário em LocalStorageTokenStorage.cs). Se algum dia
// isso precisar mudar para cookie HttpOnly, a Api também precisa mudar (Set-Cookie em vez de
// devolver o token no corpo JSON) — não é só troca de storage no head.
builder.Services.AddHttpClient<IInvestProMaxApiClient, InvestProMaxApiClient>(cliente =>
{
    // ASSUNÇÃO SINALIZADA: a especificação não define a URL da Api em produção. Lida de
    // wwwroot/appsettings.json ("ApiBaseUrl") pra não precisar recompilar por ambiente — ajuste
    // esse arquivo (ou o appsettings.Development.json) para onde a Api realmente estiver.
    cliente.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7199/");
});

// Datas dd/MM/yyyy e números no padrão brasileiro, independente do idioma do navegador.
var culturaBr = new System.Globalization.CultureInfo("pt-BR");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culturaBr;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culturaBr;

var host = builder.Build();

await host.RunAsync();
