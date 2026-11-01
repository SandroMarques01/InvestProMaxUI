using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using InvestProMax.Application.Cotacoes;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InvestProMax.IntegrationTests;

public class IntegracaoCotacoesIntegrationTests : IsolamentoTestBase
{
    private const string ApiKeyValida = "chave-api-de-teste-com-mais-de-32-caracteres-123456";

    public IntegracaoCotacoesIntegrationTests(CustomWebApplicationFactory fabrica) : base(fabrica) { }

    private sealed class FakeDisparador : IDisparadorAtualizacaoCotacoes
    {
        public bool EstaConfigurado { get; set; } = true;
        public int Chamadas { get; private set; }

        public Task DispararAsync(DateTime solicitadoEm, CancellationToken ct = default)
        {
            Chamadas++;
            return Task.CompletedTask;
        }
    }

    private (Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> FabricaConfigurada, Guid UsuarioId, FakeDisparador Fake)
        CriarFabricaComIntegracao(bool webhookConfigurado = true)
    {
        var usuarioId = Guid.NewGuid();
        var fake = new FakeDisparador { EstaConfigurado = webhookConfigurado };

        var fabricaConfigurada = Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Integracao:Cotacoes:ApiKey", ApiKeyValida);
            builder.UseSetting("Integracao:Cotacoes:UsuarioId", usuarioId.ToString());
            builder.UseSetting("Integracao:N8n:WebhookUrl", webhookConfigurado ? "https://n8n.teste/webhook" : "");
            builder.UseSetting("Integracao:N8n:Segredo", "segredo-teste");

            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IDisparadorAtualizacaoCotacoes>(_ => fake);
            });
        });

        return (fabricaConfigurada, usuarioId, fake);
    }

    [Fact]
    public async Task Sem_X_Api_Key_ou_com_chave_errada_retorna_401_ProblemDetails()
    {
        var (fabricaConfigurada, _, _) = CriarFabricaComIntegracao();
        var cliente = fabricaConfigurada.CreateClient();

        // 1. Sem header X-Api-Key
        var respostaSemChave = await cliente.GetAsync("/api/integracao/cotacoes/tickers");
        respostaSemChave.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        respostaSemChave.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problemaSemChave = await respostaSemChave.Content.ReadFromJsonAsync<ProblemDetails>();
        problemaSemChave!.Status.Should().Be(401);

        // 2. Com chave errada
        cliente.DefaultRequestHeaders.Add("X-Api-Key", "chave-errada-qualquer-12345678901234567890");
        var respostaChaveErrada = await cliente.GetAsync("/api/integracao/cotacoes/tickers");
        respostaChaveErrada.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        respostaChaveErrada.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problemaChaveErrada = await respostaChaveErrada.Content.ReadFromJsonAsync<ProblemDetails>();
        problemaChaveErrada!.Status.Should().Be(401);
    }

    [Fact]
    public async Task Chave_correta_retorna_200_mas_nao_acessa_api_papeis()
    {
        var (fabricaConfigurada, usuarioId, _) = CriarFabricaComIntegracao();

        // Cadastrar um papel para o usuário configurado
        using (var scope = fabricaConfigurada.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvestProMaxDbContext>();
            var papel = new Papel
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                Codigo = "PETR4",
                Nome = "Petrobras",
                CotacaoAtual = 35m,
                Ativo = true
            };
            papel.Transacoes.Add(new Transacao
            {
                Id = Guid.NewGuid(),
                PapelId = papel.Id,
                Data = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
                TipoTransacao = TipoTransacao.Compra,
                Quantidade = 100,
                ValorUnitario = 35m
            });
            db.Papeis.Add(papel);
            await db.SaveChangesAsync();
        }

        var cliente = fabricaConfigurada.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", ApiKeyValida);

        // 1. Acesso à integração com chave correta: 200 OK
        var respostaIntegracao = await cliente.GetAsync("/api/integracao/cotacoes/tickers");
        respostaIntegracao.StatusCode.Should().Be(HttpStatusCode.OK);

        var tickers = await respostaIntegracao.Content.ReadFromJsonAsync<TickersResponse>();
        tickers!.Tickers.Should().Contain("PETR4");

        // 2. Chave de API NÃO deve acessar endpoints protegidos por JWT (ex.: api/papeis)
        var respostaPapeis = await cliente.GetAsync("/api/papeis");
        respostaPapeis.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        respostaPapeis.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task JWT_nao_acessa_api_integracao()
    {
        var (fabricaConfigurada, _, _) = CriarFabricaComIntegracao();
        var (_, clienteJwt) = await CriarUsuarioAutenticadoComFabricaAsync(fabricaConfigurada, "usuarioJwtCotacoes");

        var resposta = await clienteJwt.GetAsync("/api/integracao/cotacoes/tickers");
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resposta.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problema = await resposta.Content.ReadFromJsonAsync<ProblemDetails>();
        problema!.Status.Should().Be(401);
    }

    [Fact]
    public async Task POST_api_cotacoes_atualizar_fluxos_de_sucesso_429_e_503()
    {
        // 1. Com webhook configurado
        var (fabricaConfigurada, _, fakeDisparador) = CriarFabricaComIntegracao(webhookConfigurado: true);
        var (_, clienteJwt) = await CriarUsuarioAutenticadoComFabricaAsync(fabricaConfigurada, "usuarioBtnCotacoes");

        // Primeiro pedido -> 202 Accepted
        var respostaPrimeira = await clienteJwt.PostAsync("/api/cotacoes/atualizar", null);
        respostaPrimeira.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var corpoPrimeira = await respostaPrimeira.Content.ReadFromJsonAsync<SolicitarAtualizacaoResponse>();
        corpoPrimeira!.SolicitadoEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        fakeDisparador.Chamadas.Should().Be(1);

        // Segundo pedido em menos de 60s -> 429 TooManyRequests
        var respostaSegunda = await clienteJwt.PostAsync("/api/cotacoes/atualizar", null);
        respostaSegunda.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        respostaSegunda.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        respostaSegunda.Headers.Contains("Retry-After").Should().BeTrue();

        var problema429 = await respostaSegunda.Content.ReadFromJsonAsync<ProblemDetails>();
        problema429!.Status.Should().Be(429);
        problema429.Title.Should().Be("Aguarde um minuto.");

        // 2. Sem webhook configurado -> 503 ServiceUnavailable
        var (fabricaSemWebhook, _, _) = CriarFabricaComIntegracao(webhookConfigurado: false);
        var (_, clienteSemWebhook) = await CriarUsuarioAutenticadoComFabricaAsync(fabricaSemWebhook, "usuarioSemWebhook");

        var respostaSemConfig = await clienteSemWebhook.PostAsync("/api/cotacoes/atualizar", null);
        respostaSemConfig.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        respostaSemConfig.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problema503 = await respostaSemConfig.Content.ReadFromJsonAsync<ProblemDetails>();
        problema503!.Status.Should().Be(503);
        problema503.Title.Should().Be("Integração de cotações não configurada.");
    }

    private static async Task<(Usuario Usuario, HttpClient Cliente)> CriarUsuarioAutenticadoComFabricaAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> fabrica, string nomeUsuario)
    {
        const string senha = "Senha@Teste123";

        using var scope = fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InvestProMaxDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<InvestProMax.Application.Common.Interfaces.IPasswordHasher>();

        var usuario = new Usuario
        {
            Nome = nomeUsuario,
            Email = $"{nomeUsuario}@teste.local",
            NomeUsuario = nomeUsuario,
            SenhaHash = hasher.Hash(senha),
            Ativo = true
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        var cliente = fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var loginResponse = await cliente.PostAsJsonAsync(
            "/api/auth/login", new InvestProMax.Application.Auth.LoginRequest(nomeUsuario, senha));
        loginResponse.EnsureSuccessStatusCode();

        var autenticacao = await loginResponse.Content.ReadFromJsonAsync<InvestProMax.Application.Auth.AutenticacaoResponse>();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", autenticacao!.AccessToken);

        return (usuario, cliente);
    }
}
