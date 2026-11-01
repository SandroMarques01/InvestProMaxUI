using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace InvestProMax.IntegrationTests;

/// <summary>
/// Fase 8 — revisão de segurança/padronização de erro: a seção 8 exige ProblemDetails (RFC
/// 7807) em TODO erro, mas um 401 vindo do próprio pipeline de autenticação (token
/// ausente/inválido) nunca passava pelo ExceptionHandlingMiddleware, então saía sem corpo.
/// Este teste garante que o pipeline de auth também devolve ProblemDetails, via os handlers
/// OnChallenge/OnForbidden configurados em Program.cs.
/// </summary>
public class ErroPadronizadoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _fabrica;

    public ErroPadronizadoTests(CustomWebApplicationFactory fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task Requisicao_sem_token_retorna_401_em_formato_ProblemDetails()
    {
        var cliente = _fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var resposta = await cliente.GetAsync("/api/papeis");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resposta.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problema = await resposta.Content.ReadFromJsonAsync<ProblemDetails>();
        problema!.Status.Should().Be(401);
        problema.Title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Requisicao_com_token_invalido_retorna_401_em_formato_ProblemDetails()
    {
        var cliente = _fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        cliente.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token-invalido-de-propósito");

        var resposta = await cliente.GetAsync("/api/papeis");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resposta.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }
}
