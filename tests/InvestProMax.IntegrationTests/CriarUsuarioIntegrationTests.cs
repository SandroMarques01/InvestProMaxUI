using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using InvestProMax.Application.Auth;
using InvestProMax.Domain.Enums;
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

    [Fact]
    public async Task Login_nao_eh_case_sensitive_e_aceita_usuario_ou_email_com_espacos()
    {
        var (_, _) = await CriarUsuarioAutenticadoAsync("usuario_case_test");
        var clienteAnonimo = Fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        foreach (var identificador in new[]
        {
            "USUARIO_CASE_TEST",
            "Usuario_Case_Test",
            "  Usuario_Case_Test  ",
            "USUARIO_CASE_TEST@TESTE.LOCAL"
        })
        {
            var loginResponse = await clienteAnonimo.PostAsJsonAsync(
                "/api/auth/login", new LoginRequest(identificador, "Senha@Teste123"));

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, $"login deveria aceitar '{identificador}'");
            var autenticacao = await loginResponse.Content.ReadFromJsonAsync<AutenticacaoResponse>();
            autenticacao!.AccessToken.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task CriarUsuario_rejeita_nome_usuario_duplicado_independentemente_de_maiusculas_e_minusculas()
    {
        var clienteAdmin = Fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var loginAdmin = await clienteAdmin.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Senha@Teste123"));
        if (loginAdmin.IsSuccessStatusCode)
        {
            var auth = await loginAdmin.Content.ReadFromJsonAsync<AutenticacaoResponse>();
            clienteAdmin.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        }
        else
        {
            (_, clienteAdmin) = await CriarUsuarioAutenticadoAsync("admin");
        }

        var primeiro = new CriarUsuarioRequest(
            "Carlos Souza",
            "carlos.souza@teste.local",
            "carlossouza",
            "Senha@Forte123");

        (await clienteAdmin.PostAsJsonAsync("/api/auth/usuarios", primeiro)).StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicadoCaseDiferente = new CriarUsuarioRequest(
            "Carlos Souza 2",
            "carlos.souza2@teste.local",
            "CarlosSouza",
            "Senha@Forte123");

        var respDuplicado = await clienteAdmin.PostAsJsonAsync("/api/auth/usuarios", duplicadoCaseDiferente);
        respDuplicado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Fluxo_completo_registrar_validar_telegram_aprovar_admin_e_logar()
    {
        const string apiKeyIntegracao = "chave-api-de-teste-telegram-com-mais-de-32-caracteres";
        var fabricaComApiKey = Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Integracao:Cotacoes:ApiKey", apiKeyIntegracao);
            builder.UseSetting("Integracao:Telegram:AdminTelegramId", "777888999");
        });

        // Garante usuário admin ativo com Token Bearer na mesma fábrica
        var (adminUsuario, clienteAdmin) = await CriarUsuarioAutenticadoFabricaAsync(fabricaComApiKey, "admin");
        _ = adminUsuario;

        var clienteAnonimo = fabricaComApiKey.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        // 1. Auto-cadastro público
        var regResp = await clienteAnonimo.PostAsJsonAsync("/api/auth/registrar", new RegistrarUsuarioRequest(
            "Novo Membro",
            "novo.membro@teste.local",
            "novomembro",
            "Senha@Forte123"));

        regResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var regBody = await regResp.Content.ReadFromJsonAsync<StatusCadastroResponse>();
        regBody!.StatusCadastro.Should().Be(StatusCadastroUsuario.PendenteTelegram);
        regBody.CodigoValidacaoTelegram.Should().NotBeNullOrWhiteSpace();
        regBody.LinkTelegramBot.Should().Contain("MarkX_InvestProMax_MarquesFC_bot");

        // 2. Tentativa de login antes do Telegram -> 403 Forbidden (pendente)
        var loginAntesTelegram = await clienteAnonimo.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("novomembro", "Senha@Forte123"));
        loginAntesTelegram.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Integração n8n/Telegram valida o código com TelegramId via X-Api-Key
        var clienteBot = fabricaComApiKey.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        clienteBot.DefaultRequestHeaders.Add("X-Api-Key", apiKeyIntegracao);

        var validarResp = await clienteBot.PostAsJsonAsync("/api/integracao/telegram/validar-cadastro",
            new ValidarTelegramRequest(regBody.CodigoValidacaoTelegram!, 888111222L, "novotg", "Novo Membro"));
        validarResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var validarBody = await validarResp.Content.ReadFromJsonAsync<ValidarTelegramResponse>();
        validarBody!.Sucesso.Should().BeTrue();
        validarBody.TelegramId.Should().Be(888111222L);
        validarBody.AdminTelegramIds.Should().Contain(777888999L);

        // 4. Tentativa de login aguardando Admin -> ainda 403 Forbidden
        var loginAntesAdmin = await clienteAnonimo.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("novomembro", "Senha@Forte123"));
        loginAntesAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Admin lista e aprova via Configurações > Usuários
        var usuarios = await clienteAdmin.GetFromJsonAsync<List<UsuarioResponse>>("/api/auth/usuarios");
        var pendente = usuarios!.Should().ContainSingle(u => u.NomeUsuario == "novomembro").Subject;
        pendente.TelegramId.Should().Be(888111222L);
        pendente.StatusCadastro.Should().Be(StatusCadastroUsuario.PendenteAprovacaoAdmin);

        var aprovarResp = await clienteAdmin.PostAsync($"/api/auth/usuarios/{pendente.Id}/aprovar", null);
        aprovarResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Login finalmente liberado -> 200 OK com JWT
        var loginFinal = await clienteAnonimo.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("novomembro", "Senha@Forte123"));
        loginFinal.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await loginFinal.Content.ReadFromJsonAsync<AutenticacaoResponse>();
        tokens!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<(InvestProMax.Domain.Entidades.Usuario Usuario, HttpClient Cliente)> CriarUsuarioAutenticadoFabricaAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> fabrica, string nomeUsuario)
    {
        const string senha = "Senha@Teste123";
        using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(fabrica.Services);
        var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<InvestProMax.Infrastructure.Persistence.InvestProMaxDbContext>(scope.ServiceProvider);
        var hasher = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<InvestProMax.Application.Common.Interfaces.IPasswordHasher>(scope.ServiceProvider);

        var existente = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            db.Usuarios, u => u.NomeUsuario == nomeUsuario);

        InvestProMax.Domain.Entidades.Usuario usuario;
        if (existente is null)
        {
            usuario = new InvestProMax.Domain.Entidades.Usuario
            {
                Nome = nomeUsuario,
                Email = $"{nomeUsuario}@teste.local",
                NomeUsuario = nomeUsuario,
                SenhaHash = hasher.Hash(senha),
                Ativo = true
            };
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
        }
        else
        {
            usuario = existente;
        }

        var cliente = fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var loginResponse = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(nomeUsuario, senha));
        loginResponse.EnsureSuccessStatusCode();

        var autenticacao = await loginResponse.Content.ReadFromJsonAsync<AutenticacaoResponse>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", autenticacao!.AccessToken);
        return (usuario, cliente);
    }
}
