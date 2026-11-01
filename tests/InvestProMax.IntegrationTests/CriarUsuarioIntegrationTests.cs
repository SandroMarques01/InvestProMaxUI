using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvestProMax.Application.Auth;
using Xunit;

namespace InvestProMax.IntegrationTests;

public class CriarUsuarioIntegrationTests : IsolamentoTestBase
{
    public CriarUsuarioIntegrationTests(CustomWebApplicationFactory fabrica) : base(fabrica) { }

    [Fact]
    public async Task Admin_pode_criar_novo_usuario_e_novo_usuario_consegue_logar()
    {
        var (_, clienteAdmin) = await CriarUsuarioAutenticadoAsync("admin");
        var novoUsuarioRequest = new CriarUsuarioRequest(
            "Maria Silva",
            "maria.silva@teste.local",
            "mariasilva",
            "Senha@Forte123");

        var response = await clienteAdmin.PostAsJsonAsync("/api/auth/usuarios", novoUsuarioRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UsuarioResponse>();
        body.Should().NotBeNull();
        body!.Nome.Should().Be("Maria Silva");
        body.Email.Should().Be("maria.silva@teste.local");
        body.NomeUsuario.Should().Be("mariasilva");
        body.Ativo.Should().BeTrue();

        // Novo usuário deve conseguir logar normalmente
        var clienteAnonimo = Fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var loginResponse = await clienteAnonimo.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("mariasilva", "Senha@Forte123"));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var autenticacao = await loginResponse.Content.ReadFromJsonAsync<AutenticacaoResponse>();
        autenticacao.Should().NotBeNull();
        autenticacao!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Usuario_comum_recebe_403_ao_tentar_criar_usuario()
    {
        var (_, clienteComum) = await CriarUsuarioAutenticadoAsync("usuario_comum");
        var novoUsuarioRequest = new CriarUsuarioRequest(
            "Outro Usuario",
            "outro@teste.local",
            "outro_usuario",
            "Senha@Forte123");

        var response = await clienteComum.PostAsJsonAsync("/api/auth/usuarios", novoUsuarioRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Requisicao_anonima_recebe_401_ao_tentar_criar_usuario()
    {
        var clienteAnonimo = Fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var novoUsuarioRequest = new CriarUsuarioRequest(
            "Outro Usuario",
            "outro@teste.local",
            "outro_usuario",
            "Senha@Forte123");

        var response = await clienteAnonimo.PostAsJsonAsync("/api/auth/usuarios", novoUsuarioRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
