using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InvestProMax.Infrastructure.Persistence.Seed;

/// <summary>
/// ASSUNÇÃO SINALIZADA: a especificação não define um endpoint de registro de usuário
/// (seção 8 não lista POST /auth/registrar) — coerente com a seção 1 ("usado pelo dono do
/// produto e por um pequeno grupo de amigos"), presumi que usuários são provisionados
/// manualmente, não por auto-cadastro público. Este seeder existe só para permitir testar o
/// login localmente em desenvolvimento; USO EM PRODUÇÃO exigiria decidir um mecanismo real de
/// provisionamento (fora de escopo — sinalizar ao dono do produto).
/// </summary>
public static class DbSeeder
{
    public static async Task SeedUsuarioDevAsync(
        InvestProMaxDbContext db, IPasswordHasher passwordHasher, IConfiguration configuration)
    {
        if (await db.Usuarios.AnyAsync())
        {
            return;
        }

        var nomeUsuario = configuration["SeedUsuarioDev:NomeUsuario"] ?? "admin";
        var senha = configuration["SeedUsuarioDev:Senha"] ?? "Troque@Esta123";
        var email = configuration["SeedUsuarioDev:Email"] ?? "admin@investpromax.local";

        var usuario = new Usuario
        {
            Nome = "Administrador",
            Email = email,
            NomeUsuario = nomeUsuario,
            SenhaHash = passwordHasher.Hash(senha),
            Ativo = true
        };

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
    }
}
