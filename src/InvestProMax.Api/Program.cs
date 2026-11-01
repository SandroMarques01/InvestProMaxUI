using System.Net;
using System.Text;
using InvestProMax.Api.Auth;
using InvestProMax.Api.BackgroundServices;
using InvestProMax.Api.Middleware;
using InvestProMax.Api.Services;
using InvestProMax.Api.Swagger;
using InvestProMax.Application;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Cotacoes;
using InvestProMax.Infrastructure;
using InvestProMax.Infrastructure.Auth;
using InvestProMax.Infrastructure.Persistence;
using InvestProMax.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog (seção 4: não existia no legado, adicionar desde o início) ---
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- Camadas ---
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- Contexto do usuário autenticado (isolamento por usuário — regra 10) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioLogado, HttpContextUsuarioLogado>();

// --- Autenticação JWT Bearer (seção 7/8) ---
var jwtSection = builder.Configuration.GetSection(JwtOptions.SecaoConfiguracao);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.ChaveSecreta))
{
    throw new InvalidOperationException(
        "Jwt:ChaveSecreta não configurada. Defina em appsettings.Development.json (fora do " +
        "controle de versão) ou em variável de ambiente/secret manager em produção — seção 7.");
}

// Reforço de segurança (Fase 8 — revisão contra a seção 7): HS256 aceita qualquer tamanho de
// chave, mas uma chave menor que o tamanho do hash (256 bits = 32 bytes UTF-8) enfraquece a
// assinatura na prática. Falha cedo, no startup, em vez de permitir um ambiente com uma chave
// fraca subir silenciosamente.
if (Encoding.UTF8.GetByteCount(jwtOptions.ChaveSecreta) < 32)
{
    throw new InvalidOperationException(
        "Jwt:ChaveSecreta precisa ter pelo menos 32 bytes (256 bits) para HS256 — seção 7.");
}

// --- Integração com n8n e Cotações Automáticas (Fase 3) ---
var cotacoesSection = builder.Configuration.GetSection(IntegracaoCotacoesOptions.SecaoConfiguracao);
var cotacoesOptions = cotacoesSection.Get<IntegracaoCotacoesOptions>() ?? new IntegracaoCotacoesOptions();

if (!string.IsNullOrWhiteSpace(cotacoesOptions.ApiKey))
{
    if (cotacoesOptions.ApiKey.Length < 32)
    {
        throw new InvalidOperationException(
            "Integracao:Cotacoes:ApiKey precisa ter pelo menos 32 caracteres quando a integração estiver habilitada.");
    }

    if (!Guid.TryParse(cotacoesOptions.UsuarioId, out _))
    {
        throw new InvalidOperationException(
            "Integracao:Cotacoes:UsuarioId precisa ser um Guid válido quando a integração estiver habilitada.");
    }
}

builder.Services.Configure<IntegracaoCotacoesOptions>(cotacoesSection);
builder.Services.Configure<IntegracaoN8nOptions>(builder.Configuration.GetSection(IntegracaoN8nOptions.SecaoConfiguracao));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Emissor,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audiencia,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.ChaveSecreta)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Fase 8 — padronização de erro (seção 8: "Erros devem seguir o formato ProblemDetails"
        // sem qualificar "só os de negócio"): sem isto, um token ausente/inválido/expirado
        // resulta num 401 sem corpo, tratado pelo middleware de autenticação antes de chegar
        // no ExceptionHandlingMiddleware — que só intercepta exceções lançadas, não os
        // curto-circuitos de challenge/forbid do próprio pipeline de auth.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async contexto =>
            {
                contexto.HandleResponse();
                await EscreverProblemDetailsAsync(contexto.HttpContext, HttpStatusCode.Unauthorized,
                    "Token ausente, inválido ou expirado.");
            },
            OnForbidden = async contexto =>
            {
                await EscreverProblemDetailsAsync(contexto.HttpContext, HttpStatusCode.Forbidden,
                    "Você não tem permissão para acessar este recurso.");
            }
        };
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.DefaultScheme, null);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IntegracaoCotacoes", policy =>
    {
        policy.AuthenticationSchemes.Add(ApiKeyAuthenticationOptions.DefaultScheme);
        policy.RequireAuthenticatedUser();
    });
});

// Cache em memória (limite de 1 pedido por 60 s para atualização de cotações)
builder.Services.AddMemoryCache();

// Cliente HTTP tipado para webhook do n8n (timeout curto de 10s, sem retentativa automática)
builder.Services.AddHttpClient<IDisparadorAtualizacaoCotacoes, DisparadorAtualizacaoCotacoes>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// --- Job diário de faturas (regras 7 e 8, seção 9) ---
builder.Services.AddHostedService<FaturaGeracaoHostedService>();

// --- Controllers + ProblemDetails nativo do ASP.NET Core para 4xx/5xx não tratados pelo middleware ---
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// --- OpenAPI/Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Invest Pro Max API", Version = "v1" });

    var jwtScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Description = "Informe apenas o token JWT (sem o prefixo 'Bearer ')."
    };
    options.AddSecurityDefinition("Bearer", jwtScheme);

    var apiKeyScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Scheme = "ApiKey",
        Name = ApiKeyAuthenticationOptions.HeaderName,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Description = "Informe a chave de API de integração no header X-Api-Key."
    };
    options.AddSecurityDefinition("ApiKey", apiKeyScheme);

    options.OperationFilter<SwaggerSecurityRequirementsOperationFilter>();
});

// --- CORS: preparado para o front-end Blazor WebAssembly (Fase 6, seção 10) chamar a API
// de uma origem diferente. Política nomeada e restrita por configuração, nunca AllowAnyOrigin
// em produção — origens permitidas ainda não definidas nesta fase, ver appsettings. ---
const string PoliticaCors = "FrontEnd";
builder.Services.AddCors(options =>
{
    options.AddPolicy(PoliticaCors, policy =>
    {
        var origensPermitidas = builder.Configuration.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? [];
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(origensPermitidas).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// --- Migrations automáticas + seed só em Development; produção aplica migration via pipeline (seção 13, em aberto) ---
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbProvider = scope.ServiceProvider.GetRequiredService<IDatabaseProviderService>();
    var supabaseDb = scope.ServiceProvider.GetService<SupabaseDbContext>();
    var localDb = scope.ServiceProvider.GetService<LocalDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    // 1. Garante que a base local (localhost / LocalDB) esteja migrada e pronta para uso/fallback
    if (localDb is not null)
    {
        try
        {
            logger.LogInformation("Aplicando migrations na base local (LocalDB / localhost)...");
            await localDb.Database.MigrateAsync();
            await DbSeeder.SeedUsuarioDevAsync(localDb, passwordHasher, app.Configuration);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Aviso ao inicializar a base LocalDB.");
        }
    }

    // 2. Verifica a conectividade com o Supabase (Banco Principal)
    if (supabaseDb is not null)
    {
        try
        {
            logger.LogInformation("Verificando conectividade com o Supabase (Banco Principal)...");
            var supabaseConectou = await dbProvider.VerificarAcessibilidadeSupabaseAsync();

            if (supabaseConectou)
            {
                logger.LogInformation("Supabase conectado com sucesso! Definido como BANCO PRINCIPAL.");
                await DatabaseDualSyncInitializer.EnsureSupabaseSchemaAsync(supabaseDb, logger);
                await DbSeeder.SeedUsuarioDevAsync(supabaseDb, passwordHasher, app.Configuration);

                if (localDb is not null)
                {
                    await DatabaseDualSyncInitializer.SincronizarBasesAsync(localDb, supabaseDb, logger);
                }
            }
            else
            {
                logger.LogWarning("Supabase INACESSÍVEL na inicialização. Alternando automaticamente para o banco LOCALHOST (LocalDB) como fallback.");
                dbProvider.MarcarSupabaseInacessivel();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao conectar ao Supabase na inicialização. Alternando para o banco LOCALHOST (LocalDB) como fallback.");
            dbProvider.MarcarSupabaseInacessivel(ex);
        }
    }
    else
    {
        var db = scope.ServiceProvider.GetRequiredService<InvestProMaxDbContext>();
        if (db.Database.IsNpgsql())
        {
            var creator = db.GetService<IRelationalDatabaseCreator>();
            if (!creator.HasTables())
            {
                creator.CreateTables();
            }
        }
        else
        {
            await db.Database.MigrateAsync();
        }
        await DbSeeder.SeedUsuarioDevAsync(db, passwordHasher, app.Configuration);
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Reforço de segurança (Fase 8): HSTS instrui o navegador a nunca tentar HTTP de novo
    // com este host, mesmo que alguém digite a URL sem "https://" — complementa o
    // UseHttpsRedirection abaixo, que só age depois que a requisição já chegou.
    app.UseHsts();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// HTTPS obrigatório em todos os ambientes, inclusive dev local (seção 7).
app.UseHttpsRedirection();

app.UseCors(PoliticaCors);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Fase 8: usado pelos handlers OnChallenge/OnForbidden acima, pra manter o mesmo formato
// ProblemDetails do ExceptionHandlingMiddleware mesmo quando o erro vem do pipeline de
// autenticação (que nunca chega a lançar uma exceção que o middleware possa capturar).
static async Task EscreverProblemDetailsAsync(HttpContext contexto, HttpStatusCode status, string titulo)
{
    var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
    {
        Status = (int)status,
        Title = titulo,
        Instance = contexto.Request.Path
    };
    contexto.Response.StatusCode = (int)status;
    contexto.Response.ContentType = "application/problem+json";
    await contexto.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
}

// Exposto para os projetos de teste de integração (WebApplicationFactory<Program>).
public partial class Program { }
