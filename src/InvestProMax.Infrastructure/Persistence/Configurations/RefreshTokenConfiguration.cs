using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.TokenHash).IsRequired().HasMaxLength(200);
        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasIndex(r => r.UsuarioId);

        // Auto-referência opcional para a cadeia de rotação (token substituído por outro).
        builder.HasOne<RefreshToken>().WithMany().HasForeignKey(r => r.SubstituidoPorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
