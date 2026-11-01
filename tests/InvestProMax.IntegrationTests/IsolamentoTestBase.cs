using System.Net.Http.Headers;
using System.Net.Http.Json;
using InvestProMax.Application.Auth;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InvestProMax.IntegrationTests;

/// <summary>
/// Base compartilhada pelos testes de isolamento (seção 11): cria um usuário direto no banco
/// de teste (não existe endpoint de registro — usuários só são provisionados via seed/admin),
/// autentica via o endpoint real de login (/auth/login) e devolve um HttpClient com o
/// Bearer token pronto, exercitando o pipeline de autenticação de ponta a ponta.
/// </summary>
public abstract class IsolamentoTestBase : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory Fabrica;

    protected IsolamentoTestBase(CustomWebApplicationFactory fabrica) => Fabrica = fabrica;

    protected async Task<(Usuario Usuario, HttpClient Cliente)> CriarUsuarioAutenticadoAsync(string nomeUsuario)
    {
        const string senha = "Senha@Teste123";

        using (var scope = Fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvestProMaxDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

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

            var cliente = Fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

            var loginResponse = await cliente.PostAsJsonAsync(
                "/api/auth/login", new LoginRequest(nomeUsuario, senha));
            loginResponse.EnsureSuccessStatusCode();

            var autenticacao = await loginResponse.Content.ReadFromJsonAsync<AutenticacaoResponse>();
            cliente.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", autenticacao!.AccessToken);

            return (usuario, cliente);
        }
    }
}
