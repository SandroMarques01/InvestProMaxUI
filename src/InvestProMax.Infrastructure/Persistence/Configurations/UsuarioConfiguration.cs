using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nome).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.NomeUsuario).IsRequired().HasMaxLength(100);
        builder.Property(u => u.SenhaHash).IsRequired().HasMaxLength(200);
        builder.Property(u => u.StatusCadastro).HasDefaultValue(StatusCadastroUsuario.Aprovado);
        builder.Property(u => u.TelegramId);
        builder.Property(u => u.GoogleSubject).HasMaxLength(150);
        builder.Property(u => u.CodigoValidacaoTelegram).HasMaxLength(64);
        builder.Property(u => u.CodigoValidacaoTelegramExpiraEmUtc);
        builder.Property(u => u.CriadoEmUtc);
        builder.Property(u => u.AprovadoEmUtc);

        // Login e isolamento por usuário dependem de nome de usuário único.
        builder.HasIndex(u => u.NomeUsuario).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.CodigoValidacaoTelegram);
        builder.HasIndex(u => u.GoogleSubject);

        builder.HasMany(u => u.Papeis).WithOne(p => p.Usuario).HasForeignKey(p => p.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(u => u.Ativos).WithOne(a => a.Usuario).HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(u => u.Contas).WithOne(c => c.Usuario).HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(u => u.RefreshTokens).WithOne(r => r.Usuario).HasForeignKey(r => r.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
